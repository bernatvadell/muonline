using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Controls.UI.Game
{
    /// <summary>
    /// Blood Castle entry window. Matches SourceMain5.2 logic and modern UI theme.
    /// </summary>
    public sealed class BloodCastleEnterControl : UIControl
    {
        private const int ENTRY_COUNT = 8;
        private const int ENTRY_ROW_HEIGHT = 34;
        private const int ENTRY_ROW_GAP = 4;
        private const int INSTRUCTION_HEIGHT = 78;
        private const int HEADER_HEIGHT = 56;
        private const int FOOTER_HEIGHT = 64;
        private const int PANEL_PADDING = 14;
        private const int WINDOW_WIDTH = 430;
        private const int ENTRY_LIST_HEIGHT = ENTRY_COUNT * ENTRY_ROW_HEIGHT + (ENTRY_COUNT - 1) * ENTRY_ROW_GAP;
        private const int WINDOW_HEIGHT = HEADER_HEIGHT + FOOTER_HEIGHT + (PANEL_PADDING * 3) + INSTRUCTION_HEIGHT + ENTRY_LIST_HEIGHT;

        private const float FONT_SCALE_TITLE = 0.55f;
        private const float FONT_SCALE_BODY = 0.40f;
        private const float FONT_SCALE_SMALL = 0.34f;

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

            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color TextGold = new(255, 220, 130);
            public static readonly Color TextGray = new(160, 165, 175);
            public static readonly Color TextDark = new(100, 105, 115);

            public static readonly Color Success = new(80, 200, 120);
            public static readonly Color Warning = new(240, 180, 60);
            public static readonly Color Danger = new(220, 80, 80);
        }

        // Normal level ranges (for most classes)
        private static readonly (int Min, int Max)[] NormalLevelRanges =
        {
            (15, 80),
            (81, 130),
            (131, 180),
            (181, 230),
            (231, 280),
            (281, 330),
            (331, 400),
            (0, 0)  // Master level
        };

        // Dark Lord/Dark/Rage Fighter level ranges
        private static readonly (int Min, int Max)[] SpecialLevelRanges =
        {
            (10, 60),
            (61, 110),
            (111, 160),
            (161, 210),
            (211, 260),
            (261, 310),
            (311, 400),
            (0, 0)  // Master level
        };

        private static BloodCastleEnterControl _instance;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;
        private SpriteFont _font;

        private Rectangle _headerRect;
        private Rectangle _contentRect;
        private Rectangle _instructionRect;
        private Rectangle _entryListRect;
        private Rectangle _footerRect;
        private Rectangle _closeButtonRect;
        private readonly Rectangle[] _entryRowRects = new Rectangle[ENTRY_COUNT];

        private readonly string[] _entryLabels = new string[ENTRY_COUNT];
        private readonly string[] _entryRanges = new string[ENTRY_COUNT];

        private int _activeIndex;
        private int _hoveredIndex = -1;
        private int _pressedIndex = -1;
        private bool _closeHovered;
        private bool _closePressed;
        private bool _mouseCaptured;
        private bool _isDragging;
        private Point _dragOffset;

        private CharacterState _characterState;
        private bool _hasMatchingTicket;
        private bool _hasAnyTicket;
        private byte _matchingTicketSlot = 0xFF;
        private byte _expectedTicketLevel;
        private byte _foundTicketLevel;
        private bool _useSpecialLevels;

        private static readonly string[] InstructionLines =
        {
            "Enter with an Invisibility Cloak ticket matching your gate level.",
            "Only the highlighted gate matches your level.",
            "Each gate corresponds to a specific level range.",
            "Special classes (Dark Lord, Dark, Rage Fighter) have different level requirements."
        };

        private BloodCastleEnterControl()
        {
            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;

            BuildLayoutMetrics();
            EnsureCharacterState();
        }

        public static BloodCastleEnterControl Instance => _instance ??= new BloodCastleEnterControl();

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
                CloseWindow();
                return;
            }

            Point mousePos = MuGame.Instance.UiMouseState.Position;
            UpdateHoverStates(mousePos);
            HandleWindowDrag(mousePos);
            HandleInput(mousePos);

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
                sb,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
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

            if (_characterState != null)
            {
                _characterState.InventoryChanged -= OnInventoryChanged;
            }
        }

        public void ShowWindow()
        {
            EnsureCharacterState();
            RefreshEntryOptions();
            Visible = true;
            BringToFront();
            SendCloseNpcRequest();
        }

        public void CloseWindow()
        {
            Visible = false;
            _mouseCaptured = false;
            _isDragging = false;
            _pressedIndex = -1;
            _closePressed = false;
            SendCloseNpcRequest();
        }

        // ───────────────────────── Layout & Rendering ─────────────────────────

        private void BuildLayoutMetrics()
        {
            _headerRect = new Rectangle(0, 0, ControlSize.X, HEADER_HEIGHT);
            _footerRect = new Rectangle(0, ControlSize.Y - FOOTER_HEIGHT, ControlSize.X, FOOTER_HEIGHT);
            _contentRect = new Rectangle(0, HEADER_HEIGHT, ControlSize.X, ControlSize.Y - HEADER_HEIGHT - FOOTER_HEIGHT);

            _instructionRect = new Rectangle(
                PANEL_PADDING,
                _contentRect.Y + PANEL_PADDING,
                ControlSize.X - PANEL_PADDING * 2,
                INSTRUCTION_HEIGHT);

            _entryListRect = new Rectangle(
                PANEL_PADDING,
                _instructionRect.Bottom + PANEL_PADDING,
                ControlSize.X - PANEL_PADDING * 2,
                ENTRY_LIST_HEIGHT);

            for (int i = 0; i < ENTRY_COUNT; i++)
            {
                int rowY = _entryListRect.Y + i * (ENTRY_ROW_HEIGHT + ENTRY_ROW_GAP);
                _entryRowRects[i] = new Rectangle(_entryListRect.X, rowY, _entryListRect.Width, ENTRY_ROW_HEIGHT);
            }

            _closeButtonRect = new Rectangle(ControlSize.X - 38, 10, 26, 22);
        }

        private void EnsureStaticSurface()
        {
            if (!_staticSurfaceDirty && _staticSurface != null && !_staticSurface.IsDisposed)
            {
                return;
            }

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
            var windowRect = new Rectangle(0, 0, ControlSize.X, ControlSize.Y);
            DrawWindowBackground(sb, windowRect);

            DrawHeaderPanel(sb);
            DrawPanel(sb, _instructionRect, Theme.BgMid);
            DrawPanel(sb, _entryListRect, Theme.BgDark);
            DrawFooterPanel(sb);
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

        private void DrawPanel(SpriteBatch sb, Rectangle rect, Color bg, bool glow = false)
        {
            UiDrawHelper.DrawPanel(sb, rect, bg,
                Theme.BorderInner, Theme.BorderOuter, Theme.BorderHighlight * 0.3f,
                glow, glow ? Theme.Accent * 0.15f : null);
        }

        private void DrawHeaderPanel(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var headerBg = new Rectangle(8, 6, ControlSize.X - 16, HEADER_HEIGHT - 10);
            DrawPanel(sb, headerBg, Theme.BgMid);

            sb.Draw(pixel, new Rectangle(20, 10, ControlSize.X - 40, 2), Theme.Accent * 0.85f);
            sb.Draw(pixel, new Rectangle(30, 12, ControlSize.X - 60, 1), Theme.Accent * 0.25f);

            int sepY = HEADER_HEIGHT - 2;
            UiDrawHelper.DrawHorizontalGradient(sb, new Rectangle(20, sepY, (ControlSize.X - 40) / 2, 1), Color.Transparent, Theme.BorderInner);
            UiDrawHelper.DrawHorizontalGradient(sb, new Rectangle(ControlSize.X / 2, sepY, (ControlSize.X - 40) / 2, 1), Theme.BorderInner, Color.Transparent);
        }

        private void DrawFooterPanel(SpriteBatch sb)
        {
            var footerRect = new Rectangle(8, ControlSize.Y - FOOTER_HEIGHT + 6, ControlSize.X - 16, FOOTER_HEIGHT - 12);
            DrawPanel(sb, footerRect, Theme.BgMid);
        }

        private void DrawDynamicContent(SpriteBatch sb)
        {
            if (_font == null) return;

            DrawHeaderTitle(sb);
            DrawCloseButton(sb);
            DrawInstructions(sb);
            DrawEntryRows(sb);
            DrawFooterStatus(sb);
        }

        private void DrawHeaderTitle(SpriteBatch sb)
        {
            string title = "BLOOD CASTLE";
            float scale = FONT_SCALE_TITLE;
            Vector2 size = _font.MeasureString(title) * scale;
            Vector2 pos = new((ControlSize.X - size.X) / 2f, (_headerRect.Height - size.Y) / 2f + 4);
            pos = Translate(pos);

            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel != null)
            {
                sb.Draw(pixel, new Rectangle((int)pos.X - 18, (int)pos.Y - 4, (int)size.X + 36, (int)size.Y + 8), Theme.AccentGlow * 0.28f);
            }

            DrawTextWithShadow(sb, title, pos.X, pos.Y, scale, Theme.TextWhite);
        }

        private void DrawCloseButton(SpriteBatch sb)
        {
            Color bg = _closeHovered ? Theme.SlotHover : Theme.SlotBg;
            var rect = Translate(_closeButtonRect);
            DrawPanel(sb, rect, bg, _closeHovered);

            string label = "X";
            float scale = FONT_SCALE_BODY;
            Vector2 size = _font.MeasureString(label) * scale;
            Vector2 pos = new(
                rect.X + (rect.Width - size.X) / 2f,
                rect.Y + (rect.Height - size.Y) / 2f);

            DrawTextWithShadow(sb, label, pos.X, pos.Y, scale, Theme.TextGold);
        }

        private void DrawInstructions(SpriteBatch sb)
        {
            float x = _instructionRect.X + 10;
            float y = _instructionRect.Y + 8;
            var offset = DisplayRectangle.Location;
            x += offset.X;
            y += offset.Y;
            float lineHeight = _font.LineSpacing * FONT_SCALE_SMALL + 2;

            for (int i = 0; i < InstructionLines.Length; i++)
            {
                DrawTextWithShadow(sb, InstructionLines[i], x, y + i * lineHeight, FONT_SCALE_SMALL, Theme.TextGray);
            }
        }

        private void DrawEntryRows(SpriteBatch sb)
        {
            for (int i = 0; i < ENTRY_COUNT; i++)
            {
                bool isActive = i == _activeIndex;
                bool isHover = i == _hoveredIndex;

                Color bg = Theme.BgDark;
                if (isActive) bg = Theme.BgLight;
                if (isHover && isActive) bg = Theme.SlotHover;

                Color borderInner = isActive ? Theme.Accent : Theme.BorderInner;
                Color borderOuter = isActive ? Theme.AccentDim : Theme.BorderOuter;

                var rowRect = Translate(_entryRowRects[i]);
                UiDrawHelper.DrawPanel(sb, rowRect, bg, borderInner, borderOuter, Theme.BorderHighlight * 0.25f, isHover && isActive, Theme.Accent * 0.15f);

                string leftText = _entryLabels[i];
                string rightText = _entryRanges[i];

                Color leftColor = isActive ? Theme.TextGold : Theme.TextGray;
                Color rightColor = isActive ? Theme.TextWhite : Theme.TextDark;

                if (isActive && !_hasMatchingTicket)
                {
                    rightColor = _hasAnyTicket ? Theme.Warning : Theme.Danger;
                }

                float scale = FONT_SCALE_BODY;
                Vector2 leftSize = _font.MeasureString(leftText) * scale;
                Vector2 rightSize = _font.MeasureString(rightText) * scale;

                float leftX = rowRect.X + 12;
                float leftY = rowRect.Y + (rowRect.Height - leftSize.Y) / 2f;

                float rightX = rowRect.Right - rightSize.X - 12;
                float rightY = rowRect.Y + (rowRect.Height - rightSize.Y) / 2f;

                DrawTextWithShadow(sb, leftText, leftX, leftY, scale, leftColor);
                DrawTextWithShadow(sb, rightText, rightX, rightY, scale, rightColor);

                if (isActive)
                {
                    string actionText = _hasMatchingTicket ? "ENTER" : "TICKET";
                    Color actionColor = _hasMatchingTicket ? Theme.Success : Theme.Warning;
                    Vector2 actionSize = _font.MeasureString(actionText) * FONT_SCALE_SMALL;
                    float actionX = rowRect.Right - actionSize.X - 12;
                    float actionY = rowRect.Bottom - actionSize.Y - 4;
                    DrawTextWithShadow(sb, actionText, actionX, actionY, FONT_SCALE_SMALL, actionColor);
                }
            }
        }

        private void DrawFooterStatus(SpriteBatch sb)
        {
            string gateText = $"Active Gate: Blood Castle {_activeIndex + 1}";
            string statusText;
            Color statusColor;

            if (_hasMatchingTicket)
            {
                statusText = $"Ticket OK: Blood Castle {_expectedTicketLevel}";
                statusColor = Theme.Success;
            }
            else if (_hasAnyTicket)
            {
                statusText = $"Ticket mismatch: need BC{_expectedTicketLevel}, have BC{_foundTicketLevel}";
                statusColor = Theme.Warning;
            }
            else
            {
                statusText = "Invisibility Cloak required to enter.";
                statusColor = Theme.Danger;
            }

            float gateY = _footerRect.Y + 10;
            float statusY = gateY + _font.LineSpacing * FONT_SCALE_SMALL + 4;
            float gateX = _footerRect.X + 20;
            float statusX = gateX;
            var offset = DisplayRectangle.Location;
            gateX += offset.X;
            statusX += offset.X;
            gateY += offset.Y;
            statusY += offset.Y;

            DrawTextWithShadow(sb, gateText, gateX, gateY, FONT_SCALE_SMALL, Theme.TextGray);
            DrawTextWithShadow(sb, statusText, statusX, statusY, FONT_SCALE_SMALL, statusColor);
        }

        private void DrawTextWithShadow(SpriteBatch sb, string text, float x, float y, float scale, Color color)
        {
            sb.DrawString(_font, text, new Vector2(x + 1, y + 1), Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(_font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        // ───────────────────────── Interaction ─────────────────────────

        private void UpdateHoverStates(Point mousePos)
        {
            _closeHovered = Translate(_closeButtonRect).Contains(mousePos);
            _hoveredIndex = -1;

            for (int i = 0; i < ENTRY_COUNT; i++)
            {
                if (Translate(_entryRowRects[i]).Contains(mousePos))
                {
                    _hoveredIndex = i;
                    break;
                }
            }
        }

        private void HandleInput(Point mousePos)
        {
            bool leftJustPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed &&
                                   MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Released &&
                                    MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Pressed;

            if (leftJustPressed)
            {
                if (_closeHovered)
                {
                    _closePressed = true;
                    _mouseCaptured = true;
                    return;
                }

                if (_hoveredIndex != -1)
                {
                    _pressedIndex = _hoveredIndex;
                    _mouseCaptured = true;
                    return;
                }
            }

            if (leftJustReleased)
            {
                if (_closePressed)
                {
                    _closePressed = false;
                    if (_closeHovered)
                    {
                        CloseWindow();
                    }
                    _mouseCaptured = false;
                    return;
                }

                if (_pressedIndex != -1)
                {
                    int clickedIndex = _pressedIndex;
                    _pressedIndex = -1;
                    if (clickedIndex == _hoveredIndex)
                    {
                        HandleEntryClick(clickedIndex);
                    }
                    _mouseCaptured = false;
                }
            }
        }

        private void HandleWindowDrag(Point mousePos)
        {
            bool leftJustPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed &&
                                   MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released;

            if (leftJustPressed && Translate(_headerRect).Contains(mousePos) && !_closeHovered)
            {
                _isDragging = true;
                _dragOffset = new Point(mousePos.X - X, mousePos.Y - Y);
                _mouseCaptured = true;
            }

            if (_isDragging)
            {
                if (MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed)
                {
                    X = mousePos.X - _dragOffset.X;
                    Y = mousePos.Y - _dragOffset.Y;
                }
                else
                {
                    _isDragging = false;
                    _mouseCaptured = false;
                }
            }
        }

        private void HandleEntryClick(int index)
        {
            if (index != _activeIndex)
            {
                return;
            }

            if (!_hasMatchingTicket)
            {
                string msg = _hasAnyTicket
                    ? $"Blood Castle {_expectedTicketLevel} Invisibility Cloak required."
                    : "Invisibility Cloak required to enter.";
                RequestDialog.ShowInfo(msg);
                return;
            }

            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendBloodCastleEnterRequestAsync((byte)(_activeIndex + 1), _matchingTicketSlot);
            }

            CloseWindow();
        }

        // ───────────────────────── Data/State ─────────────────────────

        private void EnsureCharacterState()
        {
            if (_characterState != null) return;

            _characterState = MuGame.Network?.GetCharacterState();
            if (_characterState == null) return;

            _characterState.InventoryChanged += OnInventoryChanged;
        }

        private void OnInventoryChanged()
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                RefreshEntryOptions();
            });
        }

        private void RefreshEntryOptions()
        {
            if (_characterState == null)
            {
                _activeIndex = 0;
                return;
            }

            // Determine if we use special level ranges for Dark Lord, Dark, Rage Fighter
            _useSpecialLevels = IsSpecialClass(_characterState.Class);

            _activeIndex = ComputeActiveIndex();
            _expectedTicketLevel = (byte)(_activeIndex + 1);

            var ranges = _useSpecialLevels ? SpecialLevelRanges : NormalLevelRanges;

            for (int i = 0; i < ENTRY_COUNT; i++)
            {
                _entryLabels[i] = $"Blood Castle {i + 1}";
                if (i == ENTRY_COUNT - 1)
                {
                    _entryRanges[i] = "Master Level";
                }
                else
                {
                    _entryRanges[i] = $"Lv {ranges[i].Min}~{ranges[i].Max}";
                }
            }

            RefreshTicketStatus();
        }

        private bool IsSpecialClass(CharacterClassNumber characterClass)
        {
            // Dark Lord, Dark (Dark Wizard), Rage Fighter use special level ranges
            // Based on SourceMain5.2 logic: CLASS_DARK, CLASS_DARK_LORD, CLASS_RAGEFIGHTER
            int baseClass = (int)characterClass >> 3;
            return baseClass == 2 || baseClass == 3 || baseClass == 6; // Dark Wizard=1, Dark Lord=3, Rage Fighter=6
        }

        private void RefreshTicketStatus()
        {
            _hasMatchingTicket = false;
            _hasAnyTicket = false;
            _matchingTicketSlot = 0xFF;
            _foundTicketLevel = 0;

            if (_characterState == null) return;

            foreach (var item in _characterState.GetInventoryItems())
            {
                if (item.Key < InventoryControl.InventorySlotOffsetConstant)
                {
                    continue; // equipment slots
                }

                var data = item.Value;
                if (data == null || data.Length < 6)
                {
                    continue;
                }

                byte group = ItemDatabase.GetItemGroup(data);
                short id = data[0];

                // Invisibility Cloak / Blood Castle Ticket (group 13, id 18)
                if (group != 13 || id != 18)
                {
                    continue;
                }

                _hasAnyTicket = true;
                var details = ItemDatabase.ParseItemDetails(data);
                _foundTicketLevel = (byte)details.Level;

                if (details.Level == _expectedTicketLevel)
                {
                    _hasMatchingTicket = true;
                    _matchingTicketSlot = item.Key;
                    break;
                }
            }
        }

        private int ComputeActiveIndex()
        {
            if (_characterState == null)
            {
                return 0;
            }

            if (_characterState.MasterLevel > 0)
            {
                return ENTRY_COUNT - 1;
            }

            int level = _characterState.Level;
            int result = 0;

            var ranges = _useSpecialLevels ? SpecialLevelRanges : NormalLevelRanges;

            for (int i = 0; i < ENTRY_COUNT - 1; i++)
            {
                if (level >= ranges[i].Min && level <= ranges[i].Max)
                {
                    result = i;
                    break;
                }
            }

            return result;
        }

        private void SendCloseNpcRequest()
        {
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendCloseNpcRequestAsync();
            }
        }

        private Rectangle Translate(Rectangle rect)
            => new(DisplayRectangle.X + rect.X, DisplayRectangle.Y + rect.Y, rect.Width, rect.Height);

        private Vector2 Translate(Vector2 pos)
            => new(pos.X + DisplayRectangle.X, pos.Y + DisplayRectangle.Y);
    }
}
