using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Objects.Player;
using Client.Main.Models;
using System.Text;

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
        private const int MaxBubbleWidth = 200;

        private string _text;
        private readonly string _playerName;
        private readonly ushort _targetId;
        private float _lifetime;
        private float _originalLifetime;

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
            _originalLifetime = lifetime;

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
                UseControlSizeBackground = true,
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
                UseControlSizeBackground = true,
                Visible = false
            };

            _textLabel.Text = WrapText(_textLabel.Text, _textLabel.FontSize, MaxBubbleWidth);

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

            Vector2 nameSize = MeasureLabelSize(_nameLabel);
            Vector2 textSize = MeasureLabelSize(_textLabel);

            int nameWidth = (int)nameSize.X + _nameLabel.Padding.Left + _nameLabel.Padding.Right;
            int textWidth = (int)textSize.X + _textLabel.Padding.Left + _textLabel.Padding.Right;

            int nameHeight = (int)nameSize.Y + _nameLabel.Padding.Top + _nameLabel.Padding.Bottom;
            int textHeight = (int)textSize.Y + _textLabel.Padding.Top + _textLabel.Padding.Bottom;

            int maxWidth = Math.Max(nameWidth, textWidth);

            _nameLabel.ControlSize = new Point(
                maxWidth - (_nameLabel.Padding.Left + _nameLabel.Padding.Right),
                (int)nameSize.Y);
            _textLabel.ControlSize = new Point(
                maxWidth - (_textLabel.Padding.Left + _textLabel.Padding.Right),
                (int)textSize.Y);

            int bubbleHeight = nameHeight + textHeight;

            int bubbleX = (int)(screen.X - maxWidth / 2f);

            _nameLabel.X = bubbleX;
            _nameLabel.Y = (int)(screen.Y - bubbleHeight - PixelGap);

            _textLabel.X = bubbleX;
            _textLabel.Y = _nameLabel.Y + nameHeight;

            _nameLabel.Visible = true;
            _textLabel.Visible = true;
        }

        private Vector2 MeasureLabelSize(LabelControl label)
        {
            if (_font == null)
                return Vector2.Zero;

            float scale = label.FontSize / Constants.BASE_FONT_SIZE;
            Vector2 size = _font.MeasureString(label.Text) * scale;

            if (label.HasShadow)
            {
                size.X += (float)Math.Ceiling(Math.Abs(label.ShadowOffset.X));
                size.Y += (float)Math.Ceiling(Math.Abs(label.ShadowOffset.Y));
            }

            if (label.IsBold)
            {
                size.X += (float)Math.Ceiling(label.BoldStrength * 2);
                size.Y += (float)Math.Ceiling(label.BoldStrength * 2);
            }

            return size;
        }

        private string WrapText(string rawText, float fontSize, int maxWidth)
        {
            if (_font == null || string.IsNullOrEmpty(rawText))
                return rawText;

            float scale = fontSize / Constants.BASE_FONT_SIZE;
            var words = rawText.Split(' ');
            var sb = new StringBuilder();
            var current = new StringBuilder();

            foreach (var w in words)
            {
                string test = current.Length == 0 ? w : current + " " + w;
                float width = _font.MeasureString(test).X * scale;

                if (width <= maxWidth)
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

            return sb.ToString();
        }

        public void AppendMessage(string newMessage)
        {
            if (string.IsNullOrEmpty(newMessage)) return;
            
            _text = newMessage + "\n" + _text;
            _lifetime = _elapsed + _originalLifetime;
            
            if (_textLabel != null)
            {
                _textLabel.Text = WrapText(_text, _textLabel.FontSize, MaxBubbleWidth);
            }
        }

        public ushort TargetId => _targetId;

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
