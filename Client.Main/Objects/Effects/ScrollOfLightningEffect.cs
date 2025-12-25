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
    /// Enhanced lightning bolt between two world positions (Scroll of Lightning / AT_SKILL_THUNDER).
    /// Features: true zigzag path, branching bolts, glow, sparks, impact flash, energy coronas, dynamic lighting.
    /// </summary>
    public sealed class ScrollOfLightningEffect : WorldObject
    {
        private const string JointTexturePath = "Effect/JointThunder01.OZJ";
        private const string EnergyTexturePath = "Effect/Thunder01.OZJ";
        private const string FlarePath = "Effect/flare.OZJ";
        private const string SparkPath = "Effect/Spark03.OZJ";
        private const int MaxSegments = 12;
        private const int MaxBranches = 3;
        private const int MaxSparks = 16;

        private readonly Func<Vector3> _sourceProvider;
        private readonly Func<Vector3> _targetProvider;

        // Zigzag path points (screen space, calculated each frame)
        private readonly Vector2[] _pathPoints = new Vector2[MaxSegments + 1];
        private readonly float[] _pathDepths = new float[MaxSegments + 1];

        // Persistent offsets for zigzag (normalized -1 to 1)
        private readonly float[] _zigzagOffsets = new float[MaxSegments + 1];

        // Branch data
        private readonly float[][] _branchOffsets = new float[MaxBranches][];
        private readonly int[] _branchStartSegment = new int[MaxBranches];
        private readonly float[] _branchAngle = new float[MaxBranches];
        private readonly bool[] _branchActive = new bool[MaxBranches];
        private readonly float[] _branchLengthRatio = new float[MaxBranches];
        private readonly Vector2[][] _branchPath = new Vector2[MaxBranches][];
        private readonly int[] _branchSegmentCount = new int[MaxBranches];

        // Spark system - bigger, more visible
        private readonly Vector2[] _sparkOffsets = new Vector2[MaxSparks];
        private readonly Vector2[] _sparkVelocities = new Vector2[MaxSparks];
        private readonly float[] _sparkLife = new float[MaxSparks];
        private readonly float[] _sparkMaxLife = new float[MaxSparks];
        private readonly float[] _sparkScale = new float[MaxSparks];
        private readonly float[] _sparkRotation = new float[MaxSparks];
        private readonly int[] _sparkType = new int[MaxSparks];

        // Dynamic lights
        private DynamicLight _sourceLight;
        private DynamicLight _targetLight;
        private bool _lightsAdded;

        private Texture2D _jointTexture;
        private Texture2D _energyTexture;
        private Texture2D _flareTexture;
        private Texture2D _sparkTexture;
        private SpriteBatch _spriteBatch;

        // Locked positions - source fixed at spawn, target can track
        private Vector3 _lockedSource;
        private Vector3 _currentTarget;
        private bool _sourceInitialized;

        private float _remaining;
        private float _time;
        private float _reshapeTimer;
        private float _flickerValue = 1f;
        private float _burstTimer;
        private bool _isBurst;
        private readonly float _duration;

        // Visual parameters
        private readonly float _zigzagAmplitude = 50f;
        private readonly float _thicknessGlow = 10f;
        private readonly float _thicknessOuter = 2.2f;
        private readonly float _thicknessInner = 0.9f;
        private readonly float _coronaScale = 1.5f;
        private readonly float _flashScale = 2.5f;

        // Colors
        private readonly Color _glowColor = new Color(0.3f, 0.45f, 0.95f, 0.2f);
        private readonly Color _outerColor = new Color(0.55f, 0.75f, 1.0f, 0.9f);
        private readonly Color _innerColor = new Color(0.97f, 0.98f, 1.0f, 1.0f);
        private readonly Color _branchColor = new Color(0.6f, 0.8f, 1.0f, 0.85f);
        private readonly Color _sparkColor = new Color(0.8f, 0.9f, 1.0f, 1.0f);

        // Light color (bright blue-white)
        private readonly Vector3 _lightColor = new Vector3(0.7f, 0.85f, 1.0f);

        // Corona rotation
        private float _sourceCoronaRotation;
        private float _targetCoronaRotation;

        public ScrollOfLightningEffect(
            Func<Vector3> sourceProvider,
            Func<Vector3> targetProvider,
            float durationSeconds = 0.6f)
        {
            _sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));
            _targetProvider = targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));

            _duration = MathHelper.Clamp(durationSeconds, 0.1f, 3f);
            _remaining = _duration;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            BoundingBoxLocal = new BoundingBox(Vector3.Zero, Vector3.Zero);

            // Initialize zigzag offsets (first and last are 0 to hit source/target exactly)
            _zigzagOffsets[0] = 0f;
            for (int i = 1; i < MaxSegments; i++)
            {
                _zigzagOffsets[i] = (float)(MuGame.Random.NextDouble() * 2.0 - 1.0);
            }
            _zigzagOffsets[MaxSegments] = 0f;

            // Initialize branches with LARGE angles
            for (int b = 0; b < MaxBranches; b++)
            {
                _branchOffsets[b] = new float[8];
                _branchPath[b] = new Vector2[8];
                for (int i = 0; i < 8; i++)
                {
                    _branchOffsets[b][i] = (float)(MuGame.Random.NextDouble() * 2.0 - 1.0);
                }

                _branchStartSegment[b] = 2 + MuGame.Random.Next(5);
                float baseAngle = 0.6f + (float)MuGame.Random.NextDouble() * 0.6f;
                _branchAngle[b] = (b % 2 == 0 ? 1 : -1) * baseAngle;
                _branchActive[b] = true;
                _branchLengthRatio[b] = 0.35f + (float)MuGame.Random.NextDouble() * 0.25f;
            }

            // Initialize dynamic lights
            _sourceLight = new DynamicLight
            {
                Owner = this,
                Color = _lightColor,
                Radius = 350f,
                Intensity = 1.5f
            };

            _targetLight = new DynamicLight
            {
                Owner = this,
                Color = _lightColor,
                Radius = 400f,
                Intensity = 1.8f
            };

            // Initialize sparks - bigger and longer lasting
            for (int i = 0; i < MaxSparks; i++)
            {
                InitSpark(i, true);
            }
        }

        private void InitSpark(int index, bool initial = false)
        {
            float rand = (float)MuGame.Random.NextDouble();
            // More sparks at target (impact point)
            _sparkType[index] = rand < 0.3f ? 0 : (rand < 0.5f ? 1 : 2);

            // Longer life for visibility
            float maxLife = 0.2f + (float)MuGame.Random.NextDouble() * 0.15f;
            _sparkMaxLife[index] = maxLife;
            _sparkLife[index] = initial ? (float)MuGame.Random.NextDouble() * maxLife : maxLife;

            // MUCH bigger sparks
            _sparkScale[index] = 0.6f + (float)MuGame.Random.NextDouble() * 0.5f;
            _sparkRotation[index] = (float)(MuGame.Random.NextDouble() * MathHelper.TwoPi);

            // Faster movement for dynamic feel
            float speed = 40f + (float)MuGame.Random.NextDouble() * 60f;
            float angle = (float)(MuGame.Random.NextDouble() * MathHelper.TwoPi);
            _sparkVelocities[index] = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
            _sparkOffsets[index] = Vector2.Zero;
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            _ = await TextureLoader.Instance.Prepare(JointTexturePath);
            _ = await TextureLoader.Instance.Prepare(EnergyTexturePath);
            _ = await TextureLoader.Instance.Prepare(FlarePath);
            _ = await TextureLoader.Instance.Prepare(SparkPath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _jointTexture = TextureLoader.Instance.GetTexture2D(JointTexturePath) ?? GraphicsManager.Instance.Pixel;
            _energyTexture = TextureLoader.Instance.GetTexture2D(EnergyTexturePath) ?? GraphicsManager.Instance.Pixel;
            _flareTexture = TextureLoader.Instance.GetTexture2D(FlarePath) ?? GraphicsManager.Instance.Pixel;
            _sparkTexture = TextureLoader.Instance.GetTexture2D(SparkPath) ?? GraphicsManager.Instance.Pixel;

            // Add dynamic lights to terrain
            if (World?.Terrain != null && !_lightsAdded)
            {
                World.Terrain.AddDynamicLight(_sourceLight);
                World.Terrain.AddDynamicLight(_targetLight);
                _lightsAdded = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (Status == GameControlStatus.NonInitialized)
            {
                _ = Load();
            }
            if (Status != GameControlStatus.Ready) return;

            ForceInView();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _remaining -= dt;
            _time += dt;

            if (_remaining <= 0f)
            {
                World?.RemoveObject(this);
                Dispose();
                return;
            }

            // Lock source position on first update
            if (!_sourceInitialized)
            {
                _lockedSource = _sourceProvider();
                _sourceInitialized = true;
            }

            _currentTarget = _targetProvider();
            UpdateBounds(_lockedSource, _currentTarget);

            // Corona rotation
            _sourceCoronaRotation += dt * 2f;
            _targetCoronaRotation -= dt * 2.5f;

            // Reshaping - bolt jumps to new zigzag pattern
            _reshapeTimer -= dt;
            if (_reshapeTimer <= 0f)
            {
                ReshapeBolt();
                _reshapeTimer = 0.04f + (float)MuGame.Random.NextDouble() * 0.025f;
            }

            UpdateFlicker(dt);
            UpdateSparks(dt);
            UpdateBranches();
            UpdateDynamicLights();

            base.Update(gameTime);
        }

        private void UpdateDynamicLights()
        {
            float lifeAlpha = CalculateLifeAlpha();
            float intensity = _flickerValue * lifeAlpha;

            // Source light
            _sourceLight.Position = _lockedSource + new Vector3(0, 0, 50f);
            _sourceLight.Intensity = 1.2f * intensity * (_isBurst ? 1.5f : 1f);

            // Target light (brighter - impact point)
            _targetLight.Position = _currentTarget + new Vector3(0, 0, 30f);
            _targetLight.Intensity = 1.6f * intensity * (_isBurst ? 1.8f : 1f);
        }

        private void ReshapeBolt()
        {
            for (int i = 1; i < MaxSegments; i++)
            {
                _zigzagOffsets[i] = (float)(MuGame.Random.NextDouble() * 2.0 - 1.0);
            }

            for (int b = 0; b < MaxBranches; b++)
            {
                for (int i = 0; i < _branchOffsets[b].Length; i++)
                {
                    _branchOffsets[b][i] = (float)(MuGame.Random.NextDouble() * 2.0 - 1.0);
                }
            }
        }

        private void UpdateFlicker(float dt)
        {
            float baseFlicker = 0.88f + 0.12f * MathF.Sin(_time * 100f);
            float noise = 0.92f + (float)MuGame.Random.NextDouble() * 0.08f;

            _burstTimer -= dt;
            if (_burstTimer <= 0f)
            {
                _burstTimer = 0.08f + (float)MuGame.Random.NextDouble() * 0.08f;
                _isBurst = MuGame.Random.NextDouble() > 0.65f;
            }

            _flickerValue = baseFlicker * noise * (_isBurst ? 1.35f : 1f);
        }

        private void UpdateBranches()
        {
            float lifeRatio = _remaining / _duration;

            if (lifeRatio < 0.35f)
            {
                for (int b = 0; b < MaxBranches; b++)
                    _branchActive[b] = false;
                return;
            }

            for (int b = 0; b < MaxBranches; b++)
            {
                if (MuGame.Random.NextDouble() > 0.97)
                {
                    _branchActive[b] = !_branchActive[b];
                    if (_branchActive[b])
                    {
                        _branchStartSegment[b] = 2 + MuGame.Random.Next(5);
                        float baseAngle = 0.6f + (float)MuGame.Random.NextDouble() * 0.6f;
                        _branchAngle[b] = (MuGame.Random.NextDouble() > 0.5 ? 1 : -1) * baseAngle;
                    }
                }
            }
        }

        private void UpdateSparks(float dt)
        {
            for (int i = 0; i < MaxSparks; i++)
            {
                _sparkLife[i] -= dt;
                if (_sparkLife[i] <= 0f)
                {
                    InitSpark(i);
                }
                else
                {
                    _sparkOffsets[i] += _sparkVelocities[i] * dt;
                    _sparkRotation[i] += dt * 3f;
                    // Slight gravity
                    _sparkVelocities[i].Y += dt * 20f;
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _spriteBatch == null || _jointTexture == null)
                return;

            var viewport = GraphicsDevice.Viewport;
            var view = Camera.Instance.View;
            var projection = Camera.Instance.Projection;

            Vector3 projSource = viewport.Project(_lockedSource, projection, view, Matrix.Identity);
            Vector3 projTarget = viewport.Project(_currentTarget, projection, view, Matrix.Identity);

            if (projSource.Z < 0 || projSource.Z > 1 || projTarget.Z < 0 || projTarget.Z > 1)
                return;

            Vector2 start = new Vector2(projSource.X, projSource.Y);
            Vector2 end = new Vector2(projTarget.X, projTarget.Y);
            Vector2 delta = end - start;

            float length = delta.Length();
            if (length < 5f || !float.IsFinite(length))
                return;

            Vector2 dir = delta / length;
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            int segments = Math.Clamp((int)(length / 50f), 4, MaxSegments);
            float step = length / segments;

            BuildZigzagPath(start, dir, perp, step, segments, projSource.Z, projTarget.Z);

            float lifeAlpha = CalculateLifeAlpha();
            float flickerAlpha = _flickerValue * lifeAlpha;
            float thicknessScale = 0.85f + 0.3f * _flickerValue;
            if (lifeAlpha < 0.3f)
                thicknessScale *= lifeAlpha / 0.3f;

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthState))
                {
                    DrawAllLayers(start, end, segments, flickerAlpha, thicknessScale, projSource.Z, projTarget.Z);
                }
            }
            else
            {
                DrawAllLayers(start, end, segments, flickerAlpha, thicknessScale, projSource.Z, projTarget.Z);
            }
        }

        private void BuildZigzagPath(Vector2 start, Vector2 dir, Vector2 perp, float step, int segments, float sourceDepth, float targetDepth)
        {
            float amplitude = _zigzagAmplitude;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector2 basePos = start + dir * (i * step);
                float offset = _zigzagOffsets[i] * amplitude;

                _pathPoints[i] = basePos + perp * offset;
                _pathDepths[i] = MathHelper.Lerp(sourceDepth, targetDepth, t);
            }
        }

        private float CalculateLifeAlpha()
        {
            float lifeRatio = _remaining / _duration;
            if (lifeRatio < 0.3f)
                return lifeRatio / 0.3f;
            return 1f;
        }

        private void DrawAllLayers(Vector2 start, Vector2 end, int segments, float flickerAlpha, float thicknessScale, float sourceDepth, float targetDepth)
        {
            float lifeRatio = _remaining / _duration;

            // 1. Coronas
            DrawCorona(start, sourceDepth, _sourceCoronaRotation, flickerAlpha * 0.55f, _coronaScale);
            DrawCorona(end, targetDepth, _targetCoronaRotation, flickerAlpha * 0.45f, _coronaScale * 1.15f);

            // 2. Impact flash
            if (lifeRatio > 0.85f || _isBurst)
            {
                float flashIntensity = lifeRatio > 0.85f ? (lifeRatio - 0.85f) / 0.15f : 0.4f;
                DrawFlash(end, targetDepth, flickerAlpha * flashIntensity);
            }

            // 3. Glow layer
            DrawBoltFromPath(segments, _thicknessGlow * thicknessScale, _glowColor * flickerAlpha * 0.4f);

            // 4. Branches
            DrawBranches(segments, flickerAlpha, thicknessScale);

            // 5. Outer layer
            DrawBoltFromPath(segments, _thicknessOuter * thicknessScale, _outerColor * flickerAlpha);

            // 6. Inner layer
            DrawBoltFromPath(segments, _thicknessInner * thicknessScale, _innerColor * flickerAlpha);

            // 7. Sparks
            DrawSparks(start, end, segments, flickerAlpha);
        }

        private void DrawBoltFromPath(int segments, float thickness, Color color)
        {
            Texture2D tex = _jointTexture ?? GraphicsManager.Instance.Pixel;
            float invTexWidth = tex.Width > 0 ? 1f / tex.Width : 1f;
            Vector2 texOrigin = new Vector2(0f, tex.Height * 0.5f);

            for (int i = 0; i < segments; i++)
            {
                Vector2 p0 = _pathPoints[i];
                Vector2 p1 = _pathPoints[i + 1];
                Vector2 segDelta = p1 - p0;
                float segLength = segDelta.Length();

                if (segLength < 0.5f) continue;

                float rotation = MathF.Atan2(segDelta.Y, segDelta.X);
                float depth = MathHelper.Clamp((_pathDepths[i] + _pathDepths[i + 1]) * 0.5f, 0f, 1f);

                Vector2 scale = new Vector2(segLength * invTexWidth * 1.05f, thickness);

                _spriteBatch.Draw(
                    tex,
                    p0,
                    null,
                    color,
                    rotation,
                    texOrigin,
                    scale,
                    SpriteEffects.None,
                    depth);
            }
        }

        private void DrawBranches(int mainSegments, float flickerAlpha, float thicknessScale)
        {
            Texture2D tex = _jointTexture ?? GraphicsManager.Instance.Pixel;
            float invTexWidth = tex.Width > 0 ? 1f / tex.Width : 1f;
            Vector2 texOrigin = new Vector2(0f, tex.Height * 0.5f);

            for (int b = 0; b < MaxBranches; b++)
            {
                if (!_branchActive[b]) continue;

                int startSeg = Math.Min(_branchStartSegment[b], mainSegments - 1);
                if (startSeg < 1) continue;

                Vector2 branchStart = _pathPoints[startSeg];
                float branchDepth = _pathDepths[startSeg];

                Vector2 mainDir = _pathPoints[startSeg] - _pathPoints[startSeg - 1];
                float mainLen = mainDir.Length();
                if (mainLen < 0.1f) continue;
                mainDir /= mainLen;

                float branchRot = MathF.Atan2(mainDir.Y, mainDir.X) + _branchAngle[b];
                Vector2 branchDir = new Vector2(MathF.Cos(branchRot), MathF.Sin(branchRot));
                Vector2 branchPerp = new Vector2(-branchDir.Y, branchDir.X);

                float remainingLength = 0f;
                for (int i = startSeg; i < mainSegments; i++)
                {
                    remainingLength += (_pathPoints[i + 1] - _pathPoints[i]).Length();
                }
                float branchLength = remainingLength * _branchLengthRatio[b];

                int branchSegs = Math.Clamp((int)(branchLength / 35f), 2, 6);
                float branchStep = branchLength / branchSegs;
                float branchAmplitude = _zigzagAmplitude * 0.6f;

                _branchSegmentCount[b] = branchSegs;

                _branchPath[b][0] = branchStart;
                for (int i = 1; i <= branchSegs; i++)
                {
                    float t = (float)i / branchSegs;
                    Vector2 basePos = branchStart + branchDir * (i * branchStep);
                    float offset = _branchOffsets[b][i % _branchOffsets[b].Length] * branchAmplitude * (1f - t * 0.5f);
                    _branchPath[b][i] = basePos + branchPerp * offset;
                }

                Color branchCol = _branchColor * flickerAlpha * 0.75f;
                float branchThickness = _thicknessOuter * thicknessScale * 0.55f;

                for (int i = 0; i < branchSegs; i++)
                {
                    Vector2 p0 = _branchPath[b][i];
                    Vector2 p1 = _branchPath[b][i + 1];
                    Vector2 segDelta = p1 - p0;
                    float segLength = segDelta.Length();

                    if (segLength < 0.5f) continue;

                    float rotation = MathF.Atan2(segDelta.Y, segDelta.X);
                    float fadeT = 1f - (float)i / branchSegs;
                    float segThickness = branchThickness * (0.4f + 0.6f * fadeT);
                    Color segColor = branchCol * fadeT;

                    Vector2 scale = new Vector2(segLength * invTexWidth * 1.05f, segThickness);

                    _spriteBatch.Draw(
                        tex,
                        p0,
                        null,
                        segColor,
                        rotation,
                        texOrigin,
                        scale,
                        SpriteEffects.None,
                        branchDepth);
                }
            }
        }

        private void DrawCorona(Vector2 position, float depth, float rotation, float alpha, float scale)
        {
            if (_energyTexture == null) return;

            Vector2 origin = new Vector2(_energyTexture.Width * 0.5f, _energyTexture.Height * 0.5f);
            float layerDepth = MathHelper.Clamp(depth, 0f, 1f);

            _spriteBatch.Draw(
                _energyTexture,
                position,
                null,
                _outerColor * alpha,
                rotation,
                origin,
                scale,
                SpriteEffects.None,
                layerDepth);
        }

        private void DrawFlash(Vector2 position, float depth, float alpha)
        {
            if (_flareTexture == null) return;

            Vector2 origin = new Vector2(_flareTexture.Width * 0.5f, _flareTexture.Height * 0.5f);
            float layerDepth = MathHelper.Clamp(depth, 0f, 1f);

            _spriteBatch.Draw(
                _flareTexture,
                position,
                null,
                _innerColor * alpha,
                0f,
                origin,
                _flashScale,
                SpriteEffects.None,
                layerDepth);
        }

        private void DrawSparks(Vector2 start, Vector2 end, int segments, float flickerAlpha)
        {
            if (_sparkTexture == null) return;

            Vector2 origin = new Vector2(_sparkTexture.Width * 0.5f, _sparkTexture.Height * 0.5f);
            float lifeRatio = _remaining / _duration;
            int activeSparks = (int)(MaxSparks * Math.Min(1f, lifeRatio * 1.5f));

            for (int i = 0; i < activeSparks; i++)
            {
                Vector2 basePos;
                float depth;

                switch (_sparkType[i])
                {
                    case 0: // Along bolt path
                        int pathIdx = Math.Min(i % (segments + 1), segments);
                        basePos = _pathPoints[pathIdx];
                        depth = _pathDepths[pathIdx];
                        break;
                    case 1: // Source
                        basePos = start;
                        depth = _pathDepths[0];
                        break;
                    default: // Target (most sparks here)
                        basePos = end;
                        depth = _pathDepths[segments];
                        break;
                }

                Vector2 pos = basePos + _sparkOffsets[i];

                // Higher alpha based on life ratio
                float sparkLifeRatio = _sparkLife[i] / _sparkMaxLife[i];
                float sparkAlpha = sparkLifeRatio * flickerAlpha * (_isBurst ? 1.4f : 1.0f);
                float layerDepth = MathHelper.Clamp(depth, 0f, 1f);

                _spriteBatch.Draw(
                    _sparkTexture,
                    pos,
                    null,
                    _sparkColor * sparkAlpha,
                    _sparkRotation[i],
                    origin,
                    _sparkScale[i],
                    SpriteEffects.None,
                    layerDepth);
            }
        }

        private void UpdateBounds(Vector3 source, Vector3 target)
        {
            Vector3 min = Vector3.Min(source, target);
            Vector3 max = Vector3.Max(source, target);
            Vector3 pad = new Vector3(100f, 100f, 120f);
            min -= pad;
            max += pad;

            Vector3 center = (min + max) * 0.5f;
            Position = center;
            BoundingBoxLocal = new BoundingBox(min - center, max - center);
        }

        public override void Dispose()
        {
            // Remove dynamic lights when effect is disposed
            if (_lightsAdded && World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_sourceLight);
                World.Terrain.RemoveDynamicLight(_targetLight);
                _lightsAdded = false;
            }

            base.Dispose();
        }
    }
}
