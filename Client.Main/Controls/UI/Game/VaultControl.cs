using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Client.Main.Networking;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Client.Main.Models;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Controls.UI.Game
{
    public class VaultControl : UIControl, IUiTexturePreloadable
    {
        private const string LayoutJsonResource = "Client.Main.Controls.UI.Game.Layouts.NpcShopLayout.json";
        private const string TextureRectJsonResource = "Client.Main.Controls.UI.Game.Layouts.NpcShopRect.json";
        private const string LayoutTexturePath = "Interface/GFx/NpcShop_I3.ozd";

        private const int WINDOW_WIDTH = 422;
        private const int WINDOW_HEIGHT = 624;

        public const int Columns = 8;
        public const int Rows = 15;
        private const int VaultSquareWidth = 25;
        private const int VaultSquareHeight = 25;

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

        private sealed class VaultTextEntry
        {
            public VaultTextEntry(Vector2 basePosition, float fontScale, Color color, TextAlignment alignment)
            {
                BasePosition = basePosition;
                FontScale = fontScale;
                Color = color;
                Alignment = alignment;
            }

            public Vector2 BasePosition { get; }
            public float FontScale { get; }
            public Color Color { get; set; }
            public TextAlignment Alignment { get; }
            public string Text { get; set; } = string.Empty;
            public bool Visible { get; set; } = true;
        }

        private enum TextAlignment
        {
            Left,
            Center,
            Right
        }

        private static VaultControl _instance;

        private readonly List<LayoutInfo> _layoutInfos = new();
        private readonly Dictionary<string, TextureRectData> _textureRectLookup = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<InventoryItem> _items = new();
        private InventoryItem[,] _itemGrid = new InventoryItem[Columns, Rows];

        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private readonly List<VaultTextEntry> _texts = new();
        private VaultTextEntry _titleText;
        private VaultTextEntry _zenText;

        private Texture2D _layoutTexture;
        private Texture2D _slotTexture;
        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private SpriteFont _font;
        private readonly Point _gridOffset = new(170, 180);

        private CharacterState _characterState;
        private readonly NetworkManager _networkManager;
        private readonly ILogger<VaultControl> _logger;

        private InventoryItem _hoveredItem;
        private Point _hoveredSlot = new(-1, -1);
        private Point _pendingDropSlot = new(-1, -1);

        private InventoryItem _draggedItem;
        private Point _draggedOriginalSlot = new(-1, -1);

        private GameTime _currentGameTime;
        private bool _wasVisible;
        private bool _escapeHandled;
        private bool _closeRequestSent;
        private bool _warmupPending;

        private VaultControl(NetworkManager networkManager = null, ILoggerFactory loggerFactory = null)
        {
            _networkManager = networkManager ?? MuGame.Network;
            var factory = loggerFactory ?? MuGame.AppLoggerFactory;
            _logger = factory?.CreateLogger<VaultControl>();

            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.VerticalCenter | ControlAlign.Left;

            LoadLayoutDefinitions();
            InitializeTextEntries();
            EnsureCharacterState();
        }

        public static VaultControl Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new VaultControl();
                }

                return _instance;
            }
        }

        public IEnumerable<string> GetPreloadTexturePaths()
        {
            yield return LayoutTexturePath;
        }

        public override async Task Load()
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
            _currentGameTime = gameTime;

            if (Visible)
            {
                HandleKeyboardInput();
                if (Visible)
                {
                    UpdateHoverState();
                    HandleMouseInput();
                    UpdateTextValues();
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
            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform))
                {
                    DrawDynamicContent(spriteBatch);
                }
            }
            else
            {
                DrawDynamicContent(spriteBatch);
            }
        }

        private void DrawDynamicContent(SpriteBatch spriteBatch)
        {
            if (_staticSurface != null && !_staticSurface.IsDisposed)
            {
                spriteBatch.Draw(_staticSurface, DisplayRectangle, Color.White * Alpha);
            }

            var gridOrigin = new Point(DisplayRectangle.X + _gridOffset.X, DisplayRectangle.Y + _gridOffset.Y);

            DrawDragPreviewHighlight(spriteBatch, gridOrigin);
            DrawHoveredSlotHighlight(spriteBatch, gridOrigin);
            DrawHoveredItemSlotHighlights(spriteBatch, gridOrigin);
            DrawVaultItems(spriteBatch, gridOrigin);
            DrawTexts(spriteBatch);
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

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform))
                {
                    DrawTooltip(spriteBatch, DisplayRectangle);
                }
            }
            else
            {
                DrawTooltip(spriteBatch, DisplayRectangle);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            if (_characterState != null)
            {
                _characterState.VaultItemsChanged -= RefreshVaultContent;
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

        public void CloseWindow()
        {
            if (!Visible)
            {
                return;
            }

            Visible = false;
            HandleVisibilityLost();
            _wasVisible = false;
        }

        public InventoryItem GetDraggedItem() => _draggedItem;

        public void DrawPickedPreview(SpriteBatch spriteBatch, GameTime gameTime)
        {
            if (_draggedItem == null || spriteBatch == null)
            {
                return;
            }

            int width = _draggedItem.Definition.Width * VaultSquareWidth;
            int height = _draggedItem.Definition.Height * VaultSquareHeight;

            var mouse = MuGame.Instance.UiMouseState.Position;
            var destRect = new Rectangle(mouse.X - width / 2, mouse.Y - height / 2, width, height);

            Texture2D texture = ResolveItemTexture(_draggedItem, width, height, animated: Constants.ENABLE_ITEM_MATERIAL_ANIMATION);

            if (texture != null)
            {
                spriteBatch.Draw(texture, destRect, Color.White * 0.9f);
            }
            else if (GraphicsManager.Instance?.Pixel != null)
            {
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, destRect, Color.DarkGoldenrod * 0.8f);
            }
        }

        public Point GetSlotAtScreenPosition(Point screenPos)
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

            int slotX = localX / VaultSquareWidth;
            int slotY = localY / VaultSquareHeight;

            if (slotX < 0 || slotX >= Columns || slotY < 0 || slotY >= Rows)
            {
                return new Point(-1, -1);
            }

            return new Point(slotX, slotY);
        }

        public bool CanPlaceAt(Point gridSlot, InventoryItem item)
        {
            if (item?.Definition == null)
            {
                return false;
            }

            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gx = gridSlot.X + x;
                    int gy = gridSlot.Y + y;

                    if (gx < 0 || gx >= Columns || gy < 0 || gy >= Rows)
                    {
                        return false;
                    }

                    var occupant = _itemGrid[gx, gy];
                    if (occupant != null && occupant != item)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void HandleKeyboardInput()
        {
            var keyboard = Keyboard.GetState();
            bool escapeDown = keyboard.IsKeyDown(Keys.Escape);

            if (escapeDown && !_escapeHandled)
            {
                CloseWindow();
                _escapeHandled = true;
            }
            else if (!escapeDown)
            {
                _escapeHandled = false;
            }
        }

        private void HandleMouseInput()
        {
            var mouse = MuGame.Instance.UiMouseState;
            var prev = MuGame.Instance.PrevUiMouseState;

            bool leftJustPressed = mouse.LeftButton == ButtonState.Pressed &&
                                   prev.LeftButton == ButtonState.Released;

            if (!leftJustPressed)
            {
                return;
            }

            if (_draggedItem == null)
            {
                if (_hoveredItem != null)
                {
                    BeginDrag(_hoveredItem);
                    Scene?.SetMouseInputConsumed();
                }
                return;
            }

            AttemptDrop(mouse.Position);
            Scene?.SetMouseInputConsumed();
        }

        private void BeginDrag(InventoryItem item)
        {
            _draggedItem = item;
            _draggedOriginalSlot = item.GridPosition;
            RemoveItemFromGrid(item);
            _hoveredItem = null;
            _pendingDropSlot = new Point(-1, -1);
        }

        private void AttemptDrop(Point mousePosition)
        {
            if (_draggedItem == null)
            {
                return;
            }

            var dropSlot = GetSlotAtScreenPosition(mousePosition);
            var inventory = InventoryControl.Instance;
            bool dropped = false;

            if (dropSlot.X >= 0 && CanPlaceAt(dropSlot, _draggedItem))
            {
                PlaceDraggedItem(dropSlot);
                int width = _draggedItem.Definition.Width * VaultSquareWidth;
                int height = _draggedItem.Definition.Height * VaultSquareHeight;
                _ = ResolveItemTexture(_draggedItem, width, height, animated: Constants.ENABLE_ITEM_MATERIAL_ANIMATION);
                if (dropSlot != _draggedOriginalSlot)
                {
                    SendVaultMove(_draggedOriginalSlot, dropSlot);
                }
                dropped = true;
            }
            else if (inventory != null &&
                     inventory.Visible &&
                     inventory.DisplayRectangle.Contains(mousePosition))
            {
                Point invSlot = inventory.GetSlotAtScreenPositionPublic(mousePosition);
                if (invSlot.X >= 0 && inventory.CanPlaceAt(invSlot, _draggedItem))
                {
                    MoveItemToInventory(invSlot, inventory);
                    dropped = true;
                }
            }

            if (!dropped)
            {
                ReturnDraggedItem();
            }

            _draggedItem = null;
            _draggedOriginalSlot = new Point(-1, -1);
            _pendingDropSlot = new Point(-1, -1);
        }

        private void ReturnDraggedItem()
        {
            if (_draggedItem != null && _draggedOriginalSlot.X >= 0)
            {
                PlaceItemOnGrid(_draggedItem, _draggedOriginalSlot);
            }
        }

        private void PlaceDraggedItem(Point newSlot)
        {
            if (_draggedItem == null)
            {
                return;
            }

            PlaceItemOnGrid(_draggedItem, newSlot);
        }

        private void MoveItemToInventory(Point targetSlot, InventoryControl inventory)
        {
            if (_draggedItem == null)
            {
                return;
            }

            byte fromSlot = (byte)(_draggedOriginalSlot.Y * Columns + _draggedOriginalSlot.X);
            byte toSlot = (byte)(targetSlot.Y * InventoryControl.Columns + targetSlot.X);

            var svc = _networkManager?.GetCharacterService();
            var state = _networkManager?.GetCharacterState();
            var version = _networkManager?.TargetVersion ?? TargetProtocolVersion.Season6;

            if (svc != null && state != null)
            {
                state.StashPendingVaultMove(fromSlot, 0xFF);
                state.StashPendingInventoryMove(toSlot, toSlot);

                var raw = _draggedItem.RawData ?? Array.Empty<byte>();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await svc.SendStorageItemMoveAsync(ItemStorageKind.Vault, fromSlot, ItemStorageKind.Inventory, toSlot, version, raw);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to move item from vault to inventory.");
                    }

                    await Task.Delay(1200);

                    if (_networkManager != null && state.IsVaultMovePending(fromSlot, 0xFF))
                    {
                        MuGame.ScheduleOnMainThread(state.RaiseVaultItemsChanged);
                    }

                    if (_networkManager != null && state.IsInventoryMovePending(toSlot, toSlot))
                    {
                        MuGame.ScheduleOnMainThread(state.RaiseInventoryChanged);
                    }
                });
            }

            _items.Remove(_draggedItem);
            inventory?.BringToFront();
        }

        private void SendVaultMove(Point fromSlot, Point toSlot)
        {
            byte from = (byte)(fromSlot.Y * Columns + fromSlot.X);
            byte to = (byte)(toSlot.Y * Columns + toSlot.X);

            if (_networkManager == null)
            {
                return;
            }

            var svc = _networkManager.GetCharacterService();
            var state = _networkManager.GetCharacterState();

            if (svc == null || state == null)
            {
                return;
            }

            state.StashPendingVaultMove(from, to);

            var raw = _draggedItem?.RawData ?? Array.Empty<byte>();
            var version = _networkManager.TargetVersion;

            _ = Task.Run(async () =>
            {
                try
                {
                    await svc.SendStorageItemMoveAsync(ItemStorageKind.Vault, from, ItemStorageKind.Vault, to, version, raw);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to move item inside vault.");
                }

                await Task.Delay(1200);
                if (_networkManager != null && state.IsVaultMovePending(from, to))
                {
                    MuGame.ScheduleOnMainThread(state.RaiseVaultItemsChanged);
                }
            });
        }

        private void UpdateHoverState()
        {
            var mouse = MuGame.Instance.UiMouseState.Position;

            if (_draggedItem != null)
            {
                var dropSlot = GetSlotAtScreenPosition(mouse);
                if (dropSlot.X >= 0 && CanPlaceAt(dropSlot, _draggedItem))
                {
                    _pendingDropSlot = dropSlot;
                }
                else
                {
                    _pendingDropSlot = new Point(-1, -1);
                }

                _hoveredItem = null;
                _hoveredSlot = dropSlot;
                return;
            }

            _hoveredSlot = GetSlotAtScreenPosition(mouse);
            _hoveredItem = GetItemAt(mouse);
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
                if (item == _draggedItem)
                {
                    continue;
                }

                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * VaultSquareWidth,
                    gridOrigin.Y + item.GridPosition.Y * VaultSquareHeight,
                    item.Definition.Width * VaultSquareWidth,
                    item.Definition.Height * VaultSquareHeight);

                if (rect.Contains(mousePosition))
                {
                    return item;
                }
            }

            return null;
        }

        private void HandleVisibilityLost()
        {
            SendCloseNpcRequest();
            _characterState?.ClearVaultItems();
            _items.Clear();
            ClearGrid();
            _itemTextureCache.Clear();
            _bmdPreviewCache.Clear();
            _draggedItem = null;
            _hoveredItem = null;
            _pendingDropSlot = new Point(-1, -1);
        }

        private void RefreshVaultContent()
        {
            if (_characterState == null)
            {
                return;
            }

            _items.Clear();
            ClearGrid();

            var vaultItems = _characterState.GetVaultItems();
            foreach (var kv in vaultItems)
            {
                byte slot = kv.Key;
                byte[] data = kv.Value;

                int gridX = slot % Columns;
                int gridY = slot / Columns;

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
                PlaceItemOnGrid(item, item.GridPosition);
            }

            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                {
                    _ = TextureLoader.Instance.Prepare(item.Definition.TexturePath);
                }
            }

            UpdateTextValues();

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

        private void UpdateTextValues()
        {
            if (_characterState == null || _zenText == null)
            {
                return;
            }

            _zenText.Text = $"Zen: {_characterState.InventoryZen:N0}";
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
                _characterState.VaultItemsChanged += RefreshVaultContent;
            }
        }

        private void InitializeTextEntries()
        {
            _texts.Clear();
            _titleText = null;
            _zenText = null;
        }

        private VaultTextEntry CreateText(Vector2 basePosition, float fontSize, Color color, TextAlignment alignment = TextAlignment.Left)
        {
            float fontScale = fontSize / Constants.BASE_FONT_SIZE;
            var entry = new VaultTextEntry(basePosition, fontScale, color, alignment);
            _texts.Add(entry);
            return entry;
        }

        private void DrawTexts(SpriteBatch spriteBatch)
        {
            if (_font == null)
            {
                return;
            }

            float controlScale = Scale;
            Vector2 basePosition = DisplayRectangle.Location.ToVector2();

            foreach (var entry in _texts)
            {
                if (!entry.Visible || string.IsNullOrEmpty(entry.Text))
                {
                    continue;
                }

                float textScale = entry.FontScale * controlScale;
                Vector2 position = basePosition + entry.BasePosition * controlScale;
                Vector2 size = _font.MeasureString(entry.Text) * textScale;

                switch (entry.Alignment)
                {
                    case TextAlignment.Center:
                        position.X -= size.X * 0.5f;
                        break;
                    case TextAlignment.Right:
                        position.X -= size.X;
                        break;
                }

                spriteBatch.DrawString(_font, entry.Text, position, entry.Color * Alpha, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawVaultItems(SpriteBatch spriteBatch, Point gridOrigin)
        {
            var font = _font ?? GraphicsManager.Instance.Font;
            foreach (var item in _items)
            {
                if (item == _draggedItem)
                {
                    continue;
                }

                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * VaultSquareWidth,
                    gridOrigin.Y + item.GridPosition.Y * VaultSquareHeight,
                    item.Definition.Width * VaultSquareWidth,
                    item.Definition.Height * VaultSquareHeight);

                bool isHovered = item == _hoveredItem;
                var texture = ResolveItemTexture(item, rect.Width, rect.Height, isHovered);
                if (texture != null)
                {
                    spriteBatch.Draw(texture, rect, Color.White * Alpha);
                }
                else if (GraphicsManager.Instance?.Pixel != null)
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, Color.DarkSlateGray * (0.8f * Alpha));
                }

                if (font != null && item.Definition.BaseDurability == 0 && item.Durability > 1)
                {
                    DrawStackCount(spriteBatch, font, rect, item.Durability);
                }
            }
        }

        private void DrawStackCount(SpriteBatch spriteBatch, SpriteFont font, Rectangle rect, int quantity)
        {
            string text = quantity.ToString();
            const float scale = 0.4f;
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = new(rect.Right - size.X - 2, rect.Y + 2);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    spriteBatch.DrawString(font, text, pos + new Vector2(dx, dy), Color.Black * Alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.DrawString(font, text, pos, new Color(255, 255, 180) * Alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawHoveredSlotHighlight(SpriteBatch spriteBatch, Point gridOrigin)
        {
            if (_draggedItem != null || _hoveredSlot.X < 0 || GraphicsManager.Instance?.Pixel == null)
            {
                return;
            }

            var rect = new Rectangle(
                gridOrigin.X + _hoveredSlot.X * VaultSquareWidth,
                gridOrigin.Y + _hoveredSlot.Y * VaultSquareHeight,
                VaultSquareWidth,
                VaultSquareHeight);

            spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, Color.Yellow * (0.3f * Alpha));
        }

        private void DrawHoveredItemSlotHighlights(SpriteBatch spriteBatch, Point gridOrigin)
        {
            if (_draggedItem != null || _hoveredItem == null || GraphicsManager.Instance?.Pixel == null)
            {
                return;
            }

            for (int y = 0; y < _hoveredItem.Definition.Height; y++)
            {
                for (int x = 0; x < _hoveredItem.Definition.Width; x++)
                {
                    int gx = _hoveredItem.GridPosition.X + x;
                    int gy = _hoveredItem.GridPosition.Y + y;

                    if (gx == _hoveredSlot.X && gy == _hoveredSlot.Y)
                    {
                        continue;
                    }

                    var rect = new Rectangle(
                        gridOrigin.X + gx * VaultSquareWidth,
                        gridOrigin.Y + gy * VaultSquareHeight,
                        VaultSquareWidth,
                        VaultSquareHeight);

                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, Color.CornflowerBlue * (0.35f * Alpha));
                }
            }
        }

        private void DrawDragPreviewHighlight(SpriteBatch spriteBatch, Point gridOrigin)
        {
            if (_draggedItem == null || GraphicsManager.Instance?.Pixel == null)
            {
                return;
            }

            if (_pendingDropSlot.X >= 0)
            {
                for (int y = 0; y < _draggedItem.Definition.Height; y++)
                {
                    for (int x = 0; x < _draggedItem.Definition.Width; x++)
                    {
                        int gx = _pendingDropSlot.X + x;
                        int gy = _pendingDropSlot.Y + y;

                        var rect = new Rectangle(
                            gridOrigin.X + gx * VaultSquareWidth,
                            gridOrigin.Y + gy * VaultSquareHeight,
                            VaultSquareWidth,
                            VaultSquareHeight);

                        spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, Color.LimeGreen * (0.35f * Alpha));
                    }
                }
            }
            else
            {
                var invalidSlot = GetSlotAtScreenPosition(MuGame.Instance.UiMouseState.Position);
                if (invalidSlot.X >= 0)
                {
                    for (int y = 0; y < _draggedItem.Definition.Height; y++)
                    {
                        for (int x = 0; x < _draggedItem.Definition.Width; x++)
                        {
                            int gx = invalidSlot.X + x;
                            int gy = invalidSlot.Y + y;

                            var rect = new Rectangle(
                                gridOrigin.X + gx * VaultSquareWidth,
                                gridOrigin.Y + gy * VaultSquareHeight,
                                VaultSquareWidth,
                                VaultSquareHeight);

                            spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, Color.IndianRed * (0.35f * Alpha));
                        }
                    }
                }
            }
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
                gridOrigin.X + _hoveredItem.GridPosition.X * VaultSquareWidth,
                gridOrigin.Y + _hoveredItem.GridPosition.Y * VaultSquareHeight,
                _hoveredItem.Definition.Width * VaultSquareWidth,
                _hoveredItem.Definition.Height * VaultSquareHeight);

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
                int width = item.Definition.Width * VaultSquareWidth;
                int height = item.Definition.Height * VaultSquareHeight;
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

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    var destRect = new Rectangle(
                        _gridOffset.X + x * VaultSquareWidth,
                        _gridOffset.Y + y * VaultSquareHeight,
                        VaultSquareWidth,
                        VaultSquareHeight);

                    spriteBatch.Draw(_slotTexture, destRect, SlotSourceRect, Color.White);
                }
            }
        }

        private void DrawStaticElements(SpriteBatch spriteBatch)
        {
            if (_layoutTexture != null && _layoutInfos.Count > 0)
            {
                foreach (var info in _layoutInfos.OrderBy(i => i.Z))
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
                var layout = LoadEmbeddedJson<List<LayoutInfo>>(LayoutJsonResource);
                if (layout != null)
                {
                    _layoutInfos.Clear();
                    _layoutInfos.AddRange(layout);
                }

                var rects = LoadEmbeddedJson<List<TextureRectData>>(TextureRectJsonResource);
                if (rects != null)
                {
                    _textureRectLookup.Clear();
                    foreach (var rect in rects)
                    {
                        _textureRectLookup[rect.Name] = rect;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load vault layout definitions. Falling back to flat background.");
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

        private void PlaceItemOnGrid(InventoryItem item, Point slot)
        {
            item.GridPosition = slot;
            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gx = slot.X + x;
                    int gy = slot.Y + y;
                    if (gx >= 0 && gx < Columns && gy >= 0 && gy < Rows)
                    {
                        _itemGrid[gx, gy] = item;
                    }
                }
            }
        }

        private void RemoveItemFromGrid(InventoryItem item)
        {
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    if (_itemGrid[x, y] == item)
                    {
                        _itemGrid[x, y] = null;
                    }
                }
            }
        }

        private void ClearGrid()
        {
            Array.Clear(_itemGrid, 0, _itemGrid.Length);
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
