using System;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Periodic flash that hovers around a dropped item to make it easier to spot.
    /// </summary>
    public class DroppedItemShineEffect : SpriteObject
    {
        private const float FlashDuration = 0.55f;
        private const float MinInterval = 2.4f;
        private const float MaxInterval = 4.2f;
        private const float BaseScale = 0.65f;
        private const float MaxScale = 1.1f;

        private float _timeUntilNextFlash;
        private float _flashElapsed;
        private bool _isFlashing;
        private Vector3 _flashOffset;

        public override string TexturePath => "Effect/Shiny05.jpg";

        public DroppedItemShineEffect()
        {
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            IsTransparent = true;
            AffectedByTransparency = true;
            LightEnabled = false;
            Alpha = 0f;
            Scale = BaseScale;
            BoundingBoxLocal = new BoundingBox(Vector3.Zero, Vector3.Zero);

            ScheduleNextFlash();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Status != GameControlStatus.Ready)
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f)
                return;

            if (!_isFlashing)
            {
                _timeUntilNextFlash -= dt;
                if (_timeUntilNextFlash <= 0f)
                {
                    BeginFlash();
                }
                return;
            }

            _flashElapsed += dt;
            float t = _flashElapsed / FlashDuration;

            if (t >= 1f)
            {
                _isFlashing = false;
                Alpha = 0f;
                ScheduleNextFlash();
                return;
            }

            float fade = t < 0.5f ? t * 2f : (1f - t) * 2f;
            Alpha = MathHelper.Clamp(fade, 0f, 1f);
            Scale = MathHelper.Lerp(BaseScale, MaxScale, fade);
            Position = _flashOffset;

            // Subtle rotation to avoid static-looking sprite.
            Angle = new Vector3(0f, 0f, (float)(_flashElapsed * 6f));
        }

        private void BeginFlash()
        {
            _isFlashing = true;
            _flashElapsed = 0f;
            _flashOffset = PickLocalOffset();
        }

        private void ScheduleNextFlash()
        {
            float lerp = (float)MuGame.Random.NextDouble();
            _timeUntilNextFlash = MathHelper.Lerp(MinInterval, MaxInterval, lerp);
        }

        private Vector3 PickLocalOffset()
        {
            var parent = Parent as WorldObject;
            BoundingBox bounds = parent?.BoundingBoxLocal ?? new BoundingBox(new Vector3(-15f, -15f, 0f), new Vector3(15f, 15f, 20f));

            float maxRadius = MathF.Max(MathF.Abs(bounds.Min.X), MathF.Abs(bounds.Max.X));
            maxRadius = MathF.Max(maxRadius, MathF.Max(MathF.Abs(bounds.Min.Y), MathF.Abs(bounds.Max.Y)));
            maxRadius = MathF.Max(8f, maxRadius * 0.7f);

            float radius = MathHelper.Lerp(maxRadius * 0.25f, maxRadius, (float)MuGame.Random.NextDouble());
            float angle = (float)(MuGame.Random.NextDouble() * MathHelper.TwoPi);
            float heightMin = MathF.Max(bounds.Max.Z * 0.5f, 8f);
            float heightMax = MathF.Max(bounds.Max.Z + 10f, heightMin + 4f);
            float height = MathHelper.Lerp(heightMin, heightMax, (float)MuGame.Random.NextDouble());

            return new Vector3(
                MathF.Cos(angle) * radius,
                MathF.Sin(angle) * radius,
                height);
        }
    }
}
