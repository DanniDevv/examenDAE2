# Etapa de construcción
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copiar el archivo csproj y restaurar las dependencias
COPY *.csproj ./
RUN dotnet restore

# Copiar el resto del código y compilar la aplicación
COPY . ./
RUN dotnet publish -c Release -o out

# Configura la imagen de producción
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app


# Copiar la salida de la construcción
COPY --from=build-env /app/out .

# Exponer el puerto
EXPOSE 5185

# Comando para ejecutar la aplicación después de esperar a que MySQL esté listo
ENTRYPOINT ["dotnet", "worker.dll"]
