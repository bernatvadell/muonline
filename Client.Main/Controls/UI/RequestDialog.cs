using Client.Main.Models;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using Client.Main.Controls.UI.Common;
using Client.Main.Controllers;

namespace Client.Main.Controls.UI
{
    public class RequestDialog : DialogControl
    {
        private const int BASE_BG_WIDTH = 352;
        private const int BASE_BG_HEIGHT = 113;
        private const int SIDE_PAD = 20;
        private const int TOP_PAD = 25;
        private const int BTN_GAP = 10;

        private readonly TextureControl _background;
        private readonly LabelControl _label;
        private readonly ButtonControl _acceptButton;
        private readonly ButtonControl _rejectButton;

        private bool _infoOnly = false; // single OK button mode

        private static readonly ILogger _logger
            = MuGame.AppLoggerFactory?.CreateLogger<RequestDialog>();

        public event EventHandler Accepted;
        public event EventHandler Rejected;

        private string _rawText = string.Empty;

        public string Text
        {
            get => _rawText;
            set
            {
                _rawText = value ?? string.Empty;
                UpdateWrappedText();
                AdjustSizeAndLayout();
            }
        }

        private RequestDialog()
        {
            Interactive = true; // consume mouse interactions over the dialog
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;
            AutoViewSize = false;
            BorderColor = Color.Gray * 0.7f;
            BorderThickness = 1;
            BackgroundColor = Color.Black * 0.8f;

            _background = new TextureControl
            {
                TexturePath = "Interface/message_back.tga",
                AutoViewSize = false,
                ViewSize = new Point(BASE_BG_WIDTH, BASE_BG_HEIGHT),
                BlendState = Blendings.Alpha
            };
            Controls.Add(_background);

            _label = new LabelControl
            {
                FontSize = 14f,
                TextColor = Color.White,
                TextAlign = HorizontalAlign.Center
            };
            Controls.Add(_label);

            _acceptButton = MakeButton(
                text: "Accept",
                bg: new Color(0.10f, 0.30f, 0.10f, 0.80f),
                bgH: new Color(0.15f, 0.40f, 0.15f, 0.90f),
                bgP: new Color(0.05f, 0.20f, 0.05f, 0.90f),
                border: Color.DarkGreen);
            _acceptButton.Click += (s, e) => { Accepted?.Invoke(this, EventArgs.Empty); Close(); };
            Controls.Add(_acceptButton);

            _rejectButton = MakeButton(
                text: "Reject",
                bg: new Color(0.30f, 0.10f, 0.10f, 0.80f),
                bgH: new Color(0.40f, 0.15f, 0.15f, 0.90f),
                bgP: new Color(0.20f, 0.05f, 0.05f, 0.90f),
                border: Color.DarkRed);
            _rejectButton.Click += (s, e) => { Rejected?.Invoke(this, EventArgs.Empty); Close(); };
            Controls.Add(_rejectButton);

            AdjustButtonSize(_acceptButton);
            AdjustButtonSize(_rejectButton);

            UpdateWrappedText();
            AdjustSizeAndLayout();
        }

        private void SetButtonLabels(string acceptText, string rejectText)
        {
            bool layoutNeedsUpdate = false;
            if (!string.IsNullOrWhiteSpace(acceptText) && !_acceptButton.Text.Equals(acceptText, StringComparison.Ordinal))
            {
                _acceptButton.Text = acceptText;
                AdjustButtonSize(_acceptButton);
                layoutNeedsUpdate = true;
            }

            if (!string.IsNullOrWhiteSpace(rejectText) && !_rejectButton.Text.Equals(rejectText, StringComparison.Ordinal))
            {
                _rejectButton.Text = rejectText;
                AdjustButtonSize(_rejectButton);
                layoutNeedsUpdate = true;
            }

            if (layoutNeedsUpdate)
            {
                AdjustSizeAndLayout();
            }
        }

        private static void AdjustButtonSize(ButtonControl button)
        {
            var font = GraphicsManager.Instance?.Font;
            if (font == null)
            {
                return;
            }

            float scale = button.FontSize / Constants.BASE_FONT_SIZE;
            float width = font.MeasureString(button.Text ?? string.Empty).X * scale;
            int paddedWidth = (int)Math.Ceiling(width) + 24;
            int height = button.ViewSize.Y;
            int finalWidth = Math.Max(paddedWidth, button.ViewSize.X);
            button.ViewSize = new Point(finalWidth, height);
            button.ControlSize = new Point(finalWidth, height);
        }

