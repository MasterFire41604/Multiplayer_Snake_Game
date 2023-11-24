using System.Numerics;
using System.Text.Json.Serialization;
using SnakeGame;

namespace Model
{
    /// <summary>
    /// Class for powerup objects
    /// </summary>
    public class Powerup
    {
        // The ID of the powerup
        [JsonInclude]
        public int power;
        // The location of the powerup (as a vector)
        [JsonInclude]
        public Vector2D loc;
        // A boolean for whether the powerup has died or not
        [JsonInclude]
        public bool died;

        /// <summary>
        /// The JsonConstructor for the powerup, this will be used to easily deserialize a JSON as a powerup.
        /// </summary>
        /// <param name="power"></param>
        /// <param name="loc"></param>
        /// <param name="died"></param>
        [JsonConstructor]
        public Powerup(int power, Vector2D loc, bool died) 
        {
            this.power = power;
            this.loc = loc;
            this.died = died;
        }
    }
}
