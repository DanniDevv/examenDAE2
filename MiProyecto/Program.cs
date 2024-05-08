using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySql.Data.MySqlClient;
using StackExchange.Redis;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        // Configurar conexión a Redis
        var redisConnectionString = "redis:6379"; // Ajusta según sea necesario
        var redis = ConnectionMultiplexer.Connect(redisConnectionString);

        // Configurar conexión a MySQL
        var mysqlConnectionString = "Server=mysql;Port=3306;Database=mydatabase;User=myuser;Password=mypassword;";
        var mysqlConnection = new MySqlConnection(mysqlConnectionString);

        // Inicializar la base de datos y crear la tabla si no existe
        await mysqlConnection.OpenAsync();
        var createTableQuery = @"
            CREATE TABLE IF NOT EXISTS user_genres (
                user_id VARCHAR(255) PRIMARY KEY,
                genres TEXT
            )";
        using (var createTableCommand = new MySqlCommand(createTableQuery, mysqlConnection))
        {
            await createTableCommand.ExecuteNonQueryAsync(); // Crear la tabla si no existe
        }
        await mysqlConnection.CloseAsync();

        // Iniciar la suscripción a un canal de Redis
        var subscriber = redis.GetSubscriber();
        subscriber.Subscribe("update_channel", async (channel, message) =>
        {
            var userId = message.ToString(); // Asumimos que el mensaje contiene la clave de usuario
            var db = redis.GetDatabase();
            var value = await db.StringGetAsync(userId);

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

            Console.WriteLine($"Usuario {userId} actualizado en MySQL con géneros '{value}'");
        });

        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) =>
            {
                services.AddHostedService<Worker>();
            });
}

public class Worker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Realiza aquí el trabajo del worker si es necesario
            await Task.Delay(1000, stoppingToken); // Espera 1 segundo antes de volver a verificar
        }
    }
}
