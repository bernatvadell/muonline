using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Graphics;
using Client.Main.Objects.NPCS;
using Client.Main.Objects.Monsters;
using Client.Main;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Linq;
using Client.Main.Objects;

public class CursorControl : SpriteControl
{
    public Vector2[] DefaultAnimation = { new Vector2(0, 0) };
    public Vector2[] TalkAnimation = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
    public Vector2[] CurrentAnimation;
    private int animationIndex = 0;
    private double animationTimer = 0;
    private double animationSpeed = 0.1;
    private Type[] restPlaceTypes;
    private string currentTexturePath = "";
    private Vector2[] currentAnimationState;

    // BringToFront throttling - no need to call every frame
    private double _bringToFrontTimer = 0;
    private const double BRING_TO_FRONT_INTERVAL = 0.5; // seconds

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
        currentTexturePath = TexturePath;
        currentAnimationState = DefaultAnimation;
        restPlaceTypes =
        new Type[]
        {
            typeof(Client.Main.Objects.Worlds.Devias.RestPlaceObject),
            typeof(Client.Main.Objects.Worlds.Noria.RestPlaceObject),
            typeof(Client.Main.Objects.Worlds.Lorencia.RestPlaceObject),
            typeof(Client.Main.Objects.Worlds.Dungeon.RestPlaceObject),
            typeof(Client.Main.Objects.Worlds.Atlans.RestPlaceObject)
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

    private void SetCursorState(string texturePath, Vector2[] animation)
    {
        if (currentTexturePath != texturePath || currentAnimationState != animation)
        {
            currentTexturePath = texturePath;
            currentAnimationState = animation;
            TexturePath = texturePath;
            CurrentAnimation = animation;

            // Reset animation only when texture/animation actually changes
            animationIndex = 0;
            animationTimer = 0;
            TileX = (int)CurrentAnimation[animationIndex].X;
            TileY = (int)CurrentAnimation[animationIndex].Y;
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (Scene == null)
        {
            base.Update(gameTime);
            return;
        }

        var hoveredObject = Scene.MouseHoverObject;

        if (hoveredObject == null &&
            Scene.MouseHoverControl is LabelControl lbl &&
            lbl.Tag is DroppedItemObject dropItem)
        {
            hoveredObject = dropItem;
        }

        bool sitPlace = IsSitPlace(hoveredObject);

        // Check if in repair mode (NPC shop visible and repair mode active)
        bool isRepairMode = NpcShopControl.Instance != null &&
                           NpcShopControl.Instance.Visible &&
                           NpcShopControl.Instance.IsRepairMode;

        // If touches are available, we use them - standard input on Android
        if (MuGame.Instance.Touch.Count > 0)
        {
            var touch = MuGame.Instance.Touch[0];

            // Use converted UI touch position instead of raw coordinates
            var uiTouchPos = MuGame.Instance.UiTouchPosition;
            X = uiTouchPos.X;
            Y = uiTouchPos.Y;

            // Repair mode cursor has priority when active
            if (isRepairMode)
            {
                if (touch.State == TouchLocationState.Pressed || touch.State == TouchLocationState.Moved)
                {
                    // Hammer hitting animation - could rotate if we had rotation support
                    SetCursorState("Interface/CursorRepair.ozt", DefaultAnimation);
                }
                else
                {
                    SetCursorState("Interface/CursorRepair.ozt", DefaultAnimation);
                }
            }
            // If the touch is pressed or moved, simulate a click
            else if (touch.State == TouchLocationState.Pressed || touch.State == TouchLocationState.Moved)
            {
                SetCursorState("Interface/CursorPush.ozt", DefaultAnimation);
            }
            else if (hoveredObject is MonsterObject monster && !monster.IsDead)
            {
                SetCursorState("Interface/CursorAttack.ozt", DefaultAnimation);
            }
            else if (hoveredObject is DroppedItemObject)
            {
                SetCursorState("Interface/CursorGet.ozt", DefaultAnimation);
            }
            else if (hoveredObject is NPCObject)
            {
                SetCursorState("Interface/CursorTalk.ozt", TalkAnimation);
            }
            else if (hoveredObject != null && restPlaceTypes.Contains(hoveredObject.GetType()))
            {
                SetCursorState("Interface/CursorLeanAgainst.ozt", DefaultAnimation);
            }
            else if (sitPlace)
            {
                SetCursorState("Interface/CursorSitDown.ozt", DefaultAnimation);
            }
            else
            {
                SetCursorState("Interface/Cursor.ozt", DefaultAnimation);
            }
        }
        else
        {
            // If there are no touches, handle mouse input (e.g. on PC)
            var uiMouse = MuGame.Instance.UiMouseState;
            X = uiMouse.X;
            Y = uiMouse.Y;

            // Repair mode cursor has priority when active
            if (isRepairMode)
            {
                if (uiMouse.LeftButton == ButtonState.Pressed)
                {
                    // Hammer hitting animation - could rotate if we had rotation support
                    SetCursorState("Interface/CursorRepair.ozt", DefaultAnimation);
                }
                else
                {
                    SetCursorState("Interface/CursorRepair.ozt", DefaultAnimation);
                }

                // Still handle NPC clicks even in repair mode
                if (uiMouse.LeftButton == ButtonState.Pressed && hoveredObject is NPCObject npc)
                {
                    npc.OnClick();
                }
            }
            else if (uiMouse.LeftButton == ButtonState.Pressed)
            {
                SetCursorState("Interface/CursorPush.ozt", DefaultAnimation);

                if (hoveredObject is NPCObject npc)
                {
                    npc.OnClick();
                }
            }
            else if (hoveredObject is MonsterObject monster && !monster.IsDead)
            {
                SetCursorState("Interface/CursorAttack.ozt", DefaultAnimation);
            }
            else if (hoveredObject is DroppedItemObject)
            {
                SetCursorState("Interface/CursorGet.ozt", DefaultAnimation);
            }
            else if (hoveredObject is NPCObject)
            {
                SetCursorState("Interface/CursorTalk.ozt", TalkAnimation);
            }
            else if (hoveredObject != null && restPlaceTypes.Contains(hoveredObject.GetType()))
            {
                SetCursorState("Interface/CursorLeanAgainst.ozt", DefaultAnimation);
            }
            else if (sitPlace)
            {
                SetCursorState("Interface/CursorSitDown.ozt", DefaultAnimation);
            }
            else
            {
                SetCursorState("Interface/Cursor.ozt", DefaultAnimation);
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

        // Throttled BringToFront - only call periodically instead of every frame
        _bringToFrontTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (_bringToFrontTimer >= BRING_TO_FRONT_INTERVAL)
        {
            _bringToFrontTimer = 0;
            BringToFront();
        }

        base.Update(gameTime);
    }
}
