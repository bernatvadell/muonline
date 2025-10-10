using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Networking;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;
using Client.Main.Controls.UI;

namespace Client.Main.Controls.UI.Game.Inventory
{
    public class InventoryControl : UIControl, IUiTexturePreloadable
    {
        private const string LayoutJsonResource = "Client.Main.Controls.UI.Game.Layouts.InventoryLayout.json";
        private const string TextureRectJsonResource = "Client.Main.Controls.UI.Game.Layouts.InventoryRect.json";
        private const string LayoutTexturePath = "Interface/GFx/NpcShop_I3.ozd";

        private static readonly string[] s_inventoryTexturePaths =
        {
            "Interface/newui_item_box.tga",
            "Interface/newui_item_table01(L).tga",
            "Interface/newui_item_table01(R).tga",
            "Interface/newui_item_table02(L).tga",
            "Interface/newui_item_table02(R).tga",
            "Interface/newui_item_table03(Up).tga",
            "Interface/newui_item_table03(Dw).tga",
            "Interface/newui_item_table03(L).tga",
            "Interface/newui_item_table03(R).tga",
            "Interface/newui_msgbox_back.jpg"
        };

        private const int WINDOW_WIDTH = 377;
        private const int WINDOW_HEIGHT = 540;

        public const int INVENTORY_SQUARE_WIDTH = 35;
        public const int INVENTORY_SQUARE_HEIGHT = 35;

        public const int Columns = 8;
        public const int Rows = 8;
        private const int EquipRows = 2;
        private const int InventorySlotOffsetConstant = 12;

        private static readonly Rectangle SlotSourceRect = new(546, 220, 29, 29);

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

        private enum TextAlignment
        {
            Left,
            Center,
            Right
        }

        private sealed class InventoryTextEntry
        {
            public InventoryTextEntry(Vector2 basePosition, float fontScale, Color color, TextAlignment alignment)
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

        private Texture2D _layoutTexture;
        private Texture2D _slotTexture;
        private Texture2D _texSquare;
        private Texture2D _texTableTopLeft;
        private Texture2D _texTableTopRight;
        private Texture2D _texTableBottomLeft;
        private Texture2D _texTableBottomRight;
        private Texture2D _texTableTopPixel;
        private Texture2D _texTableBottomPixel;
        private Texture2D _texTableLeftPixel;
        private Texture2D _texTableRightPixel;
        private Texture2D _texBackground;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private readonly List<InventoryTextEntry> _texts = new();
        private InventoryTextEntry _titleText;
        private InventoryTextEntry _zenText;

        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private readonly List<InventoryItem> _items = new();
        private readonly Dictionary<byte, InventoryItem> _equippedItems = new();
        private InventoryItem[,] _itemGrid;

        private readonly NetworkManager _networkManager;
        private readonly ILogger<InventoryControl> _logger;

        private SpriteFont _font;

        private readonly Point _gridOffset = new(50, 80);

        private InventoryItem _hoveredItem;
        private Point _hoveredSlot = new(-1, -1);
        private int _hoveredEquipSlot = -1;
        private int _pickedFromEquipSlot = -1;
        private Point _pickedItemOriginalGrid = new(-1, -1);

        private bool _isDragging;
        private Point _dragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        private long _zenAmount;
        private GameTime _currentGameTime;

        public readonly PickedItemRenderer _pickedItemRenderer;

        private readonly List<LayoutInfo> _layoutInfos = new();
        private readonly Dictionary<string, TextureRectData> _textureRectLookup = new(StringComparer.OrdinalIgnoreCase);

        private static InventoryControl _instance;

        private static readonly Dictionary<byte, Point> s_equipLayout = new()
        {
            { 0, new Point(0,0) },
            { 2, new Point(1,0) },
            { 3, new Point(2,0) },
            { 1, new Point(3,0) },
            { 7, new Point(4,0) },
            { 9, new Point(5,0) },
            { 8, new Point(0,1) },
            { 5, new Point(1,1) },
            { 4, new Point(2,1) },
            { 6, new Point(3,1) },
            { 10, new Point(4,1) },
            { 11, new Point(5,1) }
        };

        public InventoryControl(NetworkManager networkManager = null, ILoggerFactory loggerFactory = null)
        {
            _networkManager = networkManager;
            var factory = loggerFactory ?? MuGame.AppLoggerFactory;
            _logger = factory?.CreateLogger<InventoryControl>();

            LoadLayoutDefinitions();

            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.VerticalCenter | ControlAlign.Right;
            Scale = 1f;

            _itemGrid = new InventoryItem[Columns, Rows];
            _pickedItemRenderer = new PickedItemRenderer();

            InitializeTextEntries();
        }

        public static InventoryControl Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new InventoryControl();
                }

