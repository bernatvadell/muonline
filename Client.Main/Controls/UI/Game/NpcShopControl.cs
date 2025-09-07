using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Content;
using Client.Main.Helpers;
using Client.Main.Controllers;

namespace Client.Main.Controls.UI.Game
{
    public class NpcShopControl : DynamicLayoutControl
    {
        protected override string LayoutJsonResource => "Client.Main.Controls.UI.Game.Layouts.NpcShopLayout.json";
        protected override string TextureRectJsonResource => "Client.Main.Controls.UI.Game.Layouts.NpcShopRect.json";
        protected override string DefaultTexturePath => "Interface/GFx/NpcShop_I3.ozd";
        private static NpcShopControl _instance;

        private const int SHOP_COLUMNS = 8;
        private const int SHOP_ROWS = 14;
        private const int SHOP_SQUARE_WIDTH = 25;   // cell step used by grid cells
        private const int SHOP_SQUARE_HEIGHT = 25;

        private readonly List<InventoryItem> _items = new();
        private Point _gridTopLeft = new Point(170, 180);
        private SpriteFont _font;
        private Texture2D _slotTexture;
        private GameTime _currentGameTime;
        private InventoryItem _hoveredItem;
        private bool _wasVisible = false;

        public NpcShopControl()
        {
            Visible = false;
            Interactive = true;

            var rows = SHOP_ROWS;
            var cols = SHOP_COLUMNS;

            var ScreenX = _gridTopLeft.X;
            var ScreenY = _gridTopLeft.Y;
            for(var i = 0; i < rows; i++)
            {
                for(var j = 0; j < cols; j++)
                {
                    var textureCtrl = new TextureControl
                    {
                        AutoViewSize = false,
                        TexturePath = DefaultTexturePath,
                        BlendState = BlendState.AlphaBlend,
                        Name = "Cell-" + i + j,
                    };
                    textureCtrl.TextureRectangle = new Rectangle
                    {
                        X = 545,
                        Y = 217,
                        Width = 29,
                        Height = 31
                    };
                    textureCtrl.Tag = new LayoutInfo
                    {
                        Name = "Cell",
                        ScreenX = ScreenX,
                        ScreenY = ScreenY,
                        Width = 29,
                        Height = 29,
                        Z = 5
                    };
                    Controls.Add(textureCtrl);
                    ScreenX += SHOP_SQUARE_WIDTH;
                }
                ScreenY += SHOP_SQUARE_HEIGHT;
                ScreenX = _gridTopLeft.X;
            }
            // Hook up for live updates from server
            var state = MuGame.Network?.GetCharacterState();
            if (state != null)
            {
                state.ShopItemsChanged += RefreshShopContent;
            }
        }
        public static NpcShopControl Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new NpcShopControl();
                }
                return _instance;
            }
        }
        public override async System.Threading.Tasks.Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;
            _slotTexture = await TextureLoader.Instance.PrepareAndGetTexture(DefaultTexturePath);
        }

        public override void Update(GameTime gameTime)
        {
            KeyboardState newState = Keyboard.GetState();
            if (newState.IsKeyDown(Keys.Escape))
            {
                // Hide locally and inform server to allow reopening next time
                Visible = false;
                var svc = MuGame.Network?.GetCharacterService();
                if (svc != null)
                {
                    _ = svc.SendCloseNpcRequestAsync();
                }
                MuGame.Network?.GetCharacterState()?.ClearShopItems();
            }

            _currentGameTime = gameTime;

            // Detect visibility changes (handles any way of closing, not just ESC)
            if (_wasVisible && !Visible)
            {
                var svc = MuGame.Network?.GetCharacterService();
                if (svc != null)
                {
                    _ = svc.SendCloseNpcRequestAsync();
                }
                MuGame.Network?.GetCharacterState()?.ClearShopItems();
            }
            _wasVisible = Visible;

            HandleMouseClicks();

            base.Update(gameTime);
        }

        private void HandleMouseClicks()
        {
            if (!Visible) return;

            var mousePos = MuGame.Instance.Mouse.Position;
            bool leftPressed = MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released;
            if (!leftJustPressed)
                return;

            // Consume click so it doesn't reach the world (prevents unintended movement)
            if (Scene != null)
            {
                var rect = DisplayRectangle;
                if (rect.Contains(mousePos))
                {
                    Scene.SetMouseInputConsumed();
                }
            }

            // Find item under mouse
            var item = GetItemAt(mousePos);
            if (item != null)
            {
                byte slot = (byte)(item.GridPosition.Y * SHOP_COLUMNS + item.GridPosition.X);
                var svc = MuGame.Network?.GetCharacterService();
                if (svc != null)
                {
                    _ = svc.SendBuyItemFromNpcRequestAsync(slot);
                }
            }
        }

        private InventoryItem GetItemAt(Point mouse)
        {
            Point origin = _gridTopLeft;
            foreach (var it in _items)
            {
                var rect = new Rectangle(
                    origin.X + it.GridPosition.X * SHOP_SQUARE_WIDTH,
                    origin.Y + it.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                    it.Definition.Width * SHOP_SQUARE_WIDTH,
                    it.Definition.Height * SHOP_SQUARE_HEIGHT);
                if (rect.Contains(mouse))
                    return it;
            }
            return null;
        }

        private void RefreshShopContent()
        {
            _items.Clear();
            var state = MuGame.Network?.GetCharacterState();
            if (state == null) return;

            var shopItems = state.GetShopItems();
            foreach (var kv in shopItems)
            {
                byte slot = kv.Key;
                byte[] data = kv.Value;

                int gridX = slot % SHOP_COLUMNS;
                int gridY = slot / SHOP_COLUMNS;

                var def = ItemDatabase.GetItemDefinition(data);
                if (def == null)
                {
                    // Fallback with 1x1 unknown item if not found
                    def = new ItemDefinition(0, ItemDatabase.GetItemName(data) ?? "Unknown Item", 1, 1, "Interface/newui_item_box.tga");
                }

                var item = new InventoryItem(def, new Point(gridX, gridY), data);
                if (data.Length > 2) item.Durability = data[2];
                _items.Add(item);
            }

            // Preload textures
            foreach (var it in _items)
            {
                if (!string.IsNullOrEmpty(it.Definition.TexturePath))
                {
                    _ = TextureLoader.Instance.Prepare(it.Definition.TexturePath);
                }
            }

            if (_items.Count > 0)
            {
                Visible = true;
                BringToFront();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            base.Draw(gameTime);

            var sprite = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(sprite, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {

            // Draw items over the grid
            var font = _font ?? GraphicsManager.Instance.Font;
            Point origin = _gridTopLeft;

            // Track hovered item
            _hoveredItem = null;
            var mouse = MuGame.Instance.Mouse.Position;

            foreach (var item in _items)
            {
                var rect = new Rectangle(
                    origin.X + item.GridPosition.X * SHOP_SQUARE_WIDTH,
                    origin.Y + item.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                    item.Definition.Width * SHOP_SQUARE_WIDTH,
                    item.Definition.Height * SHOP_SQUARE_HEIGHT);

                bool isHovered = rect.Contains(mouse);
                if (isHovered)
                {
                    _hoveredItem = item;
                }

                Texture2D tex = null;
                if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                {
                    tex = TextureLoader.Instance.GetTexture2D(item.Definition.TexturePath);

                    if (tex == null && item.Definition.TexturePath.EndsWith(".bmd", System.StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            int w = rect.Width;
                            int h = rect.Height;
                            tex = isHovered && _currentGameTime != null
                                ? BmdPreviewRenderer.GetAnimatedPreview(item.Definition, w, h, _currentGameTime)
                                : BmdPreviewRenderer.GetPreview(item.Definition, w, h);
                        }
                        catch { /* ignore preview errors */ }
                    }
                }

                if (tex != null)
                {
                    sprite.Draw(tex, rect, Color.White);
                }
                else
                {
                    sprite.Draw(GraphicsManager.Instance.Pixel, rect, Color.DarkSlateGray * 0.8f);
                }

                // hovered captured above

                // Quantity for stackables
                if (item.Definition.BaseDurability == 0 && item.Durability > 1)
                {
                    string qty = item.Durability.ToString();
                    var size = font.MeasureString(qty) * 0.4f;
                    var pos = new Vector2(rect.Right - size.X - 2, rect.Y + 2);
                    // Outline
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if (dx != 0 || dy != 0)
                                sprite.DrawString(font, qty, pos + new Vector2(dx, dy), Color.Black, 0, Vector2.Zero, 0.4f, SpriteEffects.None, 0);
                    sprite.DrawString(font, qty, pos, new Color(255,255,180), 0, Vector2.Zero, 0.4f, SpriteEffects.None, 0);
                }
            }

                // Tooltip (draw after items)
            }
            DrawTooltip(sprite, DisplayRectangle);
        }

        private static List<(string txt, Color col)> BuildTooltipLines(InventoryItem it)
        {
            var d = it.Details;
            var li = new List<(string, Color)>();
            string name = d.IsExcellent ? $"Excellent {it.Definition.Name}" : d.IsAncient ? $"Ancient {it.Definition.Name}" : it.Definition.Name;
            if (d.Level > 0) name += $" +{d.Level}";
            li.Add((name, Color.White));
            var def = it.Definition;
            if (def.DamageMin > 0 || def.DamageMax > 0)
            {
                string dmgType = def.TwoHanded ? "Two-hand" : "One-hand";
                li.Add(($"{dmgType} Damage : {def.DamageMin} ~ {def.DamageMax}", Color.Orange));
            }
            if (def.Defense > 0) li.Add(($"Defense     : {def.Defense}", Color.Orange));
            if (def.DefenseRate > 0) li.Add(($"Defense Rate: {def.DefenseRate}", Color.Orange));
            if (def.AttackSpeed > 0) li.Add(($"Attack Speed: {def.AttackSpeed}", Color.Orange));
            li.Add(($"Durability : {it.Durability}/{def.BaseDurability}", Color.Silver));
            if (def.RequiredLevel > 0) li.Add(($"Required Level   : {def.RequiredLevel}", Color.LightGray));
            if (def.RequiredStrength > 0) li.Add(($"Required Strength: {def.RequiredStrength}", Color.LightGray));
            if (def.RequiredDexterity > 0) li.Add(($"Required Agility : {def.RequiredDexterity}", Color.LightGray));
            if (def.RequiredEnergy > 0) li.Add(($"Required Energy  : {def.RequiredEnergy}", Color.LightGray));
            if (def.AllowedClasses != null && def.AllowedClasses.Count > 0)
            {
                foreach (string cls in def.AllowedClasses)
                    li.Add(($"Can be equipped by {cls}", Color.LightGray));
            }
            if (d.OptionLevel > 0)
                li.Add(($"Additional Option : +{d.OptionLevel * 4}", new Color(80, 255, 80)));
            if (d.HasLuck) li.Add(("+Luck  (Crit +5 %, Jewel +25 %)", Color.CornflowerBlue));
            if (d.HasSkill) li.Add(("+Skill (Right mouse click - skill)", Color.CornflowerBlue));
            if (d.IsExcellent)
            {
                byte excByte = it.RawData.Length > 3 ? it.RawData[3] : (byte)0;
                foreach (var s in ItemDatabase.ParseExcellentOptions(excByte))
                    li.Add(($"+{s}", new Color(128, 255, 128)));
            }
            if (d.IsAncient) li.Add(("Ancient Option", new Color(0, 255, 128)));
            return li;
        }

        private void DrawTooltip(SpriteBatch sb, Rectangle frameRect)
        {
            if (_hoveredItem == null || _font == null) return;
            var lines = BuildTooltipLines(_hoveredItem);
            const float scale = 0.5f;
            int w = 0, h = 0;
            foreach (var (t, _) in lines)
            {
                Vector2 sz = _font.MeasureString(t) * scale;
                w = Math.Max(w, (int)sz.X);
                h += (int)sz.Y + 2;
            }
            w += 12; h += 8;
            Point m = MuGame.Instance.Mouse.Position;
            Rectangle screenBounds = MuGame.Instance.GraphicsDevice.Viewport.Bounds;
            Point gridTopLeft = _gridTopLeft + new Point(DisplayRectangle.X, DisplayRectangle.Y);
            Rectangle hoveredItemRect = new Rectangle(
                gridTopLeft.X + _hoveredItem.GridPosition.X * SHOP_SQUARE_WIDTH,
                gridTopLeft.Y + _hoveredItem.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                _hoveredItem.Definition.Width * SHOP_SQUARE_WIDTH,
                _hoveredItem.Definition.Height * SHOP_SQUARE_HEIGHT);
            Rectangle r = new(m.X + 15, m.Y + 15, w, h);
            if (r.Intersects(hoveredItemRect))
            {
                r.X = hoveredItemRect.X - w - 10;
                r.Y = hoveredItemRect.Y;
                if (r.Intersects(hoveredItemRect) || r.X < screenBounds.X + 10)
                {
                    r.X = hoveredItemRect.X;
                    r.Y = hoveredItemRect.Y - h - 10;
                    if (r.Intersects(hoveredItemRect) || r.Y < screenBounds.Y + 10)
                    {
                        r.X = hoveredItemRect.X;
                        r.Y = hoveredItemRect.Bottom + 10;
                    }
                }
            }
            if (r.Right > screenBounds.Right - 10) r.X = screenBounds.Right - 10 - r.Width;
            if (r.Bottom > screenBounds.Bottom - 10) r.Y = screenBounds.Bottom - 10 - r.Height;
            if (r.X < screenBounds.X + 10) r.X = screenBounds.X + 10;
            if (r.Y < screenBounds.Y + 10) r.Y = screenBounds.Y + 10;
            sb.Draw(GraphicsManager.Instance.Pixel, r, Color.Black * 0.85f);
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(r.X, r.Y, r.Width, 1), Color.White);
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), Color.White);
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(r.X, r.Y, 1, r.Height), Color.White);
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), Color.White);
            int y = r.Y + 4;
            foreach (var (t, col) in lines)
            {
                Vector2 size = _font.MeasureString(t) * scale;
                sb.DrawString(_font, t, new Vector2(r.X + (r.Width - size.X) / 2, y), col, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += (int)size.Y + 2;
            }
        }
    }
}
