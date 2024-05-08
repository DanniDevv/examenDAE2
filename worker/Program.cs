using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using StackExchange.Redis;
using System;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Configurar conexión a Redis
var redisConnectionString = "redis:6379"; // Ajusta según sea necesario
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

// Configurar conexión a MySQL
var mysqlConnectionString = "Server=mysql;Port=3306;Database=mydatabase;Uid=myuser;Pwd=mypassword;";
builder.Services.AddTransient<MySqlConnection>(_ => new MySqlConnection(mysqlConnectionString)); // Transient para conexiones únicas

using (var mysqlConnection = new MySqlConnection(mysqlConnectionString))
{
    mysqlConnection.Open();
    Console.WriteLine("Conexión a MySQL establecida correctamente.");

    // Crear la tabla user_genres si no existe
    var createTableQuery = @"
        CREATE TABLE IF NOT EXISTS user_genres (
            user_id VARCHAR(255) PRIMARY KEY,
            genres TEXT
        )";
    using (var createTableCommand = new MySqlCommand(createTableQuery, mysqlConnection))
    {
        createTableCommand.ExecuteNonQuery();
        Console.WriteLine("Tabla user_genres creada o ya existente.");
    }

    mysqlConnection.Close();
}

var app = builder.Build();

app.UseRouting();

// Iniciar la suscripción a un canal de Redis
var subscriber = redis.GetSubscriber();
subscriber.Subscribe("update_channel", async (channel, message) =>
{
    var userId = message.ToString(); // Asumimos que el mensaje contiene la clave de usuario
    var db = redis.GetDatabase();
    var value = await db.StringGetAsync(userId);

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var mysqlConnection = services.GetRequiredService<MySqlConnection>();

        try
        {
            await mysqlConnection.OpenAsync();

            var query = @"INSERT INTO user_genres (user_id, genres) 
                          VALUES (@user_id, @genres)
                          ON DUPLICATE KEY UPDATE genres = @genres";
            using (var command = new MySqlCommand(query, mysqlConnection))
            {
                command.Parameters.AddWithValue("@user_id", userId);
                command.Parameters.AddWithValue("@genres", value.ToString());
                await command.ExecuteNonQueryAsync();
            }

            await mysqlConnection.CloseAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al actualizar datos en MySQL: {ex.Message}");
        }

        Console.WriteLine($"Usuario {userId} actualizado en MySQL con géneros '{value}'");
    }
});

// Endpoint GET para obtener todos los usuarios y sus géneros
app.MapGet("/mysql/users", async (HttpContext context) =>
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var mysqlConnection = services.GetRequiredService<MySqlConnection>();

        List<Dictionary<string, string>> results = new List<Dictionary<string, string>>();

        try
        {
            await mysqlConnection.OpenAsync();

            var query = "SELECT user_id, genres FROM user_genres"; // Consulta para obtener todos los usuarios y géneros

            using (var command = new MySqlCommand(query, mysqlConnection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var record = new Dictionary<string, string>();

                        // Verificar si las columnas existen antes de leerlas
                        if (reader.HasRows)
                        {
                            record["user_id"] = reader.GetString(reader.GetOrdinal("user_id")); // Lee por nombre de columna
                            record["genres"] = reader.GetString(reader.GetOrdinal("genres")); // Lee por nombre de columna
                        }

                        results.Add(record);
                    }
                }
            }

            await mysqlConnection.CloseAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al obtener datos de MySQL: {ex.Message}");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Hubo un problema al obtener datos de la base de datos.");
            return;
        }

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(results);
    }
});

// Endpoint GET para obtener un usuario por su ID
app.MapGet("/mysql/users/{user_id}", async (HttpContext context) =>
{
    var userId = context.Request.RouteValues["user_id"].ToString();

    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var mysqlConnection = services.GetRequiredService<MySqlConnection>();

        try
        {
            await mysqlConnection.OpenAsync();

            var query = "SELECT user_id, genres FROM user_genres WHERE user_id = @user_id";
            using (var command = new MySqlCommand(query, mysqlConnection))
            {
                command.Parameters.AddWithValue("@user_id", userId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var result = new Dictionary<string, string>
                        {
                            { "user_id", reader.GetString(reader.GetOrdinal("user_id")) },
                            { "genres", reader.GetString(reader.GetOrdinal("genres")) }
                        };

                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(result);
                        return;
                    }
                }
            }

            await mysqlConnection.CloseAsync();

            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"No se encontró usuario con ID: {userId}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al obtener datos de MySQL: {ex.Message}");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Hubo un problema al obtener datos de la base de datos.");
            return;
        }
    }
});



app.Run();
