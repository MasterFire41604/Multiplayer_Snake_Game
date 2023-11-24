using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using IImage = Microsoft.Maui.Graphics.IImage;
#if MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#else
using Microsoft.Maui.Graphics.Win2D;
#endif
using System.Reflection;
using Microsoft.Maui;
using System.Net;
using Font = Microsoft.Maui.Graphics.Font;
using SizeF = Microsoft.Maui.Graphics.SizeF;
using Model;
using System.Diagnostics.Metrics;

namespace SnakeGame;
public class WorldPanel : IDrawable
{
    // Speed of the death particles
    private int speed = 0;

    private IImage wall;
    private IImage background;
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
        wall = loadImage( "wallsprite.png" );
        background = loadImage( "background.png" );
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
        switch (snakeID % 8)
        {
            case 0:
                canvas.StrokeColor = Colors.Red;
                break;
            case 1:
                canvas.StrokeColor = Colors.Orange;
                break;
            case 2:
                canvas.StrokeColor = Colors.Yellow;
                break;
            case 3:
                canvas.StrokeColor = Colors.Green;
                break;
            case 4:
                canvas.StrokeColor = Colors.Blue;
                break;
            case 5:
                canvas.StrokeColor = Colors.Indigo;
                break;
            case 6:
                canvas.StrokeColor = Colors.Violet;
                break;
            case 7:
                canvas.StrokeColor = Colors.Black;
                break;
        }

        canvas.StrokeSize = 10;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine((float)x1, (float)y1, (float)x2, (float)y2);
    }

    /// <summary>
    /// A method to draw the death particles for a snake.
    /// </summary>
    /// <param name="canvas">The canvas to draw on</param>
    /// <param name="snakeX">The snake's head's x position</param>
    /// <param name="snakeY">The snake's head's y position</param>
    private void DrawParticles(ICanvas canvas, float snakeX, float snakeY)
    {

        canvas.FillColor = Colors.Red;
        //if (speed <= 60)
        //{
            for (int i = 0; i < 20; i++)
            {
                canvas.FillCircle(snakeX + speed * (float)Math.Cos(i), snakeY + speed * (float)Math.Sin(i), 2);
            }
        //}
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (theWorld.snakes.ContainsKey(theWorld.PlayerID))
        {
            Snake playerSnake = theWorld.snakes[theWorld.PlayerID];
            float playerX = (float)playerSnake.body[playerSnake.body.Count - 1].GetX();
            float playerY = (float)playerSnake.body[playerSnake.body.Count - 1].GetY();
            canvas.Translate(-playerX + ((float)graphicsView.Width / 2), -playerY + ((float)graphicsView.Height / 2));
        }

        if ( !initializedForDrawing )
            InitializeDrawing();

        // Draw background
        canvas.DrawImage(background, (float)-theWorld.Size / 2, (float)-theWorld.Size / 2, (float)theWorld.Size, (float)theWorld.Size);

        // undo previous transformations from last frame
        canvas.ResetState();

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
        lock(this)
        {
            foreach (Snake snake in theWorld.snakes.Values)
            {
                float snakeX = (float)snake.body[snake.body.Count - 1].GetX();
                float snakeY = (float)snake.body[snake.body.Count - 1].GetY();
                if (snake.alive)
                {
                    speed = 0;
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
                    canvas.DrawString(snake.name + " " + snake.score, snakeX, snakeY - 25, HorizontalAlignment.Center);
                }
                else
                {
                    // Draw particles when the snake dies
                    speed += 3;
                    DrawParticles(canvas, snakeX, snakeY);
                }
            }
        }
        
        // Draw powerups
        lock (this)
        { 
            foreach (Powerup p in theWorld.powerups.Values)
            {
                int radius = 8;
                if (!p.died)
                {
                    canvas.FillColor = Colors.Blue;
                    canvas.FillCircle((float)p.loc.GetX(), (float)p.loc.GetY(), radius);
                }
            }
            
        }
    }

}
