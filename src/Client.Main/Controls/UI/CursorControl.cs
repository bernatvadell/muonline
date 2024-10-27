using Client.Main.Controls.UI;
using Client.Main.Objects.NPCS;
using Client.Main;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;

public class CursorControl : SpriteControl
{
    public Vector2[] DefaultAnimation = { new(0, 0) };
    public Vector2[] TalkAnimation = { new(0, 0), new(1, 0), new(0, 1), new(1, 1) };
    public Vector2[] CurrentAnimation;
    private int animationIndex = 0;
    private double animationTimer = 0;
    private double animationSpeed = 0.1;

    public CursorControl()
    {
        AutoViewSize = false;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/Cursor.tga";
        TileWidth = 32;
        TileHeight = 32;
        TileX = 0;
        TileY = 0;
        CurrentAnimation = DefaultAnimation;
    }

    public override void Update(GameTime gameTime)
    {
        X = MuGame.Instance.Mouse.X;
        Y = MuGame.Instance.Mouse.Y;

        if (MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed)
        {
            TexturePath = "Interface/CursorPush.tga";
            CurrentAnimation = DefaultAnimation;
        }
        else if (Scene.MouseHoverObject is NPCObject)
        {
            TexturePath = "Interface/CursorTalk.tga";
            CurrentAnimation = TalkAnimation;
        }
        else
        {
            TexturePath = "Interface/Cursor.tga";
            CurrentAnimation = DefaultAnimation;
        }

        animationTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (animationTimer >= animationSpeed)
        {
            animationTimer = 0;
            animationIndex = (animationIndex + 1) % CurrentAnimation.Length;
            TileX = (int)CurrentAnimation[animationIndex].X;
            TileY = (int)CurrentAnimation[animationIndex].Y;
        }

        BringToFront();

        base.Update(gameTime);
    }
}
