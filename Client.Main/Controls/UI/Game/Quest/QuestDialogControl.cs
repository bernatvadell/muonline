using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI.Common;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Controls.UI.Game.Quest
{
    /// <summary>
    /// Modern quest dialog control with visual requirement indicators.
    /// Matches the dark theme style of InventoryControl and ChaosMixControl.
    /// </summary>
    public class QuestDialogControl : UIControl
    {
        // ═══════════════════════════════════════════════════════════════
        // WINDOW DIMENSIONS
        // ═══════════════════════════════════════════════════════════════
        private const int WINDOW_WIDTH = 480;
        private const int MIN_WINDOW_HEIGHT = 440;
        private const int MAX_WINDOW_HEIGHT = 680;
        private const int HEADER_HEIGHT = 58;
        private const int SECTION_SPACING = 12;
        private const int PANEL_PADDING = 14;
        private const int BUTTON_HEIGHT = 42;
        private const int BUTTON_WIDTH = 110;
        private const int BUTTON_GAP = 18;

        // Font scales (larger for better readability)
        private const float FONT_SCALE_BODY = 0.40f;
        private const float FONT_SCALE_HEADER = 0.38f;
        private const float FONT_SCALE_TITLE = 0.55f;
        private const float FONT_SCALE_BUTTON = 0.42f;

        // ═══════════════════════════════════════════════════════════════
        // MODERN DARK THEME (muonline-ui-design skill)
        // ═══════════════════════════════════════════════════════════════
        private static class Theme
        {
            // Background layers (5 levels for depth)
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BgLight = new(35, 42, 52, 245);
            public static readonly Color BgLighter = new(48, 56, 68, 240);

            // Primary Accent - Warm Gold (MU signature color)
            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color AccentBright = new(255, 215, 120);
            public static readonly Color AccentDim = new(140, 115, 55);
            public static readonly Color AccentGlow = new(255, 200, 80, 40);

            // Secondary accent - Cool Blue
            public static readonly Color Secondary = new(90, 140, 200);
            public static readonly Color SecondaryBright = new(130, 180, 240);
            public static readonly Color SecondaryDim = new(50, 80, 120);

            // Borders (3 levels)
            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color BorderHighlight = new(100, 110, 130, 120);

            // Interactive Elements
            public static readonly Color SlotBg = new(12, 15, 20, 240);
            public static readonly Color SlotBorder = new(45, 52, 65, 180);
            public static readonly Color SlotHover = new(70, 85, 110, 150);
            public static readonly Color SlotSelected = new(212, 175, 85, 100);

            // Text (4 levels)
            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color TextGold = new(255, 220, 130);
            public static readonly Color TextGray = new(160, 165, 175);
            public static readonly Color TextDark = new(100, 105, 115);

            // Status colors
            public static readonly Color Success = new(80, 200, 120);
            public static readonly Color Warning = new(240, 180, 60);
            public static readonly Color Danger = new(220, 80, 80);
        }

        // ═══════════════════════════════════════════════════════════════
        // QUEST DATA STRUCTURES
        // ═══════════════════════════════════════════════════════════════

        public sealed class QuestRequirement
        {
            public string Label { get; init; }
            public string CurrentValue { get; init; }
            public string RequiredValue { get; init; }
            public bool IsMet { get; init; }
        }

        public sealed class QuestItem
        {
            public string Name { get; init; }
            public byte Group { get; init; }
            public short Id { get; init; }
            public int RequiredCount { get; init; } = 1;
            public int CurrentCount { get; set; }
            public bool IsMet => CurrentCount >= RequiredCount;
        }

        public sealed class QuestData
        {
            public byte QuestIndex { get; init; }
            public string Title { get; init; }
            public string NpcName { get; init; }
            public string Description { get; init; }
            public string StateText { get; init; }
            public LegacyQuestState State { get; init; }
            public List<QuestRequirement> Requirements { get; init; } = new();
            public List<QuestItem> RequiredItems { get; init; } = new();
            public bool CanProceed { get; init; }
            public string BlockReason { get; init; }
        }

        // ═══════════════════════════════════════════════════════════════
        // FIELDS
        // ═══════════════════════════════════════════════════════════════

        private static QuestDialogControl _instance;
        private static readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<QuestDialogControl>();

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;
        private SpriteFont _font;

        private Rectangle _headerRect;
        private Rectangle _contentRect;
        private Rectangle _requirementsRect;
        private Rectangle _itemsRect;
        private Rectangle _footerRect;
        private Rectangle _acceptButtonRect;
        private Rectangle _rejectButtonRect;

        private bool _acceptHovered;
        private bool _rejectHovered;
        private bool _acceptPressed;
        private bool _rejectPressed;
        private bool _mouseCaptured;
        private bool _isDragging;
        private Point _dragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        private QuestData _questData;
        private int _calculatedHeight;
        private bool _infoOnly;

        public event Action Accepted;
        public event Action Rejected;
        public event Action DialogClosed;

        // ═══════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════

        private QuestDialogControl()
        {
            Interactive = true;
            Visible = false;
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;
            AutoViewSize = false;

            _calculatedHeight = MIN_WINDOW_HEIGHT;
            ControlSize = new Point(WINDOW_WIDTH, _calculatedHeight);
            ViewSize = ControlSize;

            BuildLayoutMetrics();
        }

        public static QuestDialogControl Instance => _instance ??= new QuestDialogControl();

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        public override async Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;
            InvalidateStaticSurface();
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;
            base.Update(gameTime);

            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) &&
                MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
            {
                Close();
                return;
            }

            Point mousePos = MuGame.Instance.UiMouseState.Position;
            UpdateHoverStates(mousePos);
            HandleWindowDrag(mousePos);
            HandleButtonClicks(mousePos);

            if (_mouseCaptured && MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed)
            {
                Scene?.SetMouseInputConsumed();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Status != GameControlStatus.Ready) return;

            EnsureStaticSurface();

            var sb = GraphicsManager.Instance.Sprite;
            using var scope = new SpriteBatchScope(
                sb, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                GraphicsManager.GetQualityLinearSamplerState(),
                transform: UiScaler.SpriteTransform);

            sb.Draw(_staticSurface, DisplayRectangle, Color.White * Alpha);
            DrawDynamicContent(sb);
        }

        public override void Dispose()
        {
            base.Dispose();
            _staticSurface?.Dispose();
            _staticSurface = null;
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        public void ShowQuest(QuestData data, bool canProceed, Action onAccept = null, Action onReject = null)
        {
            _questData = data;
            _infoOnly = !canProceed;

            Accepted = null;
            Rejected = null;

            if (onAccept != null) Accepted += onAccept;
            if (onReject != null) Rejected += onReject;

            RecalculateLayout();

            ForceAlignNow();
            Align = ControlAlign.None;

            Visible = true;
            BringToFront();
            Scene.FocusControl = this;

            InvalidateStaticSurface();
        }

        public void ShowInfo(QuestData data)
        {
            ShowQuest(data, canProceed: false);
        }

        public void Close()
        {
            Visible = false;
            if (Scene?.FocusControl == this)
            {
                Scene.FocusControl = null;
            }
            DialogClosed?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════
        // LAYOUT
        // ═══════════════════════════════════════════════════════════════

        private void RecalculateLayout()
        {
            if (_font == null) _font = GraphicsManager.Instance.Font;
            if (_font == null) return;

            int y = HEADER_HEIGHT + SECTION_SPACING;
            int lineHeight = (int)(_font.LineSpacing * FONT_SCALE_BODY) + 6;

            // Content section (description + state)
            int descLines = WrapText(_questData?.Description ?? "", WINDOW_WIDTH - PANEL_PADDING * 4, FONT_SCALE_BODY).Count;
            int contentHeight = Math.Max(80, descLines * lineHeight + 50);

            // Requirements section
            int reqCount = _questData?.Requirements?.Count ?? 0;
            int requirementsHeight = reqCount > 0 ? 44 + reqCount * (lineHeight + 12) : 0;

            // Items section (only show for Active or Complete quests, not Inactive)
            bool showItems = _questData?.State == LegacyQuestState.Active || _questData?.State == LegacyQuestState.Complete;
            int itemCount = showItems ? (_questData?.RequiredItems?.Count ?? 0) : 0;
            int itemsHeight = itemCount > 0 ? 44 + itemCount * (lineHeight + 12) : 0;

            // Footer (buttons)
            int footerHeight = BUTTON_HEIGHT + 20;

            // Total height
            _calculatedHeight = y + contentHeight + SECTION_SPACING;
            if (requirementsHeight > 0) _calculatedHeight += requirementsHeight + SECTION_SPACING;
            if (itemsHeight > 0) _calculatedHeight += itemsHeight + SECTION_SPACING;
            _calculatedHeight += footerHeight + PANEL_PADDING;

            _calculatedHeight = Math.Clamp(_calculatedHeight, MIN_WINDOW_HEIGHT, MAX_WINDOW_HEIGHT);

            ControlSize = new Point(WINDOW_WIDTH, _calculatedHeight);
            ViewSize = ControlSize;

            BuildLayoutMetrics();
        }

        private void BuildLayoutMetrics()
        {
            _headerRect = new Rectangle(0, 0, WINDOW_WIDTH, HEADER_HEIGHT);

            int y = HEADER_HEIGHT + SECTION_SPACING;

            // Content (description) section
            int contentHeight = 100;
            _contentRect = new Rectangle(PANEL_PADDING, y, WINDOW_WIDTH - PANEL_PADDING * 2, contentHeight);
            y += contentHeight + SECTION_SPACING;

            // Requirements section (dynamic height based on content)
            int reqCount = _questData?.Requirements?.Count ?? 0;
            int reqHeight = reqCount > 0 ? 44 + reqCount * 36 : 0;
            if (reqHeight > 0)
            {
                _requirementsRect = new Rectangle(PANEL_PADDING, y, WINDOW_WIDTH - PANEL_PADDING * 2, reqHeight);
                y += reqHeight + SECTION_SPACING;
            }
            else
            {
                _requirementsRect = Rectangle.Empty;
            }

            // Items section (only show for Active or Complete quests, not Inactive)
            bool showItems = _questData?.State == LegacyQuestState.Active || _questData?.State == LegacyQuestState.Complete;
            int itemCount = showItems ? (_questData?.RequiredItems?.Count ?? 0) : 0;
            int itemsHeight = itemCount > 0 ? 44 + itemCount * 36 : 0;
            if (itemsHeight > 0)
            {
                _itemsRect = new Rectangle(PANEL_PADDING, y, WINDOW_WIDTH - PANEL_PADDING * 2, itemsHeight);
                y += itemsHeight + SECTION_SPACING;
            }
            else
            {
                _itemsRect = Rectangle.Empty;
            }

            // Footer with buttons
            int footerY = _calculatedHeight - BUTTON_HEIGHT - 24 - PANEL_PADDING;
            _footerRect = new Rectangle(PANEL_PADDING, footerY, WINDOW_WIDTH - PANEL_PADDING * 2, BUTTON_HEIGHT + 24);

            if (_infoOnly)
            {
                // Center single OK button
                int okX = (WINDOW_WIDTH - BUTTON_WIDTH) / 2;
                _acceptButtonRect = new Rectangle(okX, footerY + 10, BUTTON_WIDTH, BUTTON_HEIGHT);
                _rejectButtonRect = Rectangle.Empty;
            }
            else
            {
                // Two buttons centered
                int totalWidth = BUTTON_WIDTH * 2 + BUTTON_GAP;
                int startX = (WINDOW_WIDTH - totalWidth) / 2;
                _acceptButtonRect = new Rectangle(startX, footerY + 10, BUTTON_WIDTH, BUTTON_HEIGHT);
                _rejectButtonRect = new Rectangle(startX + BUTTON_WIDTH + BUTTON_GAP, footerY + 10, BUTTON_WIDTH, BUTTON_HEIGHT);
            }
        }

        private void ForceAlignNow()
        {
            if (Parent == null || Align == ControlAlign.None) return;

            if (Align.HasFlag(ControlAlign.VerticalCenter))
                Y = (Parent.DisplaySize.Y / 2) - (DisplaySize.Y / 2);
            else if (Align.HasFlag(ControlAlign.Top))
                Y = 20;
            else if (Align.HasFlag(ControlAlign.Bottom))
                Y = Parent.DisplaySize.Y - DisplaySize.Y - 20;

            if (Align.HasFlag(ControlAlign.HorizontalCenter))
                X = (Parent.DisplaySize.X / 2) - (DisplaySize.X / 2);
            else if (Align.HasFlag(ControlAlign.Left))
                X = 20;
            else if (Align.HasFlag(ControlAlign.Right))
                X = Parent.DisplaySize.X - DisplaySize.X - 20;
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
            _staticSurface = new RenderTarget2D(gd, ControlSize.X, ControlSize.Y, false, SurfaceFormat.Color, DepthFormat.None);

            var prev = gd.GetRenderTargets();
            gd.SetRenderTarget(_staticSurface);
            gd.Clear(Color.Transparent);

            var sb = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {
                DrawStaticElements(sb);
            }

            gd.SetRenderTargets(prev);
            _staticSurfaceDirty = false;
        }

        private void InvalidateStaticSurface() => _staticSurfaceDirty = true;

        private void DrawStaticElements(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var windowRect = new Rectangle(0, 0, ControlSize.X, ControlSize.Y);
            DrawWindowBackground(sb, windowRect);
            DrawHeader(sb);
            DrawContentSection(sb);

            if (_requirementsRect.Width > 0)
                DrawRequirementsSection(sb);

            if (_itemsRect.Width > 0)
                DrawItemsSection(sb);

            DrawFooterSection(sb);
        }

        private void DrawWindowBackground(SpriteBatch sb, Rectangle rect)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            sb.Draw(pixel, rect, Theme.BorderOuter);

            var inner = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
            UiDrawHelper.DrawVerticalGradient(sb, inner, Theme.BgDark, Theme.BgDarkest);

            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), Theme.BorderInner * 0.5f);
            UiDrawHelper.DrawCornerAccents(sb, rect, Theme.Accent * 0.4f);
        }

        private void DrawPanel(SpriteBatch sb, Rectangle rect, Color bg, bool withGlow = false)
        {
            UiDrawHelper.DrawPanel(sb, rect, bg,
                Theme.BorderInner, Theme.BorderOuter, Theme.BorderHighlight * 0.3f,
                withGlow, withGlow ? Theme.Accent * 0.15f : null);
        }

        private void DrawSectionHeader(SpriteBatch sb, string title, int x, int y, int width)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || _font == null) return;

            float scale = FONT_SCALE_HEADER;
            Vector2 size = _font.MeasureString(title) * scale;
            int textX = x + (width - (int)size.X) / 2;

            var left = new Rectangle(x + 8, y + 10, textX - x - 12, 1);
            var right = new Rectangle(textX + (int)size.X + 4, y + 10, x + width - 8 - (textX + (int)size.X + 4), 1);

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

            if (_font != null && _questData != null)
            {
                string title = _questData.Title ?? "QUEST";
                float scale = FONT_SCALE_TITLE;
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

        private void DrawContentSection(SpriteBatch sb)
        {
            DrawPanel(sb, _contentRect, Theme.BgMid);

            if (_font == null || _questData == null) return;

            float scale = FONT_SCALE_BODY;
            int lineHeight = (int)(_font.LineSpacing * scale) + 4;
            int x = _contentRect.X + 12;
            int y = _contentRect.Y + 10;

            // NPC and state
            if (!string.IsNullOrEmpty(_questData.NpcName))
            {
                DrawTextWithShadow(sb, $"NPC: {_questData.NpcName}", x, y, scale, Theme.TextGold);
                y += lineHeight;
            }

            if (!string.IsNullOrEmpty(_questData.StateText))
            {
                Color stateColor = _questData.State switch
                {
                    LegacyQuestState.Complete => Theme.Success,
                    LegacyQuestState.Active => Theme.Warning,
                    LegacyQuestState.Inactive => Theme.TextGray,
                    _ => Theme.TextDark
                };
                DrawTextWithShadow(sb, $"Status: {_questData.StateText}", x, y, scale, stateColor);
                y += lineHeight + 4;
            }

            // Description (wrapped)
            var lines = WrapText(_questData.Description ?? "", _contentRect.Width - 24, scale);
            foreach (var line in lines)
            {
                if (y + lineHeight > _contentRect.Bottom - 8) break;
                DrawTextWithShadow(sb, line, x, y, scale, Theme.TextGray);
                y += lineHeight;
            }
        }

        private void DrawRequirementsSection(SpriteBatch sb)
        {
            DrawPanel(sb, _requirementsRect, Theme.BgMid);
            DrawSectionHeader(sb, "REQUIREMENTS", _requirementsRect.X, _requirementsRect.Y + 2, _requirementsRect.Width);

            if (_font == null || _questData?.Requirements == null) return;

            float scale = FONT_SCALE_BODY;
            int lineHeight = 32;
            int x = _requirementsRect.X + 12;
            int y = _requirementsRect.Y + 32;

            foreach (var req in _questData.Requirements)
            {
                DrawRequirementLine(sb, req, x, y, _requirementsRect.Width - 24, scale);
                y += lineHeight;
            }
        }

        private void DrawRequirementLine(SpriteBatch sb, QuestRequirement req, int x, int y, int width, float scale)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Status indicator (checkmark or X)
            Color indicatorColor = req.IsMet ? Theme.Success : Theme.Danger;
            string indicator = req.IsMet ? "[OK]" : "[X]";

            DrawTextWithShadow(sb, indicator, x, y, scale, indicatorColor);

            // Label
            int labelX = x + 40;
            DrawTextWithShadow(sb, req.Label, labelX, y, scale, Theme.TextGray);

            // Value comparison
            int valueX = _requirementsRect.Right - 120;
            string valueText = $"{req.CurrentValue} / {req.RequiredValue}";
            DrawTextWithShadow(sb, valueText, valueX, y, scale, req.IsMet ? Theme.Success : Theme.Danger);
        }

        private void DrawItemsSection(SpriteBatch sb)
        {
            DrawPanel(sb, _itemsRect, Theme.BgMid);
            DrawSectionHeader(sb, "REQUIRED ITEMS", _itemsRect.X, _itemsRect.Y + 2, _itemsRect.Width);

            if (_font == null || _questData?.RequiredItems == null) return;

            float scale = FONT_SCALE_BODY;
            int lineHeight = 32;
            int x = _itemsRect.X + 12;
            int y = _itemsRect.Y + 32;

            foreach (var item in _questData.RequiredItems)
            {
                DrawItemLine(sb, item, x, y, _itemsRect.Width - 24, scale);
                y += lineHeight;
            }
        }

        private void DrawItemLine(SpriteBatch sb, QuestItem item, int x, int y, int width, float scale)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Status indicator
            Color indicatorColor = item.IsMet ? Theme.Success : Theme.Danger;
            string indicator = item.IsMet ? "[OK]" : "[X]";

            DrawTextWithShadow(sb, indicator, x, y, scale, indicatorColor);

            // Item name
            int labelX = x + 40;
            DrawTextWithShadow(sb, item.Name, labelX, y, scale, Theme.TextGray);

            // Count
            int valueX = _itemsRect.Right - 80;
            string countText = item.RequiredCount > 1
                ? $"{item.CurrentCount} / {item.RequiredCount}"
                : (item.IsMet ? "In Inventory" : "Missing");
            DrawTextWithShadow(sb, countText, valueX, y, scale, item.IsMet ? Theme.Success : Theme.Danger);
        }

        private void DrawFooterSection(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            // Top separator line
            int sepY = _footerRect.Y - 6;
            UiDrawHelper.DrawHorizontalGradient(sb, new Rectangle(30, sepY, (ControlSize.X - 60) / 2, 1), Color.Transparent, Theme.Accent * 0.4f);
            UiDrawHelper.DrawHorizontalGradient(sb, new Rectangle(ControlSize.X / 2, sepY, (ControlSize.X - 60) / 2, 1), Theme.Accent * 0.4f, Color.Transparent);
        }

        // ═══════════════════════════════════════════════════════════════
        // DYNAMIC CONTENT (buttons with hover states)
        // ═══════════════════════════════════════════════════════════════

        private void DrawDynamicContent(SpriteBatch sb)
        {
            DrawButton(sb, Translate(_acceptButtonRect), _infoOnly ? "OK" : "Continue", _acceptHovered, Theme.AccentDim);

            if (!_infoOnly && _rejectButtonRect.Width > 0)
            {
                DrawButton(sb, Translate(_rejectButtonRect), "Close", _rejectHovered, Theme.SecondaryDim);
            }
        }

        private void DrawButton(SpriteBatch sb, Rectangle rect, string text, bool hovered, Color accentColor)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            Color bg = hovered ? Color.Lerp(Theme.BgLight, accentColor, 0.35f) : Theme.BgDarkest;
            UiDrawHelper.DrawPanel(sb, rect, bg, Theme.BorderInner, Theme.BorderOuter, Theme.BorderHighlight * 0.3f, hovered, Theme.Accent * 0.15f);

            if (_font == null) return;

            float scale = FONT_SCALE_BUTTON;
            Vector2 size = _font.MeasureString(text) * scale;
            Vector2 pos = new(rect.X + (rect.Width - size.X) / 2, rect.Y + (rect.Height - size.Y) / 2);

            if (hovered)
            {
                sb.Draw(pixel, new Rectangle((int)pos.X - 12, (int)pos.Y - 4, (int)size.X + 24, (int)size.Y + 8), Theme.AccentGlow * 0.3f);
            }

            sb.DrawString(_font, text, pos + new Vector2(1, 1), Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(_font, text, pos, hovered ? Theme.TextGold : Theme.TextWhite, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private Rectangle Translate(Rectangle rect)
            => new(DisplayRectangle.X + rect.X, DisplayRectangle.Y + rect.Y, rect.Width, rect.Height);

        // ═══════════════════════════════════════════════════════════════
        // INTERACTION
        // ═══════════════════════════════════════════════════════════════

        private void UpdateHoverStates(Point mousePos)
        {
            _acceptHovered = Translate(_acceptButtonRect).Contains(mousePos);
            _rejectHovered = !_infoOnly && Translate(_rejectButtonRect).Contains(mousePos);
        }

        private void HandleWindowDrag(Point mousePos)
        {
            bool leftPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = !leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Pressed;

            if (leftJustPressed && IsMouseOverDragArea(mousePos) && !_isDragging)
            {
                _mouseCaptured = true;
                Scene?.SetMouseInputConsumed();

                DateTime now = DateTime.Now;
                if ((now - _lastClickTime).TotalMilliseconds < 500)
                {
                    Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;
                    ForceAlignNow();
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
                _mouseCaptured = false;
            }
            else if (_isDragging && leftPressed)
            {
                Scene?.SetMouseInputConsumed();
                X = mousePos.X - _dragOffset.X;
                Y = mousePos.Y - _dragOffset.Y;
            }
        }

        private bool IsMouseOverDragArea(Point mousePos)
        {
            var header = Translate(new Rectangle(0, 0, ControlSize.X, HEADER_HEIGHT));
            return header.Contains(mousePos);
        }

        private void HandleButtonClicks(Point mousePos)
        {
            bool leftPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = !leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Pressed;

            if (leftJustPressed)
            {
                if (_acceptHovered)
                {
                    _acceptPressed = true;
                    _rejectPressed = false;
                    _mouseCaptured = true;
                    Scene?.SetMouseInputConsumed();
                    return;
                }

                if (_rejectHovered)
                {
                    _rejectPressed = true;
                    _acceptPressed = false;
                    _mouseCaptured = true;
                    Scene?.SetMouseInputConsumed();
                }
            }

            if (_mouseCaptured && leftPressed)
            {
                Scene?.SetMouseInputConsumed();
            }

            if (!leftJustReleased)
            {
                return;
            }

            if (_mouseCaptured)
            {
                // Important: consume release as well, to prevent click-through to the world
                // when the dialog closes (or when the mouse is released outside the button).
                Scene?.SetMouseInputConsumed();
            }

            if (_acceptPressed && _acceptHovered)
            {
                int handlerCount = Accepted?.GetInvocationList()?.Length ?? 0;
                _logger?.LogInformation("[QuestDialog] Accept/Continue clicked! Handlers={Count}, infoOnly={InfoOnly}",
                    handlerCount, _infoOnly);

                SoundController.Instance.PlayBuffer("Sound/iButton.wav");

                try
                {
                    Accepted?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[QuestDialog] Error invoking Accepted event");
                }

                _acceptPressed = false;
                _rejectPressed = false;
                _mouseCaptured = false;
                Close();
                return;
            }

            if (_rejectPressed && _rejectHovered)
            {
                _logger?.LogInformation("[QuestDialog] Close/Reject clicked!");
                SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                Rejected?.Invoke();
                _acceptPressed = false;
                _rejectPressed = false;
                _mouseCaptured = false;
                Close();
                return;
            }

            _acceptPressed = false;
            _rejectPressed = false;
            _mouseCaptured = false;
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void DrawTextWithShadow(SpriteBatch sb, string text, int x, int y, float scale, Color color)
        {
            sb.DrawString(_font, text, new Vector2(x + 1, y + 1), Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(_font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private List<string> WrapText(string text, int maxWidth, float scale)
        {
            var lines = new List<string>();
            if (_font == null || string.IsNullOrEmpty(text))
            {
                lines.Add(text ?? "");
                return lines;
            }

            // Handle explicit line breaks
            var paragraphs = text.Replace("\\n", "\n").Split('\n');

            foreach (var paragraph in paragraphs)
            {
                var words = paragraph.Split(' ');
                var currentLine = new StringBuilder();

                foreach (var word in words)
                {
                    string test = currentLine.Length == 0 ? word : currentLine + " " + word;
                    float width = _font.MeasureString(test).X * scale;

                    if (width <= maxWidth)
                    {
                        currentLine.Clear();
                        currentLine.Append(test);
                    }
                    else
                    {
                        if (currentLine.Length > 0)
                        {
                            lines.Add(currentLine.ToString());
                            currentLine.Clear();
                        }
                        currentLine.Append(word);
                    }
                }

                if (currentLine.Length > 0)
                    lines.Add(currentLine.ToString());
            }

            return lines;
        }
    }
}
