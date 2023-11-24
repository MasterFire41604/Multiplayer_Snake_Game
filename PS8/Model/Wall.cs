using SnakeGame;
using System.Text.Json.Serialization;

namespace Model
{
    /// <summary>
    /// A class for wall objects.
    /// </summary>
    public class Wall
    {
        // An int representing a wall's ID
        [JsonInclude]
        public int wall;
        // A vector representing the first point of the wall
        [JsonInclude]
        public Vector2D p1;
        // A vector representing the second point of the wall
        [JsonInclude]
        public Vector2D p2;

        /// <summary>
        /// A JsonConstructor used for deserializing a JSON string representation of a wall into a wall object.
        /// </summary>
        /// <param name="wall"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        [JsonConstructor]
        public Wall(int wall, Vector2D p1, Vector2D p2)
        {
            this.wall = wall;
            this.p1 = p1;
            this.p2 = p2;
        }
    }
}
