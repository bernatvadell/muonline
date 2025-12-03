using System;
using System.Collections.Generic;
using Client.Main.Content;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game.Common
{
    /// <summary>
    /// Small pulsing shine overlay for inventory-like grids to highlight jewels.
    /// </summary>
    public static class JewelShineOverlay
    {
        private const string TexturePath = "Effect/Shiny05.jpg";
        private const float CycleSeconds = 3.4f;
        private const float FlashSeconds = 0.6f;
        private const float MinAlpha = 0.2f;
        private const float MaxAlpha = 0.85f;
        private const float BaseScale = 0.15f;
        private const float ScaleJitter = 0.2f;

        private static Texture2D _texture;

        public static bool ShouldShine(InventoryItem item)
        {
            return item?.Definition?.Name?.StartsWith("Jewel of", StringComparison.OrdinalIgnoreCase) == true;
        }

        public static void Draw(SpriteBatch spriteBatch, InventoryItem item, Rectangle rect, GameTime gameTime, float alpha = 1f)
        {
            if (spriteBatch == null || !ShouldShine(item))
                return;

            var tex = EnsureTexture();
            if (tex == null)
                return;

            float time = (float)(gameTime?.TotalGameTime.TotalSeconds ?? 0d);
            float seed = ((item?.Definition?.Id ?? 0) * 0.31f) + ((item?.Details.Level ?? 0) * 0.17f);
            float phase = (time + seed) % CycleSeconds;
            if (phase > FlashSeconds)
                return;

            float normalized = phase / FlashSeconds;
            float fade = normalized < 0.5f ? normalized * 2f : (1f - normalized) * 2f;
            float intensity = MathHelper.Lerp(MinAlpha, MaxAlpha, fade) * alpha;

            float fitScale = MathF.Min(rect.Width / (float)tex.Width, rect.Height / (float)tex.Height);
            float scale = (BaseScale + ScaleJitter * fade) * fitScale;

            Vector2 center = new(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
            Vector2 origin = new(tex.Width * 0.5f, tex.Height * 0.5f);
            float rotation = (time + seed) * 1.9f;

            spriteBatch.Draw(tex, center, null, Color.White * intensity, rotation, origin, scale, SpriteEffects.None, 0f);
        }

        private static Texture2D EnsureTexture()
        {
            if (_texture != null)
                return _texture;

            _texture = TextureLoader.Instance.GetTexture2D(TexturePath);
            return _texture;
        }

        public static void DrawBatch(
            SpriteBatch spriteBatch,
            IReadOnlyList<(InventoryItem Item, Rectangle Rect)> entries,
            GameTime gameTime,
            float alpha,
            Matrix? transform = null)
        {
            if (entries == null || entries.Count == 0 || spriteBatch == null)
                return;

            using var scope = new SpriteBatchScope(
                spriteBatch,
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                effect: null,
                transform: transform ?? Matrix.Identity);

            for (int i = 0; i < entries.Count; i++)
            {
                var (item, rect) = entries[i];
                Draw(spriteBatch, item, rect, gameTime, alpha);
            }
        }
    }
}
