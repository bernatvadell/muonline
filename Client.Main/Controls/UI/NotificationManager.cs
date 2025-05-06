// NotificationManager.cs
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Client.Main.Controls.UI
{
    /// <summary>
    /// Manages on-screen floating text notifications.
    /// </summary>
    public class NotificationManager : UIControl
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly List<FloatingText> _active = new List<FloatingText>();
        private readonly object _sync = new object();

        private Vector2 _spawnCenter;
        private float _latestTotalSeconds;

        private const float VERTICAL_GAP = 4f; // Vertical gap between notifications

        // ───────────────────────── Constructors ─────────────────────────
        public NotificationManager()
        {
            Visible = true;
            Interactive = false;

            var viewport = GraphicsManager.Instance.GraphicsDevice.Viewport;
            _spawnCenter = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.75f);
        }

        // ────────────────────────── Public API ──────────────────────────
        /// <summary>
        /// Adds a new notification, using the last known game time.
        /// </summary>
        public void AddNotification(string text, Color color)
        {
            AddNotificationInternal(text, color, _latestTotalSeconds);
        }

        /// <summary>
        /// Adds a new notification at the specified game time.
        /// </summary>
        public void AddNotification(string text, Color color, GameTime gameTime)
        {
            AddNotificationInternal(text, color, (float)gameTime.TotalGameTime.TotalSeconds);
        }

        // ──────────────────────── Private Methods ───────────────────────
        private void AddNotificationInternal(string text, Color color, float creationTime)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            lock (_sync)
            {
                var note = new FloatingText(text, color, _spawnCenter, creationTime);
                _active.Insert(0, note);

                if (Parent != null)
                    Parent.Controls.Add(note);

                RecalculateStack();
            }
        }

        /// <summary>
        /// Arranges notifications in a vertical stack without overlap.
        /// </summary>
        private void RecalculateStack()
        {
            float currentY = _spawnCenter.Y;
            foreach (var note in _active)
            {
                note.SetCenterY(currentY);
                currentY -= (note.ScaledHeight + VERTICAL_GAP);
            }
        }

        // ───────────────────────── Overrides ──────────────────────────
        public override void Update(GameTime gameTime)
        {
            _latestTotalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
            bool removedAny = false;

            lock (_sync)
            {
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    var note = _active[i];
                    if (!note.Visible || note.Status == GameControlStatus.Disposed)
                    {
                        if (Parent != null)
                            Parent.Controls.Remove(note);

                        _active.RemoveAt(i);
                        removedAny = true;
                    }
                }

                if (removedAny)
                {
                    RecalculateStack();
                }
            }
        }

        // Draw: empty because each FloatingText draws itself
    }
}
