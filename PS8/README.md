
PS8
---------------------------------
Author: Ryan Beard u1391626


Networking
---------------------------------
Handles the attempt to connect to a given server address.

Disables connect button after clicking it. Re-enables if the connection fails.

Sends movement direction to server from the correct key inputs.

Alerts the user of anything that doesn't go right. (This includes incorrect server address, no name,
too long of a name, and the inability to connect to the server.)

Receives any data sent to the client, and uses it to create the game world.


Creating the Game World
---------------------------------
Uses the data sent from the server by splitting the JSON on every \n.

Checks if the second number in the given data is just a number. If so, then that number is the world size, and the first number
is the player ID.

Only uses complete pieces of the JSON (the last piece of information sent by the server may or may not have been sent fully)

After data is finished being validated and saved, removes the sent data to keep it from stacking up infinitely.

Separates each part of the given data into their corresponding parts. (Snake pieces are used to create snakes, Walls to create walls, etc.)


Drawing the Game
---------------------------------
Uses the world that's already been previously created.

Centers the view to be around the current player's coordinates.

Draws the background using the given image.

Draws the walls using the given image. To keep from stretching the image, we calculated how many walls of 50x50 pixels needed
to be created to span the length of the wall, and still be in the correct locations.

Draws each segment of the player's snake, making sure they all connect even when the snake turns. Snakes have 8 different possible
colors depending on their ID. Snakes are rounded to look nicer and more polished.

Draws particles for each snake when they die.

Draws powerups in their correct locations, and removes them when a player has picked them up.

------------------------------------------------------------------------------------------------------------------------------------------------------


PS9
---------------------------------
Author: Isaac Anderson u0584604


Networking
---------------------------------
Starts a server that clients can connect to.

When server is started, information is read from the server settings xml file a world object is created.

Server listens and waits for clients to connect.

When the server gets a client connection, it waits to receive a name from the client, and sends world startup info back to them.

After name is received, server waits to receive commands from the client. A receive loop is started.

Repeats process for any new client connection.


Updating the World
---------------------------------
The server waits a certain amount of time (set in the settings file), and then updates the world. This is where the server
sends the client information on the world, including where their snake is and if it collided with anything. This is the bulk of what
the server does when a client is connected, constantly updating world data for the client to draw. 


settings.xml File
---------------------------------
Settings that a user can configure for the server are kept in a xml file titled 'settings' that is kept in the Server project folder.
The server will read from the file from this location. This includes the game mode (more on that below), how many milliseconds should
be in a frame, the snake respawn rate (in frames), the universe size, and finally positions of walls in the world.


Extra Features
---------------------------------
An extra feature can be enabled through the settings file. To do this, the settings file needs to contain an extra field under GameSettings
structured as follows: '<Mode>extra</Mode>'. This can contain either 'extra', enabling the extra feature, or 'basic', disabling the feature.
Not specifying the mode will default the server to have the extra feature disabled.

The extra feature enables the user to be able to 'boost' themselves, doubling their speed for a set amount of frames. This can be done
once and then has a cooldown, where the user has to wait a set amount of frames before being able to boost again. The delay for boosting
again and the time spent boosting are both set in the world object.

To boost, the user must hit a direction key when they are already moving that direction. For example, if 'w' was pressed while the user's
snake was already moving up, their speed would increase for a brief period of time, assuming they didn't just do it.

------------------------------------------------------------------------------------------------------------------------------------------------------