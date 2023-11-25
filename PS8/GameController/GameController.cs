using NetworkUtil;
using Model;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Controller
{
    /// <summary>
    /// This class is the controller for the game. Deals with network handling including processing the recieved data from the server
    /// and sending data to the server.
    /// </summary>
    public class GameController
    {
        // This will be assigned the control to be sent to the server
        private string controlSent = "";

        // Controller events that the view can subscribe to
        public delegate void JSONHandler();
        public event JSONHandler? JSONProcess;

        public delegate void ConnectedHandler();
        public event ConnectedHandler? Connected;

        public delegate void ErrorHandler(string err);
        public event ErrorHandler? Error;

        /// <summary>
        /// State representing the connection with the server
        /// </summary>
        SocketState? theServer = null;
        // A World object containg snakes, powerups, walls, the size of the world, and the player's ID. Takes in 0 for the size
        // and -1 for the player ID to start (which will be changed later).
        World theWorld = new(0, -1);

        
        /// <summary>
        /// Returns the world
        /// </summary>
        /// <returns>thWorld object</returns>
        public World GetWorld()
        {
            return theWorld;
        }
        
        /// <summary>
        /// A method that sets controlSent
        /// </summary>
        /// <param name="controlSent">The command to be sent to the server</param>
        public void SetControlSent(string controlSent) { this.controlSent = controlSent; }


        /// <summary>
        /// Connects to the server.
        /// </summary>
        /// <param name="addr">Server address to connect to</param>
        public void Connect(string addr)
        {
            Networking.ConnectToServer(OnConnect, addr, 11000);
        }

        /// <summary>
        /// Method that is called when the server starts the connection process. This will start the receive event loop to 
        /// continually recieve data.
        /// </summary>
        /// <param name="state">The SocketState for the server</param>
        private void OnConnect(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                // inform the view
                Error?.Invoke("Error connecting to server");
                return;
            }

            theServer = state;

            // inform the view
            Connected?.Invoke();

            // Start an event loop to receive messages from the server
            state.OnNetworkAction = ReceiveJSON;
            Networking.GetData(state);
        }

        /// <summary>
        /// This method is called when data is recieved from the server. This method calls another message to process the JSONs
        /// received from the server.
        /// </summary>
        /// <param name="state">The server SocketState</param>
        private void ReceiveJSON(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                // inform the view
                Error?.Invoke("Lost connection to server");
                return;
            }

            ProcessJSON(state);

            // Continue the event loop
            // state.OnNetworkAction has not been changed, 
            // so this same method (ReceiveJSON) 
            // will be invoked when more data arrives
            Networking.GetData(state);
        }

        /// <summary>
        /// This method proccesses the JSON strings sent by the server. If there is a JSON, this method will decide
        /// what it represents and deserialize it accordingly, updating the world after.
        /// </summary>
        /// <param name="state">SocketState for the server</param>
        private void ProcessJSON(SocketState state)
        {
            string[] splitData = Regex.Split(state.GetData(), @"(?<=[\n])");

            // Check for world size and player ID
            if (double.TryParse(splitData[1], out double size))
            {
                theWorld = new(size, int.Parse(splitData[0]));
            }

            List<string> dataList = new List<string>();

            foreach (string data in splitData)
            {
                // Ignore empty strings added by the regex splitter
                if (data.Length == 0)
                    continue;
                // The regex splitter will include the last string even if it doesn't end with a '\n',
                // So we need to ignore it if this happens. 
                if (data[data.Length - 1] != '\n')
                    break;

                // build a list of messages to send to the view
                dataList.Add(data);

                // Then remove it from the SocketState's growable buffer
                state.RemoveData(0, data.Length);
            }


            // Parse jsonData for snake, wall, and powerup
            foreach (string data in dataList)
            {
                if (data.Contains("{"))
                {

                    JsonDocument doc = JsonDocument.Parse(data);

                    if (doc.RootElement.TryGetProperty("snake", out _))
                    {
                        Snake? snake = JsonSerializer.Deserialize<Snake>(doc);
                        if (theWorld.snakes.ContainsKey(snake!.snake)) { theWorld.snakes[snake!.snake] = snake; }
                        else
                        {
                            theWorld.snakes.Add(snake!.snake, snake);
                        }
                    }
                    if (doc.RootElement.TryGetProperty("power", out _))
                    {
                        Powerup? powerup = JsonSerializer.Deserialize<Powerup>(doc);
                        if (theWorld.powerups.ContainsKey(powerup!.power)) { theWorld.powerups[powerup!.power] = powerup; }
                        else { theWorld.powerups.Add(powerup!.power, powerup); }
                    }
                    if (doc.RootElement.TryGetProperty("wall", out _))
                    {
                        Wall? wall = JsonSerializer.Deserialize<Wall>(doc);
                        if (theWorld.walls.ContainsKey(wall!.wall)) { theWorld.walls[wall!.wall] = wall; }
                        else { theWorld.walls.Add(wall!.wall, wall); }
                    }
                }
            }
            
            JSONProcess?.Invoke();
            // Check to see if any controls are supposed to be sent
            if (controlSent != "") { Send(controlSent); }
            controlSent = "";
        }

        /// <summary>
        /// Method to send data to the server
        /// </summary>
        /// <param name="message"></param>
        public void Send(string message)
        {
            Networking.Send(theServer!.TheSocket, message);
        }
    }
}