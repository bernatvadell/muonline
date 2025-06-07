using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Models
{
    public class ChatMessage
    {
        public string SenderID { get; }
        public string Text { get; }
        public MessageType Type { get; }
        public DateTime Timestamp { get; }

        public string DisplayText { get; private set; }

        public ChatMessage(string senderId, string text, MessageType type)
        {
            SenderID = senderId ?? string.Empty;
            Text = text ?? string.Empty;
            Type = type;
            Timestamp = DateTime.UtcNow;
            UpdateDisplayText();
        }

        public void UpdateDisplayText()
        {
            if (!string.IsNullOrEmpty(SenderID))
            {
                DisplayText = $"{SenderID} : {Text}";
            }
            else
            {
                DisplayText = Text;
            }
        }

        public Vector2 Measure(SpriteFont font, float scale)
        {
            if (font != null && !string.IsNullOrEmpty(DisplayText))
            {
                return font.MeasureString(DisplayText) * scale;
            }
            return Vector2.Zero;
        }
    }

    public class ChatMessageEventArgs : EventArgs
    {
        public string Message { get; }
        public string Receiver { get; } // Null for public/group messages
        public MessageType MessageType { get; }

        public ChatMessageEventArgs(string message, MessageType type, string receiver = null)
        {
            Message = message;
            MessageType = type;
            Receiver = receiver;
        }
    }
}