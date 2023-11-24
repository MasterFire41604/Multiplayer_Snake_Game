using Controller;
using Model;

namespace SnakeGame;

public partial class MainPage : ContentPage
{
    GameController controller;
    public MainPage()
    {
        InitializeComponent();
        controller = new GameController();
        worldPanel.SetWorld(controller.GetWorld(), graphicsView);
        graphicsView.Invalidate();

        // Subscribe to events
        controller.JSONProcess += OnFrame;
        controller.Connected += HandleConnected;
        controller.Error += ShowError;
    }

    /// <summary>
    /// Handler for the controller's Connected event
    /// </summary>
    private void HandleConnected()
    {
        controller.Send(nameText.Text + "\n");
    }

    /// <summary>
    /// Displays an error on the GUI
    /// </summary>
    /// <param name="err">The error message to be displayed</param>
    private void ShowError(string err)
    {
        Dispatcher.Dispatch(() => DisplayAlert("Error", err, "OK"));
        Dispatcher.Dispatch(() => connectButton.IsEnabled = true);
    }


    void OnTapped(object sender, EventArgs args)
    {
        keyboardHack.Focus();
    }

    /// <summary>
    /// When text is changed in the text box, check if it is a movement key and then send the proper text
    /// to the server.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        Entry entry = (Entry)sender;
        String text = entry.Text.ToLower();
        if (text == "w")
        {
            // Move up
            controller.Send("{\"moving\":\"up\"}\n");
        }
        else if (text == "a")
        {
            // Move left
            controller.Send("{\"moving\":\"left\"}\n");
        }
        else if (text == "s")
        {
            // Move down
            controller.Send("{\"moving\":\"down\"}\n");
        }
        else if (text == "d")
        {
            // Move right
            controller.Send("{\"moving\":\"right\"}\n");
        }
        entry.Text = "";
    }

    /// <summary>
    /// Event handler for the connect button
    /// We will put the connection attempt interface here in the view.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void ConnectClick(object sender, EventArgs args)
    {
        if (serverText.Text == "")
        {
            DisplayAlert("Error", "Please enter a server address", "OK");
            return;
        }
        if (nameText.Text == "")
        {
            DisplayAlert("Error", "Please enter a name", "OK");
            return;
        }
        if (nameText.Text.Length > 16)
        {
            DisplayAlert("Error", "Name must be less than 16 characters", "OK");
            return;
        }
        // Disable button
        connectButton.IsEnabled = false;

        // Controller handles connection to server
        controller.Connect(serverText.Text);

        keyboardHack.Focus();
    }

    /// <summary>
    /// Event handler for when the controller has updated the world. Every frame this sets the world in worldPanel
    /// so it may display it properly.
    /// </summary>
    public void OnFrame()
    {
        World world = controller.GetWorld();
        worldPanel.SetWorld(world, graphicsView);
        Dispatcher.Dispatch(() => graphicsView.Invalidate());
    }

    /// <summary>
    /// Information on game controls
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ControlsButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Controls",
                     "W:\t\t Move up\n" +
                     "A:\t\t Move left\n" +
                     "S:\t\t Move down\n" +
                     "D:\t\t Move right\n",
                     "OK");
    }

    /// <summary>
    /// Information about the game
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AboutButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("About",
      "SnakeGame solution\nArtwork by Jolie Uk and Alex Smith\nGame design by Daniel Kopta and Travis Martin\n" +
      "Implementation by Isaac Anderson and Ryan Beard.\n" +
        "CS 3500 Fall 2022, University of Utah", "OK");
    }

    private void ContentPage_Focused(object sender, FocusEventArgs e)
    {
        if (!connectButton.IsEnabled)
            keyboardHack.Focus();
    }
}