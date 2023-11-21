using NetworkUtil;
using Model;
using System.Text.Json;
using System.Diagnostics;

namespace Controller
{
    public class GameController
    {
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
        // TODO: might be weird
        World theWorld = new(0, -1);


        public World GetWorld()
        {
            return theWorld;
        }

        public void Connect(string addr)
        {
            Networking.ConnectToServer(OnConnect, addr, 11000);
        }

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

        private void ProcessJSON(SocketState state)
        {
            string[] splitData = state.GetData().Split('\n');
            if (splitData[splitData.Length - 1] != "") { splitData[splitData.Length - 1] = ""; }

            if (double.TryParse(splitData[1], out double size))
            {
                theWorld = new(size, int.Parse(splitData[0]));
            }

            // Parse jsonData for snake, wall, and powerup
            foreach (string data in splitData)
            {
                if (data.Contains("{"))
                {
                    
                    JsonDocument doc = JsonDocument.Parse(data);

                    if (doc.RootElement.TryGetProperty("snake", out _))
                    {
                        Snake? snake = JsonSerializer.Deserialize<Snake>(doc);
                        if (theWorld.snakes.ContainsKey(snake!.snake)) { theWorld.snakes[snake!.snake] = snake; }
                        else { theWorld.snakes.Add(snake!.snake, snake); }
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

        }

        public void Send(string message)
        {
            Networking.Send(theServer!.TheSocket, message);
        }
    }
}