        private void SetInfoMode()
        {
            _infoOnly = true;
            _acceptButton.Text = "OK";
            AdjustButtonSize(_acceptButton);
            _rejectButton.Visible = false;
            AdjustSizeAndLayout();
        }
        private void AdjustSizeAndLayout()
        {
            int width = BASE_BG_WIDTH;

            int textHeight = _label.ControlSize.Y;
            int buttonsBlock = _acceptButton.ViewSize.Y + 20;
            int wantedH = TOP_PAD + textHeight + 20 + buttonsBlock;
            int height = Math.Max(BASE_BG_HEIGHT, wantedH);

            ControlSize = new Point(width, height);
            ViewSize = ControlSize;

            _background.X = 0;
            _background.Y = 0;
            _background.ViewSize = new Point(width + 170, height + 25);

            _label.X = (width - _label.ControlSize.X) / 2;
            _label.Y = TOP_PAD;

            int btnY = height - _acceptButton.ViewSize.Y - 20;
            if (_infoOnly)
            {
                // Center single OK button
                int startX = (width - _acceptButton.ViewSize.X) / 2;
                _acceptButton.X = startX;
                _acceptButton.Y = btnY;
            }
            else
            {
                int totalBtnsWidth = _acceptButton.ViewSize.X + _rejectButton.ViewSize.X + BTN_GAP;
                int startX = (width - totalBtnsWidth) / 2;
                _acceptButton.X = startX;
                _acceptButton.Y = btnY;
                _rejectButton.X = startX + _acceptButton.ViewSize.X + BTN_GAP;
                _rejectButton.Y = btnY;
            }
        }

        private void UpdateWrappedText()
        {
            var font = GraphicsManager.Instance?.Font;
            if (font == null) { _label.Text = _rawText; return; }

            float scale = _label.FontSize / Constants.BASE_FONT_SIZE;
            float maxWidth = BASE_BG_WIDTH - SIDE_PAD * 2;
            var words = _rawText.Split(' ');
            var sb = new StringBuilder();
            var current = new StringBuilder();

            foreach (var w in words)
            {
                string test = (current.Length == 0) ? w : current + " " + w;
                float tw = font.MeasureString(test).X * scale;

                if (tw <= maxWidth)
                {
                    current.Clear();
                    current.Append(test);
                }
                else
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(current);
                    current.Clear();
                    current.Append(w);
                }
            }
            if (current.Length > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(current);
            }

            _label.Text = sb.ToString();
        }

        private static ButtonControl MakeButton(string text,
                                                Color bg, Color bgH, Color bgP,
                                                Color border)
            => new ButtonControl
            {
                Text = text,
                FontSize = 12f,
                ViewSize = new Point(70, 30),
                ControlSize = new Point(70, 30),
                BackgroundColor = bg,
                HoverBackgroundColor = bgH,
                PressedBackgroundColor = bgP,
                BorderColor = border,
                BorderThickness = 1
            };

        public static RequestDialog Show(string text,
                                         Action onAccept = null,
                                         Action onReject = null,
                                         string acceptText = null,
                                         string rejectText = null)
        {
            var scene = MuGame.Instance?.ActiveScene;
            if (scene == null)
            {
                _logger?.LogDebug("[RequestDialog.Show] Error: ActiveScene is null.");
                return null;
            }

            foreach (var r in scene.Controls.OfType<RequestDialog>().ToList())
                r.Close();

            var dlg = new RequestDialog { Text = text };
            dlg.SetButtonLabels(acceptText, rejectText);
            if (onAccept != null) dlg.Accepted += (s, e) => onAccept();
            if (onReject != null) dlg.Rejected += (s, e) => onReject();

            dlg.ShowDialog();
            dlg.BringToFront();
            return dlg;
        }

        /// <summary>
        /// Shows a simple informational dialog with a centered OK button.
        /// </summary>
        public static RequestDialog ShowInfo(string text)
        {
            var scene = MuGame.Instance?.ActiveScene;
            if (scene == null)
            {
                _logger?.LogDebug("[RequestDialog.ShowInfo] Error: ActiveScene is null.");
                return null;
            }

            // Close existing dialogs
            foreach (var r in scene.Controls.OfType<RequestDialog>().ToList())
                r.Close();

            var dlg = new RequestDialog { Text = text };
            dlg.SetInfoMode();
            dlg.ShowDialog();
            dlg.BringToFront();
            return dlg;
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible) return;

            using (new SpriteBatchScope(
                GraphicsManager.Instance.Sprite,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                transform: UiScaler.SpriteTransform))
            {
                DrawBackground();
                DrawBorder();
            }

            base.Draw(gameTime);
        }

        // Consume clicks anywhere on the dialog (background or label), so they don't reach the world.
        public override bool OnClick()
        {
            return true; // don't propagate; buttons will handle their own click events
        }
    }
}
