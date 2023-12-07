using IImage = Microsoft.Maui.Graphics.IImage;
#if MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#else
using Microsoft.Maui.Graphics.Win2D;
#endif
using System.Reflection;
using Model;

namespace SnakeGame;
public class WorldPanel : IDrawable
{
    // Dictionary relating a snake's ID to the "speed" of it's death particles
    private Dictionary<int, int> snakeSpeeds = new();

    private IImage wall;
    private IImage background;
    private IImage planet0;
    private IImage planet1;
    private IImage planet2;
    private IImage planet3;
    private bool initializedForDrawing = false;

    // A World object that contains everything that is in the world
    private World theWorld;
    private GraphicsView graphicsView;


    private IImage loadImage(string name)
    {
        Assembly assembly = GetType().GetTypeInfo().Assembly;
        string path = "SnakeClient.Resources.Images";
        using (Stream stream = assembly.GetManifestResourceStream($"{path}.{name}"))
        {
#if MACCATALYST
            return PlatformImage.FromStream(stream);
#else
            return new W2DImageLoadingService().FromStream(stream);
#endif
        }
    }

    public WorldPanel()
    {

    }

    /// <summary>
    /// Sets theWorld and graphicsView to updated versions.
    /// </summary>
    /// <param name="world">The updated world</param>
    /// <param name="graphicsView">GraphicsView to use and get data from</param>
    public void SetWorld(World world, GraphicsView graphicsView)
    {
        this.graphicsView = graphicsView;
        lock (this) { theWorld = world; }
    }

    private void InitializeDrawing()
    {
        wall = loadImage( "astroid.png" );
        background = loadImage( "space.png" );
        planet0 = loadImage( "planet0.png" );
        planet1 = loadImage( "planet1.png" );
        planet2 = loadImage( "planet2.png" );
        planet3 = loadImage( "planet3.png" );
        initializedForDrawing = true;
    }

    /// <summary>
    /// A method to draw snakes.
    /// </summary>
    /// <param name="canvas">The canvas to draw on</param>
    /// <param name="x1">First x value of snake</param>
    /// <param name="y1">First y value of snake</param>
    /// <param name="x2">Second x value of snake</param>
    /// <param name="y2">Second y value of snake</param>
    /// <param name="snakeID">The snake's ID</param>
    private void DrawSnake(ICanvas canvas, double x1, double y1, double x2, double y2, int snakeID)
    {
        switch (snakeID % 12)
        {
            case 0:
                canvas.StrokeColor = Color.FromRgb(173, 21, 15);
                break;
            case 1:
                canvas.StrokeColor = Color.FromRgb(127, 28, 24);
                break;
            case 2:
                canvas.StrokeColor = Color.FromRgb(127, 60, 40);
                break;
            case 3:
                canvas.StrokeColor = Color.FromRgb(127, 95, 32);
                break;
            case 4:
                canvas.StrokeColor = Color.FromRgb(127, 114, 42);
                break;
            case 5:
                canvas.StrokeColor = Color.FromRgb(114, 128, 65);
                break;
            case 6:
                canvas.StrokeColor = Color.FromRgb(76, 128, 89);
                break;
            case 7:
                canvas.StrokeColor = Color.FromRgb(75, 128, 112);
                break;
            case 8:
                canvas.StrokeColor = Color.FromRgb(56, 104, 127);
                break;
            case 9:
                canvas.StrokeColor = Color.FromRgb(72, 83, 127);
                break;
            case 10:
                canvas.StrokeColor = Color.FromRgb(82, 59, 127);
                break;
            case 11:
                canvas.StrokeColor = Color.FromRgb(75, 42, 108);
                break;

        }

        canvas.StrokeSize = 10;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine((float)x1, (float)y1, (float)x2, (float)y2);
    }

    /// <summary>
    /// A method to draw powerups.
    /// </summary>
    /// <param name="canvas">The canvas to draw on</param>
    /// <param name="p">The powerup to draw</param>
    private void DrawPowerup(ICanvas canvas, Powerup p)
    {
        int size = 16;
        switch (p.power % 4)
        {
            case 0:
                canvas.DrawImage(planet0, (float)p.loc.GetX() - size / 2, (float)p.loc.GetY() - size / 2, size, size);
                break;
            case 1:
                canvas.DrawImage(planet1, (float)p.loc.GetX() - size / 2, (float)p.loc.GetY() - size / 2, size, size);
                break;
            case 2:
                canvas.DrawImage(planet2, (float)p.loc.GetX() - size / 2, (float)p.loc.GetY() - size / 2, size, size);
                break;
            case 3:
                canvas.DrawImage(planet3, (float)p.loc.GetX() - size / 2, (float)p.loc.GetY() - size / 2, size, size);
                break;
        }
    }

