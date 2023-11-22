Author: Ryan Beard u1391626


Networking
---------------------------------
Handles the attempt to connect to a given server address.

Disables connect button after clicking it. Re-enables if the connection fails.

Sends movement direction to server from the correct key inputs.

Alerts the user of anything that doesn't go right. (This includes incorrect server address, no name,
too long of a name, and the inability to connect to the server.)

Receives any data sent to the client, and uses it to create the game world.
---------------------------------


Creating the Game World
---------------------------------
Uses the data sent from the server by splitting the JSON on every \n.

Checks if the second number in the given data is just a number. If so, then that number is the world size, and the first number
is the player ID.

Only uses complete pieces of the JSON (the last piece of information sent by the server may or may not have been sent fully)

After data is finished being validated and saved, removes the sent data to keep it from stacking up infinitely.

Separates each part of the given data into their corresponding parts. (Snake pieces are used to create snakes, Walls to create walls, etc.)
---------------------------------


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
---------------------------------