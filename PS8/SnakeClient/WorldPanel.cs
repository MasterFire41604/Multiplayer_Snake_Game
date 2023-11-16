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

    public void SetWorld(World world)
    { 
        theWorld = world;
    }

    private void InitializeDrawing()
    {
        wall = loadImage( "wallsprite.png" );
        background = loadImage( "background.png" );
        initializedForDrawing = true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        /*float playerX = ... (the player's world-space X coordinate)
        float playerY = ... (the player's world-space Y coordinate)
        canvas.Translate(-playerX + (viewSize / 2), -playerY + (viewSize / 2));*/


        if ( !initializedForDrawing )
            InitializeDrawing();

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
            foreach (Vector2D bodyPart in snake.body) { canvas.DrawCircle((float)bodyPart.X, (float)bodyPart.Y, 100); }
        }
    }

}
