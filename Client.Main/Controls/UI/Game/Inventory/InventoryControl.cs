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
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game.Common;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Networking;
using Client.Main.Helpers;
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

        // ═══════════════════════════════════════════════════════════════
        // WINDOW DIMENSIONS - REDESIGNED
        // ═══════════════════════════════════════════════════════════════
        private const int WINDOW_WIDTH = 396;
        private const int WINDOW_HEIGHT = 700;

        private const int HEADER_HEIGHT = 52;
        private const int SECTION_SPACING = 16;
        private const int PANEL_PADDING = 12;
        private const int EQUIP_SECTION_HEIGHT = 270;

        public const int INVENTORY_SQUARE_WIDTH = 34;
        public const int INVENTORY_SQUARE_HEIGHT = 34;

        public const int Columns = 8;
        public const int Rows = 8;
        internal const int InventorySlotOffsetConstant = 12;

        // ═══════════════════════════════════════════════════════════════
        // MODERN DARK THEME
        // ═══════════════════════════════════════════════════════════════
        private static class Theme
        {
            // Background layers
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BgLight = new(35, 42, 52, 245);
            public static readonly Color BgLighter = new(48, 56, 68, 240);

            // Accent - Warm Gold
            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color AccentBright = new(255, 215, 120);
            public static readonly Color AccentDim = new(140, 115, 55);
            public static readonly Color AccentGlow = new(255, 200, 80, 40);

            // Secondary accent - Cool Blue
            public static readonly Color Secondary = new(90, 140, 200);
            public static readonly Color SecondaryBright = new(130, 180, 240);
            public static readonly Color SecondaryDim = new(50, 80, 120);

            // Borders
            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color BorderHighlight = new(100, 110, 130, 120);

            // Slots
            public static readonly Color SlotBg = new(12, 15, 20, 240);
            public static readonly Color SlotBorder = new(45, 52, 65, 180);
            public static readonly Color SlotHover = new(70, 85, 110, 150);
            public static readonly Color SlotSelected = new(212, 175, 85, 100);

            // Item rarity glow
            public static readonly Color GlowNormal = new(150, 150, 150, 25);
            public static readonly Color GlowMagic = new(100, 150, 255, 50);
            public static readonly Color GlowExcellent = new(120, 255, 120, 60);
            public static readonly Color GlowAncient = new(80, 200, 255, 70);
            public static readonly Color GlowLegendary = new(255, 180, 80, 70);

            // Text
            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color TextGold = new(255, 220, 130);
            public static readonly Color TextGray = new(160, 165, 175);
            public static readonly Color TextDark = new(100, 105, 115);

            // Status colors
            public static readonly Color Success = new(80, 200, 120);
            public static readonly Color Warning = new(240, 180, 60);
            public static readonly Color Danger = new(220, 80, 80);
        }

        private static readonly ItemGlowPalette GlowPalette = new(
            Theme.GlowNormal,
            Theme.GlowMagic,
            Theme.GlowExcellent,
            Theme.GlowAncient,
            Theme.GlowLegendary);

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

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private readonly List<InventoryTextEntry> _texts = new();
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

        private bool _isRepairMode;
        private int _repairEnableLevel = 50;

        public bool IsSelfRepairMode => _isRepairMode;

        public readonly PickedItemRenderer _pickedItemRenderer;

        private readonly List<LayoutInfo> _layoutInfos = new();
        private readonly Dictionary<string, TextureRectData> _textureRectLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<byte, EquipSlotLayout> _equipSlots = new();
        private Vector2 _layoutScale = Vector2.One;

        private static InventoryControl _instance;

        public InventoryControl(NetworkManager networkManager = null, ILoggerFactory loggerFactory = null)
        {
            // Ensure the singleton points to the active UI instance (needed by VaultControl drops).
            _instance = this;

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

            UpdateZenFromNetwork();
            UpdateZenText();
            InvalidateStaticSurface();
        }

        public void Preload()
        {
            RefreshInventoryContent();
        }

        public InventoryItem GetDraggedItem() => _pickedItemRenderer.Item;

        public void Show()
        {
            // Force correct position BEFORE first draw to prevent flash on wrong side
            ForceAlignNow();
            Align = ControlAlign.None; // Prevent auto-realignment
            _networkManager?.GetCharacterState()?.ClearPendingInventoryMove(); // ensure no stale hides
            UpdateZenFromNetwork();
            RefreshInventoryContent();

            Visible = true;
            BringToFront();
            Scene.FocusControl = this;

            _zenText.Visible = true;
            UpdateZenText();

            _pickedItemRenderer.Visible = false;

            // Reset repair mode when reopening inventory
            _isRepairMode = false;

            InvalidateStaticSurface();
        }

        /// <summary>
        /// Forces immediate position calculation based on Align property.
        /// Call this before showing the control to prevent position flickering.
        /// </summary>
        private void ForceAlignNow()
        {
            if (Parent == null || Align == ControlAlign.None)
                return;

            const int padding = 20;

            if (Align.HasFlag(ControlAlign.Top))
                Y = padding;
            else if (Align.HasFlag(ControlAlign.Bottom))
                Y = Parent.DisplaySize.Y - DisplaySize.Y - padding;
            else if (Align.HasFlag(ControlAlign.VerticalCenter))
                Y = (Parent.DisplaySize.Y / 2) - (DisplaySize.Y / 2);

            if (Align.HasFlag(ControlAlign.Left))
                X = padding;
            else if (Align.HasFlag(ControlAlign.Right))
                X = Parent.DisplaySize.X - DisplaySize.X - padding;
            else if (Align.HasFlag(ControlAlign.HorizontalCenter))
                X = (Parent.DisplaySize.X / 2) - (DisplaySize.X / 2);
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

            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.L) &&
                MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.L))
            {
                ToggleRepairMode();
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

            if (leftJustReleased && _pickedItemRenderer.Item != null && !_isDragging)
            {
                // Simple click without moving should keep the item picked up; only place after a drag.
                if (!_itemDragMoved)
                {
                    return;
                }

                if (_hoveredEquipSlot >= 0 && TryPlacePickedItemIntoEquipSlot((byte)_hoveredEquipSlot))
                {
                    return;
                }

                if (!IsMouseOverGrid() && _itemDragMoved)
                {
                    HandleDropOutsideInventory();
                }
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

        private void ToggleRepairMode()
        {
            var state = _networkManager?.GetCharacterState();
            if (state == null || state.Level < _repairEnableLevel)
            {
                return;
            }
            _isRepairMode = !_isRepairMode;
            SoundController.Instance.PlayBuffer("Sound/iButton.wav");
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
            SpriteBatchScope? scope = null;
            if (!SpriteBatchScope.BatchIsBegun)
            {
                scope = new SpriteBatchScope(
                    spriteBatch,
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    GraphicsManager.GetQualityLinearSamplerState(),
                    transform: UiScaler.SpriteTransform);
            }

            try
            {
                if (_staticSurface != null && !_staticSurface.IsDisposed)
                {
                    spriteBatch.Draw(_staticSurface, DisplayRectangle, Color.White * Alpha);
                }

                // Draw overlays beneath items (consistent with vault/NPC shop)
                DrawGridOverlays(spriteBatch);
                DrawEquipHighlights(spriteBatch);
                DrawInventoryItems(spriteBatch);
                DrawEquippedItems(spriteBatch);
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
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            InvalidateStaticSurface();
        }

        private void InitializeTextEntries()
        {
            _texts.Clear();

            // Title is now drawn in DrawModernHeader, so we skip the title text

            // Zen text - positioned inside zen field
            _zenText = CreateText(new Vector2(_zenFieldRect.X + 8, _zenFieldRect.Y + _zenFieldRect.Height * 0.5f - 6f),
                                  12f, Theme.TextGold);
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

            // Header
            _headerRect = new Rectangle(0, 0, WINDOW_WIDTH, HEADER_HEIGHT);

            // Equipment panel - centered, fixed height
            int equipPanelWidth = WINDOW_WIDTH - PANEL_PADDING * 2;
            int equipPanelTop = HEADER_HEIGHT + 8;
            _paperdollPanelRect = new Rectangle(PANEL_PADDING, equipPanelTop, equipPanelWidth, EQUIP_SECTION_HEIGHT);

            // Grid section - positioned BELOW equipment with proper spacing
            int gridTotalWidth = Columns * INVENTORY_SQUARE_WIDTH;
            int gridTotalHeight = Rows * INVENTORY_SQUARE_HEIGHT;
            int gridX = (WINDOW_WIDTH - gridTotalWidth) / 2;
            int minGridY = _paperdollPanelRect.Bottom + Math.Max(SECTION_SPACING / 2, 4);
            int gridY = minGridY;

            int footerHeight = 50;
            int footerTop = WINDOW_HEIGHT - footerHeight - 10;

            // Ensure grid section does not overlap footer
            int availableBottom = footerTop - SECTION_SPACING;
            int maxGridY = Math.Max(minGridY, availableBottom - gridTotalHeight - 8);
            gridY = Math.Min(gridY, maxGridY);
            if (gridY < minGridY)
            {
                gridY = minGridY;
            }

            _gridRect = new Rectangle(gridX, gridY, gridTotalWidth, gridTotalHeight);
            _gridFrameRect = new Rectangle(gridX - 8, gridY - 8, gridTotalWidth + 16, gridTotalHeight + 16);

            // Footer - at bottom
            _footerRect = new Rectangle(PANEL_PADDING, footerTop,
                                         WINDOW_WIDTH - PANEL_PADDING * 2, footerHeight);

            // Zen display
            _zenIconRect = new Rectangle(_footerRect.X + 12, _footerRect.Y + 14, 22, 22);
            _zenFieldRect = new Rectangle(_zenIconRect.Right + 10, _footerRect.Y + 10, 160, 30);

            // Buttons
            int btnSize = 38;
            _closeButtonRect = new Rectangle(WINDOW_WIDTH - btnSize - 12, 10, btnSize, btnSize);
            _footerLeftButtonRect = new Rectangle(_footerRect.Right - btnSize * 2 - 20, _footerRect.Y + 6, btnSize, btnSize);
            _footerRightButtonRect = new Rectangle(_footerRect.Right - btnSize - 8, _footerRect.Y + 6, btnSize, btnSize);

            // Beam rect not used in new design
            _beamRect = Rectangle.Empty;
        }

        private void BuildEquipSlots()
        {
            _equipSlots.Clear();

            int cell = INVENTORY_SQUARE_WIDTH;
            int panelCenterX = WINDOW_WIDTH / 2;
            int baseY = HEADER_HEIGHT + 20;

            // Left column (pet, left-hand weapon, gloves)
            int leftColX = panelCenterX - cell * 4 - 24;
            AddEquipSlot(8, new Point(leftColX, baseY), new Point(2, 2), "PET");
            AddEquipSlot(0, new Point(leftColX, baseY + cell * 2 + 8), new Point(2, 3), "L.HAND");
            AddEquipSlot(5, new Point(leftColX, baseY + cell * 5 + 16), new Point(2, 2), "GLOVES");

            // Center column (helm, armor, pants + rings/pendant)
            int centerColX = panelCenterX - cell;
            AddEquipSlot(2, new Point(centerColX, baseY), new Point(2, 2), "HELM");
            AddEquipSlot(3, new Point(centerColX, baseY + cell * 2 + 8), new Point(2, 3), "ARMOR");
            AddEquipSlot(4, new Point(centerColX, baseY + cell * 5 + 16), new Point(2, 2), "PANTS");

            // Rings and pendant next to the center column
            int accessoryOffset = 6;
            AddEquipSlot(9, new Point(centerColX - cell - accessoryOffset, baseY + cell * 2 + 20), new Point(1, 1), "PEND");
            AddEquipSlot(10, new Point(centerColX - cell - accessoryOffset, baseY + cell * 5 + 28), new Point(1, 1), "RING");
            AddEquipSlot(11, new Point(centerColX + cell * 2 + accessoryOffset, baseY + cell * 5 + 28), new Point(1, 1), "RING");

            // Right column (wings, right-hand weapon, boots)
            int rightColX = panelCenterX + cell * 2 + 16;
            AddEquipSlot(7, new Point(rightColX - cell / 2, baseY - 4), new Point(3, 2), "WINGS");
            AddEquipSlot(1, new Point(rightColX, baseY + cell * 2 + 8), new Point(2, 3), "R.HAND");
            AddEquipSlot(6, new Point(rightColX, baseY + cell * 5 + 16), new Point(2, 2), "BOOTS");
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

        // ═══════════════════════════════════════════════════════════════
        // CORE DRAWING PRIMITIVES
        // ═══════════════════════════════════════════════════════════════

        private void DrawWindowBackground(SpriteBatch spriteBatch, Rectangle rect)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Outer border
            spriteBatch.Draw(pixel, rect, Theme.BorderOuter);

            // Main background with vertical gradient
            var innerRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
            UiDrawHelper.DrawVerticalGradient(spriteBatch, innerRect, Theme.BgDark, Theme.BgDarkest);

            // Subtle inner border highlight
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 1), Theme.BorderInner * 0.5f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, 1, innerRect.Height), Theme.BorderInner * 0.3f);

            // Corner accents
            UiDrawHelper.DrawCornerAccents(spriteBatch, rect, Theme.Accent * 0.4f);
        }

        private void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color bgColor, bool withBorder = true, bool withGlow = false)
        {
            UiDrawHelper.DrawPanel(spriteBatch, rect, bgColor,
                withBorder ? Theme.BorderInner : (Color?)null,
                withBorder ? Theme.BorderOuter : (Color?)null,
                withBorder ? Theme.BorderHighlight * 0.3f : null,
                withGlow, withGlow ? Theme.Accent * 0.15f : null);
        }

        private void DrawSectionHeader(SpriteBatch spriteBatch, string title, int x, int y, int width)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || _font == null) return;

            float scale = 0.36f;
            Vector2 textSize = _font.MeasureString(title) * scale;

            // Decorative lines on sides
            int lineY = y + (int)(textSize.Y / 2);
            int textPadding = 12;
            int textX = x + (width - (int)textSize.X) / 2;

            // Left line with fade
            int leftLineWidth = textX - x - textPadding;
            if (leftLineWidth > 20)
            {
                UiDrawHelper.DrawHorizontalGradient(spriteBatch,
                    new Rectangle(x, lineY, leftLineWidth, 1),
                    Theme.Accent * 0.1f, Theme.Accent * 0.5f);
                spriteBatch.Draw(pixel, new Rectangle(textX - textPadding - 3, lineY - 1, 3, 3), Theme.Accent * 0.6f);
            }

            // Right line with fade
            int rightLineStart = textX + (int)textSize.X + textPadding;
            int rightLineWidth = x + width - rightLineStart;
            if (rightLineWidth > 20)
            {
                UiDrawHelper.DrawHorizontalGradient(spriteBatch,
                    new Rectangle(rightLineStart, lineY, rightLineWidth, 1),
                    Theme.Accent * 0.5f, Theme.Accent * 0.1f);
                spriteBatch.Draw(pixel, new Rectangle(rightLineStart, lineY - 1, 3, 3), Theme.Accent * 0.6f);
            }

            // Text shadow
            spriteBatch.DrawString(_font, title, new Vector2(textX + 1, y + 1), Color.Black * 0.6f,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            // Text
            spriteBatch.DrawString(_font, title, new Vector2(textX, y), Theme.TextGold,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawStaticElements(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var fullRect = new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT);

            // ═══════════════════════════════════════════════════════════
            // 1. MAIN WINDOW BACKGROUND
            // ═══════════════════════════════════════════════════════════
            DrawWindowBackground(spriteBatch, fullRect);

            // ═══════════════════════════════════════════════════════════
            // 2. HEADER
            // ═══════════════════════════════════════════════════════════
            DrawModernHeader(spriteBatch);

            // ═══════════════════════════════════════════════════════════
            // 3. EQUIPMENT SECTION
            // ═══════════════════════════════════════════════════════════
            DrawModernEquipSection(spriteBatch);

            // ═══════════════════════════════════════════════════════════
            // 4. INVENTORY GRID SECTION
            // ═══════════════════════════════════════════════════════════
            DrawModernGridSection(spriteBatch);

            // ═══════════════════════════════════════════════════════════
            // 5. FOOTER
            // ═══════════════════════════════════════════════════════════
            DrawModernFooter(spriteBatch);
        }

        private void DrawModernHeader(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Header background
            var headerBg = new Rectangle(8, 6, WINDOW_WIDTH - 16, HEADER_HEIGHT - 8);
            DrawPanel(spriteBatch, headerBg, Theme.BgMid);

            // Gold accent line at very top
            spriteBatch.Draw(pixel, new Rectangle(20, 8, WINDOW_WIDTH - 40, 2), Theme.Accent * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(30, 10, WINDOW_WIDTH - 60, 1), Theme.AccentDim * 0.4f);

            // Title
            if (_font != null)
            {
                string title = "INVENTORY";
                float scale = 0.55f;
                Vector2 size = _font.MeasureString(title) * scale;
                Vector2 pos = new((WINDOW_WIDTH - size.X) / 2, (HEADER_HEIGHT - size.Y) / 2 + 2);

                // Glow behind text
                spriteBatch.Draw(pixel, new Rectangle((int)pos.X - 20, (int)pos.Y - 4, (int)size.X + 40, (int)size.Y + 8),
                                Theme.AccentGlow * 0.3f);

                // Shadow
                spriteBatch.DrawString(_font, title, pos + new Vector2(2, 2), Color.Black * 0.5f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                // Main text
                spriteBatch.DrawString(_font, title, pos, Theme.TextWhite,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            // Bottom separator
            int separatorY = HEADER_HEIGHT - 2;
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(20, separatorY, (WINDOW_WIDTH - 40) / 2, 1),
                                  Color.Transparent, Theme.BorderInner);
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, separatorY, (WINDOW_WIDTH - 40) / 2, 1),
                                  Theme.BorderInner, Color.Transparent);
        }

        private void DrawModernEquipSection(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Section title
            DrawSectionHeader(spriteBatch, "EQUIPMENT", _paperdollPanelRect.X, _paperdollPanelRect.Y - 18, _paperdollPanelRect.Width);

            // Main panel background
            DrawPanel(spriteBatch, _paperdollPanelRect, Theme.BgMid);

            // Character silhouette area (center darker region)
            int silhouetteWidth = INVENTORY_SQUARE_WIDTH * 2 + 20;
            int silhouetteX = (WINDOW_WIDTH - silhouetteWidth) / 2;
            var silhouetteRect = new Rectangle(silhouetteX, _paperdollPanelRect.Y + 10,
                                                silhouetteWidth, _paperdollPanelRect.Height - 20);
            spriteBatch.Draw(pixel, silhouetteRect, Theme.BgDarkest * 0.5f);

            // Draw vertical divider lines
            int dividerX1 = silhouetteX - 30;
            int dividerX2 = silhouetteX + silhouetteWidth + 30;
            UiDrawHelper.DrawVerticalGradient(spriteBatch,
                new Rectangle(dividerX1, _paperdollPanelRect.Y + 20, 1, _paperdollPanelRect.Height - 40),
                Theme.BorderInner * 0.3f, Theme.BorderInner * 0.1f);
            UiDrawHelper.DrawVerticalGradient(spriteBatch,
                new Rectangle(dividerX2, _paperdollPanelRect.Y + 20, 1, _paperdollPanelRect.Height - 40),
                Theme.BorderInner * 0.3f, Theme.BorderInner * 0.1f);

            // Draw each equipment slot
            foreach (var layout in _equipSlots.Values)
            {
                DrawModernEquipSlot(spriteBatch, layout);
            }
        }

        private void DrawModernEquipSlot(SpriteBatch spriteBatch, EquipSlotLayout layout)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            Rectangle rect = layout.Rect;
            bool hasItem = _equippedItems.ContainsKey(layout.Slot);

            // Slot background
            Color bgColor = hasItem ? Theme.BgLight : Theme.SlotBg;
            UiDrawHelper.DrawVerticalGradient(spriteBatch, rect, bgColor, Theme.BgDarkest);

            // Border
            Color borderColor = layout.AccentRed ? Theme.Danger : Theme.SlotBorder;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), borderColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Theme.BorderOuter);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), borderColor * 0.6f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Theme.BorderOuter);

            // Inner cell divisions
            for (int y = 1; y < layout.Size.Y; y++)
            {
                int lineY = rect.Y + y * INVENTORY_SQUARE_HEIGHT;
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 2, lineY, rect.Width - 4, 1), Theme.BorderOuter * 0.5f);
            }
            for (int x = 1; x < layout.Size.X; x++)
            {
                int lineX = rect.X + x * INVENTORY_SQUARE_WIDTH;
                spriteBatch.Draw(pixel, new Rectangle(lineX, rect.Y + 2, 1, rect.Height - 4), Theme.BorderOuter * 0.5f);
            }

            // Ghost label (only if no item)
            if (!hasItem)
            {
                DrawSlotGhostLabel(spriteBatch, layout);
            }
        }

        private void DrawSlotGhostLabel(SpriteBatch spriteBatch, EquipSlotLayout layout)
        {
            if (_font == null || string.IsNullOrEmpty(layout.Label)) return;

            const float scale = 0.26f;
            Vector2 size = _font.MeasureString(layout.Label) * scale;
            Vector2 center = new(layout.Rect.X + layout.Rect.Width / 2f, layout.Rect.Y + layout.Rect.Height / 2f);
            Vector2 pos = center - size / 2f;

            Color textColor = layout.AccentRed ? new Color(100, 60, 60, 120) : Theme.TextDark * 0.6f;

            spriteBatch.DrawString(_font, layout.Label, pos + Vector2.One, Color.Black * 0.3f,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, layout.Label, pos, textColor,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawModernGridSection(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Section title
            DrawSectionHeader(spriteBatch, "BACKPACK", _gridFrameRect.X, _gridFrameRect.Y + 4, _gridFrameRect.Width);

            // Outer frame
            DrawPanel(spriteBatch, _gridFrameRect, Theme.BgMid, withGlow: false);

            // Grid background
            spriteBatch.Draw(pixel, _gridRect, Theme.SlotBg);

            // Inner shadow
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, _gridRect.Width, 2), Color.Black * 0.4f);
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, 2, _gridRect.Height), Color.Black * 0.3f);

            // Grid lines
            Color gridLine = new(40, 48, 60, 100);
            Color gridLineMajor = new(55, 65, 80, 120);

            for (int x = 1; x < Columns; x++)
            {
                int lineX = _gridRect.X + x * INVENTORY_SQUARE_WIDTH;
                bool isMajor = x == Columns / 2;
                spriteBatch.Draw(pixel, new Rectangle(lineX, _gridRect.Y, 1, _gridRect.Height), isMajor ? gridLineMajor : gridLine);
            }

            for (int y = 1; y < Rows; y++)
            {
                int lineY = _gridRect.Y + y * INVENTORY_SQUARE_HEIGHT;
                bool isMajor = y == Rows / 2;
                spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, lineY, _gridRect.Width, 1), isMajor ? gridLineMajor : gridLine);
            }

            // Border highlight
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Bottom - 1, _gridRect.Width, 1), Theme.BorderHighlight * 0.2f);
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.Right - 1, _gridRect.Y, 1, _gridRect.Height), Theme.BorderHighlight * 0.15f);
        }

        private void DrawModernFooter(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Top separator line
            int sepY = _footerRect.Y - 6;
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(30, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Color.Transparent, Theme.Accent * 0.4f);
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Theme.Accent * 0.4f, Color.Transparent);

            // Footer panel
            DrawPanel(spriteBatch, _footerRect, Theme.BgMid);

            // Zen display area
            DrawZenDisplay(spriteBatch);
        }

        private void DrawZenDisplay(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Coin icon
            Rectangle iconRect = _zenIconRect;

            // Coin outer
            DrawFilledCircle(spriteBatch, iconRect.X + iconRect.Width / 2, iconRect.Y + iconRect.Height / 2,
                             iconRect.Width / 2, Theme.AccentDim);
            // Coin inner
            DrawFilledCircle(spriteBatch, iconRect.X + iconRect.Width / 2, iconRect.Y + iconRect.Height / 2,
                             iconRect.Width / 2 - 3, Theme.Accent);
            // Coin highlight
            DrawFilledCircle(spriteBatch, iconRect.X + iconRect.Width / 2 - 2, iconRect.Y + iconRect.Height / 2 - 2,
                             iconRect.Width / 4, Theme.AccentBright * 0.6f);

            // Zen field background
            DrawPanel(spriteBatch, _zenFieldRect, Theme.SlotBg);

            // Inner darker area
            var innerField = new Rectangle(_zenFieldRect.X + 2, _zenFieldRect.Y + 2,
                                           _zenFieldRect.Width - 4, _zenFieldRect.Height - 4);
            spriteBatch.Draw(pixel, innerField, Theme.BgDarkest * 0.7f);
        }

        private void DrawFilledCircle(SpriteBatch spriteBatch, int centerX, int centerY, int radius, Color color)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || radius <= 0) return;

            // Simple filled circle using rectangles
            for (int y = -radius; y <= radius; y++)
            {
                int halfWidth = (int)MathF.Sqrt(radius * radius - y * y);
                if (halfWidth > 0)
                {
                    spriteBatch.Draw(pixel, new Rectangle(centerX - halfWidth, centerY + y, halfWidth * 2, 1), color);
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
                if (!string.IsNullOrEmpty(item.Definition.TexturePath) &&
                    !item.Definition.TexturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
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
                ToggleRepairMode();
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
                    // Check if there's an item from VaultControl being dragged
                    var vaultDraggedItem = VaultControl.Instance?.GetDraggedItem();
                    if (vaultDraggedItem != null && _pickedItemRenderer.Item == null)
                    {
                        // VaultControl will handle the drop via its own AttemptDrop logic
                        // We just need to consume the mouse input to prevent picking up items underneath
                        Scene?.SetMouseInputConsumed();
                        return;
                    }

                    // Check if there's an item from TradeControl being dragged
                    var tradeDraggedItem = Client.Main.Controls.UI.Game.Trade.TradeControl.Instance?.GetDraggedItem();
                    if (tradeDraggedItem != null && _pickedItemRenderer.Item == null)
                    {
                        // TradeControl will handle the drop via its own AttemptDrop logic
                        Scene?.SetMouseInputConsumed();
                        return;
                    }

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
                        else if (TryUsePickedJewelOnInventory(gridSlot))
                        {
                            return;
                        }
                    }
                    else if (_hoveredItem != null)
                    {
                        // Check if NPC shop is in repair mode
                        var npcShop = NpcShopControl.Instance;
                        if (npcShop != null && npcShop.Visible && npcShop.IsRepairMode)
                        {
                            // Repair mode - send repair request instead of picking up item
                            if (Core.Utilities.ItemPriceCalculator.IsRepairable(_hoveredItem))
                            {
                                byte itemSlot = (byte)(InventorySlotOffsetConstant + (_hoveredItem.GridPosition.Y * Columns) + _hoveredItem.GridPosition.X);
                                var svc = _networkManager?.GetCharacterService();
                                if (svc != null)
                                {
                                    _ = svc.SendRepairItemRequestAsync(itemSlot, false);
                                    SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                                }
                            }
                            return;
                        }

                        // Check if in self repair mode
                        else if (_isRepairMode)
                        {
                            // Self repair mode - send repair request instead of picking up item
                            if (Core.Utilities.ItemPriceCalculator.IsRepairable(_hoveredItem))
                            {
                                byte itemSlot = (byte)(InventorySlotOffsetConstant + (_hoveredItem.GridPosition.Y * Columns) + _hoveredItem.GridPosition.X);
                                var svc = _networkManager?.GetCharacterService();
                                if (svc != null)
                                {
                                    _ = svc.SendRepairItemRequestAsync(itemSlot, true);
                                    SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                                }
                            }
                            return;
                        }

                        // Normal mode - pick up item
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
                        if (_hoveredItem.Definition.IsUpgradeJewel())
                        {
                            return;
                        }

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
                        if (TryPlacePickedItemIntoEquipSlot((byte)_hoveredEquipSlot))
                        {
                            return;
                        }
                    }
                    else
                    {
                        if (_equippedItems.TryGetValue((byte)_hoveredEquipSlot, out var eqItem))
                        {
                            // Check if NPC shop is in repair mode
                            var npcShop = NpcShopControl.Instance;
                            if (npcShop != null && npcShop.Visible && npcShop.IsRepairMode)
                            {
                                // Repair mode - send repair request for equipped item
                                if (Core.Utilities.ItemPriceCalculator.IsRepairable(eqItem))
                                {
                                    byte equipSlot = (byte)_hoveredEquipSlot;
                                    var svc = _networkManager?.GetCharacterService();
                                    if (svc != null)
                                    {
                                        _ = svc.SendRepairItemRequestAsync(equipSlot, false);
                                        SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                                    }
                                }
                                return;
                            }

                            // Check if in self repair mode
                            else if (_isRepairMode)
                            {
                                // Self repair mode - send repair request for equipped item
                                if (Core.Utilities.ItemPriceCalculator.IsRepairable(eqItem))
                                {
                                    byte equipSlot = (byte)_hoveredEquipSlot;
                                    var svc = _networkManager?.GetCharacterService();
                                    if (svc != null)
                                    {
                                        _ = svc.SendRepairItemRequestAsync(equipSlot, true);
                                        SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                                    }
                                }
                                return;
                            }

                            // Normal mode - pick up equipped item
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

            var shop = NpcShopControl.Instance;
            if (shop != null && shop.Visible && shop.DisplayRectangle.Contains(MuGame.Instance.UiMouseState.Position))
            {
                var itemToSell = _pickedItem_renderer_item();
                var originalGrid = _pickedItemOriginalGrid;
                int fromEquipSlot = _pickedFromEquipSlot;

                ReleasePickedItem();

                ShowSellConfirmation(itemToSell, slotIndex, originalGrid, fromEquipSlot);
            }
            else if (VaultControl.Instance is { } vault &&
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
            else if (Client.Main.Controls.UI.Game.ChaosMixControl.Instance is { } chaos &&
                     chaos.Visible &&
                     chaos.DisplayRectangle.Contains(MuGame.Instance.UiMouseState.Position) &&
                     _network_manager_exists())
            {
                var drop = chaos.GetSlotAtScreenPosition(MuGame.Instance.UiMouseState.Position);
                if (drop.X >= 0 && chaos.CanPlaceAt(drop, item))
                {
                    byte toSlot = (byte)(drop.Y * Client.Main.Controls.UI.Game.ChaosMixControl.Columns + drop.X);
                    var svc = _networkManager.GetCharacterService();
                    var raw = item.RawData ?? Array.Empty<byte>();
                    var state = _networkManager.GetCharacterState();
                    state.StashPendingInventoryMove(slotIndex, slotIndex);

                    _ = Task.Run(async () =>
                    {
                        await svc.SendStorageItemMoveAsync(ItemStorageKind.Inventory, slotIndex, ItemStorageKind.ChaosMachine, toSlot, _networkManager.TargetVersion, raw);
                        await Task.Delay(1200);
                        if (_networkManager != null && state.IsInventoryMovePending(slotIndex, slotIndex))
                        {
                            MuGame.ScheduleOnMainThread(() =>
                            {
                                state.RaiseInventoryChanged();
                                state.RaiseChaosMachineItemsChanged();
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
            else if (Client.Main.Controls.UI.Game.Trade.TradeControl.Instance is { } trade &&
                     trade.Visible &&
                     trade.DisplayRectangle.Contains(MuGame.Instance.UiMouseState.Position) &&
                     _network_manager_exists())
            {
                var drop = trade.GetSlotAtScreenPosition(MuGame.Instance.UiMouseState.Position);
                if (drop.X >= 0 && trade.CanPlaceAt(drop, item))
                {
                    trade.AcceptItemFromInventory(item, drop, slotIndex);
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

        private bool TryUsePickedJewelOnInventory(Point gridSlot)
        {
            if (!IsUpgradeJewel(_pickedItemRenderer.Item) || !IsWithinGrid(gridSlot))
            {
                return false;
            }

            var targetItem = _itemGrid[gridSlot.X, gridSlot.Y];
            if (targetItem == null)
            {
                return false;
            }

            byte targetSlot = (byte)(InventorySlotOffsetConstant + (targetItem.GridPosition.Y * Columns) + targetItem.GridPosition.X);
            return TryConsumePickedUpgradeJewel(targetSlot);
        }

        private bool TryUsePickedJewelOnEquipment(byte equipSlot)
        {
            if (!IsUpgradeJewel(_pickedItemRenderer.Item))
            {
                return false;
            }

            if (!_equippedItems.ContainsKey(equipSlot))
            {
                return false;
            }

            return TryConsumePickedUpgradeJewel(equipSlot);
        }

        private bool TryConsumePickedUpgradeJewel(byte targetSlot)
        {
            if (!IsUpgradeJewel(_pickedItemRenderer.Item))
            {
                return false;
            }

            byte? jewelSlot = GetPickedItemSlotIndex();
            if (jewelSlot == null)
            {
                _logger?.LogWarning("Cannot apply jewel: source slot is unknown.");
                return false;
            }

            if (_networkManager == null)
            {
                _logger?.LogWarning("Cannot apply jewel: not connected to the server.");
                RestorePickedItemToOriginalLocation();
                return true;
            }

            QueueConsumeItemRequest(jewelSlot.Value, targetSlot);
            ReleasePickedItem();
            return true;
        }

        private bool TryPlacePickedItemIntoEquipSlot(byte equipSlot)
        {
            var itemToPlace = _pickedItemRenderer.Item;
            if (itemToPlace == null)
            {
                return false;
            }

            if (TryUsePickedJewelOnEquipment(equipSlot))
            {
                return true;
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

            byte toSlot = equipSlot;

            // If moving to the same slot, just put it back without sending request
            if (fromSlot == toSlot)
            {
                _equippedItems[toSlot] = itemToPlace;
                ReleasePickedItem();
                return true;
            }

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
            return true;
        }

        private void QueueConsumeItemRequest(byte itemSlot, byte targetSlot)
        {
            if (_networkManager == null)
            {
                return;
            }

            var svc = _networkManager.GetCharacterService();
            _ = Task.Run(async () =>
            {
                await svc.SendConsumeItemRequestAsync(itemSlot, targetSlot);
                await Task.Delay(300);

                var state = _networkManager?.GetCharacterState();
                if (state != null)
                {
                    MuGame.ScheduleOnMainThread(() => state.RaiseInventoryChanged());
                }
            });
        }

        private byte? GetPickedItemSlotIndex()
        {
            if (_pickedItemOriginalGrid.X >= 0)
            {
                return (byte)(InventorySlotOffsetConstant + (_pickedItemOriginalGrid.Y * Columns) + _pickedItemOriginalGrid.X);
            }

            if (_pickedFromEquipSlot >= 0)
            {
                return (byte)_pickedFromEquipSlot;
            }

            return null;
        }

        private void RestorePickedItemToOriginalLocation()
        {
            var item = _pickedItemRenderer.Item;
            if (item == null)
            {
                return;
            }

            if (_pickedItemOriginalGrid.X >= 0)
            {
                item.GridPosition = _pickedItemOriginalGrid;
                AddItem(item);
            }
            else if (_pickedFromEquipSlot >= 0)
            {
                _equippedItems[(byte)_pickedFromEquipSlot] = item;
            }

            ReleasePickedItem();
        }

        private static bool IsUpgradeJewel(InventoryItem item)
        {
            return item?.Definition?.IsUpgradeJewel() == true;
        }

        private static bool IsWithinGrid(Point slot)
        {
            return slot.X >= 0 && slot.X < Columns && slot.Y >= 0 && slot.Y < Rows;
        }

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

            if (item.Definition?.BaseDurability == 0 && item.Definition.MagicDurability == 0 && item.Durability > 1)
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
            if (GraphicsManager.Instance.Pixel == null || GraphicsManager.Instance.Font == null)
                return;

            var jewelEntries = new List<(InventoryItem Item, Rectangle Rect)>();

            Point gridTopLeft = Translate(_gridRect).Location;
            var font = GraphicsManager.Instance.Font;
            var pixel = GraphicsManager.Instance.Pixel;

            // Cache items count and iterate without creating a copy
            int itemCount = _items.Count;
            for (int i = 0; i < itemCount; i++)
            {
                var item = _items[i];
                if (item == _pickedItem_renderer_item())
                    continue;

                // Skip items that are in pending move (being transferred to vault/trade)
                var state = _networkManager?.GetCharacterState();
                byte itemSlotIndex = (byte)(InventorySlotOffsetConstant + (item.GridPosition.Y * Columns) + item.GridPosition.X);
                if (state?.PendingMoveFromSlot == itemSlotIndex)
                    continue;

                Rectangle itemRect = new(
                    gridTopLeft.X + item.GridPosition.X * INVENTORY_SQUARE_WIDTH,
                    gridTopLeft.Y + item.GridPosition.Y * INVENTORY_SQUARE_HEIGHT,
                    item.Definition.Width * INVENTORY_SQUARE_WIDTH,
                    item.Definition.Height * INVENTORY_SQUARE_HEIGHT);

                // Item glow effect
                Color glowColor = ItemUiHelper.GetItemGlowColor(item, GlowPalette);
                if (glowColor.A > 0)
                {
                    ItemUiHelper.DrawItemGlow(spriteBatch, pixel, itemRect, glowColor);
                }

                // Item texture
                Texture2D itemTexture = ResolveItemTexture(item, itemRect.Width, itemRect.Height);

                if (itemTexture != null)
                {
                    spriteBatch.Draw(itemTexture, itemRect, Color.White);

                    if (JewelShineOverlay.ShouldShine(item))
                    {
                        jewelEntries.Add((item, itemRect));
                    }
                }
                else
                {
                    // Placeholder
                    ItemGridRenderHelper.DrawItemPlaceholder(spriteBatch, pixel, font, itemRect, item, Theme.BgLighter, Theme.TextGray * 0.8f);
                }

                // Stack count
                if (item.Definition.BaseDurability == 0 && item.Definition.MagicDurability == 0 && item.Durability > 1)
                {
                    ItemGridRenderHelper.DrawItemStackCount(spriteBatch, font, itemRect, item.Durability, Theme.TextGold, 1f);
                }

                // Level indicator
                if (item.Details.Level > 0)
                {
                    ItemGridRenderHelper.DrawItemLevelBadge(spriteBatch, pixel, font, itemRect, item.Details.Level,
                                       lvl => lvl >= 9 ? Theme.Danger :
                                              lvl >= 7 ? Theme.Accent :
                                              lvl >= 4 ? Theme.Secondary :
                                              Theme.TextGray,
                                       new Color(0, 0, 0, 180));
                }

                if (jewelEntries.Count > 0)
                {
                    JewelShineOverlay.DrawBatch(spriteBatch, jewelEntries, _currentGameTime, Alpha, UiScaler.SpriteTransform);
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

            // Material animation for non-hovered items (if enabled)
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
                    return BmdPreviewRenderer.GetSmoothAnimatedPreview(item, width, height, _currentGameTime);
                }
                catch
                {
                    return null;
                }
            }

            // Use cached static preview
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
            var pixel = GraphicsManager.Instance?.Pixel;
            if (pixel == null)
            {
                return;
            }

            bool isOverGrid = IsMouseOverGrid();

            // Early exit if nothing to draw
            if (!isOverGrid)
            {
                return;
            }

            Rectangle gridRect = Translate(_gridRect);
            var dragged = _pickedItem_renderer_item() ?? VaultControl.Instance?.GetDraggedItem();

            if (dragged != null)
            {
                if (_hoveredSlot.X >= 0 && _hoveredSlot.Y >= 0)
                {
                    bool canPlace = CanPlaceItem(dragged, _hoveredSlot);
                    Color overlay = canPlace ? Color.GreenYellow * 0.5f : Color.Red * 0.6f;

                    Rectangle dropRect = new(
                        gridRect.X + _hoveredSlot.X * INVENTORY_SQUARE_WIDTH,
                        gridRect.Y + _hoveredSlot.Y * INVENTORY_SQUARE_HEIGHT,
                        dragged.Definition.Width * INVENTORY_SQUARE_WIDTH,
                        dragged.Definition.Height * INVENTORY_SQUARE_HEIGHT);

                    spriteBatch.Draw(pixel, dropRect, overlay);
                }
            }
            else
            {
                // Match vault/NPC shop hover overlays: highlight hovered slot and occupied slots only
                ItemGridRenderHelper.DrawGridOverlays(
                    spriteBatch,
                    pixel,
                    DisplayRectangle,
                    _gridRect,
                    _hoveredItem,
                    _hoveredSlot,
                    INVENTORY_SQUARE_WIDTH,
                    INVENTORY_SQUARE_HEIGHT,
                    Theme.SlotHover,
                    Theme.Secondary,
                    Alpha);
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
            Color overlay = layout.AccentRed ? Theme.Danger * 0.45f : Theme.Secondary * 0.45f;
            spriteBatch.Draw(pixel, rect, overlay);

            // Border highlight
            Color light = layout.AccentRed ? Theme.Danger : Theme.Accent;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), light * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Theme.BorderOuter);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), light * 0.6f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Theme.BorderOuter);
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
                if (entry == null || !entry.Visible || string.IsNullOrEmpty(entry.Text))
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
            if (GraphicsManager.Instance?.Pixel == null)
            {
                return;
            }

            DrawCloseButton(spriteBatch);
            DrawFooterButton(spriteBatch, _footerLeftButtonRect, "X", _leftFooterHovered);
            string buttonText = (_networkManager?.GetCharacterState()?.Level >= _repairEnableLevel) ? "R" : "+";
            DrawFooterButton(spriteBatch, _footerRightButtonRect, buttonText, _rightFooterHovered);
        }

        private void DrawCloseButton(SpriteBatch spriteBatch)
        {
            var rect = Translate(_closeButtonRect);
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            bool hovered = _closeHovered;

            // Hover glow
            if (hovered)
            {
                var glowRect = new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6);
                spriteBatch.Draw(pixel, glowRect, Theme.Danger * 0.3f);
            }

            // Button background - circular feel with rounded corners simulated
            Color bgColor = hovered ? new Color(180, 60, 50) : new Color(140, 50, 45);
            spriteBatch.Draw(pixel, rect, bgColor);

            // Highlight
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2),
                            hovered ? new Color(255, 120, 100) : new Color(200, 90, 80));

            // Border
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(100, 30, 25));
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Color(60, 20, 15));

            // X icon
            if (_font != null)
            {
                string text = "X";
                float scale = 0.5f;
                Vector2 size = _font.MeasureString(text) * scale;
                Vector2 pos = new(rect.X + (rect.Width - size.X) / 2, rect.Y + (rect.Height - size.Y) / 2);

                spriteBatch.DrawString(_font, text, pos + Vector2.One, Color.Black * 0.5f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, text, pos, Color.White,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawFooterButton(SpriteBatch spriteBatch, Rectangle rectLocal, string text, bool hovered)
        {
            var rect = Translate(rectLocal);
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Hover glow
            if (hovered)
            {
                var glowRect = new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4);
                spriteBatch.Draw(pixel, glowRect, Theme.Accent * 0.3f);
            }

            // Button background
            Color bgTop = hovered ? Theme.BgLighter : Theme.BgLight;
            Color bgBottom = hovered ? Theme.BgMid : Theme.BgDark;
            UiDrawHelper.DrawVerticalGradient(spriteBatch, rect, bgTop, bgBottom);

            // Border
            Color borderTop = hovered ? Theme.Accent : Theme.BorderInner;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), borderTop);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Theme.BorderOuter);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), borderTop * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Theme.BorderOuter);

            // Inner highlight
            if (hovered)
            {
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 1), Theme.AccentBright * 0.3f);
            }

            // Text
            if (_font != null)
            {
                float scale = 0.55f;
                Vector2 size = _font.MeasureString(text) * scale;
                Vector2 pos = new(rect.X + (rect.Width - size.X) / 2, rect.Y + (rect.Height - size.Y) / 2);

                spriteBatch.DrawString(_font, text, pos + new Vector2(1, 1), Color.Black * 0.6f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, text, pos, hovered ? Theme.AccentBright : Theme.Accent,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (_pickedItem_renderer_item() != null || _hoveredItem == null || _font == null)
                return;

            var lines = ItemUiHelper.BuildTooltipLines(_hoveredItem);
            if (NpcShopControl.IsOpen)
            {
                int sellPrice = ItemPriceCalculator.CalculateSellPrice(_hoveredItem);
                if (sellPrice > 0)
                {
                    lines.Add(($"Sell Price: {sellPrice} Zen", Theme.TextGold));
                }
            }
            else if (_isRepairMode)
            {
                if (Core.Utilities.ItemPriceCalculator.IsRepairable(_hoveredItem))
                {
                    int repairCost = (int)(Core.Utilities.ItemPriceCalculator.CalculateRepairPrice(_hoveredItem, false) * 2.5);
                    if (repairCost > 0)
                    {
                        lines.Add(($"Self Repair Cost: {repairCost} Zen", Theme.TextGold));
                    }
                }
                else
                {
                    lines.Add(("Cannot be repaired", new Color(255, 100, 100)));
                }
            }
            const float scale = 0.44f;
            const int lineSpacing = 4;
            const int paddingX = 14;
            const int paddingY = 12;

            // Calculate tooltip size
            int maxWidth = 0;
            int totalHeight = 0;

            foreach (var (text, _) in lines)
            {
                Vector2 sz = _font.MeasureString(text) * scale;
                maxWidth = Math.Max(maxWidth, (int)MathF.Ceiling(sz.X));
                totalHeight += (int)MathF.Ceiling(sz.Y) + lineSpacing;
            }

            // Add separator after the first line
            totalHeight += 6;

            int tooltipWidth = maxWidth + paddingX * 2;
            int tooltipHeight = totalHeight + paddingY * 2;

            Point mousePosition = MuGame.Instance.UiMouseState.Position;

            // Hovered item position
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

            // Tooltip positioning
            Rectangle tooltipRect = new(mousePosition.X + 16, mousePosition.Y + 16, tooltipWidth, tooltipHeight);
            Rectangle screenBounds = new(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y);

            // Avoid overlapping the item
            if (tooltipRect.Intersects(hoveredItemRect))
            {
                // Try left side
                tooltipRect.X = hoveredItemRect.X - tooltipWidth - 8;
                tooltipRect.Y = hoveredItemRect.Y;

                if (tooltipRect.X < 10 || tooltipRect.Intersects(hoveredItemRect))
                {
                    // Try above
                    tooltipRect.X = hoveredItemRect.X;
                    tooltipRect.Y = hoveredItemRect.Y - tooltipHeight - 8;

                    if (tooltipRect.Y < 10)
                    {
                        // Under the item
                        tooltipRect.X = hoveredItemRect.X;
                        tooltipRect.Y = hoveredItemRect.Bottom + 8;
                    }
                }
            }

            // Clamp to screen bounds
            tooltipRect.X = Math.Clamp(tooltipRect.X, 10, screenBounds.Right - tooltipRect.Width - 10);
            tooltipRect.Y = Math.Clamp(tooltipRect.Y, 10, screenBounds.Bottom - tooltipRect.Height - 10);

            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // ═══════════════════════════════════════════════════════════
            // TOOLTIP BACKGROUND
            // ═══════════════════════════════════════════════════════════

            // Drop shadow
            var shadowRect = new Rectangle(tooltipRect.X + 4, tooltipRect.Y + 4, tooltipRect.Width, tooltipRect.Height);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * 0.5f);

            // Main background
            UiDrawHelper.DrawVerticalGradient(spriteBatch, tooltipRect, new Color(20, 24, 32, 252), new Color(12, 14, 18, 254));

            // Border color based on item rarity
            bool isExcellent = _hoveredItem.Details.IsExcellent;
            bool isAncient = _hoveredItem.Details.IsAncient;
            bool isHighLevel = _hoveredItem.Details.Level >= 7;

            Color borderColor = isExcellent ? Theme.GlowExcellent :
                                isAncient ? Theme.GlowAncient :
                                isHighLevel ? Theme.Accent :
                                Theme.TextWhite;

            // Uniform border all around
            const int borderThickness = 2;
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, tooltipRect.Width, borderThickness), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Bottom - borderThickness, tooltipRect.Width, borderThickness), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, borderThickness, tooltipRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.Right - borderThickness, tooltipRect.Y, borderThickness, tooltipRect.Height), borderColor);

            // ═══════════════════════════════════════════════════════════
            // TOOLTIP TEXT
            // ═══════════════════════════════════════════════════════════

            int textY = tooltipRect.Y + paddingY;
            bool isFirstLine = true;

            foreach (var (text, color) in lines)
            {
                Vector2 textSize = _font.MeasureString(text) * scale;
                int textX = tooltipRect.X + (tooltipRect.Width - (int)textSize.X) / 2;

                // Shadow
                spriteBatch.DrawString(_font, text, new Vector2(textX + 1, textY + 1), Color.Black * 0.7f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                // Text
                Color lineColor = isFirstLine ? borderColor : color;
                spriteBatch.DrawString(_font, text, new Vector2(textX, textY), lineColor,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                textY += (int)textSize.Y + lineSpacing;

                // Separator after item name
                if (isFirstLine)
                {
                    textY += 2;
                    spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X + 8, textY, tooltipRect.Width - 16, 1), borderColor * 0.3f);
                    textY += 4;
                    isFirstLine = false;
                }
            }
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
            return ItemGridRenderHelper.GetSlotAtScreenPosition(DisplayRectangle, _gridRect, Columns, Rows, INVENTORY_SQUARE_WIDTH, INVENTORY_SQUARE_HEIGHT, screenPos);
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
