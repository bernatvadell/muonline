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

        private const int WINDOW_WIDTH = 420;
        private const int WINDOW_HEIGHT = 690;

        private const int HEADER_HEIGHT = 70;
        private const int FOOTER_HEIGHT = 48;
        private const int PAPERDOLL_TOP = HEADER_HEIGHT;
        private const int PAPERDOLL_PADDING = 6;
        private const int COLUMN_SPACING = 24;
        private const int BEAM_HEIGHT = 8;
        private const int BEAM_SPACING = 4;
        private const int GRID_TOP_SPACING = 4;
        private const int FOOTER_SPACING = 5;
        private const int FOOTER_BUTTON_SIZE = 42;

        public const int INVENTORY_SQUARE_WIDTH = 35;
        public const int INVENTORY_SQUARE_HEIGHT = 35;

        public const int Columns = 8;
        public const int Rows = 8;
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

        private sealed class EquipSlotLayout
        {
            public EquipSlotLayout(byte slot, Rectangle rect, Point size, string label, bool accentRed = false)
            {
                Slot = slot;
                Rect = rect;
                Size = size;
                Label = label;
                AccentRed = accentRed;
            }

            public byte Slot { get; }
            public Rectangle Rect { get; }
            public Point Size { get; }
            public string Label { get; }
            public bool AccentRed { get; }
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
        private Texture2D _circleTexture;
        private Texture2D _coinTexture;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private readonly List<InventoryTextEntry> _texts = new();
        private InventoryTextEntry _titleText;
        private InventoryTextEntry _subtitleText;
        private InventoryTextEntry _zenText;

        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private readonly List<InventoryItem> _items = new();
        private readonly Dictionary<byte, InventoryItem> _equippedItems = new();
        private InventoryItem[,] _itemGrid;

        private readonly NetworkManager _networkManager;
        private readonly ILogger<InventoryControl> _logger;

        private SpriteFont _font;

        private Rectangle _headerRect;
        private Rectangle _paperdollPanelRect;
        private Rectangle _beamRect;
        private Rectangle _gridRect;
        private Rectangle _gridFrameRect;
        private Rectangle _footerRect;
        private Rectangle _zenFieldRect;
        private Rectangle _zenIconRect;
        private Rectangle _closeButtonRect;
        private Rectangle _footerLeftButtonRect;
        private Rectangle _footerRightButtonRect;

        private InventoryItem _hoveredItem;
        private Point _hoveredSlot = new(-1, -1);
        private int _hoveredEquipSlot = -1;
        private int _pickedFromEquipSlot = -1;
        private Point _pickedItemOriginalGrid = new(-1, -1);
        private Point _pickedAtMousePos;
        private bool _itemDragMoved;

        private bool _isDragging;
        private Point _dragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        private long _zenAmount;
        private GameTime _currentGameTime;
        private bool _closeHovered;
        private bool _leftFooterHovered;
        private bool _rightFooterHovered;

        public readonly PickedItemRenderer _pickedItemRenderer;

        private readonly List<LayoutInfo> _layoutInfos = new();
        private readonly Dictionary<string, TextureRectData> _textureRectLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<byte, EquipSlotLayout> _equipSlots = new();
        private Vector2 _layoutScale = Vector2.One;

        private static InventoryControl _instance;

        public InventoryControl(NetworkManager networkManager = null, ILoggerFactory loggerFactory = null)
        {
            _networkManager = networkManager;
            var factory = loggerFactory ?? MuGame.AppLoggerFactory;
            _logger = factory?.CreateLogger<InventoryControl>();

            LoadLayoutDefinitions();
            BuildLayoutMetrics();

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
            EnsurePrimitiveTextures();

            UpdateZenFromNetwork();
            UpdateZenText();
            InvalidateStaticSurface();
        }

        private void EnsurePrimitiveTextures()
        {
            var device = GraphicsManager.Instance?.GraphicsDevice;
            if (device == null)
            {
                return;
            }

            _circleTexture ??= CreateCircleTexture(device, 48, Color.White);
            _coinTexture ??= CreateCircleTexture(device, 18, Color.White);
        }

        private static Texture2D CreateCircleTexture(GraphicsDevice device, int diameter, Color color)
        {
            var texture = new Texture2D(device, diameter, diameter);
            var data = new Color[diameter * diameter];
            float radius = (diameter - 1) / 2f;
            var center = new Vector2(radius, radius);

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    data[y * diameter + x] = dist <= radius ? color : Color.Transparent;
                }
            }

            texture.SetData(data);
            return texture;
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
                ReleasePickedItem();
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
                Hide();
                return;
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
            UpdateChromeHover(mousePos);

            bool leftPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = !leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Pressed;

            if (leftJustPressed && HandleChromeClick())
            {
                return;
            }

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

            if (_hoveredItem == null && _hoveredEquipSlot >= 0 && _equippedItems.TryGetValue((byte)_hoveredEquipSlot, out var hoveredEquip))
            {
                _hoveredItem = hoveredEquip;
            }

            if (leftJustReleased && _pickedItemRenderer.Item != null && !_isDragging && !IsMouseOverGrid() && _itemDragMoved)
            {
                HandleDropOutsideInventory();
            }

            if (_pickedItemRenderer.Item != null && !_itemDragMoved)
            {
                var current = MuGame.Instance.UiMouseState.Position;
                if (Vector2.Distance(current.ToVector2(), _pickedAtMousePos.ToVector2()) > 2f)
                {
                    _itemDragMoved = true;
                }
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
                DrawEquipHighlights(spriteBatch);
                DrawGridOverlays(spriteBatch);
                DrawChrome(spriteBatch);
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
            _circleTexture?.Dispose();
            _circleTexture = null;
            _coinTexture?.Dispose();
            _coinTexture = null;
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            InvalidateStaticSurface();
        }

        private void InitializeTextEntries()
        {
            _texts.Clear();

            _titleText = CreateText(new Vector2(WINDOW_WIDTH / 2f, HEADER_HEIGHT * 0.5f - 6f), 14f, Color.White, TextAlignment.Center);
            _titleText.Text = "Inventory";

            _subtitleText = CreateText(new Vector2(WINDOW_WIDTH / 2f, HEADER_HEIGHT - 18f), 9.5f, new Color(170, 170, 170), TextAlignment.Center);
            _subtitleText.Text = "[Set option]   [Socket option]";

            _zenText = CreateText(new Vector2(_zenFieldRect.X + 8, _zenFieldRect.Y + _zenFieldRect.Height * 0.5f - 7f), 13f, new Color(235, 210, 120));
            _zenText.Visible = false;
        }

        private InventoryTextEntry CreateText(Vector2 basePosition, float fontSize, Color color, TextAlignment alignment = TextAlignment.Left)
        {
            float fontScale = fontSize / Constants.BASE_FONT_SIZE;
            var entry = new InventoryTextEntry(basePosition, fontScale, color, alignment);
            _texts.Add(entry);
            return entry;
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
                _zenText.Text = ZenAmount.ToString();
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

                if (_layoutInfos.Count == 0)
                {
                    _layoutInfos.Add(new LayoutInfo
                    {
                        Name = "Board",
                        ScreenX = 0,
                        ScreenY = 0,
                        Width = WINDOW_WIDTH,
                        Height = WINDOW_HEIGHT,
                        Z = 0
                    });
                }

                RecalculateLayoutScale();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load inventory layout definitions.");
            }
        }

        private void RecalculateLayoutScale()
        {
            // For inventory we want the JSON sizes to be used directly (no auto-scaling).
            _layoutScale = Vector2.One;
        }

        private void BuildLayoutMetrics()
        {
            BuildEquipSlots();

            _headerRect = new Rectangle(0, 0, WINDOW_WIDTH, HEADER_HEIGHT);

            var hasUnion = false;
            var union = new Rectangle();
            foreach (var slot in _equipSlots.Values)
            {
                if (!hasUnion)
                {
                    union = slot.Rect;
                    hasUnion = true;
                }
                else
                {
                    union = Rectangle.Union(union, slot.Rect);
                }
            }

            if (!hasUnion)
            {
                union = new Rectangle(PAPERDOLL_TOP, PAPERDOLL_TOP, INVENTORY_SQUARE_WIDTH * 4, INVENTORY_SQUARE_HEIGHT * 4);
            }

            union.Inflate(PAPERDOLL_PADDING, PAPERDOLL_PADDING);
            _paperdollPanelRect = union;

            _beamRect = Rectangle.Empty;

            int gridX = (WINDOW_WIDTH - Columns * INVENTORY_SQUARE_WIDTH) / 2;
            int gridY = _paperdollPanelRect.Bottom + GRID_TOP_SPACING;
            _gridRect = new Rectangle(gridX, gridY, Columns * INVENTORY_SQUARE_WIDTH, Rows * INVENTORY_SQUARE_HEIGHT);
            _gridFrameRect = new Rectangle(_gridRect.X - 12, _gridRect.Y - 12, _gridRect.Width + 24, _gridRect.Height + 24);

            _footerRect = new Rectangle(10, _gridRect.Bottom + FOOTER_SPACING, WINDOW_WIDTH - 20, FOOTER_HEIGHT);
            int buttonY = _footerRect.Bottom - FOOTER_BUTTON_SIZE - 4;
            _footerLeftButtonRect = new Rectangle(_footerRect.X + 6, buttonY, FOOTER_BUTTON_SIZE, FOOTER_BUTTON_SIZE);
            _footerRightButtonRect = new Rectangle(_footerRect.Right - FOOTER_BUTTON_SIZE - 6, buttonY, FOOTER_BUTTON_SIZE, FOOTER_BUTTON_SIZE);

            _zenIconRect = new Rectangle(_footerLeftButtonRect.Right + 10, _footerRect.Y + 10, 18, 18);
            int zenFieldX = _zenIconRect.Right + 6;
            int zenFieldRightLimit = _footerRightButtonRect.X - 16;
            int zenFieldWidth = Math.Max(110, zenFieldRightLimit - zenFieldX);
            _zenFieldRect = new Rectangle(zenFieldX, _footerRect.Y + 8, zenFieldWidth, FOOTER_BUTTON_SIZE - 6);

            _closeButtonRect = new Rectangle(_headerRect.Right - 34, _headerRect.Y + 10, 28, 28);
        }

        private void BuildEquipSlots()
        {
            _equipSlots.Clear();

            const byte PendantSlot = 9;
            const byte LeftRingSlot = 10;
            const byte RightRingSlot = 11;

            int cell = INVENTORY_SQUARE_WIDTH;
            int centerX = (WINDOW_WIDTH - cell * 2) / 2;
            int lateralOffset = COLUMN_SPACING + INVENTORY_SQUARE_WIDTH + 8;
            int leftX = centerX - (cell * 2 + lateralOffset);
            int rightX = centerX + cell * 2 + lateralOffset;
            int topY = PAPERDOLL_TOP;
            int ringOffset = 16;

            AddEquipSlot(8, new Point(leftX, topY), new Point(2, 2), "PET");
            int leftWeaponY = topY + cell * 2 + 4;
            AddEquipSlot(0, new Point(leftX, leftWeaponY), new Point(2, 3), "LEFT", accentRed: true);
            int glovesY = leftWeaponY + cell * 3 + 6;
            AddEquipSlot(5, new Point(leftX, glovesY), new Point(2, 2), "GLOVES");

            AddEquipSlot(2, new Point(centerX, topY - 2), new Point(2, 2), "HELM");
            int chestY = topY - 2 + cell * 2;
            int pendantY = chestY + cell / 2;
            // Pendant aligned horizontally with left ring
            AddEquipSlot(PendantSlot, new Point(centerX - (ringOffset + cell), pendantY), new Point(1, 1), "PEND");
            AddEquipSlot(3, new Point(centerX, chestY), new Point(2, 3), "CHEST");
            int pantsY = chestY + cell * 3 + 2;
            AddEquipSlot(4, new Point(centerX, pantsY), new Point(2, 2), "PANTS");
            int ringsY = pantsY + cell / 2;
            AddEquipSlot(LeftRingSlot, new Point(centerX - (ringOffset + cell), ringsY), new Point(1, 1), "RING");
            AddEquipSlot(RightRingSlot, new Point(centerX + cell * 2 + ringOffset, ringsY), new Point(1, 1), "RING");

            int wingsX = rightX - cell / 2;
            AddEquipSlot(7, new Point(wingsX, topY - 6), new Point(3, 2), "WINGS");
            int rightWeaponY = topY + cell * 2 + 4;
            AddEquipSlot(1, new Point(rightX, rightWeaponY), new Point(2, 3), "RIGHT");
            int bootsY = rightWeaponY + cell * 3 + 6;
            AddEquipSlot(6, new Point(rightX, bootsY), new Point(2, 2), "BOOTS");
        }

        private void AddEquipSlot(byte slot, Point origin, Point size, string ghostLabel, bool accentRed = false)
        {
            var rect = new Rectangle(origin.X, origin.Y, size.X * INVENTORY_SQUARE_WIDTH, size.Y * INVENTORY_SQUARE_HEIGHT);
            _equipSlots[slot] = new EquipSlotLayout(slot, rect, size, ghostLabel, accentRed);
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
            var pixel = GraphicsManager.Instance?.Pixel;
            var fullRect = new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT);

            if (_layoutTexture != null && _layoutInfos.Count > 0)
            {
                foreach (var info in _layoutInfos)
                {
                    var destRect = new Rectangle(
                        (int)MathF.Round(info.ScreenX * _layoutScale.X),
                        (int)MathF.Round(info.ScreenY * _layoutScale.Y),
                        (int)MathF.Round(info.Width * _layoutScale.X),
                        (int)MathF.Round(info.Height * _layoutScale.Y));

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
            else if (_layoutTexture != null && _textureRectLookup.TryGetValue("Board", out var board))
            {
                var src = new Rectangle(board.X, board.Y, board.Width, board.Height);
                spriteBatch.Draw(_layoutTexture, fullRect, src, Color.White);
            }
            else if (pixel != null)
            {
                spriteBatch.Draw(pixel, fullRect, new Color(10, 10, 10, 220));
            }

            if (pixel != null)
            {
                DrawHeaderBar(spriteBatch, pixel);
                DrawEquipBackground(spriteBatch);
                DrawGridBackground(spriteBatch);
                DrawFooterBase(spriteBatch, pixel);
            }
        }

        private void DrawHeaderBar(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // Intentionally left blank: header uses base layout only (no extra bar).
        }

        private void DrawBeam(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // Separator removed for compact layout.
        }

        private void DrawGridBackground(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance?.Pixel;
            if (pixel == null)
            {
                return;
            }

            DrawInsetPanel(spriteBatch, _gridFrameRect, new Color(12, 12, 12, 220), new Color(80, 80, 80, 180), new Color(0, 0, 0, 220), 2);
            spriteBatch.Draw(pixel, _gridRect, new Color(4, 4, 4, 235));
            DrawBevel(spriteBatch, _gridRect, new Color(60, 60, 60, 180), new Color(0, 0, 0, 200));

            Color gridLine = new Color(28, 28, 28, 180);
            for (int x = 1; x < Columns; x++)
            {
                int lineX = _gridRect.X + x * INVENTORY_SQUARE_WIDTH;
                spriteBatch.Draw(pixel, new Rectangle(lineX, _gridRect.Y, 1, _gridRect.Height), gridLine);
            }

            for (int y = 1; y < Rows; y++)
            {
                int lineY = _gridRect.Y + y * INVENTORY_SQUARE_HEIGHT;
                spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, lineY, _gridRect.Width, 1), gridLine);
            }

            if (_slotTexture != null)
            {
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < Columns; x++)
                    {
                        var slotRect = new Rectangle(
                            _gridRect.X + x * INVENTORY_SQUARE_WIDTH,
                            _gridRect.Y + y * INVENTORY_SQUARE_HEIGHT,
                            INVENTORY_SQUARE_WIDTH,
                            INVENTORY_SQUARE_HEIGHT);
                        spriteBatch.Draw(_slotTexture, slotRect, SlotSourceRect, Color.White * 0.18f);
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

            DrawInsetPanel(spriteBatch, _paperdollPanelRect, new Color(20, 20, 22, 200), new Color(80, 80, 90, 150), new Color(6, 6, 6, 200), 2);

            foreach (var layout in _equipSlots.Values)
            {
                DrawEquipSlot(spriteBatch, layout);
            }
        }

        private void DrawEquipSlot(SpriteBatch spriteBatch, EquipSlotLayout layout)
        {
            var pixel = GraphicsManager.Instance?.Pixel;
            if (pixel == null)
            {
                return;
            }

            Rectangle rect = layout.Rect;
            DrawInsetPanel(spriteBatch, rect, new Color(5, 5, 5, 200), new Color(70, 70, 70, 160), new Color(0, 0, 0, 200), 1);

            if (_slotTexture != null)
            {
                for (int y = 0; y < layout.Size.Y; y++)
                {
                    for (int x = 0; x < layout.Size.X; x++)
                    {
                        Rectangle slotRect = new(
                            rect.X + x * INVENTORY_SQUARE_WIDTH,
                            rect.Y + y * INVENTORY_SQUARE_HEIGHT,
                            INVENTORY_SQUARE_WIDTH,
                            INVENTORY_SQUARE_HEIGHT);
                        spriteBatch.Draw(_slotTexture, slotRect, SlotSourceRect, Color.White * 0.35f);
                    }
                }
            }
            else
            {
                spriteBatch.Draw(pixel, rect, new Color(24, 24, 24, 200));
            }

            DrawGhostIcon(spriteBatch, layout);
        }

        private void DrawGhostIcon(SpriteBatch spriteBatch, EquipSlotLayout layout)
        {
            if (_font == null || string.IsNullOrEmpty(layout.Label))
            {
                return;
            }

            const float scale = 0.35f;
            Vector2 size = _font.MeasureString(layout.Label) * scale;
            Vector2 center = new(layout.Rect.X + layout.Rect.Width / 2f, layout.Rect.Y + layout.Rect.Height / 2f);
            Vector2 pos = center - size / 2f;

            spriteBatch.DrawString(_font, layout.Label, pos + new Vector2(1f, 1f), new Color(0, 0, 0, 120), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, layout.Label, pos, new Color(90, 90, 90, 200), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawFooterBase(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // Only draw the zen field; no footer panel block.
            DrawInsetPanel(spriteBatch, _zenFieldRect, new Color(12, 12, 12, 230), new Color(80, 80, 80, 180), new Color(0, 0, 0, 210));
        }

        private static void DrawBevel(SpriteBatch spriteBatch, Rectangle rect, Color light, Color dark)
        {
            var pixel = GraphicsManager.Instance?.Pixel;
            if (pixel == null)
            {
                return;
            }

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), light);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), dark);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), light);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), dark);
        }

        private static void DrawInsetPanel(SpriteBatch spriteBatch, Rectangle rect, Color fill, Color light, Color dark, int thickness = 1)
        {
            var pixel = GraphicsManager.Instance?.Pixel;
            if (pixel == null)
            {
                return;
            }

            spriteBatch.Draw(pixel, rect, fill);
            for (int i = 0; i < thickness; i++)
            {
                spriteBatch.Draw(pixel, new Rectangle(rect.X + i, rect.Y + i, rect.Width - i * 2, 1), light);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + i, rect.Bottom - 1 - i, rect.Width - i * 2, 1), dark);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + i, rect.Y + i, 1, rect.Height - i * 2), light);
                spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1 - i, rect.Y + i, 1, rect.Height - i * 2), dark);
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

        private void UpdateChromeHover(Point mousePos)
        {
            var closeRect = Translate(_closeButtonRect);
            var leftRect = Translate(_footerLeftButtonRect);
            var rightRect = Translate(_footerRightButtonRect);

            _closeHovered = closeRect.Contains(mousePos);
            _leftFooterHovered = leftRect.Contains(mousePos);
            _rightFooterHovered = rightRect.Contains(mousePos);
        }

        private bool HandleChromeClick()
        {
            if (_closeHovered)
            {
                Hide();
                return true;
            }

            if (_leftFooterHovered)
            {
                Hide();
                return true;
            }

            if (_rightFooterHovered)
            {
                return true;
            }

            return false;
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
                                ReleasePickedItem();
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

                            ReleasePickedItem();
                        }
                    }
                    else if (_hoveredItem != null)
                    {
                        _pickedItemRenderer.PickUpItem(_hoveredItem);
                        _pickedItemOriginalGrid = _hoveredItem.GridPosition;
                        _pickedAtMousePos = mousePos;
                        _itemDragMoved = false;
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

                        ReleasePickedItem();
                    }
                    else
                    {
                        if (_equippedItems.TryGetValue((byte)_hoveredEquipSlot, out var eqItem))
                        {
                            _pickedItemRenderer.PickUpItem(eqItem);
                            _equippedItems.Remove((byte)_hoveredEquipSlot);
                            _pickedFromEquipSlot = _hoveredEquipSlot;
                            _pickedItemOriginalGrid = new Point(-1, -1);
                            _pickedAtMousePos = mousePos;
                            _itemDragMoved = false;
                            _hoveredItem = eqItem;
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

            byte slotIndex = _pickedFromEquipSlot >= 0
                ? (byte)_pickedFromEquipSlot
                : (byte)(InventorySlotOffsetConstant + (item.GridPosition.Y * Columns) + item.GridPosition.X);

            var shop = Game.NpcShopControl.Instance;
            if (shop != null && shop.Visible && shop.DisplayRectangle.Contains(MuGame.Instance.UiMouseState.Position))
            {
                var itemToSell = _pickedItem_renderer_item();
                var originalGrid = _pickedItemOriginalGrid;
                int fromEquipSlot = _pickedFromEquipSlot;

                ReleasePickedItem();

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

                    ReleasePickedItem();
                }
                else
                {
                    AddItem(item);
                    _networkManager?.GetCharacterState()?.RaiseInventoryChanged();
                    ReleasePickedItem();
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

                ReleasePickedItem();
            }
            else
            {
                AddItem(item);
                ReleasePickedItem();
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

            Point gridTopLeft = Translate(_gridRect).Location;
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
                if (!_equipSlots.TryGetValue(kv.Key, out var slot))
                {
                    continue;
                }

                var item = kv.Value;
                Rectangle itemRect = Translate(slot.Rect);

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

            Rectangle gridRect = Translate(_gridRect);
            var dragged = _pickedItem_renderer_item() ?? Game.VaultControl.Instance?.GetDraggedItem();
            bool isOverGrid = IsMouseOverGrid();

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    Rectangle slotRect = new(
                        gridRect.X + x * INVENTORY_SQUARE_WIDTH,
                        gridRect.Y + y * INVENTORY_SQUARE_HEIGHT,
                        INVENTORY_SQUARE_WIDTH,
                        INVENTORY_SQUARE_HEIGHT);

            if (dragged != null && isOverGrid)
            {
                var highlight = GetSlotHighlightColor(new Point(x, y), dragged);
                if (highlight.HasValue)
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, highlight.Value);
                }
            }
            else if (isOverGrid && dragged == null)
            {
                if (_hoveredSlot.X == x && _hoveredSlot.Y == y)
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, new Color(180, 170, 90, 120));
                }
                else if (_hoveredItem != null && IsSlotOccupiedByItem(new Point(x, y), _hoveredItem))
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, new Color(90, 140, 220, 90));
                }
            }
        }
    }
}

        private void DrawEquipHighlights(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance?.Pixel;
            if (pixel == null || _hoveredEquipSlot < 0)
            {
                return;
            }

            if (!_equipSlots.TryGetValue((byte)_hoveredEquipSlot, out var layout))
            {
                return;
            }

            Rectangle rect = Translate(layout.Rect);
            Color overlay = layout.AccentRed ? new Color(200, 60, 60, 140) : new Color(120, 170, 240, 90);
            spriteBatch.Draw(pixel, rect, overlay);
            DrawBevel(spriteBatch, rect, layout.AccentRed ? new Color(255, 140, 140, 140) : new Color(170, 200, 240, 120), new Color(10, 10, 10, 160));
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

        private void DrawChrome(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance?.Pixel;
            if (pixel == null)
            {
                return;
            }

            DrawCloseButton(spriteBatch);
            DrawFooterButton(spriteBatch, _footerLeftButtonRect, "X", _leftFooterHovered);
            DrawFooterButton(spriteBatch, _footerRightButtonRect, "+", _rightFooterHovered);
            DrawCoinAndField(spriteBatch, pixel);
        }

        private void DrawCloseButton(SpriteBatch spriteBatch)
        {
            Rectangle rect = Translate(_closeButtonRect);
            if (_circleTexture != null)
            {
                var outer = rect;
                var inner = new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);
                spriteBatch.Draw(_circleTexture, outer, new Color(120, 60, 20, 230));
                spriteBatch.Draw(_circleTexture, inner, _closeHovered ? new Color(255, 170, 90) : new Color(236, 142, 60));
            }
            else
            {
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, new Color(236, 142, 60));
            }

            if (_font != null)
            {
                float scale = 0.6f;
                Vector2 size = _font.MeasureString("X") * scale;
                Vector2 pos = new(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f);
                spriteBatch.DrawString(_font, "X", pos + new Vector2(1, 1), Color.Black * 0.8f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, "X", pos, new Color(12, 12, 12), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawFooterButton(SpriteBatch spriteBatch, Rectangle rectLocal, string text, bool hovered)
        {
            var rect = Translate(rectLocal);
            DrawInsetPanel(spriteBatch, rect, new Color(30, 28, 26, 230), new Color(110, 100, 80, 200), new Color(8, 8, 8, 220));
            spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Y, rect.Width, rect.Height / 2), new Color(70, 60, 50, hovered ? 200 : 150));
            spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Y + rect.Height / 2, rect.Width, rect.Height / 2), new Color(30, 24, 20, hovered ? 200 : 150));

            if (_font != null)
            {
                float scale = 0.7f;
                Vector2 size = _font.MeasureString(text) * scale;
                Vector2 pos = new(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f);
                spriteBatch.DrawString(_font, text, pos + new Vector2(1, 1), new Color(0, 0, 0, 180), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, text, pos, new Color(255, 150, 70), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawCoinAndField(SpriteBatch spriteBatch, Texture2D pixel)
        {
            Rectangle iconRect = Translate(_zenIconRect);
            if (_coinTexture != null)
            {
                spriteBatch.Draw(_coinTexture, iconRect, new Color(240, 188, 90));
                var inner = new Rectangle(iconRect.X + 3, iconRect.Y + 3, Math.Max(0, iconRect.Width - 6), Math.Max(0, iconRect.Height - 6));
                spriteBatch.Draw(_coinTexture, inner, new Color(255, 230, 140));
            }
            else
            {
                spriteBatch.Draw(pixel, iconRect, new Color(240, 188, 90));
            }

            Rectangle fieldRect = Translate(_zenFieldRect);
            DrawBevel(spriteBatch, fieldRect, new Color(70, 70, 70, 200), new Color(0, 0, 0, 220));
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

            Rectangle hoveredItemRect;
            if (_hoveredEquipSlot >= 0 && _equipSlots.TryGetValue((byte)_hoveredEquipSlot, out var layout))
            {
                hoveredItemRect = Translate(layout.Rect);
            }
            else
            {
                Point gridTopLeft = Translate(_gridRect).Location;
                hoveredItemRect = new Rectangle(
                    gridTopLeft.X + _hoveredItem.GridPosition.X * INVENTORY_SQUARE_WIDTH,
                    gridTopLeft.Y + _hoveredItem.GridPosition.Y * INVENTORY_SQUARE_HEIGHT,
                    _hoveredItem.Definition.Width * INVENTORY_SQUARE_WIDTH,
                    _hoveredItem.Definition.Height * INVENTORY_SQUARE_HEIGHT);
            }

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

        private Rectangle Translate(Rectangle rect)
        {
            return new Rectangle(DisplayRectangle.X + rect.X, DisplayRectangle.Y + rect.Y, rect.Width, rect.Height);
        }

        private void ReleasePickedItem()
        {
            _pickedItemRenderer.ReleaseItem();
            ResetPickedState();
        }

        private void ResetPickedState()
        {
            _pickedItemOriginalGrid = new Point(-1, -1);
            _pickedFromEquipSlot = -1;
            _itemDragMoved = false;
        }

        private bool IsMouseOverGrid()
        {
            Point mousePos = MuGame.Instance.UiMouseState.Position;
            Rectangle gridScreenRect = Translate(_gridRect);

            return gridScreenRect.Contains(mousePos);
        }

        private bool IsMouseOverDragArea()
        {
            Point mousePos = MuGame.Instance.UiMouseState.Position;
            return Translate(_headerRect).Contains(mousePos);
        }

        private Point GetSlotAtScreenPosition(Point screenPos)
        {
            if (DisplayRectangle.Width <= 0 || DisplayRectangle.Height <= 0)
            {
                return new Point(-1, -1);
            }

            Point localPos = new(
                screenPos.X - DisplayRectangle.X - _gridRect.X,
                screenPos.Y - DisplayRectangle.Y - _gridRect.Y);

            if (localPos.X < 0 || localPos.Y < 0 ||
                localPos.X >= _gridRect.Width ||
                localPos.Y >= _gridRect.Height)
            {
                return new Point(-1, -1);
            }

            return new Point(
                Math.Min(Columns - 1, localPos.X / INVENTORY_SQUARE_WIDTH),
                Math.Min(Rows - 1, localPos.Y / INVENTORY_SQUARE_WIDTH));
        }

        private int GetEquipSlotAtScreenPosition(Point screenPos)
        {
            foreach (var layout in _equipSlots.Values)
            {
                var slotRect = Translate(layout.Rect);
                if (slotRect.Contains(screenPos))
                {
                    return layout.Slot;
                }
            }
            return -1;
        }
    }
}
