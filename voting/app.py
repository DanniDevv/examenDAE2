from flask import Flask, render_template, request, redirect, url_for
import redis
import requests

app = Flask(__name__)

# Conexión a Redis
r = redis.Redis(host='redis', port=6379, db=0)

all_genres = [
    "Action", "Adventure", "Animation", "Children", "Comedy", "Crime",
    "Documentary", "Drama", "Fantasy", "Film-Noir", "Horror", "Musical",
    "Mystery", "Romance", "Sci-Fi", "Thriller", "War", "Western"
]

class User:
    def __init__(self, id, name):
        self.id = id
        self.name = name

users = [
    User(1, "User1"),
    User(2, "User2"),
    User(3, "User3")
]

@app.route('/')
def index():
    return render_template('index.html', users=users, genres=all_genres)

@app.route('/select_genres', methods=['POST'])
def select_genres():
    user_id = request.form['user_id']
    selected_genres = request.form.getlist('genres')
    r.set(f"user:{user_id}", ','.join(selected_genres))
    r.publish("update_channel", f"user:{user_id}")
    return redirect(url_for('recommendations', user_id=user_id))

@app.route('/recommendations/<int:user_id>', methods=['GET'])
def recommendations(user_id):
    response = requests.get(f"http://recomendacion:5050/recommendations/{user_id}")

    if response.status_code != 200:
        return f"Error al obtener recomendaciones para el usuario {user_id}"

    recommended_movies = response.json().get("recommended_movies", [])

    return render_template('recomendacion.html', user_id=user_id, recommended_movies=recommended_movies)

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
