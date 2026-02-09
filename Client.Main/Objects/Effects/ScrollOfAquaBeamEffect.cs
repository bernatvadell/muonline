#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Scroll of Aqua Beam visual effect (Skill ID 12).
    /// Based on original MU client BITMAP_BOSS_LASER rendering.
    /// </summary>
    public sealed class ScrollOfAquaBeamEffect : EffectObject
    {
        private const string BeamTexturePath = "Effect/Spark03.OZJ"; // BITMAP_SPARK + 1
        private const string SoundAquaBeam = "Sound/sAquaFlash.wav";

        private const int BeamSegments = 20;
        private const float SegmentStep = 50f;
        private const float BeamScale = 64f;
        private const float BeamLifeFrames = 20f;

        private static readonly Vector3 SpawnOffset = new(-20f, -90f, 100f);

        private readonly WalkerObject _caster;
        private readonly Vector3? _targetPosition;

        private Vector3 _startPosition;
        private Vector3 _directionStep;
        private float _lifeFrames = BeamLifeFrames;
        private float _time;
        private bool _initialized;
        private bool _soundPlayed;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _beamTexture = null!;

        private readonly DynamicLight _beamLight;
        private bool _lightAdded;

        public ScrollOfAquaBeamEffect(WalkerObject caster, Vector3? targetPosition = null)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            _targetPosition = targetPosition;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-1200f, -1200f, -120f),
                new Vector3(1200f, 1200f, 220f));

            _beamLight = new DynamicLight
            {
                Owner = this,
                Position = caster.WorldPosition.Translation + new Vector3(0f, 0f, 100f),
                Color = new Vector3(0.5f, 0.7f, 1f),
                Radius = 320f,
                Intensity = 1.1f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            _ = await TextureLoader.Instance.Prepare(BeamTexturePath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _beamTexture = TextureLoader.Instance.GetTexture2D(BeamTexturePath) ?? GraphicsManager.Instance.Pixel;

            if (World?.Terrain != null && !_lightAdded)
            {
                World.Terrain.AddDynamicLight(_beamLight);
                _lightAdded = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status == GameControlStatus.NonInitialized)
                _ = Load();

            if (Status != GameControlStatus.Ready)
                return;

            if (_caster.Status == GameControlStatus.Disposed || _caster.World == null)
            {
                RemoveSelf();
                return;
            }

            if (!_initialized)
                InitializeBeam();

            _time += (float)gameTime.ElapsedGameTime.TotalSeconds;
            _lifeFrames -= FPSCounter.Instance.FPS_ANIMATION_FACTOR;

            if (_lifeFrames <= 0f)
            {
                RemoveSelf();
                return;
            }

            UpdateDynamicLight();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _beamTexture == null || _spriteBatch == null)
                return;

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState, SamplerState.LinearClamp, DepthState))
                {
                    DrawBeam();
                }
            }
            else
            {
                DrawBeam();
            }
        }

        private void InitializeBeam()
        {
            Vector3 casterPos = _caster.WorldPosition.Translation;
            Vector3 forward = GetForwardDirection(casterPos);
            float angle = MathF.Atan2(forward.X, -forward.Y);

            float sin = MathF.Sin(angle);
            float cos = MathF.Cos(angle);

            Vector3 rotatedOffset = new Vector3(
                SpawnOffset.X * cos - SpawnOffset.Y * sin,
                SpawnOffset.X * sin + SpawnOffset.Y * cos,
                SpawnOffset.Z);

            _startPosition = casterPos + rotatedOffset;
            _directionStep = forward * SegmentStep;
            Position = _startPosition;

            if (!_soundPlayed)
            {
                SoundController.Instance.PlayBuffer(SoundAquaBeam);
                _soundPlayed = true;
            }

            _initialized = true;
        }

        private Vector3 GetForwardDirection(Vector3 casterPos)
        {
            Vector3 forward;

            if (_targetPosition.HasValue)
            {
                Vector3 delta = _targetPosition.Value - casterPos;
                delta.Z = 0f;
                if (delta.LengthSquared() > 0.0001f)
                    forward = Vector3.Normalize(delta);
                else
                    forward = new Vector3(MathF.Sin(_caster.Angle.Z), -MathF.Cos(_caster.Angle.Z), 0f);
            }
            else
            {
                forward = new Vector3(MathF.Sin(_caster.Angle.Z), -MathF.Cos(_caster.Angle.Z), 0f);
            }

            return forward;
        }

        private void UpdateDynamicLight()
        {
            float pulse = 0.9f + 0.1f * MathF.Sin(_time * 12f);
            _beamLight.Position = _startPosition + _directionStep * (BeamSegments * 0.5f);
            _beamLight.Intensity = 1.1f * pulse;
        }

        private void DrawBeam()
        {
            float pulse = 0.92f + 0.08f * MathF.Sin(_time * 18f);
            Color color = new Color(0.5f, 0.7f, 1f) * pulse;

            Vector3 pos = _startPosition;
            for (int i = 0; i < BeamSegments; i++)
            {
                DrawSprite(_beamTexture, pos, color, 0f, BeamScale);
                pos += _directionStep;
            }
        }

        private void DrawSprite(Texture2D texture, Vector3 worldPos, Color color, float rotation, float scale)
        {
            var viewport = GraphicsDevice.Viewport;
            Vector3 projected = viewport.Project(worldPos, Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            if (projected.Z < 0f || projected.Z > 1f)
                return;

            float baseScale = ComputeScreenScale(worldPos, 1f);
            float finalScale = scale * baseScale;
            float depth = MathHelper.Clamp(projected.Z, 0f, 1f);

            _spriteBatch.Draw(
                texture,
                new Vector2(projected.X, projected.Y),
                null,
                color,
                rotation,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                finalScale,
                SpriteEffects.None,
                depth);
        }

        private static float ComputeScreenScale(Vector3 worldPos, float baseScale)
        {
            float distance = Vector3.Distance(Camera.Instance.Position, worldPos);
            float scale = baseScale / (MathF.Max(distance, 0.1f) / Constants.TERRAIN_SIZE);
            return scale * Constants.RENDER_SCALE;
        }

        private void RemoveSelf()
        {
            if (Parent != null)
                Parent.Children.Remove(this);
            else
                World?.RemoveObject(this);

            Dispose();
        }

        public override void Dispose()
        {
            if (_lightAdded && World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_beamLight);
                _lightAdded = false;
            }

            base.Dispose();
        }
    }
}
