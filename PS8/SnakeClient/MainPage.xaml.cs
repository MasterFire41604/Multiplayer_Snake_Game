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

    private void ShowError(string err)
    {
        Dispatcher.Dispatch(() => DisplayAlert("Error", err, "OK"));
        Dispatcher.Dispatch(() => connectButton.IsEnabled = true);
    }

    void OnTapped(object sender, EventArgs args)
    {
        keyboardHack.Focus();
    }

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

    /*private void NetworkErrorHandler()
    {
        DisplayAlert("Error", "Disconnected from server", "OK");
    }*/


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
    /// Use this method as an event handler for when the controller has updated the world
    /// </summary>
    public void OnFrame()
    {
        World world = controller.GetWorld();
        worldPanel.SetWorld(world, graphicsView);
        Dispatcher.Dispatch(() => graphicsView.Invalidate());
    }

    private void ControlsButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Controls",
                     "W:\t\t Move up\n" +
                     "A:\t\t Move left\n" +
                     "S:\t\t Move down\n" +
                     "D:\t\t Move right\n",
                     "OK");
    }

    private void AboutButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("About",
      "SnakeGame solution\nArtwork by Jolie Uk and Alex Smith\nGame design by Daniel Kopta and Travis Martin\n" +
      "Implementation by Isaac Anderson and Ryan Beard\n" +
        "CS 3500 Fall 2022, University of Utah", "OK");
    }

    private void ContentPage_Focused(object sender, FocusEventArgs e)
    {
        if (!connectButton.IsEnabled)
            keyboardHack.Focus();
    }
}