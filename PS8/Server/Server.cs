// Authors: Isaac Anderson, Ryan Beard, Fall 2023
// Solution for CS3500 PS9 - Snake Server
// University of Utah

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NetworkUtil;
using System.Xml;
using System.Text.Json;
using Model;
using SnakeGame;
using System.Diagnostics;

namespace Server
{
    /// <summary>
    /// Creates a server that will send and recieve information with any client that connects to it.
    /// </summary>
    class Server
    {
        private static Dictionary<long, SocketState>? clients;  // A collection of clients connected to the server
        private static World theWorld = new(0, -1); // The world being sent to the clients
        private static Dictionary<long, string> commands = new();   // A collection of commands sent from the clients
        private static Dictionary<int, Snake> deadSnakes = new();   // A collection of dead snakes for handling them
        private static List<Snake> growingSnakes = new();   // A collection of growing snakes for handling them
        private static List<Snake> boostingSnakes = new();  // A collection of growing snakes for handling them
        private static int powerRespawnFrames = 0;  // The number of frames it's taken for respawning powerups
        private static bool extraModeEnabled = false;   // Whether additional features are going to be included or not

        /// <summary>
        /// Starts the main loop to update the world every frame.
        /// The number of frames per second is set in the settings file.
        /// </summary>
        /// <param name="args"></param>
        static void Main (string[] args)
        {
            Server server = new Server();
            server.StartServer();

            Stopwatch watch = new();
            watch.Start();

            XmlDocument doc = new();
            doc.Load("../../../settings.xml");

            XmlNode MSPerFrameDoc = doc.DocumentElement!.SelectSingleNode("/GameSettings/MSPerFrame")!;
            int MSPerFrame = int.Parse(MSPerFrameDoc.InnerText);

            // The main update loop updating the world every MSPerFrame frames per second
            while (true)
            {
                while (watch.ElapsedMilliseconds < MSPerFrame) { }

                watch.Restart();

                UpdateWorld();
            }
        }

        /// <summary>
        /// Initializes the server's state
        /// </summary>
        public Server()
        {
            XmlDocument doc = new();

            clients = new Dictionary<long, SocketState>();

            doc.Load("../../../settings.xml");

            // Process information from settings
            XmlNode worldSize = doc.DocumentElement!.SelectSingleNode("/GameSettings/UniverseSize")!;
            XmlNode respawnRate = doc.DocumentElement!.SelectSingleNode("/GameSettings/RespawnRate")!;
            theWorld = new(double.Parse(worldSize.InnerText), -1);
            theWorld.respawnRate = int.Parse(respawnRate.InnerText);

            // See whether the settings contains basic or extra mode
            XmlNode mode = doc.DocumentElement!.SelectSingleNode("/GameSettings/Mode")!;
            if (mode != null) 
            {
                if (mode.InnerText.ToLower() == "basic")
                    extraModeEnabled = false;
                if (mode.InnerText.ToLower() == "extra")
                    extraModeEnabled = true;
            }

            // Loads in the initial walls and powerups
            LoadWalls(doc);
            CreateInitialPowerups(theWorld.maxPower);
        }

        /// <summary>
        /// Start accepting Tcp sockets connections from clients
        /// </summary>
        public void StartServer()
        {
            // This begins an "event loop"
            Networking.StartServer(NewClientConnected, 11000);

            Console.WriteLine("Server is running");
        }

        /// <summary>
        /// Method to be invoked by the networking library
        /// when a new client connects (see line 41)
        /// </summary>
        /// <param name="state">The SocketState representing the new client</param>
        private void NewClientConnected(SocketState state)
        {
            if (state.ErrorOccurred)
                return;

            IPEndPoint stateEndpoint = (IPEndPoint)state.TheSocket.RemoteEndPoint!;
            Console.WriteLine("Accepted new Connection from " + stateEndpoint.Address + " : " + stateEndpoint.Port);

            // change the state's network action to the 
            // receive handler so we can process data when something
            // happens on the network
            state.OnNetworkAction = ReceivePlayerName;

            Networking.GetData(state);
        }

