using System;
using System.Collections.Generic;
using System.Linq;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI.Game
{
    public class NpcShopControl : UIControl
    {
        // ═══════════════════════════════════════════════════════════════
        // WINDOW DIMENSIONS
        // ═══════════════════════════════════════════════════════════════
        private const int SHOP_COLUMNS = 8;
        private const int SHOP_ROWS = 14;
        private const int SHOP_SQUARE_WIDTH = 32;
        private const int SHOP_SQUARE_HEIGHT = 32;

        private const int HEADER_HEIGHT = 46;
        private const int SECTION_HEADER_HEIGHT = 22;
        private const int GRID_PADDING = 10;
        private const int FOOTER_HEIGHT = 46;
        private const int WINDOW_MARGIN = 12;

        private static readonly int GRID_WIDTH = SHOP_COLUMNS * SHOP_SQUARE_WIDTH;
        private static readonly int GRID_HEIGHT = SHOP_ROWS * SHOP_SQUARE_HEIGHT;
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
        }

        private static NpcShopControl _instance;

        private readonly List<InventoryItem> _items = new();
        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private Rectangle _headerRect;
        private Rectangle _gridRect;
        private Rectangle _gridFrameRect;
        private Rectangle _footerRect;
        private Rectangle _closeButtonRect;

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
        private bool _closeHovered;

        // Drag support
        private bool _isDragging;
        private Point _dragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        private NpcShopControl()
        {
            BuildLayoutMetrics();

            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter;

            EnsureCharacterState();
        }

        public static NpcShopControl Instance => _instance ??= new NpcShopControl();

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
            _closeButtonRect = new Rectangle(WINDOW_WIDTH - 30, 10, 20, 20);
        }

        public override async System.Threading.Tasks.Task Load()
        {
            await base.Load();
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

                if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) &&
                    MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
                {
                    Visible = false;
                    HandleVisibilityLost();
                    _wasVisible = false;
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
                    Visible = false;
                    HandleVisibilityLost();
                    return;
                }

                // Handle window dragging
                if (leftJustPressed && IsMouseOverDragArea(mousePos) && !_isDragging)
                {
                    DateTime now = DateTime.Now;
                    if ((now - _lastClickTime).TotalMilliseconds < 500)
                    {
                        // Double-click to reset position
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
                DrawShopItems(spriteBatch);
                DrawCloseButton(spriteBatch);
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

            DrawCornerAccents(spriteBatch, rect, Theme.Accent * 0.4f);
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

            spriteBatch.Draw(pixel, new Rectangle(20, 8, WINDOW_WIDTH - 40, 2), Theme.Accent * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(30, 10, WINDOW_WIDTH - 60, 1), Theme.AccentDim * 0.4f);

            if (_font != null)
            {
                string title = "NPC SHOP";
                float scale = 0.50f;
                Vector2 size = _font.MeasureString(title) * scale;
                Vector2 pos = new((WINDOW_WIDTH - size.X) / 2, (HEADER_HEIGHT - size.Y) / 2 + 2);

                spriteBatch.Draw(pixel, new Rectangle((int)pos.X - 20, (int)pos.Y - 4, (int)size.X + 40, (int)size.Y + 8),
                                Theme.AccentGlow * 0.3f);

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

            DrawSectionHeader(spriteBatch, "ITEMS FOR SALE", _gridFrameRect.X, _gridFrameRect.Y + 4, _gridFrameRect.Width);
            DrawPanel(spriteBatch, _gridFrameRect, Theme.BgMid);

            spriteBatch.Draw(pixel, _gridRect, Theme.SlotBg);

            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, _gridRect.Width, 2), Color.Black * 0.4f);
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, 2, _gridRect.Height), Color.Black * 0.3f);

            Color gridLine = new(40, 48, 60, 100);
            Color gridLineMajor = new(55, 65, 80, 120);

            for (int x = 1; x < SHOP_COLUMNS; x++)
            {
                int lineX = _gridRect.X + x * SHOP_SQUARE_WIDTH;
                bool isMajor = x == SHOP_COLUMNS / 2;
                spriteBatch.Draw(pixel, new Rectangle(lineX, _gridRect.Y, 1, _gridRect.Height), isMajor ? gridLineMajor : gridLine);
            }

            for (int y = 1; y < SHOP_ROWS; y++)
            {
                int lineY = _gridRect.Y + y * SHOP_SQUARE_HEIGHT;
                bool isMajor = y == SHOP_ROWS / 2;
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
                                  Color.Transparent, Theme.Accent * 0.4f);
            DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Theme.Accent * 0.4f, Color.Transparent);

            DrawPanel(spriteBatch, _footerRect, Theme.BgMid);

            if (_font != null)
            {
                string hint = "Click item to buy";
                float scale = 0.38f;
                Vector2 size = _font.MeasureString(hint) * scale;
                Vector2 pos = new(_footerRect.X + (_footerRect.Width - size.X) / 2,
                                  _footerRect.Y + (_footerRect.Height - size.Y) / 2);

                spriteBatch.DrawString(_font, hint, pos + Vector2.One, Color.Black * 0.5f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, hint, pos, Theme.TextGold,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
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

            // Draw diagonal lines for X
            for (int i = -halfSize; i <= halfSize; i++)
            {
                spriteBatch.Draw(pixel, new Rectangle(cx + i - thickness / 2, cy + i - thickness / 2, thickness, thickness), btnColor);
                spriteBatch.Draw(pixel, new Rectangle(cx + i - thickness / 2, cy - i - thickness / 2, thickness, thickness), btnColor);
            }
        }

        private void DrawGridOverlays(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);

            if (_hoveredSlot.X >= 0)
            {
                var rect = new Rectangle(
                    gridOrigin.X + _hoveredSlot.X * SHOP_SQUARE_WIDTH,
                    gridOrigin.Y + _hoveredSlot.Y * SHOP_SQUARE_HEIGHT,
                    SHOP_SQUARE_WIDTH, SHOP_SQUARE_HEIGHT);
                spriteBatch.Draw(pixel, rect, Theme.SlotHover * Alpha);
            }

            if (_hoveredItem != null)
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
                            gridOrigin.X + sx * SHOP_SQUARE_WIDTH,
                            gridOrigin.Y + sy * SHOP_SQUARE_HEIGHT,
                            SHOP_SQUARE_WIDTH, SHOP_SQUARE_HEIGHT);
                        spriteBatch.Draw(pixel, rect, Theme.Accent * 0.25f * Alpha);
                    }
                }
            }
        }

        private void DrawShopItems(SpriteBatch spriteBatch)
        {
            var font = _font ?? GraphicsManager.Instance.Font;
            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);
            var pixel = GraphicsManager.Instance.Pixel;

            foreach (var item in _items)
            {
                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * SHOP_SQUARE_WIDTH,
                    gridOrigin.Y + item.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                    item.Definition.Width * SHOP_SQUARE_WIDTH,
                    item.Definition.Height * SHOP_SQUARE_HEIGHT);

                bool isHovered = item == _hoveredItem;
                Texture2D texture = ResolveItemTexture(item, rect.Width, rect.Height, isHovered);

                // Glow similar to inventory/vault
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

            Color levelColor = level >= 9 ? Theme.AccentBright :
                               level >= 7 ? Theme.Accent :
                               level >= 4 ? Theme.AccentDim :
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
            totalHeight += 6;

            int tooltipWidth = maxWidth + paddingX * 2;
            int tooltipHeight = totalHeight + paddingY * 2;

            Point mouse = MuGame.Instance.UiMouseState.Position;
            var itemRect = new Rectangle(
                DisplayRectangle.X + _gridRect.X + _hoveredItem.GridPosition.X * SHOP_SQUARE_WIDTH,
                DisplayRectangle.Y + _gridRect.Y + _hoveredItem.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                _hoveredItem.Definition.Width * SHOP_SQUARE_WIDTH,
                _hoveredItem.Definition.Height * SHOP_SQUARE_HEIGHT);

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

            if (DisplayRectangle.Contains(mousePos))
            {
                Scene?.SetMouseInputConsumed();
            }

            if (_hoveredItem == null) return;

            byte slot = (byte)(_hoveredItem.GridPosition.Y * SHOP_COLUMNS + _hoveredItem.GridPosition.X);
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendBuyItemFromNpcRequestAsync(slot);
            }
        }

        private void UpdateHoverState()
        {
            var mousePos = MuGame.Instance.UiMouseState.Position;
            _hoveredSlot = GetSlotAtScreenPosition(mousePos);
            _hoveredItem = GetItemAt(mousePos);
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private Rectangle Translate(Rectangle rect)
            => new(DisplayRectangle.X + rect.X, DisplayRectangle.Y + rect.Y, rect.Width, rect.Height);

        private Point GetSlotAtScreenPosition(Point screenPos)
        {
            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);
            int localX = screenPos.X - gridOrigin.X;
            int localY = screenPos.Y - gridOrigin.Y;

            if (localX < 0 || localY < 0) return new Point(-1, -1);

            int slotX = localX / SHOP_SQUARE_WIDTH;
            int slotY = localY / SHOP_SQUARE_HEIGHT;

            if (slotX < 0 || slotX >= SHOP_COLUMNS || slotY < 0 || slotY >= SHOP_ROWS)
                return new Point(-1, -1);

            return new Point(slotX, slotY);
        }

        private InventoryItem GetItemAt(Point mousePos)
        {
            if (!DisplayRectangle.Contains(mousePos)) return null;

            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);

            foreach (var item in _items)
            {
                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * SHOP_SQUARE_WIDTH,
                    gridOrigin.Y + item.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                    item.Definition.Width * SHOP_SQUARE_WIDTH,
                    item.Definition.Height * SHOP_SQUARE_HEIGHT);

                if (rect.Contains(mousePos)) return item;
            }

            return null;
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
            _isDragging = false;
        }

        private void SendCloseNpcRequest()
        {
            if (_closeRequestSent) return;
            _closeRequestSent = true;
            var svc = MuGame.Network?.GetCharacterService();
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
                _characterState.ShopItemsChanged += RefreshShopContent;
            }
        }

        private void RefreshShopContent()
        {
            if (_characterState == null) return;

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

                var def = ItemDatabase.GetItemDefinition(data)
                    ?? new ItemDefinition(0, ItemDatabase.GetItemName(data) ?? "Unknown Item", 1, 1, "Interface/newui_item_box.tga");

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
                int w = item.Definition.Width * SHOP_SQUARE_WIDTH;
                int h = item.Definition.Height * SHOP_SQUARE_HEIGHT;
                _ = ResolveItemTexture(item, w, h, animated: false);
            }
        }

        private Texture2D ResolveItemTexture(InventoryItem item, int width, int height, bool animated)
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

            var cacheKey = (item, width, height, animated);
            if (_bmdPreviewCache.TryGetValue(cacheKey, out var cachedPreview) && cachedPreview != null)
                return cachedPreview;

            var staticKey = (item, width, height, false);
            if (_bmdPreviewCache.TryGetValue(staticKey, out var staticPreview) && staticPreview != null)
                return staticPreview;

            bool isHovered = animated;

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
                        _bmdPreviewCache[cacheKey] = animatedTexture;
                        return animatedTexture;
                    }
                }
                catch { return null; }
            }

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
