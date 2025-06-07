using Client.Main.Controls.UI;
using Client.Main.Objects.NPCS;
using Client.Main.Objects.Monsters;
using Client.Main;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
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
        restPlaceTypes =
        new Type[]
        {
            typeof(Client.Main.Objects.Worlds.Devias.RestPlaceObject),
            typeof(Client.Main.Objects.Worlds.Noria.RestPlaceObject),
            typeof(Client.Main.Objects.Worlds.Lorencia.RestPlaceObject)
        };
    }

    // Helper method to determine if the hovered object qualifies as a sit place.
    private bool IsSitPlace(object obj)
    {
        if (obj == null)
            return false;

        // Check for Noria sit place
        if (obj is Client.Main.Objects.Worlds.Noria.SitPlaceObject)
            return true;

        // For FurnitureObject, trigger sitting action for type 145 or 146.
        if (obj is Client.Main.Objects.Worlds.Lorencia.FurnitureObject furniture)
            return furniture.Type == 145 || furniture.Type == 146;

        return false;
    }

    public override void Update(GameTime gameTime)
    {
        var hoveredObject = Scene.MouseHoverObject;
        bool sitPlace = IsSitPlace(hoveredObject);

        // If touches are available, we use them - standard input on Android
        if (MuGame.Instance.Touch.Count > 0)
        {
            var touch = MuGame.Instance.Touch[0];
            X = (int)touch.Position.X;
            Y = (int)touch.Position.Y;

            // If the touch is pressed or moved, simulate a click
            if (touch.State == TouchLocationState.Pressed || touch.State == TouchLocationState.Moved)
            {
                TexturePath = "Interface/CursorPush.ozt";
                CurrentAnimation = DefaultAnimation;
            }
            else if (hoveredObject is MonsterObject monster && !monster.IsDead)
            {
                TexturePath = "Interface/CursorAttack.ozt";
                CurrentAnimation = DefaultAnimation;
            }
            else if (hoveredObject is NPCObject)
            {
                TexturePath = "Interface/CursorTalk.ozt";
                CurrentAnimation = TalkAnimation;
            }
            else if (hoveredObject != null && restPlaceTypes.Contains(hoveredObject.GetType()))
            {
                TexturePath = "Interface/CursorLeanAgainst.ozt";
                CurrentAnimation = DefaultAnimation;
            }
            else if (sitPlace)
            {
                TexturePath = "Interface/CursorSitDown.ozt";
                CurrentAnimation = DefaultAnimation;
            }
            else
            {
                TexturePath = "Interface/Cursor.ozt";
                CurrentAnimation = DefaultAnimation;
            }
        }
        else
        {
            // If there are no touches, handle mouse input (e.g. on PC)
            X = MuGame.Instance.Mouse.X;
            Y = MuGame.Instance.Mouse.Y;

            if (MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed)
            {
                TexturePath = "Interface/CursorPush.ozt";
                CurrentAnimation = DefaultAnimation;

                if (hoveredObject is NPCObject npc) {
                    npc.OnClick();
                }
            }
            else if (hoveredObject is MonsterObject monster && !monster.IsDead)
            {
                TexturePath = "Interface/CursorAttack.ozt";
                CurrentAnimation = DefaultAnimation;
            }
            else if (hoveredObject is NPCObject)
            {
                TexturePath = "Interface/CursorTalk.ozt";
                CurrentAnimation = TalkAnimation;
            }
            else if (hoveredObject != null && restPlaceTypes.Contains(hoveredObject.GetType()))
            {
                TexturePath = "Interface/CursorLeanAgainst.ozt";
                CurrentAnimation = DefaultAnimation;
            }
            else if (sitPlace)
            {
                TexturePath = "Interface/CursorSitDown.ozt";
                CurrentAnimation = DefaultAnimation;
            }
            else
            {
                TexturePath = "Interface/Cursor.ozt";
                CurrentAnimation = DefaultAnimation;
            }
        }

        // Cursor animation handling
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
