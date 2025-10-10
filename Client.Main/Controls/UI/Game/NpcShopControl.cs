using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI.Game
{
    public class NpcShopControl : UIControl, IUiTexturePreloadable
    {
        private const string LayoutJsonResource = "Client.Main.Controls.UI.Game.Layouts.NpcShopLayout.json";
        private const string TextureRectJsonResource = "Client.Main.Controls.UI.Game.Layouts.NpcShopRect.json";
        private const string LayoutTexturePath = "Interface/GFx/NpcShop_I3.ozd";

        private const int WINDOW_WIDTH = 422;
        private const int WINDOW_HEIGHT = 624;

        private const int SHOP_COLUMNS = 8;
        private const int SHOP_ROWS = 14;
        private const int SHOP_SQUARE_WIDTH = 25;
        private const int SHOP_SQUARE_HEIGHT = 25;

        private static readonly Rectangle SlotSourceRect = new(545, 217, 29, 31);

        private readonly struct LayoutInfo
        {
            public string Name { get; init; }
            public float ScreenX { get; init; }
            public float ScreenY { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
            public int Z { get; init; }
        }

        private readonly struct TextureRectData
        {
            public string Name { get; init; }
            public int X { get; init; }
            public int Y { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
        }

        private static NpcShopControl _instance;

        private readonly List<InventoryItem> _items = new();
        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private readonly List<LayoutInfo> _layoutInfos = new();
        private readonly Dictionary<string, TextureRectData> _textureRectLookup = new(StringComparer.OrdinalIgnoreCase);

        private readonly Point _gridOffset = new(170, 180);

        private Texture2D _layoutTexture;
        private Texture2D _slotTexture;
        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private SpriteFont _font;
        private CharacterState _characterState;

        private InventoryItem _hoveredItem;
        private Point _hoveredSlot = new(-1, -1);
        private GameTime _currentGameTime;

        private bool _wasVisible;
        private bool _escapeHandled;
        private bool _closeRequestSent;
        private bool _warmupPending;

        private NpcShopControl()
        {
            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;

            LoadLayoutDefinitions();
            EnsureCharacterState();
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

        public IEnumerable<string> GetPreloadTexturePaths()
        {
            yield return LayoutTexturePath;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            await base.Load();

            var loader = TextureLoader.Instance;
            _layoutTexture = await loader.PrepareAndGetTexture(LayoutTexturePath);
            _slotTexture = _layoutTexture;

            _font = GraphicsManager.Instance.Font;

            InvalidateStaticSurface();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            EnsureCharacterState();

            if (Visible)
            {
                _currentGameTime = gameTime;
                HandleKeyboardInput();
                if (Visible)
                {
                    UpdateHoverState();
                    HandleMouseInput();
                }
            }
            else if (_wasVisible)
            {
                HandleVisibilityLost();
            }

            _wasVisible = Visible;
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible)
            {
                return;
            }

            EnsureStaticSurface();

            var graphicsManager = GraphicsManager.Instance;
            var spriteBatch = graphicsManager?.Sprite;
            if (spriteBatch == null)
            {
                return;
            }

            SpriteBatchScope scope = null;
            if (!SpriteBatchScope.BatchIsBegun)
            {
                scope = new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform);
            }

            try
            {
                if (_staticSurface != null && !_staticSurface.IsDisposed)
                {
                    spriteBatch.Draw(_staticSurface, DisplayRectangle, Color.White * Alpha);
                }

                var gridOrigin = new Point(DisplayRectangle.X + _gridOffset.X, DisplayRectangle.Y + _gridOffset.Y);

                DrawHoveredSlotHighlight(spriteBatch, gridOrigin);
                DrawHoveredItemSlotHighlights(spriteBatch, gridOrigin);
                DrawShopItems(spriteBatch, gridOrigin);
            }
            finally
            {
                scope?.Dispose();
            }
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible || _hoveredItem == null)
            {
                return;
            }

            var graphicsManager = GraphicsManager.Instance;
            var spriteBatch = graphicsManager?.Sprite;
            if (spriteBatch == null)
            {
                return;
            }

            SpriteBatchScope scope = null;
            if (!SpriteBatchScope.BatchIsBegun)
            {
                scope = new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform);
            }

            try
            {
                DrawTooltip(spriteBatch, DisplayRectangle);
            }
            finally
            {
                scope?.Dispose();
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            if (_characterState != null)
            {
                _characterState.ShopItemsChanged -= RefreshShopContent;
                _characterState = null;
            }

            _staticSurface?.Dispose();
            _staticSurface = null;
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            InvalidateStaticSurface();
        }

        private void HandleKeyboardInput()
        {
            var keyboardState = Keyboard.GetState();
            bool escapeDown = keyboardState.IsKeyDown(Keys.Escape);

            if (escapeDown && !_escapeHandled)
            {
                Visible = false;
                HandleVisibilityLost();
                _wasVisible = false;
                _escapeHandled = true;
            }
            else if (!escapeDown)
            {
                _escapeHandled = false;
            }
        }

        private void HandleMouseInput()
        {
            var mouseState = MuGame.Instance.UiMouseState;
            var prevMouseState = MuGame.Instance.PrevUiMouseState;

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed &&
                                   prevMouseState.LeftButton == ButtonState.Released;
            if (!leftJustPressed)
            {
                return;
            }

            var mousePosition = mouseState.Position;
            if (DisplayRectangle.Contains(mousePosition))
            {
                Scene?.SetMouseInputConsumed();
            }

            if (_hoveredItem == null)
            {
                return;
            }

            byte slot = (byte)(_hoveredItem.GridPosition.Y * SHOP_COLUMNS + _hoveredItem.GridPosition.X);
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendBuyItemFromNpcRequestAsync(slot);
            }
        }

        private void UpdateHoverState()
        {
            var mousePosition = MuGame.Instance.UiMouseState.Position;
            _hoveredSlot = GetSlotAtScreenPosition(mousePosition);
            _hoveredItem = GetItemAt(mousePosition);
        }

        private void HandleVisibilityLost()
        {
            SendCloseNpcRequest();
            _characterState?.ClearShopItems();
            _items.Clear();
            _itemTextureCache.Clear();
            _bmdPreviewCache.Clear();
            _hoveredItem = null;
            _hoveredSlot = new Point(-1, -1);
        }

        private void EnsureCharacterState()
        {
            if (_characterState != null)
            {
                return;
            }

            _characterState = MuGame.Network?.GetCharacterState();
            if (_characterState != null)
            {
                _characterState.ShopItemsChanged += RefreshShopContent;
            }
        }

        private void RefreshShopContent()
        {
            if (_characterState == null)
            {
                return;
            }

            _items.Clear();
            _itemTextureCache.Clear();
            _bmdPreviewCache.Clear();

            var shopItems = _characterState.GetShopItems();
            foreach (var kv in shopItems)
            {
                byte slot = kv.Key;
                byte[] data = kv.Value;

                int gridX = slot % SHOP_COLUMNS;
                int gridY = slot / SHOP_COLUMNS;

                var def = ItemDatabase.GetItemDefinition(data);
                if (def == null)
                {
                    def = new ItemDefinition(0, ItemDatabase.GetItemName(data) ?? "Unknown Item", 1, 1, "Interface/newui_item_box.tga");
                }

                var item = new InventoryItem(def, new Point(gridX, gridY), data);
                if (data.Length > 2)
                {
                    item.Durability = data[2];
                }

                _items.Add(item);
            }

            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                {
                    _ = TextureLoader.Instance.Prepare(item.Definition.TexturePath);
                }
            }

            QueueWarmup();

            if (_items.Count > 0)
            {
                Visible = true;
                BringToFront();
                SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
                _closeRequestSent = false;
                _escapeHandled = false;
            }
        }

        private void DrawHoveredSlotHighlight(SpriteBatch spriteBatch, Point gridOrigin)
        {
            if (_hoveredSlot.X < 0 || GraphicsManager.Instance?.Pixel == null)
            {
                return;
            }

            var rect = new Rectangle(
                gridOrigin.X + _hoveredSlot.X * SHOP_SQUARE_WIDTH,
                gridOrigin.Y + _hoveredSlot.Y * SHOP_SQUARE_HEIGHT,
                SHOP_SQUARE_WIDTH,
                SHOP_SQUARE_HEIGHT);

            spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, Color.Yellow * (0.3f * Alpha));
        }

        private void DrawHoveredItemSlotHighlights(SpriteBatch spriteBatch, Point gridOrigin)
        {
            if (_hoveredItem == null || GraphicsManager.Instance?.Pixel == null)
            {
                return;
            }

            for (int y = 0; y < _hoveredItem.Definition.Height; y++)
            {
                for (int x = 0; x < _hoveredItem.Definition.Width; x++)
                {
                    int slotX = _hoveredItem.GridPosition.X + x;
                    int slotY = _hoveredItem.GridPosition.Y + y;

                    if (slotX == _hoveredSlot.X && slotY == _hoveredSlot.Y)
                    {
                        continue;
                    }

                    var rect = new Rectangle(
                        gridOrigin.X + slotX * SHOP_SQUARE_WIDTH,
                        gridOrigin.Y + slotY * SHOP_SQUARE_HEIGHT,
                        SHOP_SQUARE_WIDTH,
                        SHOP_SQUARE_HEIGHT);

                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, Color.CornflowerBlue * (0.35f * Alpha));
                }
            }
        }

        private void DrawShopItems(SpriteBatch spriteBatch, Point gridOrigin)
        {
            var font = _font ?? GraphicsManager.Instance.Font;
            if (font == null)
            {
                return;
            }

            foreach (var item in _items)
            {
                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * SHOP_SQUARE_WIDTH,
                    gridOrigin.Y + item.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                    item.Definition.Width * SHOP_SQUARE_WIDTH,
                    item.Definition.Height * SHOP_SQUARE_HEIGHT);

                bool isHovered = item == _hoveredItem;

                Texture2D texture = ResolveItemTexture(item, rect.Width, rect.Height, isHovered);
                if (texture != null)
                {
                    spriteBatch.Draw(texture, rect, Color.White * Alpha);
                }
                else if (GraphicsManager.Instance?.Pixel != null)
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, Color.DarkSlateGray * (0.8f * Alpha));
                }

                if (item.Definition.BaseDurability == 0 && item.Durability > 1)
                {
                    DrawStackCount(spriteBatch, font, rect, item.Durability);
                }
            }
        }

        private void DrawStackCount(SpriteBatch spriteBatch, SpriteFont font, Rectangle rect, byte quantity)
            => DrawStackCount(spriteBatch, font, rect, (int)quantity);

        private void DrawStackCount(SpriteBatch spriteBatch, SpriteFont font, Rectangle rect, int quantity)
        {
            string qty = quantity.ToString();
            const float scale = 0.4f;
            Vector2 size = font.MeasureString(qty) * scale;
            Vector2 position = new(rect.Right - size.X - 2, rect.Y + 2);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    spriteBatch.DrawString(font, qty, position + new Vector2(dx, dy), Color.Black * Alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.DrawString(font, qty, position, new Color(255, 255, 180) * Alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawTooltip(SpriteBatch spriteBatch, Rectangle frameRect)
        {
            var font = _font ?? GraphicsManager.Instance.Font;
            if (_hoveredItem == null || font == null)
            {
                return;
            }

            var lines = BuildTooltipLines(_hoveredItem);
            const float scale = 0.5f;
            int width = 0;
            int height = 0;
            foreach (var (text, _) in lines)
            {
                Vector2 size = font.MeasureString(text) * scale;
                width = Math.Max(width, (int)size.X);
                height += (int)size.Y + 2;
            }

            width += 12;
            height += 8;

            Point mouse = MuGame.Instance.UiMouseState.Position;
            Rectangle screenBounds = new(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y);
            Point gridOrigin = new Point(frameRect.X + _gridOffset.X, frameRect.Y + _gridOffset.Y);
            Rectangle hoveredItemRect = new(
                gridOrigin.X + _hoveredItem.GridPosition.X * SHOP_SQUARE_WIDTH,
                gridOrigin.Y + _hoveredItem.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                _hoveredItem.Definition.Width * SHOP_SQUARE_WIDTH,
                _hoveredItem.Definition.Height * SHOP_SQUARE_HEIGHT);

            Rectangle tooltipRect = new(mouse.X + 15, mouse.Y + 15, width, height);
            if (tooltipRect.Intersects(hoveredItemRect))
            {
                tooltipRect.X = hoveredItemRect.X - width - 10;
                tooltipRect.Y = hoveredItemRect.Y;

                if (tooltipRect.Intersects(hoveredItemRect) || tooltipRect.X < screenBounds.X + 10)
                {
                    tooltipRect.X = hoveredItemRect.X;
                    tooltipRect.Y = hoveredItemRect.Y - height - 10;

                    if (tooltipRect.Intersects(hoveredItemRect) || tooltipRect.Y < screenBounds.Y + 10)
                    {
                        tooltipRect.X = hoveredItemRect.X;
                        tooltipRect.Y = hoveredItemRect.Bottom + 10;
                    }
                }
            }

            if (tooltipRect.Right > screenBounds.Right - 10) tooltipRect.X = screenBounds.Right - 10 - tooltipRect.Width;
            if (tooltipRect.Bottom > screenBounds.Bottom - 10) tooltipRect.Y = screenBounds.Bottom - 10 - tooltipRect.Height;
            if (tooltipRect.X < screenBounds.X + 10) tooltipRect.X = screenBounds.X + 10;
            if (tooltipRect.Y < screenBounds.Y + 10) tooltipRect.Y = screenBounds.Y + 10;

            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel != null)
            {
                spriteBatch.Draw(pixel, tooltipRect, Color.Black * (0.85f * Alpha));
                spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, tooltipRect.Width, 1), Color.White * Alpha);
                spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Bottom - 1, tooltipRect.Width, 1), Color.White * Alpha);
                spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, 1, tooltipRect.Height), Color.White * Alpha);
                spriteBatch.Draw(pixel, new Rectangle(tooltipRect.Right - 1, tooltipRect.Y, 1, tooltipRect.Height), Color.White * Alpha);
            }

            int y = tooltipRect.Y + 4;
            foreach (var (text, color) in lines)
            {
                Vector2 size = font.MeasureString(text) * scale;
                Vector2 position = new(tooltipRect.X + (tooltipRect.Width - size.X) / 2f, y);
                spriteBatch.DrawString(font, text, position, color * Alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += (int)size.Y + 2;
            }
        }

        private static List<(string text, Color color)> BuildTooltipLines(InventoryItem item)
        {
            var details = item.Details;
            var lines = new List<(string, Color)>();

            string name = details.IsExcellent
                ? $"Excellent {item.Definition.Name}"
                : details.IsAncient
                    ? $"Ancient {item.Definition.Name}"
                    : item.Definition.Name;

            if (details.Level > 0)
            {
                name += $" +{details.Level}";
            }

            lines.Add((name, Color.White));

            var def = item.Definition;
            if (def.DamageMin > 0 || def.DamageMax > 0)
            {
                string dmgType = def.TwoHanded ? "Two-hand" : "One-hand";
                lines.Add(($"{dmgType} Damage : {def.DamageMin} ~ {def.DamageMax}", Color.Orange));
            }

            if (def.Defense > 0) lines.Add(($"Defense     : {def.Defense}", Color.Orange));
            if (def.DefenseRate > 0) lines.Add(($"Defense Rate: {def.DefenseRate}", Color.Orange));
            if (def.AttackSpeed > 0) lines.Add(($"Attack Speed: {def.AttackSpeed}", Color.Orange));
            lines.Add(($"Durability : {item.Durability}/{def.BaseDurability}", Color.Silver));
            if (def.RequiredLevel > 0) lines.Add(($"Required Level   : {def.RequiredLevel}", Color.LightGray));
            if (def.RequiredStrength > 0) lines.Add(($"Required Strength: {def.RequiredStrength}", Color.LightGray));
            if (def.RequiredDexterity > 0) lines.Add(($"Required Agility : {def.RequiredDexterity}", Color.LightGray));
            if (def.RequiredEnergy > 0) lines.Add(($"Required Energy  : {def.RequiredEnergy}", Color.LightGray));

            if (def.AllowedClasses != null && def.AllowedClasses.Count > 0)
            {
                foreach (string cls in def.AllowedClasses)
                {
                    lines.Add(($"Can be equipped by {cls}", Color.LightGray));
                }
            }

            if (details.OptionLevel > 0)
            {
                lines.Add(($"Additional Option : +{details.OptionLevel * 4}", new Color(80, 255, 80)));
            }

            if (details.HasLuck) lines.Add(("+Luck  (Crit +5 %, Jewel +25 %)", Color.CornflowerBlue));
            if (details.HasSkill) lines.Add(("+Skill (Right mouse click - skill)", Color.CornflowerBlue));

            if (details.IsExcellent)
            {
                byte excByte = item.RawData.Length > 3 ? item.RawData[3] : (byte)0;
                foreach (var option in ItemDatabase.ParseExcellentOptions(excByte))
                {
                    lines.Add(($"+{option}", new Color(128, 255, 128)));
                }
            }

            if (details.IsAncient)
            {
                lines.Add(("Ancient Option", new Color(0, 255, 128)));
            }

            return lines;
        }

        private void QueueWarmup()
        {
            if (_warmupPending)
            {
                return;
            }

            _warmupPending = true;
            MuGame.ScheduleOnMainThread(WarmupTextures);
        }

        private void WarmupTextures()
        {
            _warmupPending = false;

            var graphicsManager = GraphicsManager.Instance;
            if (graphicsManager?.Sprite == null)
            {
                QueueWarmup();
                return;
            }

            foreach (var item in _items)
            {
                int width = item.Definition.Width * SHOP_SQUARE_WIDTH;
                int height = item.Definition.Height * SHOP_SQUARE_HEIGHT;
                _ = ResolveItemTexture(item, width, height, animated: false);
            }
        }

        private void SendCloseNpcRequest()
        {
            if (_closeRequestSent)
            {
                return;
            }

            _closeRequestSent = true;
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendCloseNpcRequestAsync();
            }
        }

        private void DrawGridBackground(SpriteBatch spriteBatch)
        {
            if (_slotTexture == null)
            {
                return;
            }

            for (int y = 0; y < SHOP_ROWS; y++)
            {
                for (int x = 0; x < SHOP_COLUMNS; x++)
                {
                    var destRect = new Rectangle(
                        _gridOffset.X + x * SHOP_SQUARE_WIDTH,
                        _gridOffset.Y + y * SHOP_SQUARE_HEIGHT,
                        SHOP_SQUARE_WIDTH,
                        SHOP_SQUARE_HEIGHT);

                    spriteBatch.Draw(_slotTexture, destRect, SlotSourceRect, Color.White);
                }
            }
        }

        private void DrawStaticElements(SpriteBatch spriteBatch)
        {
            if (_layoutTexture != null && _layoutInfos.Count > 0)
            {
                foreach (var info in _layoutInfos)
                {
                    var destRect = new Rectangle(
                        (int)MathF.Round(info.ScreenX),
                        (int)MathF.Round(info.ScreenY),
                        info.Width,
                        info.Height);

                    if (_textureRectLookup.TryGetValue(info.Name, out var src))
                    {
                        var sourceRect = new Rectangle(src.X, src.Y, src.Width, src.Height);
                        spriteBatch.Draw(_layoutTexture, destRect, sourceRect, Color.White);
                    }
                    else
                    {
                        spriteBatch.Draw(_layoutTexture, destRect, Color.White);
                    }
                }
            }
            else if (GraphicsManager.Instance?.Pixel != null)
            {
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT), new Color(10, 10, 10, 220));
            }

            DrawGridBackground(spriteBatch);
        }

        private void EnsureStaticSurface()
        {
            if (!_staticSurfaceDirty && _staticSurface != null && !_staticSurface.IsDisposed)
            {
                return;
            }

            var graphicsDevice = GraphicsManager.Instance?.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            _staticSurface?.Dispose();
            _staticSurface = new RenderTarget2D(graphicsDevice, WINDOW_WIDTH, WINDOW_HEIGHT, false, SurfaceFormat.Color, DepthFormat.None);

            var previousTargets = graphicsDevice.GetRenderTargets();
            graphicsDevice.SetRenderTarget(_staticSurface);
            graphicsDevice.Clear(Color.Transparent);

            var spriteBatch = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {
                DrawStaticElements(spriteBatch);
            }

            graphicsDevice.SetRenderTargets(previousTargets);
            _staticSurfaceDirty = false;
        }

        private void InvalidateStaticSurface()
        {
            _staticSurfaceDirty = true;
        }

        private void LoadLayoutDefinitions()
        {
            try
            {
                var layoutData = LoadEmbeddedJson<List<LayoutInfo>>(LayoutJsonResource);
                if (layoutData != null)
                {
                    _layoutInfos.Clear();
                    _layoutInfos.AddRange(layoutData.OrderBy(info => info.Z));
                }

                var rectData = LoadEmbeddedJson<List<TextureRectData>>(TextureRectJsonResource);
                if (rectData != null)
                {
                    _textureRectLookup.Clear();
                    foreach (var rect in rectData)
                    {
                        _textureRectLookup[rect.Name] = rect;
                    }
                }
            }
            catch (Exception)
            {
                // If layout fails we fallback to plain background.
            }
        }

        private static T LoadEmbeddedJson<T>(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Resource not found: {resourceName}. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<T>(json);
        }

        private InventoryItem GetItemAt(Point mousePosition)
        {
            if (!DisplayRectangle.Contains(mousePosition))
            {
                return null;
            }

            var gridOrigin = new Point(DisplayRectangle.X + _gridOffset.X, DisplayRectangle.Y + _gridOffset.Y);

            foreach (var item in _items)
            {
                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * SHOP_SQUARE_WIDTH,
                    gridOrigin.Y + item.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                    item.Definition.Width * SHOP_SQUARE_WIDTH,
                    item.Definition.Height * SHOP_SQUARE_HEIGHT);

                if (rect.Contains(mousePosition))
                {
                    return item;
                }
            }

            return null;
        }

        private Point GetSlotAtScreenPosition(Point screenPos)
        {
            if (!DisplayRectangle.Contains(screenPos))
            {
                return new Point(-1, -1);
            }

            var gridOrigin = new Point(DisplayRectangle.X + _gridOffset.X, DisplayRectangle.Y + _gridOffset.Y);
            int localX = screenPos.X - gridOrigin.X;
            int localY = screenPos.Y - gridOrigin.Y;

            if (localX < 0 || localY < 0)
            {
                return new Point(-1, -1);
            }

            int slotX = localX / SHOP_SQUARE_WIDTH;
            int slotY = localY / SHOP_SQUARE_HEIGHT;

            if (slotX < 0 || slotX >= SHOP_COLUMNS || slotY < 0 || slotY >= SHOP_ROWS)
            {
                return new Point(-1, -1);
            }

            return new Point(slotX, slotY);
        }

        private Texture2D ResolveItemTexture(InventoryItem item, int width, int height, bool animated)
        {
            if (item?.Definition == null)
            {
                return null;
            }

            string texturePath = item.Definition.TexturePath;
            if (string.IsNullOrEmpty(texturePath))
            {
                return null;
            }

            bool isBmd = texturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase);

            if (!isBmd)
            {
                if (_itemTextureCache.TryGetValue(texturePath, out var cachedTexture) && cachedTexture != null)
                {
                    return cachedTexture;
                }

                var texture = TextureLoader.Instance.GetTexture2D(texturePath);
                if (texture != null)
                {
                    _itemTextureCache[texturePath] = texture;
                }
                return texture;
            }

            if (!animated && Constants.ENABLE_ITEM_MATERIAL_ANIMATION)
            {
                try
                {
                    var animatedMaterial = BmdPreviewRenderer.GetMaterialAnimatedPreview(item, width, height, _currentGameTime);
                    if (animatedMaterial != null)
                    {
                        return animatedMaterial;
                    }
                }
                catch
                {
                    // ignore and fall back
                }
            }

            if (animated)
            {
                try
                {
                    return BmdPreviewRenderer.GetAnimatedPreview(item, width, height, _currentGameTime);
                }
                catch
                {
                    return null;
                }
            }

            var key = (item, width, height, false);
            if (_bmdPreviewCache.TryGetValue(key, out var preview) && preview != null)
            {
                return preview;
            }

            try
            {
                preview = BmdPreviewRenderer.GetPreview(item, width, height);
                if (preview != null)
                {
                    _bmdPreviewCache[key] = preview;
                }
                return preview;
            }
            catch
            {
                return null;
            }
        }
    }
}
