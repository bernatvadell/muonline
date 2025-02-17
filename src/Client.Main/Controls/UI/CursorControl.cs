using Client.Main.Controls.UI;
using Client.Main.Objects.NPCS;
using Client.Main.Objects.Monsters;
using Client.Main;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

public class CursorControl : SpriteControl
{
    public Vector2[] DefaultAnimation = { new Vector2(0, 0) };
    public Vector2[] TalkAnimation = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
    public Vector2[] CurrentAnimation;
    private int animationIndex = 0;
    private double animationTimer = 0;
    private double animationSpeed = 0.1;
    private Type[] restPlaceTypes;

    public CursorControl()
    {
        AutoViewSize = false;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/Cursor.ozt"; // Default cursor texture
        TileWidth = 32;
        TileHeight = 32;
        TileX = 0;
        TileY = 0;
        CurrentAnimation = DefaultAnimation;
        restPlaceTypes = [
            typeof(Client.Main.Objects.Worlds.Devias.RestPlaceObject),
            typeof(Client.Main.Objects.Worlds.Noria.RestPlaceObject),
            typeof(Client.Main.Objects.Worlds.Lorencia.RestPlaceObject)
        ];
    }

    public override void Update(GameTime gameTime)
    {
        // Update cursor position based on mouse coordinates.
        X = MuGame.Instance.Mouse.X;
        Y = MuGame.Instance.Mouse.Y;

        // Check if left mouse button is pressed.
        if (MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed)
        {
            TexturePath = "Interface/CursorPush.ozt";
            CurrentAnimation = DefaultAnimation;
        }
        else if (Scene.MouseHoverObject is MonsterObject)
        {
            // Use attack cursor when hovering over a monster
            TexturePath = "Interface/CursorAttack.ozt";
            CurrentAnimation = DefaultAnimation;
        }
        else if (Scene.MouseHoverObject is NPCObject)
        {
            // Use talk cursor when hovering over an NPC
            TexturePath = "Interface/CursorTalk.ozt";
            CurrentAnimation = TalkAnimation;
        }
        // Check if the mouse hovers over a RestPlaceObject.
        else if (Scene.MouseHoverObject is { } hoveredObject && restPlaceTypes.Contains(hoveredObject.GetType()))
        {
            TexturePath = "Interface/CursorLeanAgainst.ozt";
            CurrentAnimation = DefaultAnimation;
        }
        else
        {
            // Default cursor if no interactive object is under the mouse.
            TexturePath = "Interface/Cursor.ozt";
            CurrentAnimation = DefaultAnimation;
        }

        // Handle cursor animation timing.
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