    /// <summary>
    /// A method to draw the death particles for a snake.
    /// </summary>
    /// <param name="canvas">The canvas to draw on</param>
    /// <param name="snakeX">The snake's head's x position</param>
    /// <param name="snakeY">The snake's head's y position</param>
    private void DrawParticles(ICanvas canvas, float snakeX, float snakeY, int speed)
    {
        canvas.FillColor = Color.FromRgb(255 - speed * 2, 255 - speed * 3, 255 - speed);
        for (int i = 0; i < 20; i++)
        {
            canvas.FillCircle(snakeX + (speed + i) * (float)Math.Cos(i), snakeY + (speed + i) * (float)Math.Sin(i), 2);
            canvas.FillCircle(snakeX + (speed - i) * (float)Math.Cos(-i), snakeY + (speed - i) * (float)Math.Sin(-i), 3);
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        lock (this)
        {
            if (theWorld.snakes.ContainsKey(theWorld.PlayerID))
            {
                Snake playerSnake = theWorld.snakes[theWorld.PlayerID];
                float playerX = (float)playerSnake.body[playerSnake.body.Count - 1].GetX();
                float playerY = (float)playerSnake.body[playerSnake.body.Count - 1].GetY();
                canvas.Translate(-playerX + ((float)graphicsView.Width / 2), -playerY + ((float)graphicsView.Height / 2));
            }

            if (!initializedForDrawing)
                InitializeDrawing();

            // Draw background
            canvas.DrawImage(background, (float)-theWorld.Size / 2, (float)-theWorld.Size / 2, (float)theWorld.Size, (float)theWorld.Size);

            // undo previous transformations from last frame
            canvas.ResetState();


            // Draw powerups
            foreach (Powerup p in theWorld.powerups.Values)
            {
                if (!p.died)
                {
                    DrawPowerup(canvas, p);
                }
            }

            // Draw walls
            foreach (Wall wallData in theWorld.walls.Values)
            {
                // The width and height will always be positive, so we need to decide which point is in the top left of the rectangle
                double width = Math.Abs(wallData.p2.GetX() - wallData.p1.GetX()) + 50;
                double height = Math.Abs(wallData.p2.GetY() - wallData.p1.GetY()) + 50;
                int wallWidthSegments = (int)(width / 50);
                int wallHeightSegments = (int)(height / 50);

                // If p1 is in the top left
                if (wallData.p1.GetX() < wallData.p2.GetX() || wallData.p1.GetY() < wallData.p2.GetY())
                {
                    for (int i = 0; i < wallHeightSegments; i++)
                    {
                        for (int j = 0; j < wallWidthSegments; j++)
                        {
                            canvas.DrawImage(wall, (float)wallData.p1.GetX() + (j * 50) - 25, (float)wallData.p1.GetY() + (i * 50) - 25, 50, 50);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < wallHeightSegments; i++)
                    {
                        for (int j = 0; j < wallWidthSegments; j++)
                        {
                            canvas.DrawImage(wall, (float)wallData.p2.GetX() + (j * 50) - 25, (float)wallData.p2.GetY() + (i * 50) - 25, 50, 50);
                        }
                    }
                }

            }

            // Draw snakes
            foreach (Snake snake in theWorld.snakes.Values)
            {
                if (snake.died)
                {
                    if (snakeSpeeds.ContainsKey(snake.snake)) { snakeSpeeds[snake.snake] = 0; }
                    else { snakeSpeeds.Add(snake.snake, 0); }
                }
                if (snake.dc)
                {
                    snakeSpeeds.Remove(snake.snake);
                    snake.alive = false;
                    theWorld.snakes.Remove(snake.snake);
                    continue;
                }

                float snakeX = (float)snake.body[snake.body.Count - 1].GetX();
                float snakeY = (float)snake.body[snake.body.Count - 1].GetY();
                if (snake.alive)
                {
                    Vector2D lastSegment = null;
                    foreach (Vector2D bodyPart in snake.body)
                    {
                        if (lastSegment == null)
                        {
                            lastSegment = bodyPart;
                        }
                        else
                        {
                            DrawSnake(canvas, bodyPart.GetX(), bodyPart.GetY(), lastSegment.GetX(), lastSegment.GetY(), snake.snake);
                            lastSegment = bodyPart;
                        }
                    }
                    // Draw snake name and score
                    canvas.FontColor = Colors.White;
                    canvas.DrawString(snake.name + ": " + snake.score, snakeX, snakeY - 25, HorizontalAlignment.Center);
                }
                else
                {
                    // Draw particles when the snake dies
                    if (snakeSpeeds.ContainsKey(snake.snake))
                    {
                        snakeSpeeds[snake.snake] += 2;
                        if (snakeSpeeds[snake.snake] <= 100) { DrawParticles(canvas, snakeX, snakeY, snakeSpeeds[snake.snake]); }
                    }
                }
            }
        }
    }
}
