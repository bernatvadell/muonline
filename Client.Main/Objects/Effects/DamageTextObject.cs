using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Scenes;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Floating damage / crit text above a target.
    /// </summary>
    public class DamageTextObject : WorldObject
    {
        // Public readonly data -------------------------------------------------
        public string Text { get; }
        public Color TextColor { get; }
        public ushort TargetId { get; }

        // Animation state ------------------------------------------------------
        private float _currentVerticalOffset;
        private float _currentHorizontalOffset;
        private float _verticalVelocity;
        private float _horizontalVelocity;
        private float _currentScale = StartScale;
        private bool _falling;
        private float _elapsedTime;
        private Vector2 _screenPosition;

        // Visual-effect helpers ------------------------------------------------
        private readonly bool _isCritical;
        private readonly float _wobbleAmplitude;
        private readonly float _wobbleFrequency;
        private readonly Vector2 _shadowOffset;
        private readonly float _glowIntensity;
        private float _pulsePhase;
        private readonly Color _originalColor;

        // Tunables (already toned down vs. stock) ------------------------------
        private const float MaxVerticalOffset = 60f;
        private const float StartScale = 0.4f;
        private const float MaxScale = 1.5f;
        private const float EndScale = 0.2f;
        private const float Lifetime = 2.0f;

        private const float InitialVerticalSpeed = -120f;
        private const float HorizontalSpeedRange = 150f;
        private const float Gravity = 200f;

        // Z offsets for anchor placement
        private const float PlayerHeadBoneTextOffsetZ = 50f;
        private const float PlayerModelTopTextOffsetZ = 30f;
        private const float MonsterBBoxTopTextOffsetZ = 30f;

        // Boldness (glow / shadow / highlight) cut 50 %
        private const float ShadowAlpha = 0.125f; // was 0.25
        private const float GlowAlphaMul = 0.15f;  // was 0.30–0.60
        private const float HighlightAlphaMul = 0.075f; // was 0.15

        // ---------------------------------------------------------------------

        public DamageTextObject(string text, ushort targetId, Color color)
        {
            Text = text;
            TargetId = targetId;
            TextColor = color;
            _originalColor = color;

            Alpha = 1f;
            Scale = 1f;
            IsTransparent = true;
            AffectedByTransparency = false;
            Status = GameControlStatus.Ready;

            // Critical hit detection
            _isCritical = text.Contains("!") || text.Contains("CRIT") ||
                          (int.TryParse(text, out int dmg) && dmg > 500);

            // Randomised motion -------------------------------------------------
            float speedFactor = _isCritical ? 1.5f : 1f;
            _verticalVelocity = (InitialVerticalSpeed +
                                  MathHelper.Lerp(-30f, 30f, (float)MuGame.Random.NextDouble())) * speedFactor;
            _horizontalVelocity = MathHelper.Lerp(-HorizontalSpeedRange,
                                                  HorizontalSpeedRange,
                                                  (float)MuGame.Random.NextDouble());

            _wobbleAmplitude = MathHelper.Lerp(2f, 6f, (float)MuGame.Random.NextDouble());
            _wobbleFrequency = MathHelper.Lerp(4f, 6f, (float)MuGame.Random.NextDouble());
            _pulsePhase = (float)MuGame.Random.NextDouble() * MathHelper.TwoPi;
            _shadowOffset = new Vector2(1f, 1f);
            _glowIntensity = _isCritical ? 0.2f : 0.05f;   // halved
        }

        // ---------------------------------------------------------------------
        //  Core pipeline
        // ---------------------------------------------------------------------

        public override Task Load()
        {
            Status = GameControlStatus.Ready;
            return Task.CompletedTask;
        }

        public override void Update(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready) return;

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsedTime += delta;

            if (_elapsedTime >= Lifetime || Alpha <= 0.01f)
            {
                Hidden = true;
                World?.RemoveObject(this);
                Dispose();
                return;
            }

            #region fade-out
            float fadeStart = Lifetime * 0.5f;
            if (_elapsedTime > fadeStart)
            {
                float p = (_elapsedTime - fadeStart) / (Lifetime - fadeStart);
                Alpha = MathHelper.Clamp(1f - p * p, 0f, 1f);   // quadratic fade
            }
            #endregion

            #region motion
            if (!_falling)
            {
                _currentVerticalOffset += _verticalVelocity * delta;
                if (-_currentVerticalOffset >= MaxVerticalOffset)
                {
                    _falling = true;
                    _verticalVelocity = 0f;
                }
            }
            else
            {
                _verticalVelocity += Gravity * delta;
                _currentVerticalOffset += _verticalVelocity * delta;
            }

            _currentHorizontalOffset += _horizontalVelocity * delta;
            _horizontalVelocity *= 0.95f;
            #endregion

            _pulsePhase += delta * _wobbleFrequency;
            float wobble = (float)Math.Sin(_pulsePhase) * _wobbleAmplitude * (_currentScale / MaxScale);

            #region scaling
            float progress = _elapsedTime / Lifetime;
            if (!_falling)
            {
                float rise = MathHelper.Clamp(-_currentVerticalOffset / MaxVerticalOffset, 0f, 1f);
                _currentScale = MathHelper.SmoothStep(StartScale, MaxScale, rise);
            }
            else
            {
                float fall = MathHelper.Clamp(progress, 0.5f, 1f);
                fall = (fall - 0.5f) * 2f;
                _currentScale = MathHelper.Lerp(MaxScale, EndScale, fall * fall * fall);
            }
            #endregion

            // ------------------------------------------------ target + projection
            WalkerObject target = ResolveTarget();
            if (target == null || target.Hidden || target.Status == GameControlStatus.Disposed || target.OutOfView)
            {
                Hidden = true;
                return;
            }

            Position = CalculateAnchorPoint(target);

            Vector3 proj = GraphicsDevice.Viewport.Project(
                Position,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            // final screen coordinates
            _screenPosition = new Vector2(
                proj.X + _currentHorizontalOffset + wobble,
                proj.Y + _currentVerticalOffset);

            Hidden = (proj.Z < 0f || proj.Z > 1f);
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            /* 3-D part intentionally left empty. */
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible || Alpha <= 0.01f) return;

            var spriteBatch = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;
            if (spriteBatch == null || font == null) return;

            const float fontSize = 12f;
            float baseScale = fontSize / Constants.BASE_FONT_SIZE;
            float scale = Math.Max(0.1f, baseScale * _currentScale);
            Vector2 origin = font.MeasureString(Text) * 0.5f;

            // colour ------------------------------------------------------------------
            Color baseColor, glowColor;
            if (!_falling)
            {
                float bright = 1f + (_currentScale - StartScale) / (MaxScale - StartScale) * 0.2f;
                float pulse = (float)Math.Sin(_pulsePhase * 2) * 0.1f + 1f;

                baseColor = new Color(
                    (int)MathHelper.Clamp(_originalColor.R * bright * pulse, 0, 255),
                    (int)MathHelper.Clamp(_originalColor.G * bright * pulse, 0, 255),
                    (int)MathHelper.Clamp(_originalColor.B * bright * pulse, 0, 255),
                    _originalColor.A) * Alpha;

                glowColor = new Color(
                    Math.Min(255, baseColor.R + 20),
                    Math.Min(255, baseColor.G + 15),
                    Math.Min(255, baseColor.B + 15),
                    (int)(baseColor.A * _glowIntensity * GlowAlphaMul));
            }
            else
            {
                baseColor = TextColor * Alpha;
                glowColor = new Color(baseColor.R, baseColor.G, baseColor.B,
                                      (int)(baseColor.A * GlowAlphaMul));
            }

            // --------------------------------------------------------------------------
            using (new SpriteBatchScope(
                spriteBatch,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp,
                DepthStencilState.None,
                RasterizerState.CullNone))
            {
                // Shadow (only for bigger text)
                if (scale > 0.8f)
                {
                    spriteBatch.DrawString(
                        font, Text,
                        _screenPosition + _shadowOffset,
                        Color.Black * (Alpha * ShadowAlpha),
                        0f, origin, scale, SpriteEffects.None, 0f);
                }

                // Outline ----------------------------------------------------
                float outline = 1.5f;
                Color outlineColor = Color.Black * Alpha;
                Vector2[] outlineOffsets =
                {
                    new(-outline, 0f),
                    new(outline, 0f),
                    new(0f, -outline),
                    new(0f, outline),
                    new(-outline, -outline),
                    new(outline, -outline),
                    new(-outline, outline),
                    new(outline, outline)
                };

                foreach (Vector2 off in outlineOffsets)
                {
                    spriteBatch.DrawString(
                        font, Text,
                        _screenPosition + off,
                        outlineColor,
                        0f, origin, scale, SpriteEffects.None, 0f);
                }

                // Soft glow
                if (_glowIntensity > 0.05f)
                {
                    spriteBatch.DrawString(
                        font, Text,
                        _screenPosition + new Vector2(1f, 1f),
                        glowColor,
                        0f, origin, scale, SpriteEffects.None, 0f);
                }

                // Main text
                spriteBatch.DrawString(
                    font, Text,
                    _screenPosition,
                    baseColor,
                    0f, origin, scale, SpriteEffects.None, 0f);

                // Highlight for big crits
                if (_isCritical && !_falling && _currentScale > MaxScale * 0.8f)
                {
                    Color hi = Color.White * (Alpha * HighlightAlphaMul *
                                              (float)Math.Sin(_pulsePhase * 3));
                    if (hi.A > 5)
                    {
                        spriteBatch.DrawString(
                            font, Text,
                            _screenPosition,
                            hi,
                            0f, origin, scale * 1.02f, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------

        private WalkerObject ResolveTarget()
        {
            var scene = MuGame.Instance?.ActiveScene as GameScene;
            if (scene == null || World == null) return null;

            ushort localId = MuGame.Network.GetCharacterState().Id;
            if (TargetId == localId) return scene.Hero;

            return World.TryGetWalkerById(TargetId, out WalkerObject obj) ? obj : null;
        }

        private Vector3 CalculateAnchorPoint(WalkerObject target)
        {
            const int PlayerHeadBoneIndex = 20;
            const float ApproxHeadHeight = 130f;

            if (target is PlayerObject player)
            {
                var bones = player.GetBoneTransforms();
                if (bones != null &&
                    bones.Length > PlayerHeadBoneIndex &&
                    bones[PlayerHeadBoneIndex] != default)
                {
                    Vector3 local = bones[PlayerHeadBoneIndex].Translation;
                    Vector3 world = Vector3.Transform(local, player.WorldPosition);
                    return world + Vector3.UnitZ * PlayerHeadBoneTextOffsetZ;
                }
                return player.Position + Vector3.UnitZ *
                       (ApproxHeadHeight + PlayerModelTopTextOffsetZ);
            }

            return new Vector3(
                (target.BoundingBoxWorld.Min.X + target.BoundingBoxWorld.Max.X) * 0.5f,
                (target.BoundingBoxWorld.Min.Y + target.BoundingBoxWorld.Max.Y) * 0.5f,
                target.BoundingBoxWorld.Max.Z + MonsterBBoxTopTextOffsetZ);
        }
    }
}
