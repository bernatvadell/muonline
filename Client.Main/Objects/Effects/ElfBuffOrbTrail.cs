using System;
using System.Threading.Tasks;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Smooth, round trail effect that renders overlapping circular sprites.
    /// Creates a comet-like tail that looks organic rather than flat/rectangular.
    /// Trail positions are stored relative to a reference point (owner) so the trail
    /// moves with the player.
    /// </summary>
    public class ElfBuffOrbTrail : WorldObject
    {
        private const int MaxPoints = 48;
        private const float MinDistance = 2f;
        private const float MinDistanceSq = MinDistance * MinDistance;
        private const float MaxSampleInterval = 0.015f;

        private readonly TrailPoint[] _points = new TrailPoint[MaxPoints];
        private readonly Color _baseColor;
        private readonly float _baseScale;
        private readonly float _baseLifetime;
        private readonly float _lifetimeJitter;
        private int _pointCount;
        private int _headIndex;
        private float _timeSinceLastSample;
        private Vector3 _lastOffset;
        private bool _hasLastPosition;

        private Texture2D _texture;

        /// <summary>
        /// Returns the current position of the orb (world space).
        /// </summary>
        public Func<Vector3> SamplePoint { get; set; }

        /// <summary>
        /// Returns the current position of the owner/player (world space).
        /// Trail points are stored as offsets from this reference so they move with the player.
        /// </summary>
        public Func<Vector3> ReferencePoint { get; set; }

        private struct TrailPoint
        {
            public Vector3 Offset; // Offset from reference point, not absolute position
            public float Age;
            public float Scale;
            public float Lifetime;
            public bool Active;
        }

        public ElfBuffOrbTrail(Color color, float scale = 1f)
        {
            _baseColor = color;
            _baseScale = scale;
            _baseLifetime = MathHelper.Lerp(1.2f, 1.8f, (float)MuGame.Random.NextDouble());
            _lifetimeJitter = MathHelper.Lerp(0.18f, 0.32f, (float)MuGame.Random.NextDouble());

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            BoundingBoxLocal = new BoundingBox(Vector3.Zero, Vector3.Zero);
        }

        public override async Task Load()
        {
            await base.Load();

            var textureData = await TextureLoader.Instance.Prepare("Effect/Shiny05.jpg");
            if (textureData != null)
            {
                _texture = TextureLoader.Instance.GetTexture2D("Effect/Shiny05.jpg");
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready || Hidden)
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f)
                return;

            // Age existing points and remove expired ones
            for (int i = 0; i < MaxPoints; i++)
            {
                if (!_points[i].Active)
                    continue;

                _points[i].Age += dt;
                if (_points[i].Age >= _points[i].Lifetime)
                {
                    _points[i].Active = false;
                    _pointCount--;
                }
            }

            // Sample new position
            if (SamplePoint == null || ReferencePoint == null)
                return;

            Vector3 currentOrb = SamplePoint.Invoke();
            Vector3 currentRef = ReferencePoint.Invoke();
            Vector3 currentOffset = currentOrb - currentRef;

            if (!_hasLastPosition)
            {
                AddPoint(currentOffset);
                _lastOffset = currentOffset;
                _hasLastPosition = true;
                return;
            }

            _timeSinceLastSample += dt;
            float distSq = Vector3.DistanceSquared(currentOffset, _lastOffset);

            if (distSq >= MinDistanceSq || _timeSinceLastSample >= MaxSampleInterval)
            {
                AddPoint(currentOffset);
                _lastOffset = currentOffset;
                _timeSinceLastSample = 0f;
            }
        }

        private void AddPoint(Vector3 offset)
        {
            // Find next available slot
            int slot = -1;
            for (int i = 0; i < MaxPoints; i++)
            {
                int idx = (_headIndex + i) % MaxPoints;
                if (!_points[idx].Active)
                {
                    slot = idx;
                    break;
                }
            }

            // If no slot available, overwrite oldest
            if (slot == -1)
            {
                float maxAge = -1f;
                for (int i = 0; i < MaxPoints; i++)
                {
                    if (_points[i].Age / MathF.Max(_points[i].Lifetime, float.Epsilon) > maxAge)
                    {
                        maxAge = _points[i].Age / MathF.Max(_points[i].Lifetime, float.Epsilon);
                        slot = i;
                    }
                }
                _pointCount--;
            }

            // Add slight random jitter for organic feel
            Vector3 jitter = new Vector3(
                MathHelper.Lerp(-1.5f, 1.5f, (float)MuGame.Random.NextDouble()),
                MathHelper.Lerp(-1.5f, 1.5f, (float)MuGame.Random.NextDouble()),
                MathHelper.Lerp(-1.5f, 1.5f, (float)MuGame.Random.NextDouble()));

            float lifetime = _baseLifetime *
                             MathHelper.Lerp(
                                 1f - _lifetimeJitter,
                                 1f + _lifetimeJitter,
                                 (float)MuGame.Random.NextDouble());

            _points[slot] = new TrailPoint
            {
                Offset = offset + jitter,
                Age = 0f,
                Scale = _baseScale * MathHelper.Lerp(0.9f, 1.1f, (float)MuGame.Random.NextDouble()),
                Lifetime = lifetime,
                Active = true
            };
            _pointCount++;
            _headIndex = (slot + 1) % MaxPoints;
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (_texture == null || _pointCount == 0 || Hidden || ReferencePoint == null)
                return;

            var sb = GraphicsManager.Instance.Sprite;
            var camera = Camera.Instance;
            var viewport = GraphicsDevice.Viewport;

            // Get current reference position to convert offsets to world positions
            Vector3 currentRef = ReferencePoint.Invoke();

            // Distance scaling is effectively constant across the trail (points are close to each other).
            float refDistance = Vector3.Distance(camera.Position, currentRef);
            float distScale = 1f / MathF.Max(refDistance / 800f, 0.1f);

            void DrawPoints()
            {
                for (int i = 0; i < MaxPoints; i++)
                {
                    if (!_points[i].Active)
                        continue;

                    ref var point = ref _points[i];

                    float life = 1f - (point.Age / point.Lifetime);
                    if (life <= 0f)
                        continue;

                    // Convert offset to world position using current reference
                    Vector3 worldPos = currentRef + point.Offset;

                    // Project to screen
                    Vector3 projected = viewport.Project(
                        worldPos,
                        camera.Projection,
                        camera.View,
                        Matrix.Identity);

                    if (projected.Z < 0f || projected.Z > 1f)
                        continue;

                    // Smooth fade: starts small, grows, then shrinks and fades
                    float sizeCurve;
                    if (life > 0.7f)
                    {
                        // Grow in
                        float t = (life - 0.7f) / 0.3f;
                        sizeCurve = MathHelper.Lerp(1f, 0.6f, t);
                    }
                    else
                    {
                        // Shrink out
                        sizeCurve = life / 0.7f;
                    }

                    float scale = point.Scale * distScale * sizeCurve * 0.5f * Constants.RENDER_SCALE;

                    // Alpha fades smoothly
                    float alpha = life * life * TotalAlpha * 0.85f;

                    Color color = _baseColor * alpha;

                    sb.Draw(
                        _texture,
                        new Vector2(projected.X, projected.Y),
                        null,
                        color,
                        point.Age * 2f, // Slow rotation
                        new Vector2(_texture.Width / 2f, _texture.Height / 2f),
                        scale,
                        SpriteEffects.None,
                        projected.Z);
                }
            }

            if (!Helpers.SpriteBatchScope.BatchIsBegun)
            {
                using (new Helpers.SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.DepthRead))
                {
                    DrawPoints();
                }
            }
            else
            {
                DrawPoints();
            }
        }

        public void Clear()
        {
            for (int i = 0; i < MaxPoints; i++)
            {
                _points[i].Active = false;
            }
            _pointCount = 0;
            _hasLastPosition = false;
        }
    }
}
