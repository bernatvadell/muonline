using System;
using System.Collections.Generic;
using System.Linq;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game.Common;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Controls.UI;
using Client.Main.Models;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI.Game
{
    public class NpcShopControl : UIControl
    {
        // ═══════════════════════════════════════════════════════════════
        // SHOP MODE
        // ═══════════════════════════════════════════════════════════════
        public enum ShopMode
        {
            BuyAndSell = 1,
            Repair = 2
        }

        // ═══════════════════════════════════════════════════════════════
        // WINDOW DIMENSIONS
        // ═══════════════════════════════════════════════════════════════
        private const int SHOP_COLUMNS = 8;
        private const int SHOP_ROWS = 15;
        private const int SHOP_SQUARE_WIDTH = 32;
        private const int SHOP_SQUARE_HEIGHT = 32;

        private const int HEADER_HEIGHT = 46;
        private const int SECTION_HEADER_HEIGHT = 22;
        private const int GRID_PADDING = 10;
        private const int BUTTON_AREA_HEIGHT = 40;
        private const int FOOTER_HEIGHT = 46;
        private const int WINDOW_MARGIN = 12;

        private static readonly int GRID_WIDTH = SHOP_COLUMNS * SHOP_SQUARE_WIDTH;
        private static readonly int GRID_HEIGHT = SHOP_ROWS * SHOP_SQUARE_HEIGHT;
        private static readonly int WINDOW_WIDTH = GRID_WIDTH + GRID_PADDING * 2 + WINDOW_MARGIN * 2;
        private int WindowHeight => HEADER_HEIGHT + SECTION_HEADER_HEIGHT + GRID_PADDING * 2 + GRID_HEIGHT + (_isRepairShop ? BUTTON_AREA_HEIGHT : 0) + FOOTER_HEIGHT + WINDOW_MARGIN;

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

        private static readonly ItemGlowPalette GlowPalette = new(
            Theme.GlowNormal,
            Theme.GlowMagic,
            Theme.GlowExcellent,
            Theme.GlowAncient,
            Theme.GlowLegendary);

        private static NpcShopControl _instance;

        private readonly List<InventoryItem> _items = new();
        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private Rectangle _headerRect;
        private Rectangle _gridRect;
        private Rectangle _gridFrameRect;
        private Rectangle _buttonAreaRect;
        private Rectangle _footerRect;
        private Rectangle _closeButtonRect;
        private Rectangle _repairButtonRect;
        private Rectangle _repairAllButtonRect;
        private bool _repairButtonHovered;
        private bool _repairAllButtonHovered;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private SpriteFont _font;
        private CharacterState _characterState;

        private InventoryItem _hoveredItem;
        private Point _hoveredSlot = new(-1, -1);
        private GameTime _currentGameTime;

        private bool _wasVisible;
        private bool _closeRequestSent;
        private bool _closeHovered;
        private bool _pendingShow;
        private bool _warmupComplete;

        // Drag support
        private bool _isDragging;
        private Point _dragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        // Repair mode
        private ShopMode _shopMode = ShopMode.BuyAndSell;
        private bool _isRepairShop = false;

        private NpcShopControl()
        {
            BuildLayoutMetrics();

            ControlSize = new Point(WINDOW_WIDTH, WindowHeight);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.VerticalCenter | ControlAlign.Left;

            EnsureCharacterState();
        }

        public static NpcShopControl Instance => _instance ??= new NpcShopControl();
        public static bool IsOpen => _instance?.Visible == true;

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

        private void BuildLayoutMetrics()
        {
            int buttonAreaHeight = _isRepairShop ? BUTTON_AREA_HEIGHT : 0;

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

            _buttonAreaRect = new Rectangle(WINDOW_MARGIN, _gridFrameRect.Bottom + 2, _gridFrameRect.Width, buttonAreaHeight);
            _footerRect = new Rectangle(WINDOW_MARGIN, _buttonAreaRect.Bottom + 4, _gridFrameRect.Width, FOOTER_HEIGHT - 8);
            _closeButtonRect = new Rectangle(WINDOW_WIDTH - 30, 10, 20, 20);

            // Repair buttons in button area
            int buttonWidth = 100;
            int buttonHeight = 29;
            int buttonSpacing = 10;
            int buttonY = _buttonAreaRect.Y + (_buttonAreaRect.Height - buttonHeight) / 2;
            int startX = _buttonAreaRect.X + 10;

            _repairButtonRect = new Rectangle(startX, buttonY, buttonWidth, buttonHeight);
            _repairAllButtonRect = new Rectangle(startX + buttonWidth + buttonSpacing, buttonY, buttonWidth, buttonHeight);
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

            // Handle deferred show - wait one frame after warmup to avoid black screen
            if (_pendingShow && !Visible)
            {
                if (_warmupComplete)
                {
                    // Warmup done in previous frame, now safe to show
                    Visible = true;
                    BringToFront();
                    SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
                    _pendingShow = false;
                    _warmupComplete = false;
                }
                else
                {
                    // Do warmup this frame, show next frame
                    WarmupTexturesSync();
                    InvalidateStaticSurface();
                    EnsureStaticSurface();
                    _warmupComplete = true;
                }
            }

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

                // Handle 'L' key for repair mode toggle (only if repair shop and no dragged item)
                if (_isRepairShop &&
                    MuGame.Instance.Keyboard.IsKeyDown(Keys.L) &&
                    MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.L))
                {
                    // Only toggle if not dragging an item
                    if (InventoryControl.Instance?.GetDraggedItem() == null)
                    {
                        ToggleRepairMode();
                        SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                    }
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

                // Handle repair buttons (only if repair shop)
                if (_isRepairShop && leftJustPressed)
                {
                    if (_repairButtonHovered)
                    {
                        // Toggle repair mode
                        ToggleRepairMode();
                        SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                        return;
                    }
                    else if (_repairAllButtonHovered)
                    {
                        // Repair all items
                        var svc = MuGame.Network?.GetCharacterService();
                        if (svc != null)
                        {
                            _ = svc.SendRepairItemRequestAsync(0xFF, false); // 0xFF = repair all
                            SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                        }
                        return;
                    }
                }

                // Handle window dragging
                if (leftJustPressed && IsMouseOverDragArea(mousePos) && !_isDragging)
                {
                    DateTime now = DateTime.Now;
                    if ((now - _lastClickTime).TotalMilliseconds < 500)
                    {
                        // Double-click to reset position
                        Align = ControlAlign.None;
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

            // Handle repair button hover (only show if repair shop)
            if (_isRepairShop)
            {
                var repairRect = Translate(_repairButtonRect);
                var repairAllRect = Translate(_repairAllButtonRect);
                _repairButtonHovered = repairRect.Contains(mousePos);
                _repairAllButtonHovered = repairAllRect.Contains(mousePos);
            }
            else
            {
                _repairButtonHovered = false;
                _repairAllButtonHovered = false;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            EnsureStaticSurface();

            var gm = GraphicsManager.Instance;
            var spriteBatch = gm?.Sprite;
            if (spriteBatch == null) return;

            SpriteBatchScope? scope = null;
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

                var pixel = GraphicsManager.Instance.Pixel;
                ItemGridRenderHelper.DrawGridOverlays(spriteBatch, pixel, DisplayRectangle, _gridRect, _hoveredItem, _hoveredSlot,
                                                      SHOP_SQUARE_WIDTH, SHOP_SQUARE_HEIGHT, Theme.SlotHover, Theme.Accent, Alpha);
                DrawShopItems(spriteBatch);
                DrawCloseButton(spriteBatch);
                if (_isRepairShop)
                {
                    DrawRepairButtons(spriteBatch);
                }
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

            SpriteBatchScope? scope = null;
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
            UiDrawHelper.DrawVerticalGradient(spriteBatch, innerRect, Theme.BgDark, Theme.BgDarkest);

            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 1), Theme.BorderInner * 0.5f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, 1, innerRect.Height), Theme.BorderInner * 0.3f);

            UiDrawHelper.DrawCornerAccents(spriteBatch, rect, Theme.Accent * 0.4f);
        }

        private void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color bgColor, bool withBorder = true)
        {
            UiDrawHelper.DrawPanel(spriteBatch, rect, bgColor,
                withBorder ? Theme.BorderInner * 0.8f : (Color?)null,
                withBorder ? Theme.BorderOuter : (Color?)null,
                withBorder ? Theme.BorderInner * 0.6f : null);
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
            _staticSurface = new RenderTarget2D(gd, WINDOW_WIDTH, WindowHeight, false, SurfaceFormat.Color, DepthFormat.None);

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

            var fullRect = new Rectangle(0, 0, WINDOW_WIDTH, WindowHeight);
            DrawWindowBackground(spriteBatch, fullRect);
            DrawModernHeader(spriteBatch);
            DrawModernGridSection(spriteBatch);
            DrawModernButtonArea(spriteBatch);
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
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(20, sepY, (WINDOW_WIDTH - 40) / 2, 1),
                                  Color.Transparent, Theme.BorderInner);
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, sepY, (WINDOW_WIDTH - 40) / 2, 1),
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

        private void DrawModernButtonArea(SpriteBatch spriteBatch)
        {
            if (_buttonAreaRect.Height == 0) return;

            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            DrawPanel(spriteBatch, _buttonAreaRect, Theme.BgMid);
        }

        private void DrawModernFooter(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int sepY = _footerRect.Y - 4;
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(30, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Color.Transparent, Theme.Accent * 0.4f);
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Theme.Accent * 0.4f, Color.Transparent);

            DrawPanel(spriteBatch, _footerRect, Theme.BgMid);

            if (_font != null)
            {
                string hint = _isRepairShop
                    ? (_shopMode == ShopMode.Repair ? "Repair mode - Click items" : "Buy/Sell - Press 'L' to repair")
                    : "Click item to buy";
                float scale = 0.38f;
                Vector2 size = _font.MeasureString(hint) * scale;
                int hintX = _footerRect.X;
                Vector2 pos = new(hintX + ((_footerRect.Width - (hintX - _footerRect.X)) - size.X) / 2,
                                  _footerRect.Y + (_footerRect.Height - size.Y) / 2);

                spriteBatch.DrawString(_font, hint, pos + Vector2.One, Color.Black * 0.5f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, hint, pos, Theme.TextGold,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawRepairButtons(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || _font == null) return;

            // Draw Repair button
            var repairRect = Translate(_repairButtonRect);
            Color repairBg = _shopMode == ShopMode.Repair ? Theme.AccentDim : Theme.BgLight;
            Color repairBorder = _repairButtonHovered ? Theme.Accent : Theme.BorderInner;
            UiDrawHelper.DrawPanel(spriteBatch, repairRect, repairBg, repairBorder, Theme.BorderOuter);

            // Draw "Repair item" text for Repair
            string repairText = "Repair item";
            float scale = 0.4f;
            Vector2 textSize = _font.MeasureString(repairText) * scale;
            Vector2 textPos = new(repairRect.X + (repairRect.Width - textSize.X) / 2,
                                  repairRect.Y + (repairRect.Height - textSize.Y) / 2);
            spriteBatch.DrawString(_font, repairText, textPos + Vector2.One, Color.Black * 0.6f,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, repairText, textPos, Theme.TextWhite,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Draw Repair All button
            var repairAllRect = Translate(_repairAllButtonRect);
            Color repairAllBorder = _repairAllButtonHovered ? Theme.Accent : Theme.BorderInner;
            UiDrawHelper.DrawPanel(spriteBatch, repairAllRect, Theme.BgLight, repairAllBorder, Theme.BorderOuter);

            // Draw "Repair all" text for Repair All
            string allText = "Repair all";
            scale = 0.4f;
            textSize = _font.MeasureString(allText) * scale;
            textPos = new(repairAllRect.X + (repairAllRect.Width - textSize.X) / 2,
                          repairAllRect.Y + (repairAllRect.Height - textSize.Y) / 2);
            spriteBatch.DrawString(_font, allText, textPos + Vector2.One, Color.Black * 0.6f,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, allText, textPos, Theme.TextWhite,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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

        private void DrawShopItems(SpriteBatch spriteBatch)
        {
            var font = _font ?? GraphicsManager.Instance.Font;
            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);
            var pixel = GraphicsManager.Instance.Pixel;
            var jewelEntries = new List<(InventoryItem Item, Rectangle Rect)>();

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
                Color glowColor = ItemUiHelper.GetItemGlowColor(item, GlowPalette);
                if (glowColor.A > 0 || isHovered)
                {
                    Color finalGlow = isHovered ? Color.Lerp(glowColor, Theme.Accent, 0.4f) : glowColor;
                    finalGlow.A = (byte)Math.Min(255, finalGlow.A + (isHovered ? 40 : 0));
                    ItemUiHelper.DrawItemGlow(spriteBatch, pixel, rect, finalGlow);
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

                    if (JewelShineOverlay.ShouldShine(item))
                    {
                        jewelEntries.Add((item, rect));
                    }
                }
                else if (pixel != null)
                {
                    ItemGridRenderHelper.DrawItemPlaceholder(spriteBatch, pixel, font, rect, item, Theme.BgLight, Theme.TextGray * 0.8f);
                }

                if (font != null && item.Definition.BaseDurability == 0 && item.Definition.MagicDurability == 0 && item.Durability > 1)
                {
                    ItemGridRenderHelper.DrawItemStackCount(spriteBatch, font, rect, item.Durability, Theme.TextGold, Alpha);
                }

                ItemGridRenderHelper.DrawItemLevelBadge(spriteBatch, GraphicsManager.Instance.Pixel, font, rect, item.Details.Level,
               lvl => lvl >= 9 ? Theme.AccentBright :
                      lvl >= 7 ? Theme.Accent :
                      lvl >= 4 ? Theme.AccentDim :
                      Theme.TextGray,
               new Color(0, 0, 0, 180));

                if (jewelEntries.Count > 0)
                {
                    JewelShineOverlay.DrawBatch(spriteBatch, jewelEntries, _currentGameTime, Alpha, UiScaler.SpriteTransform);
                }
            }
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (_hoveredItem == null || _font == null) return;

            var lines = ItemUiHelper.BuildTooltipLines(_hoveredItem);
            int buyPrice = ItemPriceCalculator.CalculateBuyPrice(_hoveredItem);
            if (buyPrice > 0)
            {
                lines.Add(($"Buy Price: {buyPrice} Zen", Theme.TextGold));
            }
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

            UiDrawHelper.DrawVerticalGradient(spriteBatch, tooltipRect, new Color(20, 24, 32, 252), new Color(12, 14, 18, 254));

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

            // Prevent input when a modal dialog is open (e.g., sell confirmation)
            if (IsModalDialogOpen()) return;
            if (Scene?.FocusControl != this) return;

            // Ignore shop clicks while dragging an item from inventory/vault (so a sell drop doesn't auto-buy a shop item)
            if (InventoryControl.Instance?.GetDraggedItem() != null || VaultControl.Instance?.GetDraggedItem() != null) return;

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
            _hoveredSlot = ItemGridRenderHelper.GetSlotAtScreenPosition(DisplayRectangle, _gridRect, SHOP_COLUMNS, SHOP_ROWS, SHOP_SQUARE_WIDTH, SHOP_SQUARE_HEIGHT, mousePos);
            _hoveredItem = GetItemAt(mousePos);
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
            _pendingShow = false;

            // Reset repair mode when closing shop
            _shopMode = ShopMode.BuyAndSell;
            _warmupComplete = false;
        }

        private bool IsModalDialogOpen()
        {
            var scene = Scene;
            if (scene == null) return false;

            for (int i = scene.Controls.Count - 1; i >= 0; i--)
            {
                if (scene.Controls[i] is DialogControl dialog && dialog.Visible)
                {
                    return true;
                }
            }

            return false;
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
            int maxSlots = SHOP_COLUMNS * SHOP_ROWS;
            foreach (var kv in shopItems)
            {
                byte slot = kv.Key;
                if (slot >= maxSlots)
                    continue;

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
                if (!string.IsNullOrEmpty(item.Definition.TexturePath) &&
                    !item.Definition.TexturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
                {
                    _ = TextureLoader.Instance.Prepare(item.Definition.TexturePath);
                }
            }

            if (_items.Count > 0)
            {
                // Align left with padding before showing, then freeze position to avoid auto realignment
                ForceAlignNow();
                Align = ControlAlign.None;
                // Use deferred show - warmup happens in Update(), window shows one frame later
                // to avoid black screen flicker from render target switches during Draw().
                _pendingShow = true;
                _warmupComplete = false;
                _closeRequestSent = false;
                _isDragging = false;
            }
        }

        private void WarmupTexturesSync()
        {
            if (GraphicsManager.Instance?.Sprite == null)
                return;

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

            bool isHovered = animated;

            // Material animation for non-hovered items (if enabled)
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
                    return BmdPreviewRenderer.GetSmoothAnimatedPreview(item, width, height, _currentGameTime);
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

        // ═══════════════════════════════════════════════════════════════
        // REPAIR MODE
        // ═══════════════════════════════════════════════════════════════

        public ShopMode GetShopMode() => _shopMode;
        public bool IsRepairShop => _isRepairShop;
        public bool IsRepairMode => _shopMode == ShopMode.Repair;

        public void SetRepairShop(bool canRepair)
        {
            _isRepairShop = canRepair;
            if (!canRepair && _shopMode == ShopMode.Repair)
            {
                // If NPC can't repair, reset to buy/sell mode
                _shopMode = ShopMode.BuyAndSell;
            }
            BuildLayoutMetrics();
            var newSize = new Point(WINDOW_WIDTH, WindowHeight);
            ControlSize = newSize;
            ViewSize = newSize;              // <-- KLUCZ: utrzymuj ViewSize = ControlSize gdy AutoViewSize=false
            InvalidateStaticSurface();
        }

        public void ToggleRepairMode()
        {
            if (!_isRepairShop) return;

            if (_shopMode == ShopMode.BuyAndSell)
            {
                _shopMode = ShopMode.Repair;
            }
            else
            {
                _shopMode = ShopMode.BuyAndSell;
            }

            InvalidateStaticSurface();

            // TODO: Notify inventory control of mode change
            // InventoryControl.Instance?.SetRepairMode(_shopMode == ShopMode.Repair);
        }

    }
}
