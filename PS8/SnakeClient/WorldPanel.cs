using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using IImage = Microsoft.Maui.Graphics.IImage;
#if MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#else
using Microsoft.Maui.Graphics.Win2D;
#endif
using Color = Microsoft.Maui.Graphics.Color;
using System.Reflection;
using Microsoft.Maui;
using System.Net;
using Font = Microsoft.Maui.Graphics.Font;
using SizeF = Microsoft.Maui.Graphics.SizeF;
using Model;

namespace SnakeGame;
public class WorldPanel : IDrawable
{
    private IImage wall;
    private IImage background;

    private bool initializedForDrawing = false;

    private World theWorld;
    private GraphicsView graphicsView;
    public delegate void ObjectDrawer(object o, ICanvas canvas);

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

    public void SetWorld(World world, GraphicsView graphicsView)
    {
        this.graphicsView = graphicsView;
        theWorld = world;
    }

    private void InitializeDrawing()
    {
        wall = loadImage( "wallsprite.png" );
        background = loadImage( "background.png" );
        initializedForDrawing = true;
    }

    /*/// <summary>
    /// This method performs a translation and rotation to draw an object.
    /// </summary>
    /// <param name="canvas">The canvas object for drawing onto</param>
    /// <param name="o">The object to draw</param>
    /// <param name="worldX">The X component of the object's position in world space</param>
    /// <param name="worldY">The Y component of the object's position in world space</param>
    /// <param name="angle">The orientation of the object, measured in degrees clockwise from "up"</param>
    /// <param name="drawer">The drawer delegate. After the transformation is applied, the delegate is invoked to draw whatever it wants</param>
    private void DrawObjectWithTransform(ICanvas canvas, object o, double worldX, double worldY, double angle, ObjectDrawer drawer)
    {
        // "push" the current transform
        canvas.SaveState();

        canvas.Translate((float)worldX, (float)worldY);
        canvas.Rotate((float)angle);
        drawer(o, canvas);

        // "pop" the transform
        canvas.RestoreState();
    }*/

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// </summary>
    /// <param name="o">The snake to draw</param>
    /// <param name="canvas"></param>
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
        canvas.DrawLine((float)x1, (float)y1, (float)x2, (float)y2);
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
        canvas.FillColor = Colors.Green;
        canvas.DrawImage(background, (float)-theWorld.Size / 2, (float)-theWorld.Size / 2, (float)theWorld.Size, (float)theWorld.Size);

        canvas.FillColor = Colors.Black;
        canvas.FillRectangle(0, 0, 10, 10);
        canvas.FillColor = Colors.Red;
        canvas.FillRectangle(0, 0, -50, -50);

        // undo previous transformations from last frame
        canvas.ResetState();

        // example code for how to draw
        // (the image is not visible in the starter code)
        // Draw walls
        foreach (Wall wallData in theWorld.walls.Values)
        {
            /*float width = Math.Abs((float)wallData.p2.GetX() - (float)wallData.p1.GetX()) + 50;
            float height = Math.Abs((float)wallData.p2.GetY() - (float)wallData.p1.GetY()) + 50;


            canvas.FillColor = Colors.Gray;
            if (wallData.p1.GetX() < wallData.p2.GetX() || wallData.p1.GetY() < wallData.p2.GetY()) 
            {
                canvas.FillRectangle
                (
                (float)wallData.p1.GetX() - 25,
                (float)wallData.p1.GetY() - 25,
                width,
                height
                );
            }
            else 
            {
                canvas.FillRectangle
                (
                (float)wallData.p2.GetX() - 25,
                (float)wallData.p2.GetY() - 25,
                width,
                height
                );
            }*/
            

            // Image stuff
            double width = Math.Abs((wallData.p2.GetX()) - (wallData.p1.GetX())) + 50;
            double height = Math.Abs((wallData.p2.GetY()) - (wallData.p1.GetY())) + 50;
            int wallWidthSegments = (int)(width / 50);
            int wallHeightSegments = (int)(height / 50);
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
            if (snake.alive)
            {
                float snakeX = (float)snake.body[snake.body.Count - 1].GetX();
                float snakeY = (float)snake.body[snake.body.Count - 1].GetY();
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
        }
        // Draw powerups
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
