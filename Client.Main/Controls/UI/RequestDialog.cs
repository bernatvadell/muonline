using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using Client.Main.Controls.UI.Common;

namespace Client.Main.Controls.UI
{
    /// <summary>
    /// Simple dialog with Accept and Reject buttons.
    /// </summary>
    public class RequestDialog : DialogControl
    {
        private readonly TextureControl _background;
        private readonly LabelControl _label;
        private readonly ButtonControl _acceptButton;
        private readonly ButtonControl _rejectButton;
        private static readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<RequestDialog>();

        /// <summary>
        /// Invoked when the user accepts the request.
        /// </summary>
        public event EventHandler Accepted;

        /// <summary>
        /// Invoked when the user rejects the request.
        /// </summary>
        public event EventHandler Rejected;

        /// <summary>
        /// Gets or sets the dialog text.
        /// </summary>
        public string Text
        {
            get => _label.Text;
            set => _label.Text = value ?? string.Empty;
        }

        private RequestDialog()
        {
            AutoViewSize = false;
            BorderColor = Color.Gray * 0.7f;
            BorderThickness = 1;
            BackgroundColor = Color.Black * 0.8f;

            _background = new TextureControl
            {
                TexturePath = "Interface/message_back.tga",
                AutoViewSize = false,
                ViewSize = new Point(352, 113),
                BlendState = Blendings.Alpha,
            };
            Controls.Add(_background);

            _label = new LabelControl
            {
                FontSize = 14f,
                TextColor = Color.White,
                TextAlign = HorizontalAlign.Center,
            };
            Controls.Add(_label);

            _acceptButton = new ButtonControl
            {
                Text = "Accept",
                ViewSize = new Point(70, 30),
                ControlSize = new Point(70, 30),
                FontSize = 12f,
                BackgroundColor = new Color(0.1f, 0.3f, 0.1f, 0.8f),
                HoverBackgroundColor = new Color(0.15f, 0.4f, 0.15f, 0.9f),
                PressedBackgroundColor = new Color(0.05f, 0.2f, 0.05f, 0.9f),
                BorderColor = Color.DarkGreen,
                BorderThickness = 1,
                Align = ControlAlign.HorizontalCenter,
            };
            _acceptButton.Click += (s, e) => { Accepted?.Invoke(this, EventArgs.Empty); Close(); };
            Controls.Add(_acceptButton);

            _rejectButton = new ButtonControl
            {
                Text = "Reject",
                ViewSize = new Point(70, 30),
                ControlSize = new Point(70, 30),
                FontSize = 12f,
                BackgroundColor = new Color(0.3f, 0.1f, 0.1f, 0.8f),
                HoverBackgroundColor = new Color(0.4f, 0.15f, 0.15f, 0.9f),
                PressedBackgroundColor = new Color(0.2f, 0.05f, 0.05f, 0.9f),
                BorderColor = Color.DarkRed,
                BorderThickness = 1,
                Align = ControlAlign.HorizontalCenter,
            };
            _rejectButton.Click += (s, e) => { Rejected?.Invoke(this, EventArgs.Empty); Close(); };
            Controls.Add(_rejectButton);

            AdjustLayout();
        }

        private void AdjustLayout()
        {
            int width = Math.Max(_label.ControlSize.X + 40, 200);
            int height = 140;
            ControlSize = new Point(width, height);
            ViewSize = ControlSize;

            _label.X = (width - _label.ControlSize.X) / 2;
            _label.Y = 25;

            int gap = 10;
            int totalButtonsWidth = _acceptButton.ViewSize.X + _rejectButton.ViewSize.X + gap;
            _acceptButton.X = (width - totalButtonsWidth) / 2;
            _acceptButton.Y = height - _acceptButton.ViewSize.Y - 20;

            _rejectButton.X = _acceptButton.X + _acceptButton.ViewSize.X + gap;
            _rejectButton.Y = _acceptButton.Y;
        }

        /// <summary>
        /// Shows a request dialog with the given text.
        /// </summary>
        public static RequestDialog Show(string text, Action onAccept = null, Action onReject = null)
        {
            var scene = MuGame.Instance?.ActiveScene;
            if (scene == null)
            {
                _logger?.LogDebug("[RequestDialog.Show] Error: ActiveScene is null.");
                return null;
            }

            foreach (var existing in scene.Controls.OfType<RequestDialog>().ToList())
                existing.Close();

            var dlg = new RequestDialog { Text = text };
            if (onAccept != null)
                dlg.Accepted += (s, e) => onAccept();
            if (onReject != null)
                dlg.Rejected += (s, e) => onReject();

            dlg.ShowDialog();
            dlg.BringToFront();
            return dlg;
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            DrawBackground();
            DrawBorder();

            base.Draw(gameTime);
        }
    }
}