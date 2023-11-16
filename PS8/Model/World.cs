using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

namespace Model
{
    public class World
    {
        public Dictionary<int, Snake> snakes;
        public Dictionary<int, Powerup> powerups;
        public Dictionary<int, Wall> walls;
        public int Size { get; private set; }

        public World(int size) 
        {
            snakes = new Dictionary<int, Snake>();
            powerups = new Dictionary<int, Powerup>();
            walls = new Dictionary<int, Wall>();
            Size = size;
        }
    }
}