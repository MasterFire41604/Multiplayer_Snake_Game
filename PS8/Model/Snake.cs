using SnakeGame;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Model
{
    public class Snake
    {
        [JsonInclude]
        public int snake;
        [JsonInclude]
        public string name;
        [JsonInclude]
        public List<Vector2D> body;
        [JsonInclude]
        public Vector2D dir;
        [JsonInclude]
        public int score;
        [JsonInclude]
        public bool died;
        [JsonInclude]
        public bool alive;
        [JsonInclude]
        public bool dc;
        [JsonInclude]
        public bool join;

        [JsonConstructor]
        public Snake(int snake, string name, List<Vector2D> body, Vector2D dir, int score, bool died, bool alive, bool dc, bool join)
        {
            this.name = name;
            this.snake = snake;
            this.body = body;
            this.dir = dir;
            this.score = score;
            this.died = died;
            this.alive = alive;
            this.dc = dc;
            this.join = join;
        }
    }
}
