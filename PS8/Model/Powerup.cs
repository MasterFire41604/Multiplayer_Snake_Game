using System.Numerics;
using System.Text.Json.Serialization;
using SnakeGame;

namespace Model
{
    public class Powerup
    {
        [JsonInclude]
        public int power;
        [JsonInclude]
        public Vector2D loc;
        [JsonInclude]
        public bool died;

        [JsonConstructor]
        public Powerup(int power, Vector2D loc, bool died) 
        {
            this.power = power;
            this.loc = loc;
            this.died = died;
        }
    }
}
