using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

namespace Model
{
    /// <summary>
    /// A class representing the world, keeping track of all snakes, powerups, walls, the size of the world,
    /// and the active player's ID.
    /// </summary>
    public class World
    {
        // A dictionary of active snakes, relating the snake's ID to the snake object
        [JsonInclude]
        public Dictionary<int, Snake> snakes;
        // A dictionary of powerups, relating the powerup's ID to the powerup object
        public Dictionary<int, Powerup> powerups;
        // A dictionary of walls, relating the wall's ID to the wall object
        public Dictionary<int, Wall> walls;
        // A double representing the size of the world
        public double Size { get; set; }
        // An int representing the active player's ID
        public int PlayerID { get; set; }

        /// <summary>
        /// A constructor that initialized dictionaries and sets the size and playerID.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="playerID"></param>
        public World(double size, int playerID) 
        {
            snakes = new Dictionary<int, Snake>();
            powerups = new Dictionary<int, Powerup>();
            walls = new Dictionary<int, Wall>();
            Size = size;
            PlayerID = playerID;
        }
    }
}