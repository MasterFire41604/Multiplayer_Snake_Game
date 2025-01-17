﻿using System.Text.Json.Serialization;

namespace Model
{
    /// <summary>
    /// A class representing the world, keeping track of all snakes, powerups, walls, the size of the world,
    /// and the active player's ID.
    /// </summary>
    public class World
    {
        // Snake respawn rate (in frames)
        public int respawnRate;
        // Snake speed
        public int snakeSpeed;
        // Snake starting length
        public int snakeStartLength;
        // Snake growth
        public int snakeGrowth;
        // Max powerups
        public int maxPower;
        // Max powerup delay
        public int powerDelay;
        // How many frames between each snake boost
        public int boostStall;
        // How many frames each snake can boost
        public int boostingTime;

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
            snakeSpeed = 6;
            snakeStartLength = 120;
            snakeGrowth = 24;
            maxPower = 20;
            powerDelay = 75;
            respawnRate = 100;
            boostStall = 250;
            boostingTime = 40;

            snakes = new Dictionary<int, Snake>();
            powerups = new Dictionary<int, Powerup>();
            walls = new Dictionary<int, Wall>();
            Size = size;
            PlayerID = playerID;
        }
    }
}