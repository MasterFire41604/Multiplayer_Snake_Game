using SnakeGame;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Model
{
    /// <summary>
    /// A class for snake objects
    /// </summary>
    public class Snake
    {
        // The ID of the snake
        [JsonInclude]
        public int snake;
        // The name of the snake, selected by the user
        [JsonInclude]
        public string name;
        // A list of vector positions marking each body segment
        [JsonInclude]
        public List<Vector2D> body;
        // A unit vector markng the snake's direction
        [JsonInclude]
        public Vector2D dir;
        // The snake's score, or how many powerups have been consumed
        [JsonInclude]
        public int score;
        // A boolean representing whether the snake has died or not
        [JsonInclude]
        public bool died;
        // A boolean representing whether the snake is alive or not
        [JsonInclude]
        public bool alive;
        // A bool indicating if a player has diconnected
        [JsonInclude]
        public bool dc;
        // A bool indicating a player joined this frame
        [JsonInclude]
        public bool join;

        /// <summary>
        /// JSON constructor used for deserializing a JSON string into a snake object
        /// </summary>
        /// <param name="snake"></param>
        /// <param name="name"></param>
        /// <param name="body"></param>
        /// <param name="dir"></param>
        /// <param name="score"></param>
        /// <param name="died"></param>
        /// <param name="alive"></param>
        /// <param name="dc"></param>
        /// <param name="join"></param>
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