        /// <summary>
        /// Method to be invoked by the networking library
        /// when a network action occurs (see lines 64-66)
        /// </summary>
        /// <param name="state"></param>
        private void ReceivePlayerName(SocketState state)
        {
            int playerID = (int)state.ID;

            // Remove the client if they aren't still connected
            if (state.ErrorOccurred)
            {
                RemoveClient(playerID);
                return;
            }

            // Process name and then remove it from the state
            string playerName = state.GetData();
            state.RemoveData(0, playerName.Length);
            playerID = (int)state.ID;

            // Server shows that the player has connected successfully
            Console.WriteLine("Player(" + playerID + ")\"" + playerName.Substring(0, playerName.Length - 1) + "\" joined");

            // Create a new snake
            List<Vector2D> body = new()
            {
                new Vector2D(0, 0),
                new Vector2D(theWorld.snakeStartLength, 0)
            };
            theWorld!.PlayerID = playerID;
            Snake playerSnake = new(playerID, playerName.Substring(0, playerName.Length - 1), body, new Vector2D(1, 0), 0, false, true, false, true);

            lock (theWorld)
            { 
                theWorld.snakes.Add((int)state.ID, playerSnake);
            }

            // Spawns the snake in a random location with random direction
            RespawnSnake(playerSnake);

            // Send startup info
            StringBuilder startupInfo = new();
            startupInfo.Append(playerID + "\n" + theWorld.Size + "\n");
            foreach (Wall wall in theWorld.walls.Values)
            {
                startupInfo.Append(JsonSerializer.Serialize(wall) + "\n");
            }
            Networking.Send(state.TheSocket, startupInfo.ToString());


            // Save the client state
            // Need to lock here because clients can disconnect at any time
            lock (clients!)
            {
                clients[playerID] = state;
                commands.Add(state.ID, "");
            }

            // Next strings sent from client should be movement commands
            state.OnNetworkAction = ReceiveCommand;

            // Continue the event loop that receives messages from this client
            Networking.GetData(state);
        }

        /// <summary>
        /// Method to be invoked by the networking library
        /// when a network action occurs (see lines 64-66)
        /// </summary>
        /// <param name="state"></param>
        private void ReceiveCommand(SocketState state)
        {
            // Remove the client if they aren't still connected
            if (state.ErrorOccurred)
            {
                RemoveClient(state.ID);
                return;
            }

            ProcessCommands(state);

            // Continue the event loop that receives messages from this client
            Networking.GetData(state);
        }


        /// <summary>
        /// Given the data that has arrived so far, 
        /// potentially from multiple receive operations, 
        /// determine if we have enough to make a complete message,
        /// and process it (print it and broadcast it to other clients).
        /// </summary>
        /// <param name="sender">The SocketState that represents the client</param>
        private void ProcessCommands(SocketState state)
        {
            string totalData = state.GetData();

            string[] parts = Regex.Split(totalData, @"(?<=[\n])");

            // Loop until we have processed all messages.
            // We may have received more than one.
            foreach (string p in parts)
            {
                // Ignore empty strings added by the regex splitter
                if (p.Length == 0)
                    continue;
                // The regex splitter will include the last string even if it doesn't end with a '\n',
                // So we need to ignore it if this happens. 
                if (p[p.Length - 1] != '\n')
                    break;

                // Remove it from the SocketState's growable buffer
                state.RemoveData(0, p.Length);

                // Broadcast the message to all clients
                // Lock here beccause we can't have new connections 
                // adding while looping through the clients list.
                // We also need to remove any disconnected clients.
                HashSet<long> disconnectedClients = new HashSet<long>();
                lock (clients!)
                {
                    foreach (SocketState client in clients.Values)
                    {
                        if (!Networking.Send(client.TheSocket!, ""))
                            disconnectedClients.Add(client.ID);
                    }
                }
                foreach (long id in disconnectedClients)
                    RemoveClient(id);
            }


            commands[state.ID] = parts[0];
        }

        /// <summary>
        /// Removes a client from the clients dictionary
        /// </summary>
        /// <param name="id">The ID of the client</param>
        private void RemoveClient(long id)
        {
            Console.WriteLine("Client " + id + " disconnected");
            lock (clients!)
            {
                if (theWorld.snakes.ContainsKey((int)id))
                {
                    theWorld.snakes[(int)id].dc = true;
                    theWorld.snakes[(int)id].alive = false;
                }
                else
                {
                    deadSnakes[(int)id].dc = true;
                    deadSnakes[(int)id].alive = false;
                }

                clients.Remove(id);
            }
        }

