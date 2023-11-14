using NetworkUtil;
using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Controller
{
    public class GameController
    {
        // Controller events that the view can subscribe to
        public delegate void JSONHandler(string json);
        public event JSONHandler? JSONArrived;

        public delegate void ErrorHandler(string err);
        public event ErrorHandler? Error;

        /// <summary>
        /// State representing the connection with the server
        /// </summary>
        SocketState? theServer = null;


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
            // so this same method (ReceiveMessage) 
            // will be invoked when more data arrives
            Networking.GetData(state);
        }

        private void ProcessJSON(SocketState state)
        {
            string jsonData = state.GetData();

            // Use our model

            // 
            JSONArrived?.Invoke(jsonData);
        }
    }
}