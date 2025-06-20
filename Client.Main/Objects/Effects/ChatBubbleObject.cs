using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Objects.Player;
using Client.Main.Models;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Simple chat bubble displayed above a player for a short time.
    /// </summary>
    public class ChatBubbleObject : WorldObject
    {
        private const float DefaultLifetime = 5f;
        private const float OffsetZ = 60f;
        private const int PixelGap = 8;

        private readonly string _text;
        private readonly string _playerName;
        private readonly ushort _targetId;
        private readonly float _lifetime;

        private LabelControl _nameLabel;
        private LabelControl _textLabel;
        private SpriteFont _font;
        private float _elapsed;

        /// <summary>
        /// Creates a new chat bubble.
        /// </summary>
        /// <param name="text">Message text to display.</param>
        /// <param name="targetId">Network id of the player.</param>
        /// <param name="playerName">Name of the player.</param>
        /// <param name="lifetime">Optional lifetime in seconds.</param>
        public ChatBubbleObject(string text, ushort targetId, string playerName, float lifetime = DefaultLifetime)
        {
            _text = text ?? string.Empty;
            _playerName = playerName ?? string.Empty;
            _targetId = targetId;
            _lifetime = lifetime;

            IsTransparent = true;
            AffectedByTransparency = false;
        }

        public override async Task Load()
        {
            _font = GraphicsManager.Instance.Font;

            _nameLabel = new LabelControl
            {
                Text = _playerName,
                FontSize = 10f,
                TextColor = Color.Yellow,
                HasShadow = true,
                ShadowColor = Color.Black,
                ShadowOpacity = 0.8f,
                BackgroundColor = new Color(20, 20, 60, 180),
                Padding = new Margin { Left = 4, Right = 4, Top = 2, Bottom = 2 },
                UseManualPosition = true,
                Visible = false
            };

            _textLabel = new LabelControl
            {
                Text = _text,
                FontSize = 10f,
                TextColor = Color.White,
                HasShadow = true,
                ShadowColor = Color.Black,
                ShadowOpacity = 0.8f,
                BackgroundColor = new Color(0, 0, 0, 160),
                Padding = new Margin { Left = 4, Right = 4, Top = 2, Bottom = 2 },
                UseManualPosition = true,
                Visible = false
            };

            if (World?.Scene != null)
            {
                World.Scene.Controls.Add(_nameLabel);
                World.Scene.Controls.Add(_textLabel);
                await _nameLabel.Load();
                await _textLabel.Load();
            }

            Status = GameControlStatus.Ready;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Status != GameControlStatus.Ready) return;

            _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_elapsed >= _lifetime)
            {
                World?.Scene?.Controls.Remove(_nameLabel);
                World?.Scene?.Controls.Remove(_textLabel);
                _nameLabel.Dispose();
                _textLabel.Dispose();
                World?.RemoveObject(this);
                Dispose();
                return;
            }

            var target = ResolveTarget();
            if (target == null || target.Hidden || target.Status != GameControlStatus.Ready)
            {
                _nameLabel.Visible = false;
                _textLabel.Visible = false;
                return;
            }

            UpdateLabelPosition(target);
        }

        public override void Draw(GameTime gameTime) { }

        private WalkerObject ResolveTarget()
        {
            if (World == null) return null;
            return World.TryGetWalkerById(_targetId, out var walker) ? walker : null;
        }

        private void UpdateLabelPosition(WalkerObject target)
        {
            Vector3 anchor = new(
                (target.BoundingBoxWorld.Min.X + target.BoundingBoxWorld.Max.X) * 0.5f,
                (target.BoundingBoxWorld.Min.Y + target.BoundingBoxWorld.Max.Y) * 0.5f,
                target.BoundingBoxWorld.Max.Z + OffsetZ);

            Vector3 screen = GraphicsDevice.Viewport.Project(
                anchor,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            if (screen.Z < 0f || screen.Z > 1f)
            {
                _nameLabel.Visible = false;
                _textLabel.Visible = false;
                return;
            }

            float scaleName = _font != null ? _nameLabel.FontSize / _font.LineSpacing : 1f;
            float scaleText = _font != null ? _textLabel.FontSize / _font.LineSpacing : 1f;

            Vector2 nameSize = _font != null ? _font.MeasureString(_nameLabel.Text) * scaleName : Vector2.Zero;
            Vector2 textSize = _font != null ? _font.MeasureString(_textLabel.Text) * scaleText : Vector2.Zero;

            int nameWidth = (int)(nameSize.X + _nameLabel.Padding.Left + _nameLabel.Padding.Right);
            int textWidth = (int)(textSize.X + _textLabel.Padding.Left + _textLabel.Padding.Right);
            int nameHeight = (int)(nameSize.Y + _nameLabel.Padding.Top + _nameLabel.Padding.Bottom);
            int textHeight = (int)(textSize.Y + _textLabel.Padding.Top + _textLabel.Padding.Bottom);

            int maxWidth = Math.Max(nameWidth, textWidth);

            _nameLabel.ControlSize = new Point(maxWidth, nameHeight);
            _textLabel.ControlSize = new Point(maxWidth, textHeight);

            int bubbleHeight = nameHeight + textHeight;

            int bubbleX = (int)(screen.X - maxWidth / 2f);

            _nameLabel.X = bubbleX;
            _nameLabel.Y = (int)(screen.Y - bubbleHeight - PixelGap);

            _textLabel.X = bubbleX;
            _textLabel.Y = _nameLabel.Y + nameHeight;

            _nameLabel.Visible = true;
            _textLabel.Visible = true;
        }

        public override void Dispose()
        {
            if (_nameLabel != null)
            {
                _nameLabel.Parent?.Controls.Remove(_nameLabel);
                _nameLabel.Dispose();
            }
            if (_textLabel != null)
            {
                _textLabel.Parent?.Controls.Remove(_textLabel);
                _textLabel.Dispose();
            }
            base.Dispose();
        }
    }
}