        /// <summary>
        /// Updates the world for each client every frame
        /// </summary>
        private static void UpdateWorld()
        {
            lock (clients!)
            {
                lock (theWorld)
                {
                    foreach (SocketState client in clients!.Values)
                    {
                        // Gets the current client's snake
                        Snake clientSnake = theWorld.snakes[(int)client.ID];
                        clientSnake.died = false;

                        // Checks for any turning commands sent from the client
                        ProcessMovement((int)client.ID);

                        // With the additional feature, if the snake has used a boost already then wait boostStall (setting in theWorld) amount of frames
                        // before allowing the snake to be able to boost again.
                        if (!clientSnake.canBoost && (clientSnake.boostStallFrames > theWorld.boostStall))
                        {
                            clientSnake.boostStallFrames = 0;
                            clientSnake.canBoost = true;
                        }
                        else if (!clientSnake.canBoost)
                        {
                            clientSnake.boostStallFrames++;
                        }

                        // If the client's snake is alive, move it.
                        if (clientSnake.alive)
                        {
                            // If the settings file allows for the extra mode, and the snake is boosting - boost for the set amount of frames
                            if (extraModeEnabled && boostingSnakes.Contains(clientSnake))
                            {
                                if (clientSnake.boostingTimeFrames > theWorld.boostingTime)
                                {
                                    clientSnake.boostStallFrames = 0;
                                    clientSnake.boostingTimeFrames = 0;
                                    boostingSnakes.Remove(clientSnake);
                                }
                                else
                                {
                                    // This is where the snake is boosting (an additional move snake makes the snake move twice as fast)
                                    clientSnake.boostingTimeFrames++;
                                    MoveSnake(clientSnake, false);
                                }
                            }

                            MoveSnake(clientSnake);
                        }


                        // Respawn dead snake after respawnRate frames
                        if (deadSnakes.ContainsKey(clientSnake.snake))
                        {
                            clientSnake.framesDead++;
                            if (clientSnake.framesDead >= theWorld.respawnRate)
                            {
                                RespawnSnake(clientSnake);
                                clientSnake.framesDead = 0;

                            }
                        }

                        // Move tail after snakeGrowth frames
                        if (growingSnakes.Contains(clientSnake))
                        {
                            clientSnake.framesGrowing--;
                            if (clientSnake.framesGrowing <= 0)
                            {
                                clientSnake.growing = false;
                            }
                        }

                        // If the snakes in growingSnakes are no longer growing, remove them from the list
                        for (int i = 0; i < growingSnakes.Count; i++)
                        {
                            if (!growingSnakes[i].growing)
                            {
                                growingSnakes.Remove(growingSnakes[i]);
                            }
                        }

                        // If there are powerups that have been collected, respawn the missing powerups after a set delay
                        if (theWorld.powerups.Count < theWorld.maxPower)
                        {
                            powerRespawnFrames++;

                            if (powerRespawnFrames >= theWorld.powerDelay)
                            {
                                powerRespawnFrames = 0;
                                CreateInitialPowerups(theWorld.maxPower - theWorld.powerups.Count);
                            }
                        }
                    }

                    // Creates a string to store all the world information to send to the clients
                    StringBuilder worldData = new();

                    // Adds world information to the string
                    foreach (Snake snake in theWorld.snakes.Values)
                    {
                        worldData.Append(JsonSerializer.Serialize(snake) + "\n");
                    }

                    foreach (Powerup powerup in theWorld.powerups.Values)
                    {
                        worldData.Append(JsonSerializer.Serialize(powerup) + "\n");
                    }

                    // Removes the collected powerups from the world
                    foreach (Powerup power in theWorld.powerups.Values)
                    {
                        if (power.died)
                        {
                            theWorld.powerups.Remove(power.power);
                        }
                    }

                    // Removes respawned snakes from the deadSnakes dictionary
                    foreach (Snake snake in deadSnakes.Values)
                    {
                        if (snake.alive)
                        {
                            deadSnakes.Remove(snake.snake);
                        }
                    }

                    // Sends the world information to each client
                    foreach (SocketState client in clients.Values) 
                    {
                        Networking.Send(client.TheSocket, worldData.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Moves the head of the snake in the current direction.
        /// Moves the tail of the snake in the direction of the next point in the snake.
        /// </summary>
        /// <param name="snake">The snake that is getting moved</param>
        /// <param name="killSnake">Whether to check for collisions (only used for the additional movement call for the boost)</param>
        private static void MoveSnake(Snake snake, bool killSnake = true)
        {
            // Move head
            snake.body[snake.body.Count - 1] += snake.dir * theWorld.snakeSpeed;

            // Check for collisions with walls
            if (WallCollisionCheck(snake.body[snake.body.Count - 1], 0))
            {
                if (killSnake)
                    KillSnake(snake);
                return;
            }

            // Check for collisions with powerups
            PowerupCollisionCheck(snake);

            // Move tail if the snake is not currently growing
            if (!snake.growing)
            {
                Vector2D tailDir = (snake.body[1] - snake.body[0]);

                // Check if tail is at next vertex
                if (tailDir.GetX() == 0 && tailDir.GetY() == 0)
                {
                    // If wrapping around the edges of the world
                    if (Math.Abs(snake.body[0].GetX()) > theWorld.Size / 2 || Math.Abs(snake.body[0].GetY()) > theWorld.Size / 2)
                    {
                        // Remove tail and temporary vertex
                        snake.body.RemoveAt(0);
                        snake.body.RemoveAt(0);

                        // Gets the new tail direction from the vertex on the opposite side of the world and moves the new tail
                        tailDir = (snake.body[1] - snake.body[0]);
                        tailDir.Normalize();
                        snake.body[0] += tailDir * theWorld.snakeSpeed;
                        return;
                    }

                    // If the tail has reached the next vertex, remove it and set the next vertex to be the new tail
                    snake.body.RemoveAt(0);
                    tailDir = (snake.body[1] - snake.body[0]);
                    tailDir.Normalize();
                }
                else { tailDir.Normalize(); }

                // Moves the tail
                snake.body[0] += tailDir * theWorld.snakeSpeed;
            }

            // Check for collisions with self
            if (SelfCollisionCheck(snake))
            {
                if (killSnake)
                    KillSnake(snake);
                return;
            }

            // Check for collisions with other snakes
            if (SnakeCollisionCheck(snake))
            {
                if (killSnake)
                    KillSnake(snake);
                return;
            }

            // Checks for any world wraparound
            WrapAround(snake);
        }

        /// <summary>
        /// Checks for movement inputs sent from the client
        /// </summary>
        /// <param name="ID">The client ID</param>
        private static void ProcessMovement(int ID) 
        {
            lock (theWorld)
            {
                Snake player = theWorld.snakes[ID];

                // Checks for directional movements
                switch (commands[ID])
                {
                    case "{\"moving\":\"up\"}\n":
                        // If the snake is doing a 180 degree turn before they've moved at least the snakes width.
                        if (Math.Abs(player.body[player.body.Count - 1].GetX() - player.body[player.body.Count - 2].GetX()) > 10 ||
                            player.body[player.body.Count - 1].GetX() == player.body[player.body.Count - 2].GetX())
                        {
                            // Checking if the snake is wanting to boost
                            if (player.dir.GetY() == -1 && player.canBoost)
                            {
                                boostingSnakes.Add(player);
                                player.canBoost = false;
                            }

                            // If the same/opposite instruction was sent, don't add an additional vertex to the snake
                            if (player.dir.GetY() != -1 && player.dir.GetY() != 1)
                            {
                                player.dir = new Vector2D(0, -1);
                                player.body.Add(player.body[player.body.Count - 1]);
                            }
                        }
                        break;
                    case "{\"moving\":\"down\"}\n":
                        // If the snake is doing a 180 degree turn before they've moved at least the snakes width.
                        if (Math.Abs(player.body[player.body.Count - 1].GetX() - player.body[player.body.Count - 2].GetX()) > 10 ||
                            player.body[player.body.Count - 1].GetX() == player.body[player.body.Count - 2].GetX())
                        {
                            // Checking if the snake is wanting to boost
                            if (player.dir.GetY() == 1 && player.canBoost)
                            {
                                boostingSnakes.Add(player);
                                player.canBoost = false;
                            }

                            // If the same/opposite instruction was sent, don't add an additional vertex to the snake
                            if (player.dir.GetY() != 1 && player.dir.GetY() != -1)
                            {
                                player.dir = new Vector2D(0, 1);
                                player.body.Add(player.body[player.body.Count - 1]);
                            }
                        }
                        break;
                    case "{\"moving\":\"left\"}\n":
                        // If the snake is doing a 180 degree turn before they've moved at least the snakes width.
                        if (Math.Abs(player.body[player.body.Count - 1].GetY() - player.body[player.body.Count - 2].GetY()) > 10 ||
                            player.body[player.body.Count - 1].GetY() == player.body[player.body.Count - 2].GetY())
                        {
                            // Checking if the snake is wanting to boost
                            if (player.dir.GetX() == -1 && player.canBoost)
                            {
                                boostingSnakes.Add(player);
                                player.canBoost = false;
                            }

                            // If the same/opposite instruction was sent, don't add an additional vertex to the snake
                            if (player.dir.GetX() != -1 && player.dir.GetX() != 1)
                            {
                                player.dir = new Vector2D(-1, 0);
                                player.body.Add(player.body[player.body.Count - 1]);
                            }
                        }
                        break;
                    case "{\"moving\":\"right\"}\n":
                        // If the snake is doing a 180 degree turn before they've moved at least the snakes width.
                        if (Math.Abs(player.body[player.body.Count - 1].GetY() - player.body[player.body.Count - 2].GetY()) > 10 ||
                            player.body[player.body.Count - 1].GetY() == player.body[player.body.Count - 2].GetY())
                        {
                            // Checking if the snake is wanting to boost
                            if (player.dir.GetX() == 1 && player.canBoost)
                            {
                                boostingSnakes.Add(player);
                                player.canBoost = false;
                            }

                            // If the same/opposite instruction was sent, don't add an additional vertex to the snake
                            if (player.dir.GetX() != 1 && player.dir.GetX() != -1)
                            {
                                player.dir = new Vector2D(1, 0);
                                player.body.Add(player.body[player.body.Count - 1]);
                            }
                        }
                        break;
                }

                // Clears the command sent so it doesn't keep stacking up infinitely and mess with future commands
                commands[ID] = "";
            }
        }

        /// <summary>
        /// Spawns powerups in a random valid location in the world
        /// </summary>
        /// <param name="powerCount">The number of powerups to spawn</param>
        private static void CreateInitialPowerups(int powerCount)
        {
            // Starts at the size of the powerups dictionary to not conflict with existing keys when respawning
            for (int i = theWorld.powerups.Count; i < powerCount; i++)
            {
                // When respawning multiple powerups, change the key to a non-existing key if necessary
                while (theWorld.powerups.ContainsKey(i))
                {
                    i++;
                }

                // Sets the location to a random location in the world
                Vector2D loc = new
                    (new Random().Next((int)-theWorld.Size / 2, (int)theWorld.Size / 2),
                    new Random().Next((int)-theWorld.Size / 2, (int)theWorld.Size / 2));

                // Checks if powerup is currently in a wall. If so, redo the spawning process for that powerup
                if (WallCollisionCheck(loc, 10))
                {
                    i--;
                    continue;
                }

                lock (theWorld)
                {
                    theWorld.powerups.Add(i, new(i, loc, false));
                }
            }
        }

        /// <summary>
        /// Loads the walls from the settings file and puts them in the world
        /// </summary>
        /// <param name="doc"></param>
        private static void LoadWalls(XmlDocument doc)
        { 
            foreach (XmlElement element in doc.DocumentElement!.SelectSingleNode("/GameSettings/Walls")!)
            {
                int wallID = int.Parse(element.SelectSingleNode("ID")!.InnerText);
                Vector2D p1 = new(int.Parse(element.SelectSingleNode("p1/x")!.InnerText),
                    int.Parse(element.SelectSingleNode("p1/y")!.InnerText));
                Vector2D p2 = new(int.Parse(element.SelectSingleNode("p2/x")!.InnerText),
                    int.Parse(element.SelectSingleNode("p2/y")!.InnerText));

                Wall wall = new(wallID, p1, p2);
                theWorld.walls.Add(wallID, wall);
            }

        }

        /// <summary>
        /// Checks if the given object's location is within the bounds of a wall
        /// </summary>
        /// <param name="loc">The location of the object (whether snake or powerup)</param>
        /// <param name="additionalAmount">"Increase" the size of the wall to make objects spawn further away from the wall</param>
        /// <returns></returns>
        private static bool WallCollisionCheck(Vector2D loc, int additionalAmount) 
        {
            // Check for collision with each wall
            foreach (Wall wall in theWorld.walls.Values)
            {
                Vector2D p1 = wall.p1;
                Vector2D p2 = wall.p2;

                if (p1.GetX() > p2.GetX() || p1.GetY() > p2.GetY())
                {
                    p1 = wall.p2;
                    p2 = wall.p1;
                }

                // Checks within the width of the walls while also accounting for the width of the snake/powerup
                if (loc.GetX() > p1.GetX() - 30 - additionalAmount && loc.GetX() < p2.GetX() + 30 + additionalAmount)
                {
                    if (loc.GetY() > p1.GetY() - 30 - additionalAmount && loc.GetY() < p2.GetY() + 30 + additionalAmount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the given snake's location is within the bounds of a powerup
        /// </summary>
        /// <param name="snake">The snake checking for collision</param>
        private static void PowerupCollisionCheck(Snake snake)
        {
            Vector2D snakeHead = snake.body[snake.body.Count - 1];

            // Check for collision with each powerup
            lock (theWorld) 
            {
                foreach (Powerup powerup in theWorld.powerups.Values)
                {
                    // Checks within the width of the powerup while also accounting for the width of the snake
                    if (snakeHead.GetX() < powerup.loc.GetX() + 10 && snakeHead.GetX() > powerup.loc.GetX() - 10)
                    {
                        if (snakeHead.GetY() < powerup.loc.GetY() + 10 && snakeHead.GetY() > powerup.loc.GetY() - 10)
                        {
                            powerup.died = true;
                            growingSnakes.Add(snake);
                            snake.growing = true;
                            snake.framesGrowing += theWorld.snakeGrowth;
                            snake.score++;
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the given snake has collided with it's own body
        /// </summary>
        /// <param name="snake">The snake checking for the collision</param>
        /// <returns>True if the snake has collided with itself</returns>
        private static bool SelfCollisionCheck(Snake snake) 
        {
            Vector2D snakeHead = snake.body[snake.body.Count - 1];
            int firstCollisionIndex = 0;

            // Starts looking at each segment of the snake excluding the head
            for (int i = snake.body.Count - 2; i > 0; i--) 
            {
                Vector2D prevSegDir = (snake.body[i] - snake.body[i-1]);
                prevSegDir.Normalize();

                // If snake is facing opposite direction of the segment's direction, that segement and everything before it is available to collide with
                if (prevSegDir.GetX() != 0 && prevSegDir.GetX() == -snake.dir.GetX())
                {
                    firstCollisionIndex = i;
                    break;
                }
                if (prevSegDir.GetY() != 0 && prevSegDir.GetY() == -snake.dir.GetY())
                {
                    firstCollisionIndex = i;
                    break;
                }
            }

            // Checks for collision from the first part of the snake available to collide with down to the tail
            for (int i = firstCollisionIndex; i > 0; i--) 
            { 
                Vector2D p1 = snake.body[i];
                Vector2D p2 = snake.body[i - 1];

                if (p1.GetX() > p2.GetX() || p1.GetY() > p2.GetY())
                {
                    p1 = snake.body[i - 1];
                    p2 = snake.body[i];
                }

                // Check if we are colliding with an imaginary segment (used for wraparound)
                if (Math.Abs((p1 - p2).GetX()) > theWorld.Size || Math.Abs((p1 - p2).GetY()) > theWorld.Size)
                {
                    continue;
                }

                // If the head of the snake is within any of it's segments areas, it has collided
                if (snakeHead.GetX() > p1.GetX() - 10 && snakeHead.GetX() < p2.GetX() + 10) 
                {
                    if (snakeHead.GetY() > p1.GetY() - 10 && snakeHead.GetY() < p2.GetY() + 10)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checking for snake collisions with other snakes
        /// </summary>
        /// <param name="playerSnake">The snake checking for the collision</param>
        /// <returns>True if the player snake has collided with another snake</returns>
        private static bool SnakeCollisionCheck(Snake playerSnake)
        {
            Vector2D snakeHead = playerSnake.body[playerSnake.body.Count - 1];

            foreach (Snake snake in theWorld.snakes.Values) 
            {
                // No need to check for collision with a dead snake or itself
                if (!snake.alive) { continue; }
                if (playerSnake.snake == snake.snake) { continue; }

                // Loops through each segment of each snake. If the player's snake head is within any other snake's segment areas, the player snake has collided
                for (int i = snake.body.Count - 1; i > 0; i--)
                {
                    Vector2D p1 = snake.body[i];
                    Vector2D p2 = snake.body[i - 1];

                    if (p1.GetX() > p2.GetX() || p1.GetY() > p2.GetY())
                    {
                        p1 = snake.body[i - 1];
                        p2 = snake.body[i];
                    }

                    if (snakeHead.GetX() > p1.GetX() - 10 && snakeHead.GetX() < p2.GetX() + 10)
                    {
                        if (snakeHead.GetY() > p1.GetY() - 10 && snakeHead.GetY() < p2.GetY() + 10)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// "Wraps" the given snake around the world if they go over the edge of the world.
        /// </summary>
        /// <param name="snake">The snake checking whether to wrap around the world</param>
        private static void WrapAround(Snake snake) 
        {
            Vector2D snakeHead = snake.body[snake.body.Count - 1];

            // Checks if the snake is outside the bounds of the world on each side.
            // If so, adds a new vertex at the other side of the world, and also adds a new head at that point
            if (snakeHead.GetX() > theWorld.Size / 2)
            {
                Vector2D newSnakeHead = new(-theWorld.Size / 2, snakeHead.GetY());
                snake.body.Add(newSnakeHead);
                snake.body.Add(newSnakeHead);

            }
            else if (snakeHead.GetX() < -theWorld.Size / 2)
            {
                Vector2D newSnakeHead = new(theWorld.Size / 2, snakeHead.GetY());
                snake.body.Add(newSnakeHead);
                snake.body.Add(newSnakeHead);

            }
            else if (snakeHead.GetY() > theWorld.Size / 2)
            {
                Vector2D newSnakeHead = new(snakeHead.GetX(), -theWorld.Size / 2);
                snake.body.Add(newSnakeHead);
                snake.body.Add(newSnakeHead);

            }
            else if (snakeHead.GetY() < -theWorld.Size / 2)
            {
                Vector2D newSnakeHead = new(snakeHead.GetX(), theWorld.Size / 2);
                snake.body.Add(newSnakeHead);
                snake.body.Add(newSnakeHead);

            }
        }

        /// <summary>
        /// Kills the given snake
        /// </summary>
        /// <param name="snake">The snake getting killed</param>
        private static void KillSnake(Snake snake) 
        {
            snake.alive = false;
            snake.died = true;
            deadSnakes.Add(snake.snake, snake);
        }

        /// <summary>
        /// Respawns the given snake at a random location in the world with a random rotation.
        /// </summary>
        /// <param name="snake">The snake to respawn</param>
        private static void RespawnSnake(Snake snake)
        {
            // Reset state
            snake.alive = true;
            snake.score = 0;
            snake.boostingTimeFrames = 0;
            snake.boostStallFrames = 0;
            snake.canBoost = true;
            if (boostingSnakes.Contains(snake))
                boostingSnakes.Remove(snake);

            // Set random direction
            switch (new Random().Next(4))
            {
                case 0:
                    snake.dir = new Vector2D(0, 1);
                    break;
                case 1:
                    snake.dir = new Vector2D(0, -1);
                    break;
                case 2:
                    snake.dir = new Vector2D(1, 0);
                    break;
                case 3:
                    snake.dir = new Vector2D(-1, 0);
                    break;
            }

            // Set random position
            int headRandX = new Random().Next((int)-theWorld.Size / 2 + 100, (int)theWorld.Size / 2 - 100);
            int headRandY = new Random().Next((int)-theWorld.Size / 2 + 100, (int)theWorld.Size / 2 - 100);

            // Creates a new body for the snake with the given random rotation and position
            List<Vector2D> body;
            if (snake.dir.GetY() == -1)
            {
                body = new()
                {
                    new Vector2D(headRandX, headRandY + theWorld.snakeStartLength),
                    new Vector2D(headRandX, headRandY)
                };
            }
            else if (snake.dir.GetY() == 1)
            {
                body = new()
                {
                    new Vector2D(headRandX, headRandY - theWorld.snakeStartLength),
                    new Vector2D(headRandX, headRandY)
                };
            }
            else if (snake.dir.GetX() == -1)
            {
                body = new()
                {
                    new Vector2D(headRandX + theWorld.snakeStartLength, headRandY),
                    new Vector2D(headRandX, headRandY)
                };
            }
            else
            {
                body = new()
                {
                    new Vector2D(headRandX - theWorld.snakeStartLength, headRandY),
                    new Vector2D(headRandX, headRandY)
                };
            }

            snake.body = body;

            // Checks if the newly respawned snake is inside a wall, if so - respawn it again
            if (WallCollisionCheck(snake.body[snake.body.Count - 1], 150))
            {
                RespawnSnake(snake);
            }

            theWorld.snakes[snake.snake] = snake;
        }
    }
}
