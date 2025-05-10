using Client.Main.Controllers;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Client.Main.Objects.Effects
{
    // Particle representing a water mist droplet with full 3D properties.
    public class WaterMistParticle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Life;
        public float MaxLife;
        public Color Color;
        // BaseScale is the scale at emission.
        public float BaseScale;
        public float Rotation;       // Fixed rotation if RotateParticles is false.
        public float RotationSpeed;  // Not used if RotateParticles is false.

        // Constructor.
        public WaterMistParticle(Vector3 position, Vector3 velocity, float maxLife, Color color, float baseScale, float rotation, float rotationSpeed)
        {
            Position = position;
            Velocity = velocity;
            MaxLife = maxLife;
            Life = maxLife;
            Color = color;
            BaseScale = baseScale;
            Rotation = rotation;
            RotationSpeed = rotationSpeed;
        }

        // Update particle: move, update rotation (if enabled) and decrease its life.
        public void Update(GameTime gameTime, float upwardAcceleration, float scaleGrowthFactor, Vector2 wind)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            // Apply velocity.
            Position += Velocity * delta;
            // Apply wind influence.
            Position.X += wind.X * delta;
            Position.Y += wind.Y * delta;
            // Apply gentle upward acceleration.
            Velocity.Z += upwardAcceleration * delta;
            Life -= delta;
            // If rotation is updated, update it.
            Rotation += RotationSpeed * delta;
        }

        // Normalized life ratio (from 0 to 1).
        public float LifeRatio => Life / MaxLife;

        // Alpha using quadratic fade-out for smoother effect.
        public float Alpha => MathHelper.Clamp(LifeRatio * LifeRatio, 0, 1);
    }

    // System to manage water mist particles with customizable properties.
    public class WaterMistParticleSystem : WorldObject
    {
        private List<WaterMistParticle> particles;
        private Texture2D texture;
        private Random random;
        private float emissionAccumulator = 0f;

        // === Customization properties ===
        public float EmissionRate { get; set; } = 10f; // particles per second.
        public Vector2 ParticleScaleRange { get; set; } = new Vector2(0.25f, 0.5f);
        public Vector2 ParticleLifetimeRange { get; set; } = new Vector2(3f, 4f);
        public Vector2 ParticleHorizontalVelocityRange { get; set; } = new Vector2(-10f, 10f);
        public Vector2 ParticleUpwardVelocityRange { get; set; } = new Vector2(10f, 20f);
        public float UpwardAcceleration { get; set; } = 3f;
        public Color ParticleColor { get; set; } = new Color(200, 200, 255) * 0.5f;
        public Vector2 ParticlePositionOffsetRange { get; set; } = new Vector2(5f, 5f);
        public bool RotateParticles { get; set; } = false;
        public Vector2 ParticleRotationSpeedRange { get; set; } = new Vector2(-MathHelper.PiOver4, MathHelper.PiOver4);
        // Depth scaling: near (DepthScaleMax) and far (DepthScaleMin).
        public float DepthScaleMax { get; set; } = 1f;
        public float DepthScaleMin { get; set; } = 0.5f;
        // Maximum distance used for normalization.
        public float MaxEffectiveDistance { get; set; } = 2000f;
        // Exponent for non-linear scaling based on distance.
        public float DistanceScaleExponent { get; set; } = 2f;
        // Factor for scaling growth over particle's lifetime.
        public float ScaleGrowthFactor { get; set; } = 0.5f;
        // Global wind applied to particles (horizontal only).
        public Vector2 Wind { get; set; } = Vector2.Zero;

        public WaterMistParticleSystem(Texture2D texture)
        {
            this.texture = texture;
            particles = new List<WaterMistParticle>();
            random = new Random();
        }

        // Emit new particles at a given 3D position.
        public void Emit(Vector3 position, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float offsetX = (float)(random.NextDouble() * 2 * ParticlePositionOffsetRange.X - ParticlePositionOffsetRange.X);
                float offsetY = (float)(random.NextDouble() * 2 * ParticlePositionOffsetRange.Y - ParticlePositionOffsetRange.Y);
                Vector3 pos = new Vector3(position.X + offsetX, position.Y + offsetY, position.Z);

                float vx = (float)(random.NextDouble() * (ParticleHorizontalVelocityRange.Y - ParticleHorizontalVelocityRange.X) + ParticleHorizontalVelocityRange.X);
                float vy = (float)(random.NextDouble() * (ParticleHorizontalVelocityRange.Y - ParticleHorizontalVelocityRange.X) + ParticleHorizontalVelocityRange.X);
                float vz = (float)(random.NextDouble() * (ParticleUpwardVelocityRange.Y - ParticleUpwardVelocityRange.X) + ParticleUpwardVelocityRange.X);
                Vector3 velocity = new Vector3(vx, vy, vz);

                float life = (float)(random.NextDouble() * (ParticleLifetimeRange.Y - ParticleLifetimeRange.X) + ParticleLifetimeRange.X);
                float baseScale = (float)(random.NextDouble() * (ParticleScaleRange.Y - ParticleScaleRange.X) + ParticleScaleRange.X);
                float rotation = (float)(random.NextDouble() * MathHelper.TwoPi);
                float rotationSpeed = RotateParticles
                    ? (float)(random.NextDouble() * (ParticleRotationSpeedRange.Y - ParticleRotationSpeedRange.X) + ParticleRotationSpeedRange.X)
                    : 0f;

                particles.Add(new WaterMistParticle(pos, velocity, life, ParticleColor, baseScale, rotation, rotationSpeed));
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            // Update existing particles.
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                particles[i].Update(gameTime, UpwardAcceleration, ScaleGrowthFactor, Wind);
                if (particles[i].Life <= 0)
                    particles.RemoveAt(i);
            }
            // Continuous emission.
            emissionAccumulator += EmissionRate * delta;
            int emitCount = (int)emissionAccumulator;
            if (emitCount > 0)
            {
                emissionAccumulator -= emitCount;
                Emit(Position, emitCount);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            var graphicsDevice = GraphicsManager.Instance.GraphicsDevice;
            var spriteBatch = GraphicsManager.Instance.Sprite;

            // Save original graphics states
            var prevBlend = graphicsDevice.BlendState;
            var prevDepthStencil = graphicsDevice.DepthStencilState;
            var prevRasterizer = graphicsDevice.RasterizerState;
            var prevSampler = graphicsDevice.SamplerStates[0];

            // Draw all particles in a nested SpriteBatchScope
            using (new SpriteBatchScope(
                spriteBatch,
                SpriteSortMode.BackToFront,
                BlendState.Additive,
                SamplerState.LinearClamp,
                DepthStencilState.DepthRead,
                RasterizerState.CullNone))
            {
                foreach (var particle in particles)
                {
                    // Project to screen
                    Vector3 screenPos = graphicsDevice.Viewport.Project(
                        particle.Position,
                        Camera.Instance.Projection,
                        Camera.Instance.View,
                        Matrix.Identity);
                    if (screenPos.Z < 0f || screenPos.Z > 1f)
                        continue;

                    // Compute color & scale
                    var drawColor = particle.Color * particle.Alpha;
                    float distance = Vector3.Distance(Camera.Instance.Position, particle.Position);
                    float normDist = MathHelper.Clamp(distance / MaxEffectiveDistance, 0f, 1f);
                    float factor = (float)Math.Pow(normDist, DistanceScaleExponent);
                    float depthScale = MathHelper.Lerp(DepthScaleMax, DepthScaleMin, factor);
                    float scaleGrowth = 1f + ScaleGrowthFactor * (1f - particle.Life / particle.MaxLife);
                    float effectiveScale = particle.BaseScale * scaleGrowth * depthScale;

                    // Draw the sprite
                    spriteBatch.Draw(
                        texture,
                        new Vector2(screenPos.X, screenPos.Y),
                        null,
                        drawColor,
                        particle.Rotation,
                        new Vector2(texture.Width / 2f, texture.Height / 2f),
                        effectiveScale,
                        SpriteEffects.None,
                        screenPos.Z);
                }
            }

            // Restore original graphics states
            graphicsDevice.BlendState = prevBlend;
            graphicsDevice.DepthStencilState = prevDepthStencil;
            graphicsDevice.RasterizerState = prevRasterizer;
            graphicsDevice.SamplerStates[0] = prevSampler;

            base.Draw(gameTime);
        }
    }
}
