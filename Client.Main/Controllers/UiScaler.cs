using System;
using Microsoft.Xna.Framework;

namespace Client.Main.Controllers
{
    /// <summary>
    /// Centralizes UI scaling between the virtual layout resolution and the physical back buffer.
    /// </summary>
    public static class UiScaler
    {
        private const float MinScale = 0.0001f;

        public static Point VirtualSize { get; private set; } = new(1280, 720);
        public static Point ActualSize { get; private set; } = new(1280, 720);
        public static float Scale { get; private set; } = 1f;
        public static float InverseScale { get; private set; } = 1f;
        public static Vector2 Offset { get; private set; } = Vector2.Zero;
        public static Matrix SpriteTransform { get; private set; } = Matrix.Identity;

        public static void Configure(int actualWidth, int actualHeight, int virtualWidth, int virtualHeight)
        {
            if (virtualWidth <= 0 || virtualHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(virtualWidth), "Virtual resolution must be positive.");
            }

            // Store original screen size for mouse conversion
            ActualSize = new Point(Math.Max(1, actualWidth), Math.Max(1, actualHeight));
            VirtualSize = new Point(Math.Max(1, virtualWidth), Math.Max(1, virtualHeight));

            // Calculate UI scale based on original screen size vs virtual size
            float scaleX = (float)ActualSize.X / VirtualSize.X;
            float scaleY = (float)ActualSize.Y / VirtualSize.Y;

            Scale = MathF.Max(Math.Min(scaleX, scaleY), MinScale);
            InverseScale = 1f / Scale;

            float scaledWidth = VirtualSize.X * Scale;
            float scaledHeight = VirtualSize.Y * Scale;

            Offset = new Vector2(
                MathF.Max(0f, (ActualSize.X - scaledWidth) * 0.5f),
                MathF.Max(0f, (ActualSize.Y - scaledHeight) * 0.5f));

            // Apply render scale to the transform for rendering
            float finalScale = Scale * Constants.RENDER_SCALE;
            var transform = Matrix.CreateScale(finalScale, finalScale, 1f);
            transform.Translation = new Vector3(Offset * Constants.RENDER_SCALE, 0f);
            SpriteTransform = transform;
        }

        public static Point ToVirtual(Point actual)
        {
            float x = (actual.X - Offset.X) * InverseScale;
            float y = (actual.Y - Offset.Y) * InverseScale;
            return new Point((int)MathF.Round(x), (int)MathF.Round(y));
        }

        public static Point ToActual(Point virtualPoint)
        {
            float x = virtualPoint.X * Scale + Offset.X;
            float y = virtualPoint.Y * Scale + Offset.Y;
            return new Point((int)MathF.Round(x), (int)MathF.Round(y));
        }

        public static Rectangle ToActual(Rectangle virtualRect)
        {
            Point topLeft = ToActual(virtualRect.Location);
            int width = (int)MathF.Round(virtualRect.Width * Scale);
            int height = (int)MathF.Round(virtualRect.Height * Scale);
            return new Rectangle(topLeft.X, topLeft.Y, width, height);
        }
    }
}
