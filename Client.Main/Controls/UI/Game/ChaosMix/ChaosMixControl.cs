using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game.Common;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Client.Main.Networking;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Packets.ServerToClient;

namespace Client.Main.Controls.UI.Game
{
    public class ChaosMixControl : UIControl
    {
        // ═══════════════════════════════════════════════════════════════
        // WINDOW DIMENSIONS
        // ═══════════════════════════════════════════════════════════════
        public const int Columns = 8;
        public const int Rows = 4;
        private const int SLOT_WIDTH = 34;
        private const int SLOT_HEIGHT = 34;

        private const int HEADER_HEIGHT = 52;
        private const int SECTION_HEADER_HEIGHT = 22;
        private const int GRID_PADDING = 10;
        private const int INFO_PANEL_HEIGHT = 170;
        private const int BUTTON_AREA_HEIGHT = 44;
        private const int WINDOW_MARGIN = 12;
        private const int SECTION_SPACING = 10;

        private static readonly int GRID_WIDTH = Columns * SLOT_WIDTH;
        private static readonly int GRID_HEIGHT = Rows * SLOT_HEIGHT;
        private static readonly int WINDOW_WIDTH = GRID_WIDTH + GRID_PADDING * 2 + WINDOW_MARGIN * 2;

        // ═══════════════════════════════════════════════════════════════
        // MODERN DARK THEME (muonline-ui-design skill)
        // ═══════════════════════════════════════════════════════════════
        private static class Theme
        {
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BgLight = new(35, 42, 52, 245);
            public static readonly Color BgLighter = new(48, 56, 68, 240);

            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color AccentBright = new(255, 215, 120);
            public static readonly Color AccentDim = new(140, 115, 55);
            public static readonly Color AccentGlow = new(255, 200, 80, 40);

            public static readonly Color Secondary = new(90, 140, 200);
            public static readonly Color SecondaryBright = new(130, 180, 240);
            public static readonly Color SecondaryDim = new(50, 80, 120);

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
            public static readonly Color TextDark = new(100, 105, 115);

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

        private static ChaosMixControl _instance;

        private readonly NetworkManager _networkManager;
        private readonly ILogger<ChaosMixControl> _logger;
        private CharacterState _characterState;

        private readonly List<InventoryItem> _items = new();
        private InventoryItem[,] _itemGrid = new InventoryItem[Columns, Rows];

        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private SpriteFont _font;

        private Rectangle _headerRect;
        private Rectangle _gridFrameRect;
        private Rectangle _gridRect;
        private Rectangle _infoRect;
        private Rectangle _mixButtonRect;
        private Rectangle _closeButtonRect;

        private bool _closeHovered;
        private bool _mixHovered;

        private bool _isDraggingWindow;
        private Point _windowDragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        private InventoryItem _hoveredItem;
        private Point _hoveredSlot = new(-1, -1);
        private Point _pendingDropSlot = new(-1, -1);

        private InventoryItem _draggedItem;
        private Point _draggedOriginalSlot = new(-1, -1);

        private ChaosMixEvaluation _evaluation;
        private readonly List<string> _infoLines = new();
        private string _warningLine = "Failure may destroy items.";

        private static readonly Dictionary<int, string> s_mixTypeNames = new();

        private bool _closeRequestSent;
        private bool _mixInProgress;

        private GameTime _currentGameTime;

        private ChaosMixControl(NetworkManager networkManager = null, ILoggerFactory loggerFactory = null)
        {
            _networkManager = networkManager ?? MuGame.Network;
            var factory = loggerFactory ?? MuGame.AppLoggerFactory;
            _logger = factory?.CreateLogger<ChaosMixControl>();

            BuildLayoutMetrics();

            AutoViewSize = false;
            ControlSize = new Point(WINDOW_WIDTH, _mixButtonRect.Bottom + WINDOW_MARGIN);
            ViewSize = ControlSize;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.VerticalCenter | ControlAlign.Left;

            EnsureCharacterState();
        }


        public override bool NonDisposable => true;
        public static ChaosMixControl Instance => _instance ??= new ChaosMixControl();

        public static bool IsOpen => _instance?.Visible == true;

        public override async Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;
            InvalidateStaticSurface();
        }

        public void Show()
        {
            ForceAlignNow();
            Align = ControlAlign.None;

            Visible = true;
            BringToFront();
            Scene.FocusControl = this;

            _closeRequestSent = false;
            _mixInProgress = false;
            EnsureCharacterState();
            RefreshChaosMachineContent();

            InvalidateStaticSurface();
        }

        public bool CloseWindow()
        {
            if (!CanCloseWindow(out string blockReason))
            {
                if (!string.IsNullOrWhiteSpace(blockReason))
                {
                    RequestDialog.ShowInfo(blockReason);
                }
                return false;
            }

            Hide();
            SendCraftingDialogCloseRequest();
            return true;
        }

        public void Hide()
        {
            if (_draggedItem != null && _draggedOriginalSlot.X >= 0)
            {
                PlaceItemOnGrid(_draggedItem, _draggedOriginalSlot);
                _draggedItem = null;
                _draggedOriginalSlot = new Point(-1, -1);
            }

            Visible = false;
            if (Scene?.FocusControl == this)
            {
                Scene.FocusControl = null;
            }
        }

