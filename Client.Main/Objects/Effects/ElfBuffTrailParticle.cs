using System;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Trail particle that stays in place and fades out.
    /// Creates a smooth, organic particle trail behind orbiting lights.
    /// </summary>
    public class ElfBuffTrailParticle : SpriteObject
    {
        private readonly float _lifetime;
        private readonly float _initialScale;
        private readonly float _initialAlpha;
        private readonly Vector3 _drift;
        private float _age;

        public override string TexturePath => "Effect/Shiny05.jpg";

        public ElfBuffTrailParticle(Vector3 position, float hueShift, float sizeMultiplier = 1f)
        {
            Position = position;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            LightEnabled = true;
            BoundingBoxLocal = new BoundingBox(Vector3.Zero, Vector3.Zero);

            // Randomize trail particle properties
            _lifetime = MathHelper.Lerp(0.6f, 1.2f, (float)MuGame.Random.NextDouble());
            _initialScale = MathHelper.Lerp(0.5f, 0.9f, (float)MuGame.Random.NextDouble()) * sizeMultiplier;
            _initialAlpha = MathHelper.Lerp(0.7f, 1f, (float)MuGame.Random.NextDouble());

            Scale = _initialScale;
            Alpha = _initialAlpha;

            // Green-cyan light color
            float lightIntensity = MathHelper.Lerp(0.4f, 0.7f, (float)MuGame.Random.NextDouble());
            Light = new Vector3(
                0.3f * hueShift * lightIntensity,
                0.9f * lightIntensity,
                0.6f * hueShift * lightIntensity);

            // Slight random drift for organic feel
            _drift = new Vector3(
                MathHelper.Lerp(-8f, 8f, (float)MuGame.Random.NextDouble()),
                MathHelper.Lerp(-8f, 8f, (float)MuGame.Random.NextDouble()),
                MathHelper.Lerp(-3f, 12f, (float)MuGame.Random.NextDouble()));
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready)
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _age += dt;

            if (_age >= _lifetime)
            {
                Despawn();
                return;
            }

            float t = _age / _lifetime;

            // Gentle drift
            Position += _drift * dt * (1f - t);

            // Smooth fade out (ease out)
            float fadeT = 1f - (1f - t) * (1f - t);
            Alpha = _initialAlpha * (1f - fadeT);

            // Slight size reduction
            Scale = _initialScale * MathHelper.Lerp(1f, 0.4f, t);

            // Dim the light as it fades
            Light *= (1f - dt * 2f);
        }

        private void Despawn()
        {
            World?.RemoveObject(this);
        }
    }
}
