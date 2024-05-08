# Dockerfile
FROM python:3.8-slim

WORKDIR /app

# Instalar Flask
COPY requirements.txt .
RUN pip install -r requirements.txt

# Copiar el código de la aplicación
COPY . .

# Exponer el puerto donde Flask escucha
EXPOSE 5000

# Comando para ejecutar la aplicación
CMD ["python", "app.py"]