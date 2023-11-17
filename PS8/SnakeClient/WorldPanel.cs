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

    /// <summary>
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
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// </summary>
    /// <param name="o">The snake to draw</param>
    /// <param name="canvas"></param>
    private void SnakeSegmentDrawer(object o, ICanvas canvas)
    {
        double s = (double)o;
        canvas.FillColor = Colors.Purple;

        // Ellipses are drawn starting from the top-left corner.
        // So if we want the circle centered on the powerup's location, we have to offset it
        // by half its size to the left (-width/2) and up (-height/2)
        //canvas.DrawImage(wall, 0, 0, wall.Width, wall.Height);
        //canvas.FillEllipse(-(width / 2), -(width / 2), width, width);
        canvas.DrawLine(0, 0, 0, (float)-s);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (theWorld.snakes.ContainsKey(theWorld.PlayerID))
        {
            Snake playerSnake = theWorld.snakes[theWorld.PlayerID];
            float playerX = (float)playerSnake.body[0].GetX();
            float playerY = (float)playerSnake.body[0].GetY();
            canvas.Translate(-playerX + ((float)graphicsView.X / 2), -playerY + ((float)graphicsView.Y / 2));
        }

        if ( !initializedForDrawing )
            InitializeDrawing();

        // Draw background
        canvas.FillColor = Colors.Green;
        canvas.DrawImage(background, (float)-theWorld.Size / 2, (float)-theWorld.Size / 2, (float)theWorld.Size, (float)theWorld.Size);

        // undo previous transformations from last frame
        canvas.ResetState();

        // example code for how to draw
        // (the image is not visible in the starter code)
        foreach (Wall wallData in theWorld.walls.Values)
        {
            canvas.DrawImage(wall, (float)wallData.p1.X, (float)wallData.p1.Y, wall.Width, wall.Height);
        }
        foreach (Snake snake in theWorld.snakes.Values)
        {
            Vector2D lastSegment = null;
            foreach (Vector2D bodyPart in snake.body) 
            {
                double length = 0;
                if (lastSegment == null)
                {
                    lastSegment = bodyPart;
                }
                else
                    length = (bodyPart - lastSegment).Length();

                DrawObjectWithTransform(canvas, length, (float)bodyPart.GetX(), (float)bodyPart.GetY(), 0, SnakeSegmentDrawer); 
            }
        }
    }

}
