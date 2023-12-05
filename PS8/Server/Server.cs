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
        private static List<Snake> deadSnakes = new();
        private static List<Snake> growingSnakes = new();

        static void Main (string[] args)
        {
            Server server = new Server();
            server.StartServer();

            Stopwatch watch = new();
            watch.Start();

            XmlDocument doc = new();
            doc.Load("settings.xml");


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

            doc.Load("settings.xml");

            // Process information from settings
            XmlNode worldSize = doc.DocumentElement!.SelectSingleNode("/GameSettings/UniverseSize")!;
            XmlNode respawnRate = doc.DocumentElement!.SelectSingleNode("/GameSettings/RespawnRate")!;

            theWorld = new(double.Parse(worldSize.InnerText), -1);
            theWorld.respawnRate = int.Parse(respawnRate.InnerText);

            CreateInitialPowerups(theWorld.maxPower);
            LoadWalls(doc);
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
            Snake playerSnake = new(playerID, playerName.Substring(0, playerName.Length - 1), body, new Vector2D(1, 0), 0, false, true, false, true);
            RespawnSnake(playerSnake);

            lock (theWorld)
            { 
                theWorld.snakes.Add((int)state.ID, playerSnake);
            }


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
                theWorld.snakes[(int)id].dc = true;
                theWorld.snakes[(int)id].alive = false;
                clients.Remove(id);
            }
        }

        private static void UpdateWorld()
        {
            foreach (SocketState client in clients!.Values)
            {
                StringBuilder worldData = new();
                theWorld!.PlayerID = (int)client.ID;
                Snake clientSnake = theWorld.snakes[(int)client.ID];
            

                // Respawn dead snake after respawnRate frames
                foreach (Snake snake in deadSnakes)
                {
                    snake.died = false;
                    snake.framesDead++;
                    if (snake.framesDead >= theWorld.respawnRate)
                    {
                        RespawnSnake(snake);
                        snake.framesDead = 0;

                    }
                }
                for (int i = 0; i < deadSnakes.Count; i++) 
                {
                    if (deadSnakes[i].alive) 
                    {
                        deadSnakes.Remove(deadSnakes[i]);
                    }
                }

                // Move tail after snakeGrowth frames
                foreach (Snake snake in growingSnakes)
                {
                    snake.framesGrowing++;
                    if (snake.framesGrowing >= theWorld.snakeGrowth)
                    {
                        snake.growing = false;
                        snake.framesGrowing = 0;

                    }
                }
                for (int i = 0; i < growingSnakes.Count; i++)
                {
                    if (!growingSnakes[i].growing)
                    {
                        growingSnakes.Remove(growingSnakes[i]);
                    }
                }


                ProcessMovement((int)client.ID);
                if (clientSnake.alive) { MoveSnake(clientSnake); }


                foreach (Snake snake in theWorld.snakes.Values)
                {
                    worldData.Append(JsonSerializer.Serialize(snake) + "\n");
                }

                foreach (Powerup powerup in theWorld.powerups.Values)
                {
                    worldData.Append(JsonSerializer.Serialize(powerup) + "\n");
                    if (powerup.died) { theWorld.powerups.Remove(powerup.power); }
                }

                /*for (int i = 0; i < theWorld.powerups.Count; i++)
                {
                    if (theWorld.powerups[i].died)
                    {
                        theWorld.powerups.Remove(theWorld.powerups[i].power);
                    }
                }*/


                Networking.Send(client.TheSocket, worldData.ToString());
            }
        }

        private static void MoveSnake(Snake snake)
        {
            // Move head
            snake.body[snake.body.Count - 1] += snake.dir * theWorld.snakeSpeed;
            // Check for collisions with walls
            if (WallCollisionCheck(snake))
            {
                snake.alive = false;
                snake.died = true;
                deadSnakes.Add(snake);
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
                    snake.body.RemoveAt(0);
                    tailDir = (snake.body[1] - snake.body[0]);
                    tailDir.Normalize();
                }
                else { tailDir.Normalize(); }

                snake.body[0] += tailDir * theWorld.snakeSpeed;
            }

            //TODO: Implement wraparound
            /*if (snake.body[snake.body.Count - 1].GetX() > theWorld.Size / 2)
            {
                snake.body[snake.body.Count - 1] = new Vector2D(snake.body[snake.body.Count - 1].GetX() - theWorld.Size, snake.body[snake.body.Count - 1].GetY());
                snake.body.Add(snake.body[snake.body.Count - 1]);
            }*/
        }
        private static void ProcessMovement(int ID) 
        {
            lock (theWorld)
            {
                Snake player = theWorld.snakes[ID];

                switch (commands[ID])
                {
                    case "{\"moving\":\"up\"}\n":
                        if (player.dir.GetY() != -1 && player.dir.GetY() != 1)
                        { 
                            player.dir = new Vector2D(0, -1);
                            player.body.Add(player.body[player.body.Count - 1]);
                        }
                        break;
                    case "{\"moving\":\"down\"}\n":
                        if (player.dir.GetY() != 1 && player.dir.GetY() != -1)
                        {
                            player.dir = new Vector2D(0, 1);
                            player.body.Add(player.body[player.body.Count - 1]);
                        }
                        break;
                    case "{\"moving\":\"left\"}\n":
                        if (player.dir.GetX() != -1 && player.dir.GetX() != 1)
                        {
                            player.dir = new Vector2D(-1, 0);
                            player.body.Add(player.body[player.body.Count - 1]);
                        }
                        break;
                    case "{\"moving\":\"right\"}\n":
                        if (player.dir.GetX() != 1 && player.dir.GetX() != -1)
                        {
                            player.dir = new Vector2D(1, 0);
                            player.body.Add(player.body[player.body.Count - 1]);
                        }
                        break;
                }
                commands[ID] = "";
            }
        }

        private static void CreateInitialPowerups(int powerCount)
        {
            for (int i = 0; i < powerCount; i++)
            {
                Vector2D loc = new
                    (new Random().Next((int)-theWorld.Size / 2, (int)theWorld.Size / 2),
                    new Random().Next((int)-theWorld.Size / 2, (int)theWorld.Size / 2));

                theWorld.powerups.Add(i, new(i, loc, false));
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

        private static bool WallCollisionCheck(Snake snake) 
        {
            Vector2D snakeHead = snake.body[snake.body.Count - 1];
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

                if (snakeHead.GetX() > p1.GetX() - 30 && snakeHead.GetX() < p2.GetX() + 30)
                {
                    if (snakeHead.GetY() > p1.GetY() -30 && snakeHead.GetY() < p2.GetY() + 30)
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
            foreach (Powerup powerup in theWorld.powerups.Values)
            {
                if (snakeHead.GetX() < powerup.loc.GetX() + 10 && snakeHead.GetX() > powerup.loc.GetX() - 10) 
                {
                    if (snakeHead.GetY() < powerup.loc.GetY() + 10 && snakeHead.GetY() > powerup.loc.GetY() - 10)
                    {
                        powerup.died = true;
                        snake.growing = true;
                        snake.score++;
                        growingSnakes.Add(snake);
                        return;
                    }
                }
            }
        }

        private static void RespawnSnake(Snake snake) 
        {
            snake.alive = true;
            snake.score = 0;

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
        }
    }
}