        public override void Update(GameTime gameTime)
        {
            _currentGameTime = gameTime;

            if (!Visible)
            {
                return;
            }

            base.Update(gameTime);

            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) &&
                MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
            {
                CloseWindow();
                return;
            }

            Point mousePos = MuGame.Instance.UiMouseState.Position;
            UpdateHoverStates(mousePos);

            HandleWindowDrag(mousePos);

            bool leftJustPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed &&
                                   MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;

            if (leftJustPressed && HandleChromeClick())
            {
                return;
            }

            HandleMouseInput(mousePos, leftJustPressed);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Status != GameControlStatus.Ready)
            {
                return;
            }

            EnsureStaticSurface();

            var sb = GraphicsManager.Instance.Sprite;
            using var scope = new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend, GraphicsManager.GetQualityLinearSamplerState(), transform: UiScaler.SpriteTransform);

            sb.Draw(_staticSurface, DisplayRectangle, Color.White * Alpha);

            DrawDynamicContent(sb);
        }

        public void DrawPickedPreview(SpriteBatch spriteBatch, GameTime gameTime)
        {
            if (!Visible || _draggedItem == null || spriteBatch == null)
            {
                return;
            }

            int w = _draggedItem.Definition.Width * SLOT_WIDTH;
            int h = _draggedItem.Definition.Height * SLOT_HEIGHT;

            Texture2D texture = ResolveItemTexture(_draggedItem, w, h, animated: false, allowGenerate: false)
                                ?? ResolveItemTexture(_draggedItem, w, h, animated: false, allowGenerate: true);

            Point mouse = MuGame.Instance.UiMouseState.Position;
            var rect = new Rectangle(mouse.X - w / 2, mouse.Y - h / 2, w, h);

            if (texture != null)
            {
                spriteBatch.Draw(texture, rect, Color.White * 0.9f);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _staticSurface?.Dispose();
            _staticSurface = null;
        }

        // ═══════════════════════════════════════════════════════════════
        // Layout
        // ═══════════════════════════════════════════════════════════════

        private void BuildLayoutMetrics()
        {
            _headerRect = new Rectangle(0, 0, WINDOW_WIDTH, HEADER_HEIGHT);

            int frameX = WINDOW_MARGIN;
            int frameY = HEADER_HEIGHT;
            int frameW = WINDOW_WIDTH - WINDOW_MARGIN * 2;
            int frameH = SECTION_HEADER_HEIGHT + GRID_PADDING * 2 + GRID_HEIGHT;
            _gridFrameRect = new Rectangle(frameX, frameY, frameW, frameH);

            _gridRect = new Rectangle(
                frameX + GRID_PADDING,
                frameY + SECTION_HEADER_HEIGHT + GRID_PADDING,
                GRID_WIDTH,
                GRID_HEIGHT);

            _infoRect = new Rectangle(frameX, _gridFrameRect.Bottom + SECTION_SPACING, frameW, INFO_PANEL_HEIGHT);
            _mixButtonRect = new Rectangle(frameX, _infoRect.Bottom + SECTION_SPACING, frameW, BUTTON_AREA_HEIGHT);
            _closeButtonRect = new Rectangle(WINDOW_WIDTH - 30, 14, 20, 20);
        }

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

        // ═══════════════════════════════════════════════════════════════
        // Static surface rendering
        // ═══════════════════════════════════════════════════════════════

        private void EnsureStaticSurface()
        {
            if (!_staticSurfaceDirty && _staticSurface != null && !_staticSurface.IsDisposed)
            {
                return;
            }

            var gd = GraphicsManager.Instance?.GraphicsDevice;
            if (gd == null)
            {
                return;
            }

            _staticSurface?.Dispose();
            _staticSurface = new RenderTarget2D(gd, ControlSize.X, ControlSize.Y, false, SurfaceFormat.Color, DepthFormat.None);

            var prevTargets = gd.GetRenderTargets();
            gd.SetRenderTarget(_staticSurface);
            gd.Clear(Color.Transparent);

            var sb = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {
                DrawStaticElements(sb);
            }

            gd.SetRenderTargets(prevTargets);
            _staticSurfaceDirty = false;
        }

        private void InvalidateStaticSurface() => _staticSurfaceDirty = true;

        private void DrawStaticElements(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null)
            {
                return;
            }

            DrawWindowBackground(sb, new Rectangle(0, 0, ControlSize.X, ControlSize.Y));
            DrawHeader(sb);
            DrawGridSection(sb);
            DrawInfoPanel(sb);
        }

        private void DrawWindowBackground(SpriteBatch sb, Rectangle rect)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            sb.Draw(pixel, rect, Theme.BorderOuter);

            var inner = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
            UiDrawHelper.DrawVerticalGradient(sb, inner, Theme.BgDark, Theme.BgDarkest);

            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), Theme.BorderInner * 0.5f);
            UiDrawHelper.DrawCornerAccents(sb, rect, Theme.Accent * 0.35f);
        }

        private void DrawPanel(SpriteBatch sb, Rectangle rect, Color bg, bool withGlow = false)
        {
            UiDrawHelper.DrawPanel(sb, rect, bg,
                Theme.BorderInner,
                Theme.BorderOuter,
                Theme.BorderHighlight * 0.3f,
                withGlow,
                withGlow ? Theme.Accent * 0.15f : null);
        }

        private void DrawSectionHeader(SpriteBatch sb, string title, int x, int y, int width)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || _font == null) return;

            float scale = 0.32f;
            Vector2 size = _font.MeasureString(title) * scale;
            int textX = x + (width - (int)size.X) / 2;

            var left = new Rectangle(x + 8, y + 10, textX - x - 12, 1);
            var right = new Rectangle(textX + (int)size.X + 4, y + 10, (x + width - 8) - (textX + (int)size.X + 4), 1);

            if (left.Width > 0) UiDrawHelper.DrawHorizontalGradient(sb, left, Theme.Accent * 0.1f, Theme.Accent * 0.6f);
            if (right.Width > 0) UiDrawHelper.DrawHorizontalGradient(sb, right, Theme.Accent * 0.6f, Theme.Accent * 0.1f);

            sb.DrawString(_font, title, new Vector2(textX + 1, y + 2 + 1), Color.Black * 0.65f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(_font, title, new Vector2(textX, y + 2), Theme.TextGold, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawHeader(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var headerBg = new Rectangle(8, 6, ControlSize.X - 16, HEADER_HEIGHT - 10);
            DrawPanel(sb, headerBg, Theme.BgMid);

            sb.Draw(pixel, new Rectangle(20, 10, ControlSize.X - 40, 2), Theme.Accent * 0.85f);
            sb.Draw(pixel, new Rectangle(30, 12, ControlSize.X - 60, 1), Theme.Accent * 0.25f);

            if (_font != null)
            {
                string title = "CHAOS MIX";
                float scale = 0.52f;
                Vector2 size = _font.MeasureString(title) * scale;
                Vector2 pos = new((ControlSize.X - size.X) / 2, (HEADER_HEIGHT - size.Y) / 2 + 3);

                sb.Draw(pixel, new Rectangle((int)pos.X - 18, (int)pos.Y - 4, (int)size.X + 36, (int)size.Y + 8), Theme.AccentGlow * 0.28f);

                sb.DrawString(_font, title, pos + new Vector2(2, 2), Color.Black * 0.55f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                sb.DrawString(_font, title, pos, Theme.TextWhite, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            int sepY = HEADER_HEIGHT - 2;
            UiDrawHelper.DrawHorizontalGradient(sb, new Rectangle(20, sepY, (ControlSize.X - 40) / 2, 1), Color.Transparent, Theme.BorderInner);
            UiDrawHelper.DrawHorizontalGradient(sb, new Rectangle(ControlSize.X / 2, sepY, (ControlSize.X - 40) / 2, 1), Theme.BorderInner, Color.Transparent);
        }

        private void DrawInfoPanel(SpriteBatch sb)
        {
            DrawPanel(sb, _infoRect, Theme.BgMid);
        }

        private void DrawGridSection(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            DrawPanel(sb, _gridFrameRect, Theme.BgMid);
            DrawSectionHeader(sb, "MIX BOX", _gridFrameRect.X, _gridFrameRect.Y + 2, _gridFrameRect.Width);

            sb.Draw(pixel, _gridRect, Theme.SlotBg);

            Color gridLine = new(40, 48, 60, 100);
            Color gridLineMajor = new(55, 65, 80, 120);

            for (int x = 1; x < Columns; x++)
            {
                int lineX = _gridRect.X + x * SLOT_WIDTH;
                bool major = x == Columns / 2;
                sb.Draw(pixel, new Rectangle(lineX, _gridRect.Y, 1, _gridRect.Height), major ? gridLineMajor : gridLine);
            }

            for (int y = 1; y < Rows; y++)
            {
                int lineY = _gridRect.Y + y * SLOT_HEIGHT;
                bool major = y == Rows / 2;
                sb.Draw(pixel, new Rectangle(_gridRect.X, lineY, _gridRect.Width, 1), major ? gridLineMajor : gridLine);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Dynamic rendering
        // ═══════════════════════════════════════════════════════════════

        private void DrawDynamicContent(SpriteBatch sb)
        {
            DrawInfoText(sb);
            DrawMixButton(sb);
            DrawCloseButton(sb);
            DrawGridOverlays(sb);
            DrawChaosItems(sb);
        }

        private void DrawCloseButton(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var rect = Translate(_closeButtonRect);
            Color bg = _closeHovered ? Color.Lerp(Theme.BgLight, Theme.Danger, 0.3f) : Theme.BgDarkest;
            UiDrawHelper.DrawPanel(sb, rect, bg, Theme.BorderInner, Theme.BorderOuter, Theme.BorderHighlight * 0.3f, _closeHovered, Theme.Accent * 0.15f);

            // Draw an X
            int cx = rect.X + rect.Width / 2;
            int cy = rect.Y + rect.Height / 2;
            int half = 6;
            int thickness = 2;
            Color color = _closeHovered ? Theme.TextWhite : Theme.TextGray;

            for (int i = -half; i <= half; i++)
            {
                sb.Draw(pixel, new Rectangle(cx + i - thickness / 2, cy + i - thickness / 2, thickness, thickness), color);
                sb.Draw(pixel, new Rectangle(cx + i - thickness / 2, cy - i - thickness / 2, thickness, thickness), color);
            }
        }

        private void DrawInfoText(SpriteBatch sb)
        {
            if (_font == null) return;

            float scale = 0.32f;
            int x = DisplayRectangle.X + _infoRect.X + 10;
            int y = DisplayRectangle.Y + _infoRect.Y + 10;

            foreach (string line in _infoLines)
            {
                sb.DrawString(_font, line, new Vector2(x + 1, y + 1), Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                sb.DrawString(_font, line, new Vector2(x, y), Theme.TextGray, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += (int)(_font.LineSpacing * scale) + 2;
                if (y > DisplayRectangle.Y + _infoRect.Bottom - 28) break;
            }

            string warn = _warningLine ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(warn))
            {
                float warnScale = 0.30f;
                Vector2 warnSize = _font.MeasureString(warn) * warnScale;
                Vector2 warnPos = new(
                    DisplayRectangle.X + _infoRect.X + (_infoRect.Width - warnSize.X) / 2,
                    DisplayRectangle.Y + _infoRect.Bottom - warnSize.Y - 10);

                sb.DrawString(_font, warn, warnPos + Vector2.One, Color.Black * 0.7f, 0f, Vector2.Zero, warnScale, SpriteEffects.None, 0f);
                sb.DrawString(_font, warn, warnPos, Theme.Warning, 0f, Vector2.Zero, warnScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawMixButton(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var rect = Translate(_mixButtonRect);
            Color bg = _mixHovered ? Color.Lerp(Theme.BgLight, Theme.AccentDim, 0.35f) : Theme.BgDarkest;
            if (_mixInProgress) bg = Color.Lerp(bg, Theme.SecondaryDim, 0.35f);
            UiDrawHelper.DrawPanel(sb, rect, bg, Theme.BorderInner, Theme.BorderOuter, Theme.BorderHighlight * 0.3f, _mixHovered, Theme.Accent * 0.15f);

            if (_font == null) return;

            string text = _mixInProgress ? "MIXING..." : "MIX";
            float scale = 0.45f;
            Vector2 size = _font.MeasureString(text) * scale;
            Vector2 pos = new(rect.X + (rect.Width - size.X) / 2, rect.Y + (rect.Height - size.Y) / 2);

            sb.Draw(pixel, new Rectangle((int)pos.X - 18, (int)pos.Y - 4, (int)size.X + 36, (int)size.Y + 8), Theme.AccentGlow * (_mixHovered ? 0.35f : 0.22f));

            Color textColor = _mixInProgress ? Theme.TextGray : Theme.TextWhite;
            sb.DrawString(_font, text, pos + new Vector2(2, 2), Color.Black * 0.55f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(_font, text, pos, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawGridOverlays(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var activeDragged = _draggedItem ?? InventoryControl.Instance?.GetDraggedItem();

            if (activeDragged != null && _pendingDropSlot.X >= 0)
            {
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < Columns; x++)
                    {
                        var highlight = GetSlotHighlightColor(new Point(x, y), activeDragged);
                        if (!highlight.HasValue)
                        {
                            continue;
                        }

                        var rect = new Rectangle(
                            DisplayRectangle.X + _gridRect.X + x * SLOT_WIDTH,
                            DisplayRectangle.Y + _gridRect.Y + y * SLOT_HEIGHT,
                            SLOT_WIDTH, SLOT_HEIGHT);
                        sb.Draw(pixel, rect, highlight.Value);
                    }
                }
            }

            if (activeDragged == null)
            {
                ItemGridRenderHelper.DrawGridOverlays(sb, pixel, DisplayRectangle, _gridRect, _hoveredItem, _hoveredSlot,
                    SLOT_WIDTH, SLOT_HEIGHT, Theme.SlotHover, Theme.Secondary, Alpha);
            }
        }

        private void DrawChaosItems(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            var font = _font ?? GraphicsManager.Instance.Font;
            if (pixel == null) return;

            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);

            foreach (var item in _items)
            {
                if (item == null || item == _draggedItem) continue;

                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * SLOT_WIDTH,
                    gridOrigin.Y + item.GridPosition.Y * SLOT_HEIGHT,
                    item.Definition.Width * SLOT_WIDTH,
                    item.Definition.Height * SLOT_HEIGHT);

                bool isHovered = item == _hoveredItem;
                Texture2D texture = ResolveItemTexture(item, rect.Width, rect.Height, animated: isHovered, allowGenerate: true);

                Color glowColor = ItemUiHelper.GetItemGlowColor(item, GlowPalette);
                if (glowColor.A > 0 || isHovered)
                {
                    Color finalGlow = isHovered ? Color.Lerp(glowColor, Theme.Accent, 0.4f) : glowColor;
                    finalGlow.A = (byte)Math.Min(255, finalGlow.A + (isHovered ? 40 : 0));
                    ItemUiHelper.DrawItemGlow(sb, pixel, rect, finalGlow);
                }

                sb.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2), isHovered ? Theme.SlotHover : Theme.SlotBg);

                if (texture != null)
                {
                    sb.Draw(texture, rect, Color.White * Alpha);
                }
                else
                {
                    ItemGridRenderHelper.DrawItemPlaceholder(sb, pixel, font, rect, item, Theme.BgLight, Theme.TextGray * 0.8f);
                }

                if (font != null && item.Definition.BaseDurability == 0 && item.Definition.MagicDurability == 0 && item.Durability > 1)
                {
                    ItemGridRenderHelper.DrawItemStackCount(sb, font, rect, item.Durability, Theme.TextGold, Alpha);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Interaction
        // ═══════════════════════════════════════════════════════════════

        private void UpdateHoverStates(Point mousePos)
        {
            _closeHovered = Translate(_closeButtonRect).Contains(mousePos);
            _mixHovered = Translate(_mixButtonRect).Contains(mousePos);

            UpdateHoverStateForGrid(mousePos);
        }

        private void UpdateHoverStateForGrid(Point mousePos)
        {
            var externalDragged = InventoryControl.Instance?.GetDraggedItem();

            if (_draggedItem != null || externalDragged != null)
            {
                var dropSlot = GetSlotAtScreenPosition(mousePos);
                _pendingDropSlot = dropSlot.X >= 0 ? dropSlot : new Point(-1, -1);
                _hoveredItem = null;
                _hoveredSlot = dropSlot;
                return;
            }

            _hoveredSlot = GetSlotAtScreenPosition(mousePos);
            _hoveredItem = GetItemAt(mousePos);
            _pendingDropSlot = new Point(-1, -1);
        }

        private bool HandleChromeClick()
        {
            if (_closeHovered)
            {
                CloseWindow();
                return true;
            }

            if (_mixHovered)
            {
                TryMix();
                return true;
            }

            return false;
        }

        private void HandleWindowDrag(Point mousePos)
        {
            bool leftPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = !leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Pressed;

            if (leftJustPressed && IsMouseOverDragArea(mousePos) && !_isDraggingWindow)
            {
                DateTime now = DateTime.Now;
                if ((now - _lastClickTime).TotalMilliseconds < 500)
                {
                    Align = ControlAlign.VerticalCenter | ControlAlign.Left;
                    ForceAlignNow();
                    Align = ControlAlign.None;
                    _lastClickTime = DateTime.MinValue;
                }
                else
                {
                    _isDraggingWindow = true;
                    _windowDragOffset = new Point(mousePos.X - X, mousePos.Y - Y);
                    Align = ControlAlign.None;
                    _lastClickTime = now;
                }
            }
            else if (leftJustReleased && _isDraggingWindow)
            {
                _isDraggingWindow = false;
            }
            else if (_isDraggingWindow && leftPressed)
            {
                X = mousePos.X - _windowDragOffset.X;
                Y = mousePos.Y - _windowDragOffset.Y;
            }
        }

        private bool IsMouseOverDragArea(Point mousePos)
        {
            var header = Translate(new Rectangle(0, 0, ControlSize.X, HEADER_HEIGHT));
            return header.Contains(mousePos);
        }

        private void HandleMouseInput(Point mousePos, bool leftJustPressed)
        {
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

            AttemptDrop(mousePos);
            Scene?.SetMouseInputConsumed();
        }

        private void TryMix()
        {
            if (_mixInProgress)
            {
                return;
            }

            if (_evaluation?.CurrentRecipe == null)
            {
                RequestDialog.ShowInfo("Incorrect mix items.");
                return;
            }

            int mixId = _evaluation.CurrentRecipe.MixId;
            int chance = _evaluation.SuccessRate;
            uint cost = _evaluation.RequiredZen;

            string message = $"Proceed with mix?\\nChance: {chance}%\\nCost: {FormatZen(cost)} Zen\\n\\nFailure may destroy items.";

            RequestDialog.Show(
                message,
                onAccept: () => SendMixRequest(mixId),
                onReject: null,
                acceptText: "Mix",
                rejectText: "Cancel");
        }

        private void SendMixRequest(int mixId)
        {
            var svc = _networkManager?.GetCharacterService();
            if (svc == null)
            {
                return;
            }

            _mixInProgress = true;
            SoundController.Instance.PlayBuffer("Sound/iButton.wav");

            _ = Task.Run(async () =>
            {
                try
                {
                    await svc.SendChaosMachineMixRequestAsync(mixId, socketSlot: 0);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send chaos machine mix request.");
                }
            });
        }

        public void NotifyCraftingResult(ItemCraftingResult.CraftingResult result)
        {
            _mixInProgress = false;
            string message = result switch
            {
                ItemCraftingResult.CraftingResult.Success => "Mix succeeded.",
                ItemCraftingResult.CraftingResult.Failed => "Mix failed.",
                ItemCraftingResult.CraftingResult.NotEnoughMoney => "Not enough Zen.",
                ItemCraftingResult.CraftingResult.TooManyItems => "Too many items in mix box.",
                ItemCraftingResult.CraftingResult.CharacterLevelTooLow => "Character level too low.",
                ItemCraftingResult.CraftingResult.LackingMixItems => "Missing required items.",
                ItemCraftingResult.CraftingResult.IncorrectMixItems => "Incorrect mix items.",
                ItemCraftingResult.CraftingResult.InvalidItemLevel => "Invalid item level.",
                ItemCraftingResult.CraftingResult.CharacterClassTooLow => "Character class too low.",
                _ => $"Mix result: {result}"
            };

            _warningLine = message;
        }

        // ═══════════════════════════════════════════════════════════════
        // Items & grid helpers
        // ═══════════════════════════════════════════════════════════════

        private void EnsureCharacterState()
        {
            if (_characterState != null)
            {
                return;
            }

            _characterState = _networkManager?.GetCharacterState() ?? MuGame.Network?.GetCharacterState();
            if (_characterState != null)
            {
                _characterState.ChaosMachineItemsChanged += () => MuGame.ScheduleOnMainThread(RefreshChaosMachineContent);
            }
        }

        private void RefreshChaosMachineContent()
        {
            if (_characterState == null)
            {
                return;
            }

            _items.Clear();
            ClearGrid();

            var entries = _characterState.GetChaosMachineItems();
            foreach (var kv in entries)
            {
                byte slot = kv.Key;
                byte[] data = kv.Value;

                int gridX = slot % Columns;
                int gridY = slot / Columns;
                if (gridX < 0 || gridX >= Columns || gridY < 0 || gridY >= Rows)
                {
                    continue;
                }

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

            UpdateRecipeInfo();
            InvalidateStaticSurface();
        }

        private Rectangle Translate(Rectangle rect)
            => new(DisplayRectangle.X + rect.X, DisplayRectangle.Y + rect.Y, rect.Width, rect.Height);

        private void UpdateRecipeInfo()
        {
            _evaluation = MixRecipeDatabase.EvaluateChaosMachine(_items);
            _infoLines.Clear();

            var recipe = _evaluation?.CurrentRecipe;
            var similar = _evaluation?.MostSimilarRecipe;

            if (recipe != null)
            {
                _infoLines.Add($"Mix: {FormatMixType(recipe.MixId)}");
                _infoLines.Add($"Chance: {_evaluation.SuccessRate}%");
                _infoLines.Add($"Cost: {FormatZen(_evaluation.RequiredZen)} Zen");
                _infoLines.Add(string.Empty);
            }
            else
            {
                _infoLines.Add("Put items into the mix box.");
                if (similar != null)
                {
                    _infoLines.Add($"Closest mix: {FormatMixType(similar.MixId)}");
                }
                _infoLines.Add(string.Empty);
            }

            var showRecipe = recipe ?? similar;
            if (showRecipe != null)
            {
                _infoLines.Add("Requirements:");
                int count = Math.Clamp(showRecipe.NumSources, 0, showRecipe.Sources.Length);
                for (int i = 0; i < count; i++)
                {
                    var src = showRecipe.Sources[i];
                    if (src.CountMin <= 0 && src.CountMax <= 0)
                    {
                        continue;
                    }

                    _infoLines.Add($"- {FormatSource(src)}");
                }
            }

            _warningLine = "Failure may destroy items.";
        }

        private static string FormatZen(uint value)
            // Original MU uses commas for grouping (see `ConvertGold` in SourceMain).
            => value.ToString("#,0", CultureInfo.InvariantCulture);

        private static string FormatMixType(int mixId)
        {
            if (s_mixTypeNames.TryGetValue(mixId, out var cached))
            {
                return cached;
            }

            string name = System.Enum.GetName(typeof(ChaosMachineMixRequest.ChaosMachineMixType), mixId);
            if (string.IsNullOrWhiteSpace(name))
            {
                cached = $"Unknown (#{mixId})";
            }
            else
            {
                cached = ToSpacedTitle(name);
            }

            s_mixTypeNames[mixId] = cached;
            return cached;
        }

        private static string ToSpacedTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                    {
                        sb.Append(' ');
                    }
                    continue;
                }

                if (sb.Length > 0)
                {
                    char prev = value[i - 1];
                    bool prevIsSpace = sb[sb.Length - 1] == ' ';
                    bool boundary =
                        (char.IsUpper(c) && (char.IsLower(prev) || char.IsDigit(prev))) ||
                        (char.IsDigit(c) && char.IsLetter(prev)) ||
                        (char.IsLetter(c) && char.IsDigit(prev));

                    if (boundary && !prevIsSpace)
                    {
                        sb.Append(' ');
                    }
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static string FormatSource(MixRecipeItem src)
        {
            static short ItemType(byte group, short id) => (short)((group * 512) + id);

            static bool IsWildcard(int min, int max) => min == 0 && max == 255;

            static string FormatRange(string minName, string maxName)
            {
                if (string.IsNullOrWhiteSpace(minName) && string.IsNullOrWhiteSpace(maxName))
                {
                    return "Item";
                }

                if (string.IsNullOrWhiteSpace(maxName))
                {
                    return $"{minName}..";
                }

                if (string.IsNullOrWhiteSpace(minName))
                {
                    return $"..{maxName}";
                }

                return $"{minName}..{maxName}";
            }

            static string GetNameFromType(short type)
            {
                if (type < 0)
                {
                    return null;
                }

                int group = type / 512;
                int id = type % 512;
                return ItemDatabase.GetItemName((byte)group, (short)id) ?? $"Type {group}:{id}";
            }

            string baseName;

            if (src.SpecialItem.HasFlag(MixSpecialItem.Add380Item))
            {
                baseName = "380+ Item";
            }
            else if (src.TypeMin == 0 && (src.TypeMax == ItemType(11, 511) || src.TypeMax == ItemType(13, 511)))
            {
                baseName = "Item";
            }
            else if (src.TypeMin == 0 && src.TypeMax == ItemType(5, 511))
            {
                baseName = "Weapon";
            }
            else if (src.TypeMin == ItemType(6, 0) && src.TypeMax == ItemType(11, 511))
            {
                baseName = "Armor";
            }
            else if (src.TypeMin == ItemType(12, 0) && src.TypeMax == ItemType(12, 2))
            {
                baseName = "Wings (1st)";
            }
            else if (src.TypeMin == ItemType(12, 3) && src.TypeMax == ItemType(12, 6))
            {
                baseName = "Wings (2nd)";
            }
            else if (src.TypeMin == ItemType(12, 60) && src.TypeMax == ItemType(12, 65))
            {
                baseName = "Seeds";
            }
            else if (src.TypeMin == ItemType(12, 70) && src.TypeMax == ItemType(12, 74))
            {
                baseName = "Spheres";
            }
            else if ((src.TypeMin == ItemType(12, 100) && src.TypeMax == ItemType(12, 128)) ||
                     (src.TypeMin == ItemType(12, 101) && src.TypeMax == ItemType(12, 129)))
            {
                baseName = "Seed Spheres";
            }
            else if (src.TypeMin == src.TypeMax)
            {
                baseName = GetNameFromType(src.TypeMin) ?? $"Type {src.TypeMin}";
            }
            else
            {
                baseName = FormatRange(GetNameFromType(src.TypeMin), GetNameFromType(src.TypeMax));
            }

            if (src.SpecialItem.HasFlag(MixSpecialItem.SocketItem))
            {
                baseName = $"Socket {baseName}";
            }

            if (src.SpecialItem.HasFlag(MixSpecialItem.Harmony))
            {
                baseName = $"Harmony {baseName}";
            }

            if (src.SpecialItem.HasFlag(MixSpecialItem.SetItem))
            {
                baseName = $"Ancient {baseName}";
            }

            if (src.SpecialItem.HasFlag(MixSpecialItem.Excellent))
            {
                baseName = $"Excellent {baseName}";
            }

            if (!IsWildcard(src.DurabilityMin, src.DurabilityMax) && src.DurabilityMin == src.DurabilityMax)
            {
                baseName = $"{baseName}({src.DurabilityMin})";
            }

            if (!IsWildcard(src.LevelMin, src.LevelMax))
            {
                if (src.LevelMin == src.LevelMax)
                {
                    baseName = $"{baseName} +{src.LevelMin}";
                }
                else if (src.LevelMin == 0)
                {
                    baseName = $"{baseName} +{src.LevelMax} or less";
                }
                else if (src.LevelMax == 255)
                {
                    baseName = $"{baseName} +{src.LevelMin} or more";
                }
                else
                {
                    baseName = $"{baseName} +{src.LevelMin}~{src.LevelMax}";
                }
            }

            if (!IsWildcard(src.OptionMin, src.OptionMax))
            {
                if (src.OptionMin == src.OptionMax)
                {
                    baseName = $"{baseName} opt +{src.OptionMin}";
                }
                else if (src.OptionMin == 0)
                {
                    baseName = $"{baseName} opt +{src.OptionMax} or less";
                }
                else if (src.OptionMax == 255)
                {
                    baseName = $"{baseName} opt +{src.OptionMin} or more";
                }
                else
                {
                    baseName = $"{baseName} opt +{src.OptionMin}~{src.OptionMax}";
                }
            }

            string countPart = src.CountMin == 0 && src.CountMax == 255
                ? "(Optional)"
                : src.CountMin == src.CountMax
                    ? $"x{src.CountMin}"
                    : src.CountMin == 0
                        ? $"x≤{src.CountMax}"
                        : src.CountMax == 255
                            ? $"x{src.CountMin}+"
                            : $"x{src.CountMin}-{src.CountMax}";

            return $"{baseName} {countPart}";
        }

        private void ClearGrid()
        {
            _itemGrid = new InventoryItem[Columns, Rows];
        }

        private void PlaceItemOnGrid(InventoryItem item, Point pos)
        {
            item.GridPosition = pos;

            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gx = pos.X + x;
                    int gy = pos.Y + y;
                    if (gx < 0 || gx >= Columns || gy < 0 || gy >= Rows)
                    {
                        continue;
                    }

                    _itemGrid[gx, gy] = item;
                }
            }
        }

        private void RemoveItemFromGrid(InventoryItem item)
        {
            if (item == null) return;

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

        private InventoryItem GetItemAt(Point screenPos)
        {
            var slot = GetSlotAtScreenPosition(screenPos);
            if (slot.X < 0) return null;
            return _itemGrid[slot.X, slot.Y];
        }

        public Point GetSlotAtScreenPosition(Point screenPos)
            => ItemGridRenderHelper.GetSlotAtScreenPosition(DisplayRectangle, _gridRect, Columns, Rows, SLOT_WIDTH, SLOT_HEIGHT, screenPos);

        public bool CanPlaceAt(Point gridSlot, InventoryItem item) => CanPlaceItem(item, gridSlot);

        private bool CanPlaceItem(InventoryItem item, Point slot)
        {
            if (item == null) return false;
            if (slot.X < 0 || slot.Y < 0) return false;
            if (slot.X + item.Definition.Width > Columns) return false;
            if (slot.Y + item.Definition.Height > Rows) return false;

            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    var existing = _itemGrid[slot.X + x, slot.Y + y];
                    if (existing != null && existing != item)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private Color? GetSlotHighlightColor(Point slot, InventoryItem dragged)
        {
            if (dragged == null) return null;

            bool inside = slot.X >= _pendingDropSlot.X &&
                          slot.X < _pendingDropSlot.X + dragged.Definition.Width &&
                          slot.Y >= _pendingDropSlot.Y &&
                          slot.Y < _pendingDropSlot.Y + dragged.Definition.Height;

            if (!inside) return null;

            bool valid = CanPlaceAt(_pendingDropSlot, dragged);
            return valid ? Theme.Success * 0.25f : Theme.Danger * 0.25f;
        }

        private void BeginDrag(InventoryItem item)
        {
            _draggedItem = item;
            _draggedOriginalSlot = item.GridPosition;
            RemoveItemFromGrid(item);
            _hoveredItem = null;
            _pendingDropSlot = new Point(-1, -1);

            int w = item.Definition.Width * SLOT_WIDTH;
            int h = item.Definition.Height * SLOT_HEIGHT;
            _ = ResolveItemTexture(item, w, h, animated: false, allowGenerate: false);
        }

        private void AttemptDrop(Point mousePos)
        {
            if (_draggedItem == null) return;

            var dropSlot = GetSlotAtScreenPosition(mousePos);
            var inventory = InventoryControl.Instance;
            bool dropped = false;

            if (dropSlot.X >= 0 && CanPlaceAt(dropSlot, _draggedItem))
            {
                PlaceItemOnGrid(_draggedItem, dropSlot);
                if (dropSlot != _draggedOriginalSlot)
                {
                    SendChaosMachineMove(_draggedOriginalSlot, dropSlot);
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
                PlaceItemOnGrid(_draggedItem, _draggedOriginalSlot);
            }

            _draggedItem = null;
            _draggedOriginalSlot = new Point(-1, -1);
            _pendingDropSlot = new Point(-1, -1);
        }

        private void SendChaosMachineMove(Point fromSlot, Point toSlot)
        {
            if (_networkManager == null) return;

            byte from = (byte)(fromSlot.Y * Columns + fromSlot.X);
            byte to = (byte)(toSlot.Y * Columns + toSlot.X);

            var svc = _networkManager.GetCharacterService();
            var state = _networkManager.GetCharacterState();
            if (svc == null || state == null) return;

            state.StashPendingChaosMachineMove(from, to);
            _logger?.LogInformation("ChaosMachine move requested: {From} -> {To}", from, to);

            var raw = _draggedItem?.RawData ?? Array.Empty<byte>();
            var version = _networkManager.TargetVersion;

            _ = Task.Run(async () =>
            {
                try
                {
                    await svc.SendStorageItemMoveAsync(ItemStorageKind.ChaosMachine, from, ItemStorageKind.ChaosMachine, to, version, raw);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to move item inside chaos machine.");
                }

                await Task.Delay(1200);
                if (_networkManager != null && state.IsChaosMachineMovePending(from, to))
                {
                    MuGame.ScheduleOnMainThread(state.RaiseChaosMachineItemsChanged);
                }
            });
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
            var raw = _draggedItem.RawData ?? Array.Empty<byte>();

            if (svc != null && state != null)
            {
                state.StashPendingChaosMachineMove(fromSlot, 0xFF);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await svc.SendStorageItemMoveAsync(ItemStorageKind.ChaosMachine, fromSlot, ItemStorageKind.Inventory, toSlot, version, raw);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to move item from chaos machine to inventory.");
                    }
                });
            }

            _items.Remove(_draggedItem);
            inventory?.BringToFront();
        }

        // ═══════════════════════════════════════════════════════════════
        // Textures
        // ═══════════════════════════════════════════════════════════════

        private Texture2D ResolveItemTexture(InventoryItem item, int width, int height, bool animated, bool allowGenerate = true)
        {
            if (item?.Definition == null) return null;

            string texturePath = item.Definition.TexturePath;
            if (string.IsNullOrEmpty(texturePath)) return null;

            bool isBmd = texturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase);

            if (!isBmd)
            {
                if (_itemTextureCache.TryGetValue(texturePath, out var cached) && cached != null)
                {
                    return cached;
                }

                var tex = TextureLoader.Instance.GetTexture2D(texturePath);
                if (tex != null)
                {
                    _itemTextureCache[texturePath] = tex;
                }
                return tex;
            }

            if (!allowGenerate)
            {
                var cachedPreview = BmdPreviewRenderer.TryGetCachedPreview(item, width, height);
                return cachedPreview;
            }

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
                catch
                {
                    // ignore
                }
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
                catch
                {
                    return null;
                }
            }

            var key = (item, width, height, false);
            if (_bmdPreviewCache.TryGetValue(key, out var cachedStatic) && cachedStatic != null)
            {
                return cachedStatic;
            }

            try
            {
                var preview = BmdPreviewRenderer.GetPreview(item, width, height);
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

        // ═══════════════════════════════════════════════════════════════
        // Networking close
        // ═══════════════════════════════════════════════════════════════

        private void SendCraftingDialogCloseRequest()
        {
            if (_closeRequestSent)
            {
                return;
            }

            _closeRequestSent = true;
            var svc = _networkManager?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendCraftingDialogCloseRequestAsync();
            }
        }

        private bool CanCloseWindow(out string blockReason)
        {
            if (_mixInProgress)
            {
                blockReason = "Mix is in progress. Please wait for the result.";
                return false;
            }

            if (_draggedItem != null)
            {
                blockReason = "Place the picked item back before closing.";
                return false;
            }

            var externalDragged = InventoryControl.Instance?.GetDraggedItem();
            if (externalDragged != null)
            {
                blockReason = "Finish moving the item before closing.";
                return false;
            }

            if (_items.Count > 0)
            {
                blockReason = "Remove all items from the Chaos Mix box before closing.";
                return false;
            }

            blockReason = null;
            return true;
        }

    }
}
