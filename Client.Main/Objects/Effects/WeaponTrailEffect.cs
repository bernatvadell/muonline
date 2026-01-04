using System;
using System.Collections.Generic;
using Client.Data.BMD;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    public class WeaponTrailEffect : EffectObject
    {
        private const int MaxSamples = 24;
        private readonly List<TrailSample> _samples = new(MaxSamples);
        private readonly VertexPositionColor[] _vertices = new VertexPositionColor[MaxSamples * 2];
        private readonly short[] _indices = new short[(MaxSamples - 1) * 6];

        private Vector3 _localTipOffset = Vector3.Zero;
        private Vector4 _startColor = new Vector4(0.82f, 0.92f, 1f, 0.16f);
        private Vector4 _endColor = new Vector4(0.82f, 0.92f, 1f, 0f);

        private float _trailDuration = 0.18f;
        private float _minDistance = 4.5f;
        private float _minDistanceSq = 4.5f * 4.5f;
        private float _maxSampleInterval = 0.035f;
        private float _baseWidth = 2.5f;
        private float _maxWidth = 8f;
        private float _widthFromSpeed = 0.006f;
        private float _alphaFromSpeed = 0.0006f;
        private float _timeSinceLastSample;

        private bool _hasLast;
        private Vector3 _lastPosition;

        public Func<Vector3> SamplePoint { get; set; }

        private struct TrailSample
        {
            public Vector3 Position;
            public float Width;
            public float Age;
            public float AlphaScale;
        }

        public WeaponTrailEffect()
        {
            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            BoundingBoxLocal = new BoundingBox(Vector3.Zero, Vector3.Zero);
        }

        public void SetTrailColor(Color baseColor)
        {
            var vec = baseColor.ToVector4();
            _startColor = new Vector4(vec.X, vec.Y, vec.Z, 0.16f);
            _endColor = new Vector4(vec.X, vec.Y, vec.Z, 0f);
        }

        public void ResetTrail()
        {
            _samples.Clear();
            _hasLast = false;
            _timeSinceLastSample = 0f;
            _lastPosition = Vector3.Zero;
        }

        /// <summary>
        /// Optional tuning for trail visuals without affecting defaults used by weapons.
        /// </summary>
        public void ConfigureStyle(
            float? duration = null,
            float? minDistance = null,
            float? maxSampleInterval = null,
            float? baseWidth = null,
            float? maxWidth = null,
            float? widthFromSpeed = null,
            float? alphaFromSpeed = null,
            float? startAlpha = null,
            float? endAlpha = null)
        {
            if (duration.HasValue) _trailDuration = MathF.Max(0.02f, duration.Value);
            if (minDistance.HasValue)
            {
                _minDistance = MathF.Max(0.5f, minDistance.Value);
                _minDistanceSq = _minDistance * _minDistance;
            }
            if (maxSampleInterval.HasValue) _maxSampleInterval = MathF.Max(0.001f, maxSampleInterval.Value);
            if (baseWidth.HasValue) _baseWidth = MathF.Max(0.25f, baseWidth.Value);
            if (maxWidth.HasValue) _maxWidth = MathF.Max(_baseWidth + 0.5f, maxWidth.Value);
            if (widthFromSpeed.HasValue) _widthFromSpeed = MathF.Max(0f, widthFromSpeed.Value);
            if (alphaFromSpeed.HasValue) _alphaFromSpeed = MathF.Max(0f, alphaFromSpeed.Value);
            if (startAlpha.HasValue) _startColor.W = MathHelper.Clamp(startAlpha.Value, 0f, 1f);
            if (endAlpha.HasValue) _endColor.W = MathHelper.Clamp(endAlpha.Value, 0f, 1f);
        }

        public void SetTipFromModel(BMD model)
        {
            if (model?.Meshes == null)
            {
                _localTipOffset = Vector3.Zero;
                return;
            }

            Vector3 farthest = Vector3.Zero;
            float maxLenSq = 0f;

            foreach (var mesh in model.Meshes)
            {
                var vertices = mesh.Vertices;
                if (vertices == null)
                    continue;

                for (int i = 0; i < vertices.Length; i++)
                {
                    var pos = vertices[i].Position;
                    float lenSq = pos.X * pos.X + pos.Y * pos.Y + pos.Z * pos.Z;
                    if (lenSq > maxLenSq)
                    {
                        maxLenSq = lenSq;
                        farthest = new Vector3(pos.X, pos.Y, pos.Z);
                    }
                }
            }

            _localTipOffset = maxLenSq > 0 ? farthest : Vector3.Zero;

            if (maxLenSq > 0)
            {
                float length = MathF.Sqrt(maxLenSq);
                _baseWidth = MathHelper.Clamp(length * 0.03f, 2f, 8f);
                _maxWidth = MathHelper.Clamp(length * 0.06f, _baseWidth + 1f, 12f);
                _minDistance = MathHelper.Clamp(length * 0.04f, 2.5f, 10f);
            }
        }

        public override void Update(GameTime gameTime)
        {
            var parentModel = Parent as ModelObject;

            // Keep visibility in sync with the weapon to avoid trailing after it is hidden.
            if (parentModel != null)
            {
                Hidden = parentModel.Hidden || parentModel.Model == null;
                if (parentModel.LowQuality)
                {
                    _samples.Clear();
                    _hasLast = false;
                    base.Update(gameTime);
                    return;
                }
            }

            if (Hidden)
            {
                _samples.Clear();
                _hasLast = false;
                base.Update(gameTime);
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f)
            {
                base.Update(gameTime);
                return;
            }

            for (int i = _samples.Count - 1; i >= 0; i--)
            {
                var sample = _samples[i];
                sample.Age += dt;
                if (sample.Age >= _trailDuration)
                {
                    _samples.RemoveAt(i);
                    continue;
                }
                _samples[i] = sample;
            }

            Vector3 current = SampleCurrent();

            if (!_hasLast)
            {
                AddSample(current, 0f);
                _lastPosition = current;
                _hasLast = true;
            }
            else
                {
                    _timeSinceLastSample += dt;
                    Vector3 delta = current - _lastPosition;
                    float distSq = delta.LengthSquared();

                    if (distSq >= _minDistanceSq || _timeSinceLastSample >= _maxSampleInterval)
                    {
                        float dist = MathF.Sqrt(distSq);
                        float speed = dist / MathF.Max(dt, 0.0001f);
                        AddSample(current, speed);
                        _lastPosition = current;
                        _timeSinceLastSample = 0f;
                }
            }

            base.Update(gameTime);
        }

        private Vector3 SampleCurrent()
        {
            if (SamplePoint != null)
                return SamplePoint.Invoke();

            if (_localTipOffset != Vector3.Zero)
                return Vector3.Transform(_localTipOffset, WorldPosition);

            return WorldPosition.Translation;
        }

        private void AddSample(Vector3 position, float speed)
        {
            if (_samples.Count == MaxSamples)
            {
                _samples.RemoveAt(0);
            }

            float width = _baseWidth + speed * _widthFromSpeed;
            width = MathHelper.Clamp(width, _baseWidth, _maxWidth);

            float speedFactor = MathHelper.Clamp(speed * _alphaFromSpeed, 0f, 1f);
            float alphaScale = MathHelper.Lerp(0.05f, 1f, speedFactor);

            _samples.Add(new TrailSample
            {
                Position = position,
                Width = width,
                Age = 0f,
                AlphaScale = alphaScale
            });
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (_samples.Count < 2 || Hidden)
            {
                base.DrawAfter(gameTime);
                return;
            }

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var effect = GraphicsManager.Instance.BasicEffect3D;
            var camera = Camera.Instance;

            var prevBlend = gd.BlendState;
            var prevDepth = gd.DepthStencilState;
            var prevRasterizer = gd.RasterizerState;
            bool prevTextureEnabled = effect.TextureEnabled;
            bool prevVertexColorEnabled = effect.VertexColorEnabled;
            bool prevLightingEnabled = effect.LightingEnabled;
            Matrix prevWorld = effect.World;
            Matrix prevView = effect.View;
            Matrix prevProj = effect.Projection;

            gd.BlendState = BlendState.Additive;
            gd.DepthStencilState = DepthStencilState.DepthRead;
            gd.RasterizerState = RasterizerState.CullNone;

            effect.TextureEnabled = false;
            effect.VertexColorEnabled = true;
            effect.LightingEnabled = false;
            effect.World = Matrix.Identity;
            effect.View = camera.View;
            effect.Projection = camera.Projection;

            int vertexCount = BuildVertices(camera);
            int primitiveCount = (_samples.Count - 1) * 2;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertices,
                    0,
                    vertexCount,
                    _indices,
                    0,
                    primitiveCount);
            }

            effect.TextureEnabled = prevTextureEnabled;
            effect.VertexColorEnabled = prevVertexColorEnabled;
            effect.LightingEnabled = prevLightingEnabled;
            effect.World = prevWorld;
            effect.View = prevView;
            effect.Projection = prevProj;
            gd.BlendState = prevBlend;
            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRasterizer;

            base.DrawAfter(gameTime);
        }

        private int BuildVertices(Camera camera)
        {
            int count = _samples.Count;

            for (int i = 0; i < count; i++)
            {
                var sample = _samples[i];
                float life = MathHelper.Clamp(1f - (sample.Age / _trailDuration), 0f, 1f);
                float width = sample.Width * MathHelper.Lerp(0.25f, 1f, life);

                // Smooth tangent using previous and next samples to avoid sharp joints.
                Vector3 dir;
                if (i == 0)
                    dir = _samples[i + 1].Position - sample.Position;
                else if (i == count - 1)
                    dir = sample.Position - _samples[i - 1].Position;
                else
                    dir = _samples[i + 1].Position - _samples[i - 1].Position;

                if (dir.LengthSquared() < 0.0001f)
                    dir = Vector3.Normalize(camera.Position - sample.Position);
                else
                    dir.Normalize();

                Vector3 view = camera.Position - sample.Position;
                if (view.LengthSquared() < 0.0001f)
                    view = Vector3.Backward;
                view.Normalize();

                Vector3 side = Vector3.Cross(dir, view);
                if (side.LengthSquared() < 0.0001f)
                    side = Vector3.Cross(Vector3.Up, view);
                if (side.LengthSquared() < 0.0001f)
                    side = Vector3.Right;
                side.Normalize();

                Vector3 offset = side * (width * 0.5f);

                Vector4 colorVec = Vector4.Lerp(_endColor, _startColor, life) * (TotalAlpha * sample.AlphaScale);
                var color = new Color(colorVec);

                int vIndex = i * 2;
                _vertices[vIndex] = new VertexPositionColor(sample.Position + offset, color);
                _vertices[vIndex + 1] = new VertexPositionColor(sample.Position - offset, color);

                if (i < count - 1)
                {
                    int baseVert = vIndex;
                    int nextVert = vIndex + 2;
                    int idx = i * 6;
                    _indices[idx] = (short)baseVert;
                    _indices[idx + 1] = (short)(baseVert + 1);
                    _indices[idx + 2] = (short)nextVert;
                    _indices[idx + 3] = (short)(baseVert + 1);
                    _indices[idx + 4] = (short)(nextVert + 1);
                    _indices[idx + 5] = (short)nextVert;
                }
            }

            return count * 2;
        }
    }
}
