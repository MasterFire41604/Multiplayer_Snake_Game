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
using System.Security.Cryptography;
using System;
using System.Reflection;
using System.Diagnostics.Metrics;

namespace Server
{
    class Server
    {
        private static Dictionary<long, SocketState>? clients;
        private static World theWorld = new(0, -1);
        private static Dictionary<long, string> commands = new();
        private static Dictionary<int, Snake> deadSnakes = new();
        private static List<Snake> growingSnakes = new();
        private static List<Snake> boostingSnakes = new();
        private static int powerRespawnFrames = 0;
        private static bool extraModeEnabled = false;
        // TODO: private static int tailShrinkFrames = 0;


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


            while (true)
            {
                while (watch.ElapsedMilliseconds < MSPerFrame) { }

                watch.Restart();

                UpdateWorld();
            }
        }

        /// <summary>
        /// Initialized the server's state
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

        private static void UpdateWorld()
        {
            lock (clients!)
            {
                lock (theWorld)
                {
                    foreach (SocketState client in clients!.Values)
                    {
                        Snake clientSnake = theWorld.snakes[(int)client.ID];
                        clientSnake.died = false;

                        ProcessMovement((int)client.ID);

                        if (!clientSnake.canBoost && (clientSnake.boostStallFrames > theWorld.boostStall))
                        {
                            clientSnake.boostStallFrames = 0;
                            clientSnake.canBoost = true;
                        }
                        else if (!clientSnake.canBoost)
                        {
                            clientSnake.boostStallFrames++;
                        }

                        if (clientSnake.alive)
                        {
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

                        for (int i = 0; i < growingSnakes.Count; i++)
                        {
                            if (!growingSnakes[i].growing)
                            {
                                growingSnakes.Remove(growingSnakes[i]);
                            }
                        }


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

                    StringBuilder worldData = new();

                    foreach (Snake snake in theWorld.snakes.Values)
                    {
                        worldData.Append(JsonSerializer.Serialize(snake) + "\n");
                    }

                    foreach (Powerup powerup in theWorld.powerups.Values)
                    {
                        worldData.Append(JsonSerializer.Serialize(powerup) + "\n");
                        //if (powerup.died)
                        //    theWorld.powerups.Remove(powerup.power);
                    }

                    foreach (Powerup power in theWorld.powerups.Values)
                    {
                        if (power.died)
                        {
                            theWorld.powerups.Remove(power.power);
                        }
                    }

                    //for (int i = 0; i < deadSnakes.Count; i++)
                    foreach (Snake snake in deadSnakes.Values)
                    {
                        if (snake.alive)
                        {
                            deadSnakes.Remove(snake.snake);
                        }
                    }

                    foreach (SocketState client in clients.Values) 
                    {
                        Networking.Send(client.TheSocket, worldData.ToString());
                    }
                }
            }
        }

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

            if (!snake.growing)
            {
                // Move tail
                Vector2D tailDir = (snake.body[1] - snake.body[0]);
                // Check if tail is at next vertex
                if (tailDir.GetX() == 0 && tailDir.GetY() == 0)
                {
                    if (Math.Abs(snake.body[0].GetX()) > theWorld.Size / 2 || Math.Abs(snake.body[0].GetY()) > theWorld.Size / 2)
                    {
                        // Remove tail and temporary vertex
                        snake.body.RemoveAt(0);
                        snake.body.RemoveAt(0);

                        tailDir = (snake.body[1] - snake.body[0]);
                        tailDir.Normalize();
                        return;
                        //break;
                    }

                    snake.body.RemoveAt(0);
                    tailDir = (snake.body[1] - snake.body[0]);
                    tailDir.Normalize();
                }
                else { tailDir.Normalize(); }

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

            WrapAround(snake);
        }
        private static void ProcessMovement(int ID) 
        {
            lock (theWorld)
            {
                Snake player = theWorld.snakes[ID];

                switch (commands[ID])
                {
                    case "{\"moving\":\"up\"}\n":
                        if (Math.Abs(player.body[player.body.Count - 1].GetX() - player.body[player.body.Count - 2].GetX()) > 10 ||
                            player.body[player.body.Count - 1].GetX() == player.body[player.body.Count - 2].GetX())
                        {
                            if (player.dir.GetY() == -1 && player.canBoost)
                            {
                                boostingSnakes.Add(player);
                                player.canBoost = false;
                            }

                            if (player.dir.GetY() != -1 && player.dir.GetY() != 1)
                            {
                                player.dir = new Vector2D(0, -1);
                                player.body.Add(player.body[player.body.Count - 1]);
                            }
                        }
                        break;
                    case "{\"moving\":\"down\"}\n":
                        if (Math.Abs(player.body[player.body.Count - 1].GetX() - player.body[player.body.Count - 2].GetX()) > 10 ||
                            player.body[player.body.Count - 1].GetX() == player.body[player.body.Count - 2].GetX())
                        {
                            if (player.dir.GetY() == 1 && player.canBoost)
                            {
                                boostingSnakes.Add(player);
                                player.canBoost = false;
                            }

                            if (player.dir.GetY() != 1 && player.dir.GetY() != -1)
                            {
                                player.dir = new Vector2D(0, 1);
                                player.body.Add(player.body[player.body.Count - 1]);
                            }
                        }
                        break;
                    case "{\"moving\":\"left\"}\n":
                        if (Math.Abs(player.body[player.body.Count - 1].GetY() - player.body[player.body.Count - 2].GetY()) > 10 ||
                            player.body[player.body.Count - 1].GetY() == player.body[player.body.Count - 2].GetY())
                        {
                            if (player.dir.GetX() == -1 && player.canBoost)
                            {
                                boostingSnakes.Add(player);
                                player.canBoost = false;
                            }

                            if (player.dir.GetX() != -1 && player.dir.GetX() != 1)
                            {
                                player.dir = new Vector2D(-1, 0);
                                player.body.Add(player.body[player.body.Count - 1]);
                            }
                        }
                        break;
                    case "{\"moving\":\"right\"}\n":
                        if (Math.Abs(player.body[player.body.Count - 1].GetY() - player.body[player.body.Count - 2].GetY()) > 10 ||
                            player.body[player.body.Count - 1].GetY() == player.body[player.body.Count - 2].GetY())
                        {
                            if (player.dir.GetX() == 1 && player.canBoost)
                            {
                                boostingSnakes.Add(player);
                                player.canBoost = false;
                            }

                            if (player.dir.GetX() != 1 && player.dir.GetX() != -1)
                            {
                                player.dir = new Vector2D(1, 0);
                                player.body.Add(player.body[player.body.Count - 1]);
                            }
                        }
                        break;
                }
                commands[ID] = "";
            }
        }

        private static void CreateInitialPowerups(int powerCount)
        {
            for (int i = theWorld.powerups.Count; i < powerCount; i++)
            {
                while (theWorld.powerups.ContainsKey(i))
                {
                    i++;
                }

                Vector2D loc = new
                    (new Random().Next((int)-theWorld.Size / 2, (int)theWorld.Size / 2),
                    new Random().Next((int)-theWorld.Size / 2, (int)theWorld.Size / 2));

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

        private static bool WallCollisionCheck(Vector2D loc, int additionalAmount) 
        {
            //if (snakeHead.GetX() <= -1000 || snakeHead.GetY() <= -1000 || snakeHead.GetX() >= 1000 || snakeHead.GetX() >= 1000) { return true; }
            // Check wall collision
            foreach (Wall wall in theWorld.walls.Values)
            {
                Vector2D p1 = wall.p1;
                Vector2D p2 = wall.p2;

                if (p1.GetX() > p2.GetX() || p1.GetY() > p2.GetY())
                {
                    p1 = wall.p2;
                    p2 = wall.p1;
                }

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

        private static void PowerupCollisionCheck(Snake snake)
        {
            Vector2D snakeHead = snake.body[snake.body.Count - 1];
            //if (snakeHead.GetX() <= -1000 || snakeHead.GetY() <= -1000 || snakeHead.GetX() >= 1000 || snakeHead.GetX() >= 1000) { return true; }
            // Check wall collision
            lock (theWorld) 
            {
                foreach (Powerup powerup in theWorld.powerups.Values)
                {
                    if (snakeHead.GetX() < powerup.loc.GetX() + 10 && snakeHead.GetX() > powerup.loc.GetX() - 10)
                    {
                        if (snakeHead.GetY() < powerup.loc.GetY() + 10 && snakeHead.GetY() > powerup.loc.GetY() - 10)
                        {
                            powerup.died = true;

                            //int randPoisonChance = new Random().Next(10);

                            // TODO: Remove?
                            /* if (randPoisonChance > 2)
                            {*/
                            growingSnakes.Add(snake);
                            snake.growing = true;
                            snake.framesGrowing += theWorld.snakeGrowth;
                            snake.score++;
                            /*}*/
                            /*else
                            {
                                tailShrinkFrames += theWorld.snakeGrowth;
                                snake.score++;
                            }*/

                            return;
                        }
                    }
                }
            }
        }

        private static bool SelfCollisionCheck(Snake snake) 
        {
            Vector2D snakeHead = snake.body[snake.body.Count - 1];
            int firstCollisionIndex = 0;

            for (int i = snake.body.Count - 2; i > 0; i--) 
            {
                Vector2D prevSegDir = (snake.body[i] - snake.body[i-1]);
                prevSegDir.Normalize();

                //if (prevSegDir.Equals(new Vector2D(-snake.dir.GetX(), -snake.dir.GetY())))
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


            for (int i = firstCollisionIndex; i > 0; i--) 
            { 
                Vector2D p1 = snake.body[i];
                Vector2D p2 = snake.body[i - 1];


                if (p1.GetX() > p2.GetX() || p1.GetY() > p2.GetY())
                {
                    p1 = snake.body[i - 1];
                    p2 = snake.body[i];
                }

                // Check if we are colliding with an imaginary segment
                if (Math.Abs((p1 - p2).GetX()) > theWorld.Size || Math.Abs((p1 - p2).GetY()) > theWorld.Size)
                {
                    continue;
                }

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

        private static bool SnakeCollisionCheck(Snake playerSnake)
        {
            Vector2D snakeHead = playerSnake.body[playerSnake.body.Count - 1];

            foreach (Snake snake in theWorld.snakes.Values) 
            {
                if (!snake.alive) { continue; }
                if (playerSnake.snake == snake.snake) { continue; }

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

        private static void WrapAround(Snake snake) 
        {
            Vector2D snakeHead = snake.body[snake.body.Count - 1];
            if (snakeHead.GetX() > theWorld.Size / 2)
            {
                Vector2D newSnakeHead = new(-theWorld.Size / 2, snakeHead.GetY());
                snake.body.Add(new Vector2D(newSnakeHead.GetX(), newSnakeHead.GetY()));
                snake.body.Add(newSnakeHead);

            }
            else if (snakeHead.GetX() < -theWorld.Size / 2)
            {
                Vector2D newSnakeHead = new(theWorld.Size / 2, snakeHead.GetY());
                snake.body.Add(new Vector2D(newSnakeHead.GetX(), newSnakeHead.GetY()));
                snake.body.Add(newSnakeHead);

            }
            else if (snakeHead.GetY() > theWorld.Size / 2)
            {
                Vector2D newSnakeHead = new(snakeHead.GetX(), -theWorld.Size / 2);
                snake.body.Add(new Vector2D(newSnakeHead.GetX(), newSnakeHead.GetY()));
                snake.body.Add(newSnakeHead);

            }
            else if (snakeHead.GetY() < -theWorld.Size / 2)
            {
                Vector2D newSnakeHead = new(snakeHead.GetX(), theWorld.Size / 2);
                snake.body.Add(new Vector2D(newSnakeHead.GetX(), newSnakeHead.GetY()));
                snake.body.Add(newSnakeHead);

            }
        }

        private static void KillSnake(Snake snake) 
        {
            snake.alive = false;
            snake.died = true;
            deadSnakes.Add(snake.snake, snake);
            /*lock (theWorld) 
            {
                theWorld.snakes.Remove(snake.snake);
            }*/
        }
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
            int headRandX = new Random().Next((int)-theWorld.Size / 2 + 100, (int)theWorld.Size / 2 - 100);
            int headRandY = new Random().Next((int)-theWorld.Size / 2 + 100, (int)theWorld.Size / 2 - 100);

            // Sets random positon
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

            if (WallCollisionCheck(snake.body[snake.body.Count - 1], 150))
            {
                RespawnSnake(snake);
            }

            theWorld.snakes[snake.snake] = snake;
        }
    }
}
