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

# Copiar el script wait-for-it.sh
COPY wait-for-it.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/wait-for-it.sh

# Copiar la salida de la construcción
COPY --from=build-env /app/out .

# Exponer el puerto
EXPOSE 5051

# Comando para ejecutar la aplicación después de esperar a que MySQL esté listo
ENTRYPOINT ["/usr/local/bin/wait-for-it.sh", "mysql:3306", "--timeout=30", "--strict", "--", "dotnet", "worker.dll"]
