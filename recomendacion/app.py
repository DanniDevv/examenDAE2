from flask import Flask, request, jsonify
import pandas as pd
from scipy.spatial.distance import cosine
import requests

app = Flask(__name__)

# Cargar datos de películas
movies_df = pd.read_csv("movies.csv")

# Crear vectores de géneros para cada película
genres_set = set(genre for sublist in movies_df['genres'].str.split('|') for genre in sublist)
movies_df['genre_vector'] = movies_df['genres'].apply(lambda x: [1 if genre in x.split('|') else 0 for genre in genres_set])

@app.route("/recommendations/<user_id>", methods=["GET"])
def recommend_for_user(user_id):
    # Obtener géneros del usuario desde un servicio externo
    response = requests.get(f"http://MiProyecto:8080/mysql/users/{user_id}")

    if response.status_code != 200:
        return jsonify({"error": "Error al obtener datos de usuarios"}), 500

    user_data = response.json()
    if not user_data:
        return jsonify({"error": f"No se encontró usuario con ID: {user_id}"}), 404

    # Convertir géneros del usuario a un vector
    user_genres = set(user_data.get("genres", "").split(','))
    user_vector = [1 if genre in user_genres else 0 for genre in genres_set]

    # Calcular similitud del coseno para cada película
    movies_df['similarity'] = movies_df['genre_vector'].apply(lambda x: 1 - cosine(user_vector, x))

    # Obtener las películas con mayor similitud (ordenar y tomar las mejores)
    recommended_movies = movies_df.sort_values(by='similarity', ascending=False).head(10)['title'].tolist()

    return jsonify({
        "user_id": user_id,
        "recommended_movies": recommended_movies
    })

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5050, debug=True)