                return _instance;
            }
        }

        public IEnumerable<string> GetPreloadTexturePaths()
            => s_inventoryTexturePaths.Append(LayoutTexturePath);

        public long ZenAmount
        {
            get => _zenAmount;
            set
            {
                if (_zenAmount != value)
                {
                    _zenAmount = value;
                    UpdateZenText();
                }
            }
        }

        public override async Task Load()
        {
            await base.Load();

            var tl = TextureLoader.Instance;

            var textureLoadTasks = s_inventoryTexturePaths.Select(path => tl.PrepareAndGetTexture(path)).ToList();
            var loadedTextures = await Task.WhenAll(textureLoadTasks);

            _texSquare = loadedTextures.ElementAtOrDefault(0);
            _texTableTopLeft = loadedTextures.ElementAtOrDefault(1);
            _texTableTopRight = loadedTextures.ElementAtOrDefault(2);
            _texTableBottomLeft = loadedTextures.ElementAtOrDefault(3);
            _texTableBottomRight = loadedTextures.ElementAtOrDefault(4);
            _texTableTopPixel = loadedTextures.ElementAtOrDefault(5);
            _texTableBottomPixel = loadedTextures.ElementAtOrDefault(6);
            _texTableLeftPixel = loadedTextures.ElementAtOrDefault(7);
            _texTableRightPixel = loadedTextures.ElementAtOrDefault(8);
            _texBackground = loadedTextures.ElementAtOrDefault(9);

            _layoutTexture = await tl.PrepareAndGetTexture(LayoutTexturePath);
            _slotTexture = _layoutTexture;

            _font = GraphicsManager.Instance.Font;

            UpdateZenFromNetwork();
            UpdateZenText();
            InvalidateStaticSurface();
        }

        public void Preload()
        {
            RefreshInventoryContent();
        }

        public void Show()
        {
            UpdateZenFromNetwork();
            RefreshInventoryContent();

            Visible = true;
            BringToFront();
            Scene.FocusControl = this;

            _zenText.Visible = true;
            UpdateZenText();

            _pickedItemRenderer.Visible = false;

            InvalidateStaticSurface();
        }

        public void Hide()
        {
            if (_pickedItemRenderer.Item != null)
            {
                InventoryItem itemToReturn = _pickedItemRenderer.Item;
                AddItem(itemToReturn);
                _pickedItemRenderer.ReleaseItem();
            }

            Visible = false;
            if (Scene?.FocusControl == this)
            {
                Scene.FocusControl = null;
            }

            _zenText.Visible = false;
        }

        public void HookEvents()
        {
            if (_networkManager == null)
            {
                return;
            }

            var state = _networkManager.GetCharacterState();
            state.InventoryChanged += () => MuGame.ScheduleOnMainThread(RefreshInventoryContent);
            state.MoneyChanged += () => MuGame.ScheduleOnMainThread(() => ZenAmount = state.InventoryZen);
        }

        public bool AddItem(InventoryItem item)
        {
            if (CanPlaceItem(item, item.GridPosition))
            {
                _items.Add(item);
                PlaceItemOnGrid(item);
                return true;
            }

            return false;
        }

        public Point GetSlotAtScreenPositionPublic(Point screenPos) => GetSlotAtScreenPosition(screenPos);

        public bool CanPlaceAt(Point gridSlot, InventoryItem item) => CanPlaceItem(item, gridSlot);

        public override void Update(GameTime gameTime)
        {
            _currentGameTime = gameTime;

            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) &&
                MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
            {
                if (_pickedItemRenderer.Item != null)
                {
                    InventoryItem itemToReturn = _pickedItemRenderer.Item;
                    AddItem(itemToReturn);
                    _pickedItemRenderer.ReleaseItem();
                }

                Visible = false;
                _zenText.Visible = false;
            }

            if (!Visible)
            {
                _pickedItemRenderer.Visible = false;
                return;
            }

            base.Update(gameTime);

            Point mousePos = MuGame.Instance.UiMouseState.Position;
            _hoveredItem = null;
            _hoveredSlot = new Point(-1, -1);
            _hoveredEquipSlot = GetEquipSlotAtScreenPosition(mousePos);

            bool leftPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = !leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Pressed;

            if (leftJustPressed && IsMouseOverDragArea() && !_isDragging)
            {
                DateTime now = DateTime.Now;
                if ((now - _lastClickTime).TotalMilliseconds < 500)
                {
                    Align = ControlAlign.VerticalCenter | ControlAlign.Right;
                    _lastClickTime = DateTime.MinValue;
                }
                else
                {
                    _isDragging = true;
                    _dragOffset = new Point(mousePos.X - X, mousePos.Y - Y);
                    Align = ControlAlign.None;
                    _lastClickTime = now;
                }
            }
            else if (leftJustReleased && _isDragging)
            {
                _isDragging = false;
            }
            else if (_isDragging && leftPressed)
            {
                X = mousePos.X - _dragOffset.X;
                Y = mousePos.Y - _dragOffset.Y;
            }

            if (IsMouseOver && !_isDragging)
            {
                HandleInventoryInteraction(mousePos, leftJustPressed, leftJustReleased);
            }

            if (leftJustReleased && _pickedItemRenderer.Item != null && !_isDragging && !IsMouseOverGrid())
            {
                HandleDropOutsideInventory();
            }

            _pickedItemRenderer.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible)
            {
                return;
            }

            var graphicsManager = GraphicsManager.Instance;
            if (graphicsManager?.Sprite == null)
            {
                return;
            }

            EnsureStaticSurface();

            var spriteBatch = graphicsManager.Sprite;
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

                DrawInventoryItems(spriteBatch);
                DrawEquippedItems(spriteBatch);
                DrawGridOverlays(spriteBatch);
                DrawTexts(spriteBatch);
                DrawTooltip(spriteBatch);
            }
            finally
            {
                scope?.Dispose();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _staticSurface?.Dispose();
            _staticSurface = null;
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            InvalidateStaticSurface();
        }

        private void InitializeTextEntries()
        {
            _texts.Clear();

            _titleText = CreateText(new Vector2(WINDOW_WIDTH / 2f, 36f), 13f, Color.Orange, TextAlignment.Center);
            _titleText.Text = "Inventory";

            _zenText = CreateText(new Vector2(_gridOffset.X, CalculateZenTextY()), 14f, Color.Gold);
            _zenText.Visible = false;
        }

        private InventoryTextEntry CreateText(Vector2 basePosition, float fontSize, Color color, TextAlignment alignment = TextAlignment.Left)
        {
            float fontScale = fontSize / Constants.BASE_FONT_SIZE;
            var entry = new InventoryTextEntry(basePosition, fontScale, color, alignment);
            _texts.Add(entry);
            return entry;
        }

        private float CalculateZenTextY()
        {
            int gridBottomY = _gridOffset.Y + Rows * INVENTORY_SQUARE_HEIGHT;
            int equipAreaBottomY = gridBottomY + 20 + (EquipRows * INVENTORY_SQUARE_HEIGHT);
            return equipAreaBottomY + 10f;
        }

        private void UpdateZenFromNetwork()
        {
            if (_networkManager == null)
            {
                return;
            }

            var state = _networkManager.GetCharacterState();
            ZenAmount = state?.InventoryZen ?? 0;
        }

        private void UpdateZenText()
        {
            if (_zenText != null)
            {
                _zenText.Text = $"ZEN: {ZenAmount}";
            }
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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load inventory layout definitions.");
            }
        }

        private static T LoadEmbeddedJson<T>(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Resource not found: {resourceName}. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            return System.Text.Json.JsonSerializer.Deserialize<T>(json);
        }

        private void InvalidateStaticSurface()
        {
            _staticSurfaceDirty = true;
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

        private void DrawStaticElements(SpriteBatch spriteBatch)
        {
            if (_texBackground != null)
            {
                spriteBatch.Draw(_texBackground, new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT), Color.White);
            }
            else if (GraphicsManager.Instance?.Pixel != null)
            {
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT), new Color(10, 10, 10, 220));
            }

            if (_layoutTexture != null && _layoutInfos.Count > 0)
            {
                foreach (var info in _layoutInfos)
                {
                    var destRect = new Rectangle(
                        (int)MathF.Round(info.ScreenX),
                        (int)MathF.Round(info.ScreenY),
                        info.Width,
                        info.Height);

                    if (_textureRectLookup.TryGetValue(info.Name, out var texRect))
                    {
                        var sourceRect = new Rectangle(texRect.X, texRect.Y, texRect.Width, texRect.Height);
                        spriteBatch.Draw(_layoutTexture, destRect, sourceRect, Color.White);
                    }
                    else
                    {
                        spriteBatch.Draw(_layoutTexture, destRect, Color.White * 0.2f);
                    }
                }
            }

            DrawGridBackground(spriteBatch);
            DrawEquipBackground(spriteBatch);
        }

        private void DrawGridBackground(SpriteBatch spriteBatch)
        {
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    Rectangle slotRect = new(
                        _gridOffset.X + x * INVENTORY_SQUARE_WIDTH,
                        _gridOffset.Y + y * INVENTORY_SQUARE_HEIGHT,
                        INVENTORY_SQUARE_WIDTH,
                        INVENTORY_SQUARE_HEIGHT);

                    if (_slotTexture != null)
                    {
                        spriteBatch.Draw(_slotTexture, slotRect, SlotSourceRect, Color.White);
                    }
                    else if (GraphicsManager.Instance?.Pixel != null)
                    {
                        spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, Color.DarkSlateGray * 0.3f);
                    }
                }
            }
        }

        private void DrawEquipBackground(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance?.Pixel;
            if (pixel == null)
            {
                return;
            }

            Point equipTopLeft = GetEquipAreaTopLeft();

            int equipCols = 6;
            int panelWidth = equipCols * INVENTORY_SQUARE_WIDTH + 16;
            int panelHeight = EquipRows * INVENTORY_SQUARE_WIDTH + 24;

            var panelRect = new Rectangle(
                equipTopLeft.X - 8,
                equipTopLeft.Y - 14,
                panelWidth,
                panelHeight);

            spriteBatch.Draw(pixel, panelRect, new Color(8, 8, 8, 180));
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 1), Color.Black * 0.6f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width, 1), Color.Black * 0.6f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, 1, panelRect.Height), Color.Black * 0.6f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - 1, panelRect.Y, 1, panelRect.Height), Color.Black * 0.6f);

            foreach (var kv in s_equipLayout)
            {
                var cell = kv.Value;
                Rectangle slotRect = new(
                    equipTopLeft.X + cell.X * INVENTORY_SQUARE_WIDTH,
                    equipTopLeft.Y + cell.Y * INVENTORY_SQUARE_HEIGHT,
                    INVENTORY_SQUARE_WIDTH,
                    INVENTORY_SQUARE_HEIGHT);

                if (_slotTexture != null)
                {
                    spriteBatch.Draw(_slotTexture, slotRect, SlotSourceRect, Color.White);
                }
                else
                {
                    spriteBatch.Draw(pixel, slotRect, Color.DarkSlateGray * 0.3f);
                    spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Y, slotRect.Width, 1), Color.Gray * 0.8f);
                    spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Bottom - 1, slotRect.Width, 1), Color.Gray * 0.8f);
                    spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Y, 1, slotRect.Height), Color.Gray * 0.8f);
                    spriteBatch.Draw(pixel, new Rectangle(slotRect.Right - 1, slotRect.Y, 1, slotRect.Height), Color.Gray * 0.8f);
                }
            }
        }

        private void RefreshInventoryContent()
        {
            if (_networkManager == null)
            {
                return;
            }

            _items.Clear();
            _itemGrid = new InventoryItem[Columns, Rows];
            _equippedItems.Clear();
            _bmdPreviewCache.Clear();

            var characterItems = _network_manager_getitems();
            const string defaultItemIconTexturePath = "Interface/newui_item_box.tga";

            foreach (var entry in characterItems.Where(e => e.Key <= 11))
            {
                byte slotIndex = entry.Key;
                byte[] itemData = entry.Value;

                ItemDefinition itemDef = ItemDatabase.GetItemDefinition(itemData)
                    ?? new ItemDefinition(0, ItemDatabase.GetItemName(itemData) ?? "Unknown Item", 1, 1, defaultItemIconTexturePath);

                var invItem = new InventoryItem(itemDef, Point.Zero, itemData);
                if (itemData.Length > 2)
                {
                    invItem.Durability = itemData[2];
                }

                _equippedItems[slotIndex] = invItem;
            }

            foreach (var entry in characterItems.Where(e => e.Key >= InventorySlotOffsetConstant))
            {
                byte slotIndex = entry.Key;
                byte[] itemData = entry.Value;

                int adjustedIndex = slotIndex - InventorySlotOffsetConstant;
                if (adjustedIndex < 0)
                {
                    _logger?.LogWarning("SlotIndex {SlotIndex} is below inventory offset. Skipping.", slotIndex);
                    continue;
                }

                int gridX = adjustedIndex % Columns;
                int gridY = adjustedIndex / Columns;

                if (gridX >= Columns || gridY >= Rows)
                {
                    string itemName = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
                    _logger?.LogWarning("Item at slot {SlotIndex} ({ItemName}) has invalid grid position ({GridX},{GridY}). Skipping.", slotIndex, itemName, gridX, gridY);
                    continue;
                }

                string itemNameFinal = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
                ItemDefinition itemDef = ItemDatabase.GetItemDefinition(itemData);
                if (itemDef == null)
                {
                    itemDef = new ItemDefinition(0, itemNameFinal, 1, 1, defaultItemIconTexturePath);
                }

                InventoryItem newItem = new(itemDef, new Point(gridX, gridY), itemData);

                if (itemData.Length > 2)
                {
                    newItem.Durability = itemData[2];
                }

                if (!AddItem(newItem))
                {
                    _logger?.LogWarning("Failed to add item '{ItemName}' to inventory UI at slot {SlotIndex}. Slot might be occupied unexpectedly.", itemNameFinal, slotIndex);
                }
            }

            var preloadTasks = new List<Task>();
            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                {
                    preloadTasks.Add(TextureLoader.Instance.Prepare(item.Definition.TexturePath));
                }
            }

            if (preloadTasks.Count > 0)
            {
                _ = Task.WhenAll(preloadTasks);
            }

            InvalidateStaticSurface();
        }

        private Dictionary<byte, byte[]> _network_manager_getitems()
        {
            return new Dictionary<byte, byte[]>(_networkManager.GetCharacterState().GetInventoryItems());
        }

        private void HandleInventoryInteraction(Point mousePos, bool leftJustPressed, bool leftJustReleased)
        {
            Point gridSlot = GetSlotAtScreenPosition(mousePos);
            _hoveredSlot = gridSlot;

            if (gridSlot.X != -1)
            {
                _hoveredItem = _itemGrid[gridSlot.X, gridSlot.Y];

                if (leftJustPressed)
                {
                    if (_pickedItemRenderer.Item != null)
                    {
                        if (CanPlaceItem(_pickedItemRenderer.Item, gridSlot))
                        {
                            InventoryItem itemToPlace = _pickedItemRenderer.Item;
                            if (_pickedItemOriginalGrid.X >= 0 && gridSlot == _pickedItemOriginalGrid)
                            {
                                itemToPlace.GridPosition = gridSlot;
                                AddItem(itemToPlace);
                                _pickedItemRenderer.ReleaseItem();
                                _pickedItemOriginalGrid = new Point(-1, -1);
                                return;
                            }

                            byte fromSlot = 0;
                            if (_pickedItemOriginalGrid.X >= 0)
                            {
                                fromSlot = (byte)(InventorySlotOffsetConstant + (_pickedItemOriginalGrid.Y * Columns) + _pickedItemOriginalGrid.X);
                            }
                            else if (_pickedFromEquipSlot >= 0)
                            {
                                fromSlot = (byte)_pickedFromEquipSlot;
                            }

                            byte toSlot = (byte)(InventorySlotOffsetConstant + (gridSlot.Y * Columns) + gridSlot.X);

                            itemToPlace.GridPosition = gridSlot;
                            AddItem(itemToPlace);

                            if (_networkManager != null)
                            {
                                var svc = _networkManager.GetCharacterService();
                                var version = _networkManager.TargetVersion;
                                var raw = itemToPlace.RawData ?? Array.Empty<byte>();
                                var state = _networkManager.GetCharacterState();
                                state.StashPendingInventoryMove(fromSlot, toSlot);
                                _ = Task.Run(async () =>
                                {
                                    await svc.SendItemMoveRequestAsync(fromSlot, toSlot, version, raw);
                                    await Task.Delay(1200);
                                    if (_networkManager != null && state.IsInventoryMovePending(fromSlot, toSlot))
                                    {
                                        MuGame.ScheduleOnMainThread(() => state.RaiseInventoryChanged());
                                    }
                                });
                            }

                            _pickedItemRenderer.ReleaseItem();
                            _pickedItemOriginalGrid = new Point(-1, -1);
                            _pickedFromEquipSlot = -1;
                        }
                    }
                    else if (_hoveredItem != null)
                    {
                        _pickedItemRenderer.PickUpItem(_hoveredItem);
                        _pickedItemOriginalGrid = _hoveredItem.GridPosition;
                        RemoveItemFromGrid(_hoveredItem);
                        _items.Remove(_hoveredItem);
                        _hoveredItem = null;
                        _pickedFromEquipSlot = -1;
                    }
                }

                bool rightJustPressed = MuGame.Instance.UiMouseState.RightButton == ButtonState.Pressed &&
                                        MuGame.Instance.PrevUiMouseState.RightButton == ButtonState.Released;

                if (rightJustPressed && _hoveredItem != null && _pickedItemRenderer.Item == null)
                {
                    if (_hoveredItem.Definition?.IsConsumable() == true)
                    {
                        string itemName = _hoveredItem.Definition?.Name?.ToLowerInvariant() ?? string.Empty;
                        if (itemName.Contains("apple"))
                        {
                            SoundController.Instance.PlayBuffer("Sound/pEatApple.wav");
                        }
                        else
                        {
                            SoundController.Instance.PlayBuffer("Sound/pDrink.wav");
                        }

                        byte itemSlot = (byte)(InventorySlotOffsetConstant + (_hoveredItem.GridPosition.Y * Columns) + _hoveredItem.GridPosition.X);

                        if (_networkManager != null)
                        {
                            var svc = _networkManager.GetCharacterService();
                            _ = Task.Run(async () =>
                            {
                                await svc.SendConsumeItemRequestAsync(itemSlot);
                                await Task.Delay(300);

                                var state = _networkManager.GetCharacterState();
                                MuGame.ScheduleOnMainThread(() => state.RaiseInventoryChanged());
                            });
                        }
                    }
                }
            }
            else if (_hoveredEquipSlot >= 0)
            {
                if (leftJustPressed)
                {
                    if (_pickedItemRenderer.Item != null)
                    {
                        var itemToPlace = _pickedItemRenderer.Item;

                        byte fromSlot = 0;
                        if (_pickedItemOriginalGrid.X >= 0)
                        {
                            fromSlot = (byte)(InventorySlotOffsetConstant + (_pickedItemOriginalGrid.Y * Columns) + _pickedItemOriginalGrid.X);
                        }
                        else if (_pickedFromEquipSlot >= 0)
                        {
                            fromSlot = (byte)_pickedFromEquipSlot;
                        }

                        byte toSlot = (byte)_hoveredEquipSlot;

                        _equippedItems[toSlot] = itemToPlace;

                        if (_networkManager != null)
                        {
                            var svc = _networkManager.GetCharacterService();
                            var version = _networkManager.TargetVersion;
                            var raw = itemToPlace.RawData ?? Array.Empty<byte>();
                            var state = _networkManager.GetCharacterState();
                            state.StashPendingInventoryMove(fromSlot, toSlot);
                            _ = Task.Run(async () =>
                            {
                                await svc.SendItemMoveRequestAsync(fromSlot, toSlot, version, raw);
                                await Task.Delay(1200);
                                if (_networkManager != null && state.IsInventoryMovePending(fromSlot, toSlot))
                                {
                                    MuGame.ScheduleOnMainThread(() => state.RaiseInventoryChanged());
                                }
                            });
                        }

                        _pickedItemRenderer.ReleaseItem();
                        _pickedItemOriginalGrid = new Point(-1, -1);
                        _pickedFromEquipSlot = -1;
                    }
                    else
                    {
                        if (_equippedItems.TryGetValue((byte)_hoveredEquipSlot, out var eqItem))
                        {
                            _pickedItemRenderer.PickUpItem(eqItem);
                            _equippedItems.Remove((byte)_hoveredEquipSlot);
                            _pickedFromEquipSlot = _hoveredEquipSlot;
                            _pickedItemOriginalGrid = new Point(-1, -1);
                        }
                    }
                }
            }
        }

        private void HandleDropOutsideInventory()
        {
            var item = _pickedItemRenderer.Item;
            if (item == null)
            {
                return;
            }

            byte slotIndex = (byte)(InventorySlotOffsetConstant + (item.GridPosition.Y * Columns) + item.GridPosition.X);

            var shop = Game.NpcShopControl.Instance;
            if (shop != null && shop.Visible && shop.DisplayRectangle.Contains(MuGame.Instance.UiMouseState.Position))
            {
                var itemToSell = _pickedItem_renderer_item();
                var originalGrid = _pickedItemOriginalGrid;
                int fromEquipSlot = _pickedFromEquipSlot;

                _pickedItemRenderer.ReleaseItem();
                _pickedItemOriginalGrid = new Point(-1, -1);
                _pickedFromEquipSlot = -1;

                ShowSellConfirmation(itemToSell, slotIndex, originalGrid, fromEquipSlot);
            }
            else if (Game.VaultControl.Instance is { } vault &&
                     vault.Visible &&
                     vault.DisplayRectangle.Contains(MuGame.Instance.UiMouseState.Position) &&
                     _network_manager_exists())
            {
                var drop = vault.GetSlotAtScreenPosition(MuGame.Instance.UiMouseState.Position);
                if (drop.X >= 0 && vault.CanPlaceAt(drop, item))
                {
                    byte toSlot = (byte)(drop.Y * 8 + drop.X);
                    var svc = _networkManager.GetCharacterService();
                    var raw = item.RawData ?? Array.Empty<byte>();
                    var state = _networkManager.GetCharacterState();
                    state.StashPendingInventoryMove(slotIndex, slotIndex);

                    _ = Task.Run(async () =>
                    {
                        await svc.SendStorageItemMoveAsync(ItemStorageKind.Inventory, slotIndex, ItemStorageKind.Vault, toSlot, _networkManager.TargetVersion, raw);
                        await Task.Delay(1200);
                        if (_networkManager != null && state.IsInventoryMovePending(slotIndex, slotIndex))
                        {
                            MuGame.ScheduleOnMainThread(() =>
                            {
                                state.RaiseInventoryChanged();
                                state.RaiseVaultItemsChanged();
                            });
                        }
                    });

                    _pickedItemRenderer.ReleaseItem();
                    _pickedItemOriginalGrid = new Point(-1, -1);
                }
                else
                {
                    AddItem(item);
                    _networkManager?.GetCharacterState()?.RaiseInventoryChanged();
                    _pickedItemRenderer.ReleaseItem();
                    _pickedItemOriginalGrid = new Point(-1, -1);
                }
            }
            else if (Scene?.World is Controls.WalkableWorldControl world && _network_manager_exists())
            {
                byte tileX = world.MouseTileX;
                byte tileY = world.MouseTileY;

                _ = Task.Run(async () =>
                {
                    var svc = _networkManager.GetCharacterService();
                    await svc.SendDropItemRequestAsync(tileX, tileY, slotIndex);
                    await Task.Delay(1200);
                    var state = _networkManager.GetCharacterState();
                    if (state.HasInventoryItem(slotIndex))
                    {
                        MuGame.ScheduleOnMainThread(() => state.RaiseInventoryChanged());
                    }
                });

                _pickedItemRenderer.ReleaseItem();
                _pickedItemOriginalGrid = new Point(-1, -1);
            }
            else
            {
                AddItem(item);
                _pickedItemRenderer.ReleaseItem();
                _pickedItemOriginalGrid = new Point(-1, -1);
            }
        }

        private InventoryItem _pickedItem_renderer_item() => _pickedItemRenderer.Item;

        private bool _network_manager_exists() => _networkManager != null;

        private void PlaceItemOnGrid(InventoryItem item)
        {
            if (item?.Definition == null)
            {
                return;
            }

            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gridX = item.GridPosition.X + x;
                    int gridY = item.GridPosition.Y + y;

                    if (gridX < Columns && gridY < Rows)
                    {
                        _itemGrid[gridX, gridY] = item;
                    }
                }
            }
        }

        private void RemoveItemFromGrid(InventoryItem item)
        {
            if (item?.Definition == null)
            {
                return;
            }

            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gridX = item.GridPosition.X + x;
                    int gridY = item.GridPosition.Y + y;

                    if (gridX < Columns && gridY < Rows)
                    {
                        _itemGrid[gridX, gridY] = null;
                    }
                }
            }
        }

        private bool CanPlaceItem(InventoryItem itemToPlace, Point targetSlot)
        {
            if (itemToPlace == null || itemToPlace.Definition == null)
            {
                return false;
            }

            if (targetSlot.X < 0 || targetSlot.Y < 0 ||
                targetSlot.X + itemToPlace.Definition.Width > Columns ||
                targetSlot.Y + itemToPlace.Definition.Height > Rows)
            {
                return false;
            }

            for (int y = 0; y < itemToPlace.Definition.Height; y++)
            {
                for (int x = 0; x < itemToPlace.Definition.Width; x++)
                {
                    int checkX = targetSlot.X + x;
                    int checkY = targetSlot.Y + y;

                    if (checkX >= Columns || checkY >= Rows)
                    {
                        return false;
                    }

                    if (_itemGrid[checkX, checkY] != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool TryFindFirstFreeSlot(InventoryItem item, out Point slot)
        {
            slot = new Point(-1, -1);
            if (item?.Definition == null)
            {
                return false;
            }

            for (int y = 0; y <= Rows - item.Definition.Height; y++)
            {
                for (int x = 0; x <= Columns - item.Definition.Width; x++)
                {
                    var candidate = new Point(x, y);
                    if (CanPlaceItem(item, candidate))
                    {
                        slot = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string BuildItemDisplayName(InventoryItem item)
        {
            if (item == null)
            {
                return "item";
            }

            string name = item.Definition?.Name ?? ItemDatabase.GetItemName(item.RawData) ?? "item";
            if (item.Details.Level > 0)
            {
                name += $" +{item.Details.Level}";
            }

            if (item.Definition?.BaseDurability == 0 && item.Durability > 1)
            {
                name += $" x{item.Durability}";
            }

            return name;
        }

        private void ShowSellConfirmation(InventoryItem item, byte slotIndex, Point originalGrid, int fromEquipSlot)
        {
            if (item == null)
            {
                return;
            }

            if (_networkManager == null)
            {
                MessageWindow.Show("No connection to server. Sale is not possible.");
                RestoreItemAfterCancelledSell(item, originalGrid, fromEquipSlot);
                return;
            }

            var definition = item.Definition;
            if (definition == null)
            {
                MessageWindow.Show("Cannot identify the selected item.");
                RestoreItemAfterCancelledSell(item, originalGrid, fromEquipSlot);
                return;
            }

            string displayName = BuildItemDisplayName(item);

            if (!definition.CanSellToNpc)
            {
                MessageWindow.Show($"Item '{displayName}' cannot be sold to NPC shop.");
                RestoreItemAfterCancelledSell(item, originalGrid, fromEquipSlot);
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Sell {displayName}?");
            if (definition.IsExpensive)
            {
                builder.AppendLine();
                builder.AppendLine("WARNING: This item is marked as expensive.");
            }

            RequestDialog.Show(
                builder.ToString(),
                onAccept: () => ExecuteSellToNpc(slotIndex),
                onReject: () => RestoreItemAfterCancelledSell(item, originalGrid, fromEquipSlot),
                acceptText: "Sell",
                rejectText: "Cancel");
        }

        private void ExecuteSellToNpc(byte slotIndex)
        {
            if (_networkManager == null)
            {
                MessageWindow.Show("No connection to server. Sale is not possible.");
                return;
            }

            var svc = _networkManager.GetCharacterService();
            if (svc == null)
            {
                MessageWindow.Show("Failed to connect to NPC shop server.");
                return;
            }

            var state = _networkManager.GetCharacterState();
            state.StashPendingSellSlot(slotIndex);

            _ = Task.Run(async () =>
            {
                try
                {
                    await svc.SendSellItemToNpcRequestAsync(slotIndex);
                    await Task.Delay(1200);

                    var refreshedState = _networkManager?.GetCharacterState();
                    if (refreshedState != null && refreshedState.HasInventoryItem(slotIndex))
                    {
                        MuGame.ScheduleOnMainThread(refreshedState.RaiseInventoryChanged);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error while sending item sale request from slot {Slot}.", slotIndex);
                    var refreshedState = _networkManager?.GetCharacterState();
                    if (refreshedState != null)
                    {
                        MuGame.ScheduleOnMainThread(refreshedState.RaiseInventoryChanged);
                    }

                    MuGame.ScheduleOnMainThread(() => MessageWindow.Show("Failed to sell item. Please try again."));
                }
            });
        }

        private void RestoreItemAfterCancelledSell(InventoryItem item, Point originalGrid, int fromEquipSlot)
        {
            if (item == null)
            {
                return;
            }

            if (fromEquipSlot >= 0)
            {
                _equippedItems[(byte)fromEquipSlot] = item;
                _networkManager?.GetCharacterState()?.RaiseEquipmentChanged();
                return;
            }

            Point targetSlot = originalGrid;
            if (targetSlot.X < 0 || targetSlot.Y < 0 || !CanPlaceItem(item, targetSlot))
            {
                if (!TryFindFirstFreeSlot(item, out targetSlot))
                {
                    _logger?.LogWarning("No free space to restore item '{Name}' in inventory.", item.Definition?.Name ?? "Unknown");
                    MessageWindow.Show("No space in inventory to restore item.");
                    return;
                }
            }

            item.GridPosition = targetSlot;
            if (!AddItem(item))
            {
                _logger?.LogWarning("Failed to restore item '{Name}' to inventory.", item.Definition?.Name ?? "Unknown");
                MessageWindow.Show("Restoring item to inventory failed.");
            }
        }

        private void DrawInventoryItems(SpriteBatch spriteBatch)
        {
            if (GraphicsManager.Instance?.Pixel == null || GraphicsManager.Instance?.Font == null)
            {
                return;
            }

            Point gridTopLeft = new(DisplayRectangle.X + _gridOffset.X, DisplayRectangle.Y + _gridOffset.Y);
            var font = GraphicsManager.Instance.Font;

            foreach (var item in _items.ToList())
            {
                if (item == _pickedItem_renderer_item())
                {
                    continue;
                }

                Rectangle itemRect = new(
                    gridTopLeft.X + item.GridPosition.X * INVENTORY_SQUARE_WIDTH,
                    gridTopLeft.Y + item.GridPosition.Y * INVENTORY_SQUARE_HEIGHT,
                    item.Definition.Width * INVENTORY_SQUARE_WIDTH,
                    item.Definition.Height * INVENTORY_SQUARE_HEIGHT);

                Texture2D itemTexture = ResolveItemTexture(item, itemRect.Width, itemRect.Height);

                if (itemTexture != null)
                {
                    spriteBatch.Draw(itemTexture, itemRect, Color.White);
                }
                else
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, itemRect, Color.DarkSlateGray);
                }

                if (item.Definition.BaseDurability == 0 && item.Durability > 1)
                {
                    DrawStackCount(spriteBatch, font, itemRect, item.Durability.ToString());
                }
            }
        }

        private void DrawEquippedItems(SpriteBatch spriteBatch)
        {
            foreach (var kv in _equippedItems)
            {
                if (!s_equipLayout.TryGetValue(kv.Key, out var cell))
                {
                    continue;
                }

                var item = kv.Value;
                Rectangle itemRect = new(
                    GetEquipAreaTopLeft().X + cell.X * INVENTORY_SQUARE_WIDTH,
                    GetEquipAreaTopLeft().Y + cell.Y * INVENTORY_SQUARE_WIDTH,
                    INVENTORY_SQUARE_WIDTH,
                    INVENTORY_SQUARE_WIDTH);

                Texture2D itemTexture = ResolveItemTexture(item, itemRect.Width, itemRect.Height);

                if (itemTexture != null)
                {
                    spriteBatch.Draw(itemTexture, itemRect, Color.White);
                }
                else if (GraphicsManager.Instance?.Pixel != null)
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, itemRect, new Color(40, 40, 40, 200));
                }
            }
        }

        private Texture2D ResolveItemTexture(InventoryItem item, int width, int height)
        {
            if (item == null)
            {
                return null;
            }

            string texturePath = item.Definition?.TexturePath;
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

            bool isHovered = item == _hoveredItem;

            if (!isHovered && Constants.ENABLE_ITEM_MATERIAL_ANIMATION)
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
                    // ignore and fall back below
                }
            }

            if (isHovered)
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

            var cacheKey = (item, width, height, false);
            if (_bmdPreviewCache.TryGetValue(cacheKey, out var previewTexture) && previewTexture != null)
            {
                return previewTexture;
            }

            try
            {
                previewTexture = BmdPreviewRenderer.GetPreview(item, width, height);
                if (previewTexture != null)
                {
                    _bmdPreviewCache[cacheKey] = previewTexture;
                }
                return previewTexture;
            }
            catch
            {
                return null;
            }
        }

        private void DrawGridOverlays(SpriteBatch spriteBatch)
        {
            if (GraphicsManager.Instance?.Pixel == null)
            {
                return;
            }

            Point gridTopLeft = new(DisplayRectangle.X + _gridOffset.X, DisplayRectangle.Y + _gridOffset.Y);
            var dragged = _pickedItem_renderer_item() ?? Game.VaultControl.Instance?.GetDraggedItem();

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    Rectangle slotRect = new(
                        gridTopLeft.X + x * INVENTORY_SQUARE_WIDTH,
                        gridTopLeft.Y + y * INVENTORY_SQUARE_WIDTH,
                        INVENTORY_SQUARE_WIDTH,
                        INVENTORY_SQUARE_WIDTH);

                    if (dragged != null && IsMouseOverGrid())
                    {
                        var highlight = GetSlotHighlightColor(new Point(x, y), dragged);
                        if (highlight.HasValue)
                        {
                            spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, highlight.Value);
                        }
                    }
                    else if (IsMouseOverGrid() && dragged == null)
                    {
                        if (_hoveredSlot.X == x && _hoveredSlot.Y == y)
                        {
                            spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, Color.Yellow * 0.3f);
                        }
                        else if (_hoveredItem != null && IsSlotOccupiedByItem(new Point(x, y), _hoveredItem))
                        {
                            spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, Color.CornflowerBlue * 0.35f);
                        }
                    }
                }
            }
        }

        private void DrawTexts(SpriteBatch spriteBatch)
        {
            if (_font == null)
            {
                return;
            }

            Vector2 basePosition = DisplayRectangle.Location.ToVector2();
            foreach (var entry in _texts)
            {
                if (!entry.Visible || string.IsNullOrEmpty(entry.Text))
                {
                    continue;
                }

                float textScale = entry.FontScale * Scale;
                Vector2 pos = basePosition + entry.BasePosition * Scale;
                Vector2 size = _font.MeasureString(entry.Text) * textScale;

                switch (entry.Alignment)
                {
                    case TextAlignment.Center:
                        pos.X -= size.X * 0.5f;
                        break;
                    case TextAlignment.Right:
                        pos.X -= size.X;
                        break;
                }

                spriteBatch.DrawString(_font, entry.Text, pos, entry.Color * Alpha, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (_pickedItem_renderer_item() != null || _hoveredItem == null || _font == null)
            {
                return;
            }

            var lines = BuildTooltipLines(_hoveredItem);
            const float scale = 0.5f;

            int width = 0;
            int height = 0;
            foreach (var (text, _) in lines)
            {
                Vector2 sz = _font.MeasureString(text) * scale;
                width = Math.Max(width, (int)sz.X);
                height += (int)sz.Y + 2;
            }
            width += 12;
            height += 8;

            Point mousePosition = MuGame.Instance.UiMouseState.Position;

            Point gridTopLeft = new(DisplayRectangle.X + _gridOffset.X, DisplayRectangle.Y + _gridOffset.Y);
            Rectangle hoveredItemRect = new(
                gridTopLeft.X + _hoveredItem.GridPosition.X * INVENTORY_SQUARE_WIDTH,
                gridTopLeft.Y + _hoveredItem.GridPosition.Y * INVENTORY_SQUARE_WIDTH,
                _hoveredItem.Definition.Width * INVENTORY_SQUARE_WIDTH,
                _hoveredItem.Definition.Height * INVENTORY_SQUARE_WIDTH);

            Rectangle tooltipRect = new(mousePosition.X + 15, mousePosition.Y + 15, width, height);
            Rectangle screenBounds = new(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y);

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

            var pixel = GraphicsManager.Instance?.Pixel;
            if (pixel == null)
            {
                return;
            }

            spriteBatch.Draw(pixel, tooltipRect, Color.Black * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, tooltipRect.Width, 1), Color.White);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Bottom - 1, tooltipRect.Width, 1), Color.White);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, 1, tooltipRect.Height), Color.White);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.Right - 1, tooltipRect.Y, 1, tooltipRect.Height), Color.White);

            int y = tooltipRect.Y + 4;
            foreach (var (text, color) in lines)
            {
                Vector2 size = _font.MeasureString(text) * scale;
                spriteBatch.DrawString(_font, text, new Vector2(tooltipRect.X + (tooltipRect.Width - size.X) / 2, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += (int)size.Y + 2;
            }
        }

        private static List<(string txt, Color col)> BuildTooltipLines(InventoryItem item)
        {
            var details = item.Details;
            var result = new List<(string, Color)>();

            string name = details.IsExcellent ? $"Excellent {item.Definition.Name}"
                        : details.IsAncient ? $"Ancient {item.Definition.Name}"
                        : item.Definition.Name;

            if (details.Level > 0)
            {
                name += $" +{details.Level}";
            }

            result.Add((name, Color.White));

            var def = item.Definition;
            if (def.DamageMin > 0 || def.DamageMax > 0)
            {
                string dmgType = def.TwoHanded ? "Two-hand" : "One-hand";
                result.Add(($"{dmgType} Damage : {def.DamageMin} ~ {def.DamageMax}", Color.Orange));
            }
            if (def.Defense > 0)
            {
                result.Add(("Defense     : " + def.Defense, Color.Orange));
            }
            if (def.DefenseRate > 0)
            {
                result.Add(("Defense Rate: " + def.DefenseRate, Color.Orange));
            }
            if (def.AttackSpeed > 0)
            {
                result.Add(("Attack Speed: " + def.AttackSpeed, Color.Orange));
            }

            result.Add(($"Durability : {item.Durability}/{def.BaseDurability}", Color.Silver));

            if (def.RequiredLevel > 0) result.Add(($"Required Level   : {def.RequiredLevel}", Color.LightGray));
            if (def.RequiredStrength > 0) result.Add(($"Required Strength: {def.RequiredStrength}", Color.LightGray));
            if (def.RequiredDexterity > 0) result.Add(($"Required Agility : {def.RequiredDexterity}", Color.LightGray));
            if (def.RequiredEnergy > 0) result.Add(($"Required Energy  : {def.RequiredEnergy}", Color.LightGray));

            if (def.AllowedClasses != null)
            {
                foreach (var cls in def.AllowedClasses)
                {
                    result.Add(($"Can be equipped by {cls}", Color.LightGray));
                }
            }

            if (details.OptionLevel > 0)
            {
                result.Add(($"Additional Option : +{details.OptionLevel * 4}", new Color(80, 255, 80)));
            }

            if (details.HasLuck) result.Add(("+Luck  (Crit +5 %, Jewel +25 %)", Color.CornflowerBlue));
            if (details.HasSkill) result.Add(("+Skill (Right mouse click - skill)", Color.CornflowerBlue));

            if (details.IsExcellent)
            {
                byte excByte = item.RawData.Length > 3 ? item.RawData[3] : (byte)0;
                foreach (var option in ItemDatabase.ParseExcellentOptions(excByte))
                {
                    result.Add(($"+{option}", new Color(128, 255, 128)));
                }
            }

            if (details.IsAncient)
            {
                result.Add(("Ancient Option", new Color(0, 255, 128)));
            }

            if (def.IsConsumable())
            {
                result.Add(("Right-click to use", new Color(255, 215, 0)));
            }

            return result;
        }

        private static void DrawStackCount(SpriteBatch spriteBatch, SpriteFont font, Rectangle itemRect, string quantityText)
        {
            const float textScale = 0.4f;

            Vector2 textSize = font.MeasureString(quantityText);
            Vector2 scaledSize = textSize * textScale;
            Vector2 textPosition = new(itemRect.Right - scaledSize.X - 2, itemRect.Y + 2);

            Color outlineColor = Color.Black;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    spriteBatch.DrawString(font, quantityText, textPosition + new Vector2(dx, dy), outlineColor, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.DrawString(font, quantityText, textPosition, new Color(255, 255, 180), 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        }

        private Color? GetSlotHighlightColor(Point slot, InventoryItem draggedItem)
        {
            if (draggedItem == null || _hoveredSlot.X == -1 || _hoveredSlot.Y == -1)
            {
                return null;
            }

            if (!IsSlotInDropArea(slot, _hoveredSlot, draggedItem))
            {
                return null;
            }

            return CanPlaceItem(draggedItem, _hoveredSlot)
                ? Color.GreenYellow * 0.5f
                : Color.Red * 0.6f;
        }

        private static bool IsSlotInDropArea(Point slot, Point dropPosition, InventoryItem item)
        {
            return slot.X >= dropPosition.X &&
                   slot.X < dropPosition.X + item.Definition.Width &&
                   slot.Y >= dropPosition.Y &&
                   slot.Y < dropPosition.Y + item.Definition.Height;
        }

        private bool IsSlotOccupiedByItem(Point slot, InventoryItem item)
        {
            if (slot.X < 0 || slot.Y < 0 || slot.X >= Columns || slot.Y >= Rows || item?.Definition == null)
            {
                return false;
            }

            return slot.X >= item.GridPosition.X &&
                   slot.X < item.GridPosition.X + item.Definition.Width &&
                   slot.Y >= item.GridPosition.Y &&
                   slot.Y < item.GridPosition.Y + item.Definition.Height;
        }

        private bool IsMouseOverGrid()
        {
            Point mousePos = MuGame.Instance.UiMouseState.Position;
            Rectangle gridScreenRect = new(
                DisplayRectangle.X + _gridOffset.X,
                DisplayRectangle.Y + _gridOffset.Y,
                Columns * INVENTORY_SQUARE_WIDTH,
                Rows * INVENTORY_SQUARE_WIDTH);

            return gridScreenRect.Contains(mousePos);
        }

        private bool IsMouseOverDragArea()
        {
            Point mousePos = MuGame.Instance.UiMouseState.Position;
            int dragAreaHeight = DisplayRectangle.Height / 10;
            Rectangle dragRect = new(
                DisplayRectangle.X,
                DisplayRectangle.Y,
                DisplayRectangle.Width,
                dragAreaHeight);

            return dragRect.Contains(mousePos);
        }

        private Point GetSlotAtScreenPosition(Point screenPos)
        {
            if (DisplayRectangle.Width <= 0 || DisplayRectangle.Height <= 0)
            {
                return new Point(-1, -1);
            }

            Point localPos = new(
                screenPos.X - DisplayRectangle.X - _gridOffset.X,
                screenPos.Y - DisplayRectangle.Y - _gridOffset.Y);

            if (localPos.X < 0 || localPos.Y < 0 ||
                localPos.X >= Columns * INVENTORY_SQUARE_WIDTH ||
                localPos.Y >= Rows * INVENTORY_SQUARE_WIDTH)
            {
                return new Point(-1, -1);
            }

            return new Point(
                Math.Min(Columns - 1, localPos.X / INVENTORY_SQUARE_WIDTH),
                Math.Min(Rows - 1, localPos.Y / INVENTORY_SQUARE_WIDTH));
        }

        private Point GetEquipAreaTopLeft()
        {
            int gridBottomY = DisplayRectangle.Y + _gridOffset.Y + Rows * INVENTORY_SQUARE_WIDTH;
            return new Point(DisplayRectangle.X + _gridOffset.X, gridBottomY + 20);
        }

        private int GetEquipSlotAtScreenPosition(Point screenPos)
        {
            Point equipTopLeft = GetEquipAreaTopLeft();
            foreach (var kv in s_equipLayout)
            {
                var cell = kv.Value;
                var slotRect = new Rectangle(
                    equipTopLeft.X + cell.X * INVENTORY_SQUARE_WIDTH,
                    equipTopLeft.Y + cell.Y * INVENTORY_SQUARE_WIDTH,
                    INVENTORY_SQUARE_WIDTH,
                    INVENTORY_SQUARE_WIDTH);
                if (slotRect.Contains(screenPos))
                    return kv.Key;
            }
            return -1;
        }
    }
}
