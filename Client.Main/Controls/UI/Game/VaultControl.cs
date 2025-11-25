using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Client.Main.Networking;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Controls.UI.Game
{
    public class VaultControl : UIControl
    {
        // ═══════════════════════════════════════════════════════════════
        // WINDOW DIMENSIONS
        // ═══════════════════════════════════════════════════════════════
        public const int Columns = 8;
        public const int Rows = 15;
        private const int VAULT_SQUARE_WIDTH = 32;
        private const int VAULT_SQUARE_HEIGHT = 32;

        private const int HEADER_HEIGHT = 46;
        private const int SECTION_HEADER_HEIGHT = 22;
        private const int GRID_PADDING = 10;
        private const int FOOTER_HEIGHT = 50;
        private const int WINDOW_MARGIN = 12;

        private static readonly int GRID_WIDTH = Columns * VAULT_SQUARE_WIDTH;
        private static readonly int GRID_HEIGHT = Rows * VAULT_SQUARE_HEIGHT;
        private static readonly int WINDOW_WIDTH = GRID_WIDTH + GRID_PADDING * 2 + WINDOW_MARGIN * 2;
        private static readonly int WINDOW_HEIGHT = HEADER_HEIGHT + SECTION_HEADER_HEIGHT + GRID_PADDING * 2 + GRID_HEIGHT + FOOTER_HEIGHT + WINDOW_MARGIN;

        // ═══════════════════════════════════════════════════════════════
        // MODERN DARK THEME
        // ═══════════════════════════════════════════════════════════════
        private static class Theme
        {
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BgLight = new(35, 42, 52, 245);

            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color AccentBright = new(255, 215, 120);
            public static readonly Color AccentDim = new(140, 115, 55);
            public static readonly Color AccentGlow = new(255, 200, 80, 40);

            public static readonly Color Secondary = new(90, 140, 200);

            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color BorderHighlight = new(100, 110, 130, 120);

            public static readonly Color SlotBg = new(12, 15, 20, 240);
            public static readonly Color SlotBorder = new(45, 52, 65, 180);
            public static readonly Color SlotHover = new(70, 85, 110, 150);
            public static readonly Color SlotSelected = new(212, 175, 85, 100);

            public static readonly Color GlowNormal = new(150, 150, 150, 25);
            public static readonly Color GlowMagic = new(100, 150, 255, 50);
            public static readonly Color GlowExcellent = new(120, 255, 120, 60);
            public static readonly Color GlowAncient = new(80, 200, 255, 70);
            public static readonly Color GlowLegendary = new(255, 180, 80, 70);

            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color TextGold = new(255, 220, 130);
            public static readonly Color TextGray = new(160, 165, 175);

            public static readonly Color Success = new(80, 200, 120);
            public static readonly Color Danger = new(220, 80, 80);
        }

        private static VaultControl _instance;

        private readonly List<InventoryItem> _items = new();
        private InventoryItem[,] _itemGrid = new InventoryItem[Columns, Rows];

        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private Rectangle _headerRect;
        private Rectangle _gridRect;
        private Rectangle _gridFrameRect;
        private Rectangle _footerRect;
        private Rectangle _zenFieldRect;
        private Rectangle _closeButtonRect;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private SpriteFont _font;

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
        private bool _closeHovered;

        private long _vaultZen;

        // Window drag support
        private bool _isDragging;
        private Point _dragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        private VaultControl(NetworkManager networkManager = null, ILoggerFactory loggerFactory = null)
        {
            _networkManager = networkManager ?? MuGame.Network;
            var factory = loggerFactory ?? MuGame.AppLoggerFactory;
            _logger = factory?.CreateLogger<VaultControl>();

            BuildLayoutMetrics();

            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter;

            EnsureCharacterState();
        }

        public static VaultControl Instance => _instance ??= new VaultControl();

        private void BuildLayoutMetrics()
        {
            _headerRect = new Rectangle(0, 0, WINDOW_WIDTH, HEADER_HEIGHT);

            int gridFrameX = WINDOW_MARGIN;
            int gridFrameY = HEADER_HEIGHT;
            int gridFrameWidth = GRID_WIDTH + GRID_PADDING * 2;
            int gridFrameHeight = SECTION_HEADER_HEIGHT + GRID_PADDING * 2 + GRID_HEIGHT;
            _gridFrameRect = new Rectangle(gridFrameX, gridFrameY, gridFrameWidth, gridFrameHeight);

            _gridRect = new Rectangle(
                gridFrameX + GRID_PADDING,
                gridFrameY + SECTION_HEADER_HEIGHT + GRID_PADDING,
                GRID_WIDTH,
                GRID_HEIGHT);

            _footerRect = new Rectangle(WINDOW_MARGIN, _gridFrameRect.Bottom + 4, _gridFrameRect.Width, FOOTER_HEIGHT - 8);
            _zenFieldRect = new Rectangle(_footerRect.X + 36, _footerRect.Y + 8, _footerRect.Width - 46, 28);
            _closeButtonRect = new Rectangle(WINDOW_WIDTH - 30, 10, 20, 20);
        }

        public override async Task Load()
        {
            await base.Load();
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
                if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) &&
                    MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
                {
                    CloseWindow();
                    return;
                }

                Point mousePos = MuGame.Instance.UiMouseState.Position;
                bool leftPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed;
                bool leftJustPressed = leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;
                bool leftJustReleased = !leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Pressed;

                UpdateChromeHover(mousePos);

                // Handle close button
                if (leftJustPressed && _closeHovered)
                {
                    CloseWindow();
                    return;
                }

                // Handle window dragging
                if (leftJustPressed && IsMouseOverDragArea(mousePos) && !_isDragging && _draggedItem == null)
                {
                    DateTime now = DateTime.Now;
                    if ((now - _lastClickTime).TotalMilliseconds < 500)
                    {
                        Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter;
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

                if (!_isDragging)
                {
                    UpdateHoverState();
                    HandleMouseInput();
                }

                PrecacheAnimatedPreviews();
            }
            else if (_wasVisible)
            {
                HandleVisibilityLost();
            }

            _wasVisible = Visible;
        }

        private bool IsMouseOverDragArea(Point mousePos)
        {
            Rectangle headerScreen = Translate(_headerRect);
            Rectangle closeScreen = Translate(_closeButtonRect);
            return headerScreen.Contains(mousePos) && !closeScreen.Contains(mousePos);
        }

        private void UpdateChromeHover(Point mousePos)
        {
            var closeRect = Translate(_closeButtonRect);
            _closeHovered = closeRect.Contains(mousePos);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            EnsureStaticSurface();

            var gm = GraphicsManager.Instance;
            var spriteBatch = gm?.Sprite;
            if (spriteBatch == null) return;

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

                DrawGridOverlays(spriteBatch);
                DrawVaultItems(spriteBatch);
                DrawCloseButton(spriteBatch);
                DrawZenText(spriteBatch);
            }
            finally
            {
                scope?.Dispose();
            }
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible || _hoveredItem == null) return;

            var gm = GraphicsManager.Instance;
            var spriteBatch = gm?.Sprite;
            if (spriteBatch == null) return;

            SpriteBatchScope scope = null;
            if (!SpriteBatchScope.BatchIsBegun)
            {
                scope = new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform);
            }

            try
            {
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
            if (!Visible) return;
            Visible = false;
            HandleVisibilityLost();
            _wasVisible = false;
        }

        public InventoryItem GetDraggedItem() => _draggedItem;

        public void DrawPickedPreview(SpriteBatch spriteBatch, GameTime gameTime)
        {
            if (_draggedItem == null || spriteBatch == null) return;

            int w = _draggedItem.Definition.Width * VAULT_SQUARE_WIDTH;
            int h = _draggedItem.Definition.Height * VAULT_SQUARE_HEIGHT;

            var mouse = MuGame.Instance.UiMouseState.Position;
            var destRect = new Rectangle(mouse.X - w / 2, mouse.Y - h / 2, w, h);

            // Use cached previews only to avoid render-target switches while a sprite batch is active.
            Texture2D texture = ResolveItemTexture(_draggedItem, w, h, animated: false, allowGenerate: false)
                                ?? ResolveItemTexture(
                                    _draggedItem,
                                    _draggedItem.Definition.Width * InventoryControl.INVENTORY_SQUARE_WIDTH,
                                    _draggedItem.Definition.Height * InventoryControl.INVENTORY_SQUARE_HEIGHT,
                                    animated: false,
                                    allowGenerate: false);

            if (texture != null)
            {
                spriteBatch.Draw(texture, destRect, Color.White * 0.9f);
            }
            else if (GraphicsManager.Instance?.Pixel != null)
            {
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, destRect, Theme.Accent * 0.8f);
            }
        }

        public Point GetSlotAtScreenPosition(Point screenPos)
        {
            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);
            int localX = screenPos.X - gridOrigin.X;
            int localY = screenPos.Y - gridOrigin.Y;

            if (localX < 0 || localY < 0) return new Point(-1, -1);

            int slotX = localX / VAULT_SQUARE_WIDTH;
            int slotY = localY / VAULT_SQUARE_HEIGHT;

            if (slotX < 0 || slotX >= Columns || slotY < 0 || slotY >= Rows)
                return new Point(-1, -1);

            return new Point(slotX, slotY);
        }

        public bool CanPlaceAt(Point gridSlot, InventoryItem item)
        {
            if (item?.Definition == null) return false;

            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gx = gridSlot.X + x;
                    int gy = gridSlot.Y + y;

                    if (gx < 0 || gx >= Columns || gy < 0 || gy >= Rows)
                        return false;

                    var occupant = _itemGrid[gx, gy];
                    if (occupant != null && occupant != item)
                        return false;
                }
            }

            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAWING PRIMITIVES
        // ═══════════════════════════════════════════════════════════════

        private void DrawWindowBackground(SpriteBatch spriteBatch, Rectangle rect)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            spriteBatch.Draw(pixel, rect, Theme.BorderOuter);

            var innerRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
            DrawVerticalGradient(spriteBatch, innerRect, Theme.BgDark, Theme.BgDarkest);

            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 1), Theme.BorderInner * 0.5f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, 1, innerRect.Height), Theme.BorderInner * 0.3f);

            DrawCornerAccents(spriteBatch, rect, Theme.Secondary * 0.5f);
        }

        private void DrawVerticalGradient(SpriteBatch spriteBatch, Rectangle rect, Color top, Color bottom)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int steps = Math.Min(rect.Height, 64);
            int stepHeight = Math.Max(1, rect.Height / steps);

            if (steps <= 1 || rect.Height <= 1)
            {
                spriteBatch.Draw(pixel, rect, bottom);
                return;
            }

            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                Color color = Color.Lerp(top, bottom, t);
                int y = rect.Y + i * stepHeight;
                int height = (i == steps - 1) ? rect.Bottom - y : stepHeight;
                spriteBatch.Draw(pixel, new Rectangle(rect.X, y, rect.Width, height), color);
            }
        }

        private void DrawHorizontalGradient(SpriteBatch spriteBatch, Rectangle rect, Color left, Color right)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int steps = Math.Min(rect.Width, 64);
            int stepWidth = Math.Max(1, rect.Width / steps);

            if (steps <= 1 || rect.Width <= 1)
            {
                spriteBatch.Draw(pixel, rect, right);
                return;
            }

            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                Color color = Color.Lerp(left, right, t);
                int x = rect.X + i * stepWidth;
                int width = (i == steps - 1) ? rect.Right - x : stepWidth;
                spriteBatch.Draw(pixel, new Rectangle(x, rect.Y, width, rect.Height), color);
            }
        }

        private void DrawCornerAccents(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int size = 12;
            int thickness = 2;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, size, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, size), color);

            spriteBatch.Draw(pixel, new Rectangle(rect.Right - size, rect.Y, size, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, size), color);

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, size, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - size, thickness, size), color);

            spriteBatch.Draw(pixel, new Rectangle(rect.Right - size, rect.Bottom - thickness, size, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Bottom - size, thickness, size), color);
        }

        private void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color bgColor, bool withBorder = true)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            spriteBatch.Draw(pixel, rect, bgColor);

            if (withBorder)
            {
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Theme.BorderInner * 0.8f);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Theme.BorderOuter);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Theme.BorderInner * 0.6f);
                spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Theme.BorderOuter);
            }
        }

        private void DrawSectionHeader(SpriteBatch spriteBatch, string title, int x, int y, int width)
        {
            if (_font == null) return;

            float scale = 0.32f;
            Vector2 size = _font.MeasureString(title) * scale;
            float textX = x + (width - size.X) / 2;

            spriteBatch.DrawString(_font, title, new Vector2(textX + 1, y + 1), Color.Black * 0.6f,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, title, new Vector2(textX, y), Theme.TextGold,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawFilledCircle(SpriteBatch spriteBatch, int centerX, int centerY, int radius, Color color)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || radius <= 0) return;

            for (int y = -radius; y <= radius; y++)
            {
                int halfWidth = (int)MathF.Sqrt(radius * radius - y * y);
                if (halfWidth > 0)
                {
                    spriteBatch.Draw(pixel, new Rectangle(centerX - halfWidth, centerY + y, halfWidth * 2, 1), color);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // STATIC SURFACE RENDERING
        // ═══════════════════════════════════════════════════════════════

        private void EnsureStaticSurface()
        {
            if (!_staticSurfaceDirty && _staticSurface != null && !_staticSurface.IsDisposed)
                return;

            var gd = GraphicsManager.Instance?.GraphicsDevice;
            if (gd == null) return;

            _staticSurface?.Dispose();
            _staticSurface = new RenderTarget2D(gd, WINDOW_WIDTH, WINDOW_HEIGHT, false, SurfaceFormat.Color, DepthFormat.None);

            var previousTargets = gd.GetRenderTargets();
            gd.SetRenderTarget(_staticSurface);
            gd.Clear(Color.Transparent);

            var spriteBatch = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {
                DrawStaticElements(spriteBatch);
            }

            gd.SetRenderTargets(previousTargets);
            _staticSurfaceDirty = false;
        }

        private void InvalidateStaticSurface() => _staticSurfaceDirty = true;

        private void DrawStaticElements(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var fullRect = new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT);
            DrawWindowBackground(spriteBatch, fullRect);
            DrawModernHeader(spriteBatch);
            DrawModernGridSection(spriteBatch);
            DrawModernFooter(spriteBatch);
        }

        private void DrawModernHeader(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var headerBg = new Rectangle(8, 6, WINDOW_WIDTH - 16, HEADER_HEIGHT - 8);
            DrawPanel(spriteBatch, headerBg, Theme.BgMid);

            spriteBatch.Draw(pixel, new Rectangle(20, 8, WINDOW_WIDTH - 40, 2), Theme.Secondary * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(30, 10, WINDOW_WIDTH - 60, 1), Theme.Secondary * 0.3f);

            if (_font != null)
            {
                string title = "VAULT";
                float scale = 0.50f;
                Vector2 size = _font.MeasureString(title) * scale;
                Vector2 pos = new((WINDOW_WIDTH - size.X) / 2, (HEADER_HEIGHT - size.Y) / 2 + 2);

                spriteBatch.Draw(pixel, new Rectangle((int)pos.X - 20, (int)pos.Y - 4, (int)size.X + 40, (int)size.Y + 8),
                                new Color(90, 140, 200, 30));

                spriteBatch.DrawString(_font, title, pos + new Vector2(2, 2), Color.Black * 0.5f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, title, pos, Theme.TextWhite,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            int sepY = HEADER_HEIGHT - 2;
            DrawHorizontalGradient(spriteBatch, new Rectangle(20, sepY, (WINDOW_WIDTH - 40) / 2, 1),
                                  Color.Transparent, Theme.BorderInner);
            DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, sepY, (WINDOW_WIDTH - 40) / 2, 1),
                                  Theme.BorderInner, Color.Transparent);
        }

        private void DrawModernGridSection(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            DrawSectionHeader(spriteBatch, "STORED ITEMS", _gridFrameRect.X, _gridFrameRect.Y + 4, _gridFrameRect.Width);
            DrawPanel(spriteBatch, _gridFrameRect, Theme.BgMid);

            spriteBatch.Draw(pixel, _gridRect, Theme.SlotBg);

            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, _gridRect.Width, 2), Color.Black * 0.4f);
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, 2, _gridRect.Height), Color.Black * 0.3f);

            Color gridLine = new(40, 48, 60, 100);
            Color gridLineMajor = new(55, 65, 80, 120);

            for (int x = 1; x < Columns; x++)
            {
                int lineX = _gridRect.X + x * VAULT_SQUARE_WIDTH;
                bool isMajor = x == Columns / 2;
                spriteBatch.Draw(pixel, new Rectangle(lineX, _gridRect.Y, 1, _gridRect.Height), isMajor ? gridLineMajor : gridLine);
            }

            for (int y = 1; y < Rows; y++)
            {
                int lineY = _gridRect.Y + y * VAULT_SQUARE_HEIGHT;
                bool isMajor = y == Rows / 2;
                spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, lineY, _gridRect.Width, 1), isMajor ? gridLineMajor : gridLine);
            }

            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Bottom - 1, _gridRect.Width, 1), Theme.BorderHighlight * 0.2f);
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.Right - 1, _gridRect.Y, 1, _gridRect.Height), Theme.BorderHighlight * 0.15f);
        }

        private void DrawModernFooter(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int sepY = _footerRect.Y - 4;
            DrawHorizontalGradient(spriteBatch, new Rectangle(30, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Color.Transparent, Theme.Secondary * 0.4f);
            DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Theme.Secondary * 0.4f, Color.Transparent);

            DrawPanel(spriteBatch, _footerRect, Theme.BgMid);

            // Zen coin icon
            int coinX = _footerRect.X + 18;
            int coinY = _footerRect.Y + _footerRect.Height / 2;

            DrawFilledCircle(spriteBatch, coinX, coinY, 10, Theme.AccentDim);
            DrawFilledCircle(spriteBatch, coinX, coinY, 7, Theme.Accent);
            DrawFilledCircle(spriteBatch, coinX - 2, coinY - 2, 3, Theme.AccentBright * 0.6f);

            // Zen field background
            DrawPanel(spriteBatch, _zenFieldRect, Theme.SlotBg);
            var innerField = new Rectangle(_zenFieldRect.X + 2, _zenFieldRect.Y + 2,
                                           _zenFieldRect.Width - 4, _zenFieldRect.Height - 4);
            spriteBatch.Draw(pixel, innerField, Theme.BgDarkest * 0.7f);
        }

        // ═══════════════════════════════════════════════════════════════
        // DYNAMIC DRAWING
        // ═══════════════════════════════════════════════════════════════

        private void DrawCloseButton(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var rect = Translate(_closeButtonRect);
            Color btnColor = _closeHovered ? Theme.Accent : Theme.TextGray;

            // Draw X symbol
            int cx = rect.X + rect.Width / 2;
            int cy = rect.Y + rect.Height / 2;
            int halfSize = 6;
            int thickness = 2;

            for (int i = -halfSize; i <= halfSize; i++)
            {
                spriteBatch.Draw(pixel, new Rectangle(cx + i - thickness / 2, cy + i - thickness / 2, thickness, thickness), btnColor);
                spriteBatch.Draw(pixel, new Rectangle(cx + i - thickness / 2, cy - i - thickness / 2, thickness, thickness), btnColor);
            }
        }

        private void DrawZenText(SpriteBatch spriteBatch)
        {
            if (_font == null) return;

            var zenRect = Translate(_zenFieldRect);
            string zenText = _vaultZen.ToString("N0");
            float scale = 0.35f;
            Vector2 size = _font.MeasureString(zenText) * scale;
            Vector2 pos = new(zenRect.X + 8, zenRect.Y + (zenRect.Height - size.Y) / 2);

            spriteBatch.DrawString(_font, zenText, pos, Theme.TextGold * Alpha,
                                  0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawGridOverlays(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);

            // Drag preview highlight
            if (_draggedItem != null && _pendingDropSlot.X >= 0)
            {
                bool canPlace = CanPlaceAt(_pendingDropSlot, _draggedItem);
                Color highlightColor = canPlace ? Theme.Success * 0.4f : Theme.Danger * 0.4f;

                for (int y = 0; y < _draggedItem.Definition.Height; y++)
                {
                    for (int x = 0; x < _draggedItem.Definition.Width; x++)
                    {
                        int sx = _pendingDropSlot.X + x;
                        int sy = _pendingDropSlot.Y + y;

                        if (sx >= 0 && sx < Columns && sy >= 0 && sy < Rows)
                        {
                            var rect = new Rectangle(
                                gridOrigin.X + sx * VAULT_SQUARE_WIDTH,
                                gridOrigin.Y + sy * VAULT_SQUARE_HEIGHT,
                                VAULT_SQUARE_WIDTH, VAULT_SQUARE_HEIGHT);
                            spriteBatch.Draw(pixel, rect, highlightColor);
                        }
                    }
                }
            }

            // Hovered slot highlight (when not dragging item)
            if (_draggedItem == null && _hoveredSlot.X >= 0)
            {
                var rect = new Rectangle(
                    gridOrigin.X + _hoveredSlot.X * VAULT_SQUARE_WIDTH,
                    gridOrigin.Y + _hoveredSlot.Y * VAULT_SQUARE_HEIGHT,
                    VAULT_SQUARE_WIDTH, VAULT_SQUARE_HEIGHT);
                spriteBatch.Draw(pixel, rect, Theme.SlotHover * Alpha);
            }

            // Hovered item multi-slot highlight
            if (_draggedItem == null && _hoveredItem != null)
            {
                for (int y = 0; y < _hoveredItem.Definition.Height; y++)
                {
                    for (int x = 0; x < _hoveredItem.Definition.Width; x++)
                    {
                        int sx = _hoveredItem.GridPosition.X + x;
                        int sy = _hoveredItem.GridPosition.Y + y;

                        if (sx == _hoveredSlot.X && sy == _hoveredSlot.Y)
                            continue;

                        var rect = new Rectangle(
                            gridOrigin.X + sx * VAULT_SQUARE_WIDTH,
                            gridOrigin.Y + sy * VAULT_SQUARE_HEIGHT,
                            VAULT_SQUARE_WIDTH, VAULT_SQUARE_HEIGHT);
                        spriteBatch.Draw(pixel, rect, Theme.Secondary * 0.25f * Alpha);
                    }
                }
            }
        }

        private void DrawVaultItems(SpriteBatch spriteBatch)
        {
            var font = _font ?? GraphicsManager.Instance.Font;
            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);
            var pixel = GraphicsManager.Instance.Pixel;

            foreach (var item in _items)
            {
                if (item == _draggedItem) continue;

                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * VAULT_SQUARE_WIDTH,
                    gridOrigin.Y + item.GridPosition.Y * VAULT_SQUARE_HEIGHT,
                    item.Definition.Width * VAULT_SQUARE_WIDTH,
                    item.Definition.Height * VAULT_SQUARE_HEIGHT);

                bool isHovered = item == _hoveredItem;
                Texture2D texture = ResolveItemTexture(item, rect.Width, rect.Height, isHovered, allowGenerate: true);

                // Glow similar to inventory
                Color glowColor = GetItemGlowColor(item);
                if (glowColor.A > 0 || isHovered)
                {
                    Color finalGlow = isHovered ? Color.Lerp(glowColor, Theme.Accent, 0.4f) : glowColor;
                    finalGlow.A = (byte)Math.Min(255, finalGlow.A + (isHovered ? 40 : 0));
                    DrawItemGlow(spriteBatch, rect, finalGlow);
                }

                // Cell background
                if (pixel != null)
                {
                    var bgRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                    spriteBatch.Draw(pixel, bgRect, isHovered ? Theme.SlotHover : Theme.SlotBg);
                }

                if (texture != null)
                {
                    spriteBatch.Draw(texture, rect, Color.White * Alpha);
                }
                else if (pixel != null)
                {
                    DrawItemPlaceholder(spriteBatch, rect, item);
                }

                if (font != null && item.Definition.BaseDurability == 0 && item.Durability > 1)
                {
                    DrawItemStackCount(spriteBatch, font, rect, item.Durability);
                }

                if (font != null && item.Details.Level > 0)
                {
                    DrawItemLevelBadge(spriteBatch, font, rect, item.Details.Level);
                }
            }
        }

        private void DrawItemStackCount(SpriteBatch spriteBatch, SpriteFont font, Rectangle rect, int quantity)
        {
            string text = quantity.ToString();
            const float scale = 0.38f;
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = new(rect.Right - size.X - 2, rect.Y + 2);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    spriteBatch.DrawString(font, text, pos + new Vector2(dx, dy), Color.Black * Alpha,
                                          0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.DrawString(font, text, pos, Theme.TextGold * Alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private Color GetItemGlowColor(InventoryItem item)
        {
            if (item.Details.IsExcellent) return Theme.GlowExcellent;
            if (item.Details.IsAncient) return Theme.GlowAncient;
            if (item.Details.Level >= 9) return Theme.GlowLegendary;
            if (item.Details.Level >= 5) return Theme.GlowMagic;
            return Theme.GlowNormal;
        }

        private void DrawItemGlow(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int glowSize = 4;
            for (int i = glowSize; i > 0; i--)
            {
                float alpha = (float)(glowSize - i + 1) / glowSize * 0.6f;
                Color layerColor = color * alpha;

                var glowRect = new Rectangle(rect.X - i, rect.Y - i, rect.Width + i * 2, rect.Height + i * 2);

                spriteBatch.Draw(pixel, new Rectangle(glowRect.X, glowRect.Y, glowRect.Width, 1), layerColor);
                spriteBatch.Draw(pixel, new Rectangle(glowRect.X, glowRect.Bottom - 1, glowRect.Width, 1), layerColor);
                spriteBatch.Draw(pixel, new Rectangle(glowRect.X, glowRect.Y, 1, glowRect.Height), layerColor);
                spriteBatch.Draw(pixel, new Rectangle(glowRect.Right - 1, glowRect.Y, 1, glowRect.Height), layerColor);
            }
        }

        private void DrawItemPlaceholder(SpriteBatch spriteBatch, Rectangle rect, InventoryItem item)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            spriteBatch.Draw(pixel, rect, Theme.BgLight);

            if (_font != null && item.Definition.Name != null)
            {
                string shortName = item.Definition.Name.Length > 5
                    ? item.Definition.Name[..5] + ".."
                    : item.Definition.Name;

                float scale = 0.24f;
                Vector2 size = _font.MeasureString(shortName) * scale;
                Vector2 pos = new(rect.X + (rect.Width - size.X) / 2, rect.Y + (rect.Height - size.Y) / 2);

                spriteBatch.DrawString(_font, shortName, pos, Theme.TextGray * 0.8f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawItemLevelBadge(SpriteBatch spriteBatch, SpriteFont font, Rectangle rect, int level)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            string text = $"+{level}";
            const float scale = 0.30f;

            Vector2 textSize = font.MeasureString(text) * scale;
            Vector2 pos = new(rect.X + 2, rect.Bottom - textSize.Y - 2);

            Color levelColor = level >= 9 ? Theme.Danger :
                               level >= 7 ? Theme.Secondary :
                               level >= 4 ? Theme.Secondary :
                               Theme.TextGray;

            var bgRect = new Rectangle((int)pos.X - 2, (int)pos.Y - 1, (int)textSize.X + 4, (int)textSize.Y + 2);
            spriteBatch.Draw(pixel, bgRect, new Color(0, 0, 0, 180));

            spriteBatch.DrawString(font, text, pos + Vector2.One, Color.Black * 0.8f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, pos, levelColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (_hoveredItem == null || _font == null) return;

            var lines = BuildTooltipLines(_hoveredItem);
            const float scale = 0.44f;
            const int lineSpacing = 4;
            const int paddingX = 14;
            const int paddingY = 12;

            int maxWidth = 0;
            int totalHeight = 0;
            foreach (var (text, _) in lines)
            {
                Vector2 sz = _font.MeasureString(text) * scale;
                maxWidth = Math.Max(maxWidth, (int)MathF.Ceiling(sz.X));
                totalHeight += (int)MathF.Ceiling(sz.Y) + lineSpacing;
            }
            totalHeight += 6; // separator after first line

            int tooltipWidth = maxWidth + paddingX * 2;
            int tooltipHeight = totalHeight + paddingY * 2;

            Point mouse = MuGame.Instance.UiMouseState.Position;
            var itemRect = new Rectangle(
                DisplayRectangle.X + _gridRect.X + _hoveredItem.GridPosition.X * VAULT_SQUARE_WIDTH,
                DisplayRectangle.Y + _gridRect.Y + _hoveredItem.GridPosition.Y * VAULT_SQUARE_HEIGHT,
                _hoveredItem.Definition.Width * VAULT_SQUARE_WIDTH,
                _hoveredItem.Definition.Height * VAULT_SQUARE_HEIGHT);

            Rectangle tooltipRect = new(mouse.X + 16, mouse.Y + 16, tooltipWidth, tooltipHeight);
            Rectangle screenBounds = new(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y);

            if (tooltipRect.Intersects(itemRect))
            {
                tooltipRect.X = itemRect.X - tooltipWidth - 8;
                tooltipRect.Y = itemRect.Y;

                if (tooltipRect.X < 10 || tooltipRect.Intersects(itemRect))
                {
                    tooltipRect.X = itemRect.X;
                    tooltipRect.Y = itemRect.Y - tooltipHeight - 8;

                    if (tooltipRect.Y < 10)
                    {
                        tooltipRect.X = itemRect.X;
                        tooltipRect.Y = itemRect.Bottom + 8;
                    }
                }
            }

            tooltipRect.X = Math.Clamp(tooltipRect.X, 10, screenBounds.Right - tooltipRect.Width - 10);
            tooltipRect.Y = Math.Clamp(tooltipRect.Y, 10, screenBounds.Bottom - tooltipRect.Height - 10);

            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var shadowRect = new Rectangle(tooltipRect.X + 4, tooltipRect.Y + 4, tooltipRect.Width, tooltipRect.Height);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * 0.5f);

            DrawVerticalGradient(spriteBatch, tooltipRect, new Color(20, 24, 32, 252), new Color(12, 14, 18, 254));

            bool isExcellent = _hoveredItem.Details.IsExcellent;
            bool isAncient = _hoveredItem.Details.IsAncient;
            bool isHighLevel = _hoveredItem.Details.Level >= 7;

            Color borderColor = isExcellent ? Theme.GlowExcellent :
                                isAncient ? Theme.GlowAncient :
                                isHighLevel ? Theme.Accent :
                                Theme.TextWhite;

            const int borderThickness = 2;
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, tooltipRect.Width, borderThickness), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Bottom - borderThickness, tooltipRect.Width, borderThickness), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, borderThickness, tooltipRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.Right - borderThickness, tooltipRect.Y, borderThickness, tooltipRect.Height), borderColor);

            int textY = tooltipRect.Y + paddingY;
            bool firstLine = true;
            foreach (var (text, color) in lines)
            {
                Vector2 textSize = _font.MeasureString(text) * scale;
                int textX = tooltipRect.X + (tooltipRect.Width - (int)textSize.X) / 2;

                spriteBatch.DrawString(_font, text, new Vector2(textX + 1, textY + 1), Color.Black * 0.7f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                Color lineColor = firstLine ? borderColor : color;
                spriteBatch.DrawString(_font, text, new Vector2(textX, textY), lineColor,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                textY += (int)textSize.Y + lineSpacing;

                if (firstLine)
                {
                    textY += 2;
                    spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X + 8, textY, tooltipRect.Width - 16, 1), borderColor * 0.3f);
                    textY += 4;
                    firstLine = false;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // INPUT HANDLING
        // ═══════════════════════════════════════════════════════════════

        private void HandleMouseInput()
        {
            var mouse = MuGame.Instance.UiMouseState;
            var prev = MuGame.Instance.PrevUiMouseState;

            bool leftJustPressed = mouse.LeftButton == ButtonState.Pressed &&
                                   prev.LeftButton == ButtonState.Released;

            if (!leftJustPressed) return;

            Point mousePos = mouse.Position;

            if (_draggedItem == null)
            {
                if (_hoveredItem != null)
                {
                    BeginDrag(_hoveredItem);
                    Scene?.SetMouseInputConsumed();
                }
                return;
            }

            AttemptDrop(mousePos);
            Scene?.SetMouseInputConsumed();
        }

        private void UpdateHoverState()
        {
            var mouse = MuGame.Instance.UiMouseState.Position;

            if (_draggedItem != null)
            {
                var dropSlot = GetSlotAtScreenPosition(mouse);
                _pendingDropSlot = (dropSlot.X >= 0 && CanPlaceAt(dropSlot, _draggedItem)) ? dropSlot : new Point(-1, -1);
                _hoveredItem = null;
                _hoveredSlot = dropSlot;
                return;
            }

            _hoveredSlot = GetSlotAtScreenPosition(mouse);
            _hoveredItem = GetItemAt(mouse);
        }

        private void PrecacheAnimatedPreviews()
        {
            if (_currentGameTime == null)
                return;

            CacheAnimatedPreview(_hoveredItem);
            CacheAnimatedPreview(_draggedItem);
        }

        private void CacheAnimatedPreview(InventoryItem item)
        {
            if (item == null)
                return;

            int w = item.Definition.Width * VAULT_SQUARE_WIDTH;
            int h = item.Definition.Height * VAULT_SQUARE_HEIGHT;
            var key = (item, w, h, true);
            if (_bmdPreviewCache.TryGetValue(key, out var existing) && existing != null)
            {
                return;
            }

            try
            {
                var animated = BmdPreviewRenderer.GetAnimatedPreview(item, w, h, _currentGameTime);
                if (animated != null)
                {
                    _bmdPreviewCache[key] = animated;
                }
            }
            catch
            {
                // ignore and leave cache empty; will fall back to static
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAG & DROP
        // ═══════════════════════════════════════════════════════════════

        private void BeginDrag(InventoryItem item)
        {
            _draggedItem = item;
            _draggedOriginalSlot = item.GridPosition;
            RemoveItemFromGrid(item);
            _hoveredItem = null;
            _pendingDropSlot = new Point(-1, -1);

            // Pre-cache drag preview while we're outside the draw pass to avoid render-target thrashing.
            int w = item.Definition.Width * VAULT_SQUARE_WIDTH;
            int h = item.Definition.Height * VAULT_SQUARE_HEIGHT;
            _ = ResolveItemTexture(item, w, h, animated: false);
        }

        private void AttemptDrop(Point mousePos)
        {
            if (_draggedItem == null) return;

            var dropSlot = GetSlotAtScreenPosition(mousePos);
            var inventory = InventoryControl.Instance;
            bool dropped = false;

            if (dropSlot.X >= 0 && CanPlaceAt(dropSlot, _draggedItem))
            {
                PlaceDraggedItem(dropSlot);
                int w = _draggedItem.Definition.Width * VAULT_SQUARE_WIDTH;
                int h = _draggedItem.Definition.Height * VAULT_SQUARE_HEIGHT;
                _ = ResolveItemTexture(_draggedItem, w, h, animated: Constants.ENABLE_ITEM_MATERIAL_ANIMATION);
                if (dropSlot != _draggedOriginalSlot)
                {
                    SendVaultMove(_draggedOriginalSlot, dropSlot);
                }
                dropped = true;
            }
            else if (inventory != null && inventory.Visible && inventory.DisplayRectangle.Contains(mousePos))
            {
                Point invSlot = inventory.GetSlotAtScreenPositionPublic(mousePos);
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
            if (_draggedItem == null) return;
            PlaceItemOnGrid(_draggedItem, newSlot);
        }

        private void MoveItemToInventory(Point targetSlot, InventoryControl inventory)
        {
            if (_draggedItem == null) return;

            byte fromSlot = (byte)(_draggedOriginalSlot.Y * Columns + _draggedOriginalSlot.X);
            byte toSlot = (byte)(InventoryControl.InventorySlotOffsetConstant +
                                 (targetSlot.Y * InventoryControl.Columns) + targetSlot.X);

            var svc = _networkManager?.GetCharacterService();
            var state = _networkManager?.GetCharacterState();
            var version = _networkManager?.TargetVersion ?? TargetProtocolVersion.Season6;

            if (svc != null && state != null)
            {
                state.StashPendingVaultMove(fromSlot, 0xFF);

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

            if (_networkManager == null) return;

            var svc = _networkManager.GetCharacterService();
            var state = _networkManager.GetCharacterState();

            if (svc == null || state == null) return;

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

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private Rectangle Translate(Rectangle rect)
            => new(DisplayRectangle.X + rect.X, DisplayRectangle.Y + rect.Y, rect.Width, rect.Height);

        private InventoryItem GetItemAt(Point mousePos)
        {
            if (!DisplayRectangle.Contains(mousePos)) return null;

            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);

            foreach (var item in _items)
            {
                if (item == _draggedItem) continue;

                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * VAULT_SQUARE_WIDTH,
                    gridOrigin.Y + item.GridPosition.Y * VAULT_SQUARE_HEIGHT,
                    item.Definition.Width * VAULT_SQUARE_WIDTH,
                    item.Definition.Height * VAULT_SQUARE_HEIGHT);

                if (rect.Contains(mousePos)) return item;
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
            _isDragging = false;
        }

        private void SendCloseNpcRequest()
        {
            if (_closeRequestSent) return;
            _closeRequestSent = true;
            var svc = _networkManager?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendCloseNpcRequestAsync();
            }
        }

        private void EnsureCharacterState()
        {
            if (_characterState != null) return;

            _characterState = MuGame.Network?.GetCharacterState();
            if (_characterState != null)
            {
                _characterState.VaultItemsChanged += RefreshVaultContent;
            }
        }

        private void RefreshVaultContent()
        {
            if (_characterState == null) return;

            bool wasVisible = Visible;

            _items.Clear();
            ClearGrid();

            // TODO: Get actual vault zen from server/character state
            _vaultZen = 0;

            var vaultItems = _characterState.GetVaultItems();
            foreach (var kv in vaultItems)
            {
                byte slot = kv.Key;
                byte[] data = kv.Value;

                int gridX = slot % Columns;
                int gridY = slot / Columns;

                var def = ItemDatabase.GetItemDefinition(data)
                    ?? new ItemDefinition(0, ItemDatabase.GetItemName(data) ?? "Unknown Item", 1, 1, "Interface/newui_item_box.tga");

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

            QueueWarmup();

            if (_items.Count > 0 || _vaultZen > 0)
            {
                Visible = true;
                BringToFront();
                // Play the open sound only when the window becomes visible, not on every refresh.
                if (!wasVisible)
                {
                    SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
                }
                _closeRequestSent = false;
                _isDragging = false;
            }
        }

        private void QueueWarmup()
        {
            if (_warmupPending) return;
            _warmupPending = true;
            MuGame.ScheduleOnMainThread(WarmupTextures);
        }

        private void WarmupTextures()
        {
            _warmupPending = false;

            if (GraphicsManager.Instance?.Sprite == null)
            {
                QueueWarmup();
                return;
            }

            foreach (var item in _items)
            {
                int w = item.Definition.Width * VAULT_SQUARE_WIDTH;
                int h = item.Definition.Height * VAULT_SQUARE_HEIGHT;
                _ = ResolveItemTexture(item, w, h, animated: false);
            }
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

        private void ClearGrid() => Array.Clear(_itemGrid, 0, _itemGrid.Length);

        private Texture2D ResolveItemTexture(InventoryItem item, int width, int height, bool animated, bool allowGenerate = true)
        {
            if (item?.Definition == null) return null;

            string texturePath = item.Definition.TexturePath;
            if (string.IsNullOrEmpty(texturePath)) return null;

            bool isBmd = texturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase);

            if (!isBmd)
            {
                if (_itemTextureCache.TryGetValue(texturePath, out var cached) && cached != null)
                    return cached;

                var tex = TextureLoader.Instance.GetTexture2D(texturePath);
                if (tex != null) _itemTextureCache[texturePath] = tex;
                return tex;
            }

            bool isHovered = animated;
            if (!allowGenerate)
            {
                var rendererCached = BmdPreviewRenderer.TryGetCachedPreview(item, width, height);
                if (rendererCached != null)
                    return rendererCached;
                return null;
            }

            // Match inventory behavior: material animation when idle, rotation when hovered
            if (!isHovered && Constants.ENABLE_ITEM_MATERIAL_ANIMATION)
            {
                try
                {
                    var mat = BmdPreviewRenderer.GetMaterialAnimatedPreview(item, width, height, _currentGameTime);
                    if (mat != null)
                    {
                        return mat;
                    }
                }
                catch { }
            }

            if (isHovered)
            {
                try
                {
                    var animatedTexture = BmdPreviewRenderer.GetAnimatedPreview(item, width, height, _currentGameTime);
                    if (animatedTexture != null)
                    {
                        return animatedTexture;
                    }
                }
                catch { return null; }
            }

            var cacheKey = (item, width, height, false);
            if (_bmdPreviewCache.TryGetValue(cacheKey, out var cachedPreview) && cachedPreview != null)
                return cachedPreview;

            try
            {
                var preview = BmdPreviewRenderer.GetPreview(item, width, height);
                if (preview != null)
                {
                    _bmdPreviewCache[cacheKey] = preview;
                }
                return preview;
            }
            catch { return null; }
        }

        private static List<(string text, Color color)> BuildTooltipLines(InventoryItem item)
        {
            var details = item.Details;
            var lines = new List<(string, Color)>();

            string name = details.IsExcellent ? $"Excellent {item.Definition.Name}"
                        : details.IsAncient ? $"Ancient {item.Definition.Name}"
                        : item.Definition.Name;

            if (details.Level > 0)
                name += $" +{details.Level}";

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
                    lines.Add(($"Can be equipped by {cls}", Color.LightGray));
            }

            if (details.OptionLevel > 0)
                lines.Add(($"Additional Option : +{details.OptionLevel * 4}", new Color(80, 255, 80)));

            if (details.HasLuck) lines.Add(("+Luck  (Crit +5 %, Jewel +25 %)", Color.CornflowerBlue));
            if (details.HasSkill) lines.Add(("+Skill (Right mouse click - skill)", Color.CornflowerBlue));

            if (details.IsExcellent)
            {
                byte excByte = item.RawData.Length > 3 ? item.RawData[3] : (byte)0;
                foreach (var option in ItemDatabase.ParseExcellentOptions(excByte))
                    lines.Add(($"+{option}", new Color(128, 255, 128)));
            }

            if (details.IsAncient)
                lines.Add(("Ancient Option", new Color(0, 255, 128)));

            return lines;
        }
    }
}
