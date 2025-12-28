#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Graphics;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Scroll of Evil Spirit visual effect (Skill ID 9).
    /// Creates 4 spirit bolts at 90° intervals that spiral inward towards the caster.
    /// Renders as 3D cross-shaped ribbons (two perpendicular quads per segment).
    /// Based on original MU client: ZzzEffectJoint.cpp
    /// </summary>
    public sealed class ScrollOfEvilSpiritEffect : WorldObject
    {
        private const string JointSpiritTexturePath = "Effect/JointSpirit01.jpg";
        private const string SoundEvil = "Sound/sEvil.wav";

        // Original parameters from ZzzEffectJoint.cpp SubType 0
        private const float InitialZOffset = 100f;
        private const float TargetZOffset = 80f;
        private const float Velocity = 40f;
        private const float LifeTimeFrames = 49f;
        private const int MaxTails = 6;
        private const int SpiritCount = 4;
        private const float HummingTurnRate = 10f;

        // Larger, more transparent spirits
        private const float MainSpiritScale = 180f;
        private const float VisualSpiritScale = 60f;
        private const float AlphaMultiplier = 0.4f;

        // Dark light settings - shadows don't stack thanks to TerrainLightManager Min logic
        private const float DarkLightRadius = 160f;
        private const float DarkLightIntensity = 0.6f;

        // Subtractive blend state (RENDER_TYPE_ALPHA_BLEND_MINUS)
        private static readonly BlendState SubtractiveBlend = new BlendState
        {
            ColorSourceBlend = Blend.SourceAlpha,
            ColorDestinationBlend = Blend.One,
            ColorBlendFunction = BlendFunction.ReverseSubtract,
            AlphaSourceBlend = Blend.SourceAlpha,
            AlphaDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.ReverseSubtract
        };

        private readonly WalkerObject _caster;

        private Texture2D? _spiritTexture;
        private BasicEffect? _effect;

        private readonly SpiritBolt[] _spirits = new SpiritBolt[SpiritCount * 2];
        private int _spiritCount;

        // Dark lights for shadow effect (one per main spirit)
        private readonly DynamicLight[] _darkLights = new DynamicLight[SpiritCount];
        private bool _lightsAdded;

        private bool _soundPlayed;
        private bool _initialized;

        // Vertex/index buffers for 3D rendering
        private VertexPositionColorTexture[] _vertices = null!;
        private short[] _indices = null!;

        private struct SpiritBolt
        {
            public Vector3 Position;
            public Vector3 Angle;         // Euler angles (Pitch, unused, Yaw) in degrees
            public Vector3 Direction;     // Perturbation accumulator
            public TailVertex[] Tails;    // Each tail has 4 vertices (cross shape)
            public int NumTails;
            public float LifeTime;
            public float Scale;
        }

        private struct TailVertex
        {
            public Vector3 V0;  // Left (-X)
            public Vector3 V1;  // Right (+X)
            public Vector3 V2;  // Down (-Z)
            public Vector3 V3;  // Up (+Z)
        }

        public ScrollOfEvilSpiritEffect(WalkerObject caster, float targetAngle)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));

            Position = caster.WorldPosition.Translation;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-600f, -600f, -150f),
                new Vector3(600f, 600f, 350f));

            // Pre-allocate vertex/index buffers
            // Each spirit has MaxTails segments, each segment has 2 quads (4 verts each) = 8 verts per segment
            // Total: SpiritCount*2 spirits * MaxTails segments * 8 verts = lots
            int maxSpirits = SpiritCount * 2;
            int maxQuads = maxSpirits * MaxTails * 2;  // 2 quads per segment (cross shape)
            _vertices = new VertexPositionColorTexture[maxQuads * 4];
            _indices = new short[maxQuads * 6];

            // Pre-build indices
            for (int i = 0; i < maxQuads; i++)
            {
                int vi = i * 4;
                int ii = i * 6;
                _indices[ii + 0] = (short)(vi + 0);
                _indices[ii + 1] = (short)(vi + 1);
                _indices[ii + 2] = (short)(vi + 2);
                _indices[ii + 3] = (short)(vi + 0);
                _indices[ii + 4] = (short)(vi + 2);
                _indices[ii + 5] = (short)(vi + 3);
            }
        }

        private void InitializeSpirits()
        {
            if (_initialized)
                return;

            _initialized = true;
            _spiritCount = 0;

            Vector3 casterPos = _caster.WorldPosition.Translation;
            Vector3 startPos = casterPos + new Vector3(0, 0, InitialZOffset);

            for (int i = 0; i < SpiritCount; i++)
            {
                float angleOffset = i * 90f;

                // Main bolt (larger scale)
                _spirits[_spiritCount++] = new SpiritBolt
                {
                    Position = startPos,
                    Angle = new Vector3(0f, 0f, angleOffset),
                    Direction = Vector3.Zero,
                    Tails = new TailVertex[MaxTails + 1],
                    NumTails = 0,
                    LifeTime = LifeTimeFrames,
                    Scale = MainSpiritScale
                };

                // Visual bolt (smaller scale)
                _spirits[_spiritCount++] = new SpiritBolt
                {
                    Position = startPos,
                    Angle = new Vector3(0f, 0f, angleOffset),
                    Direction = Vector3.Zero,
                    Tails = new TailVertex[MaxTails + 1],
                    NumTails = 0,
                    LifeTime = LifeTimeFrames,
                    Scale = VisualSpiritScale
                };

                // Create dark light for this main spirit
                _darkLights[i] = new DynamicLight
                {
                    Owner = this,
                    Position = startPos,
                    Color = new Vector3(-1f, -1f, -1f),  // Negative color = shadow/darkness
                    Radius = DarkLightRadius,
                    Intensity = DarkLightIntensity
                };
            }
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            _ = await TextureLoader.Instance.Prepare(JointSpiritTexturePath);
            _spiritTexture = TextureLoader.Instance.GetTexture2D(JointSpiritTexturePath) ?? GraphicsManager.Instance.Pixel;

            _effect = new BasicEffect(GraphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false
            };

            // Add dark lights to terrain
            if (World?.Terrain != null && !_lightsAdded && _initialized)
            {
                for (int i = 0; i < SpiritCount; i++)
                {
                    if (_darkLights[i] != null)
                        World.Terrain.AddDynamicLight(_darkLights[i]);
                }
                _lightsAdded = true;
            }
        }

        private void RemoveDarkLights()
        {
            if (!_lightsAdded || World?.Terrain == null)
                return;

            for (int i = 0; i < SpiritCount; i++)
            {
                if (_darkLights[i] != null)
                    World.Terrain.RemoveDynamicLight(_darkLights[i]);
            }
            _lightsAdded = false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status == GameControlStatus.NonInitialized)
                _ = Load();

            if (Status != GameControlStatus.Ready)
                return;

            ForceInView();

            if (!_initialized)
                InitializeSpirits();

            // Add lights after spirits are initialized
            if (_initialized && !_lightsAdded && World?.Terrain != null)
            {
                for (int i = 0; i < SpiritCount; i++)
                {
                    if (_darkLights[i] != null)
                        World.Terrain.AddDynamicLight(_darkLights[i]);
                }
                _lightsAdded = true;
            }

            float frameFactor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;

            if (!_soundPlayed)
            {
                SoundController.Instance.PlayBuffer(SoundEvil);
                _soundPlayed = true;
            }

            if (_caster.Status == GameControlStatus.Disposed || _caster.World == null)
            {
                RemoveSelf();
                return;
            }

            Vector3 targetPos = _caster.WorldPosition.Translation;
            targetPos.Z += TargetZOffset;

            bool anyAlive = false;
            for (int i = 0; i < _spiritCount; i++)
            {
                ref var spirit = ref _spirits[i];

                if (spirit.LifeTime <= 0)
                    continue;

                anyAlive = true;

                // Shift tail positions
                if (spirit.NumTails < MaxTails)
                    spirit.NumTails++;

                for (int t = spirit.NumTails - 1; t > 0; t--)
                {
                    spirit.Tails[t] = spirit.Tails[t - 1];
                }

                // Create new tail at current position
                CreateTail(ref spirit);

                // MoveHumming - adjust angle towards target
                MoveHumming(ref spirit.Position, ref spirit.Angle, targetPos, HummingTurnRate * frameFactor);

                // Random perturbation
                spirit.Direction.X += (MuGame.Random.Next(32) - 16) * 0.2f;
                spirit.Direction.Z += (MuGame.Random.Next(32) - 16) * 0.8f;

                spirit.Angle.X += spirit.Direction.X * frameFactor;
                spirit.Angle.Z += spirit.Direction.Z * frameFactor;

                spirit.Direction.X *= 0.6f;
                spirit.Direction.Z *= 0.8f;

                // Move forward
                float yawRad = MathHelper.ToRadians(spirit.Angle.Z);
                float pitchRad = MathHelper.ToRadians(spirit.Angle.X);

                Vector3 forward = new Vector3(
                    MathF.Sin(yawRad) * MathF.Cos(pitchRad),
                    -MathF.Cos(yawRad) * MathF.Cos(pitchRad),
                    MathF.Sin(pitchRad)
                );

                spirit.Position += forward * Velocity * frameFactor;

                // Height constraints
                if (World?.Terrain != null)
                {
                    float terrainHeight = World.Terrain.RequestTerrainHeight(spirit.Position.X, spirit.Position.Y);
                    if (spirit.Position.Z < terrainHeight + 100f)
                    {
                        spirit.Direction.X = 0f;
                        spirit.Angle.X = -5f;
                    }
                    if (spirit.Position.Z > terrainHeight + 400f)
                    {
                        spirit.Direction.X = 0f;
                        spirit.Angle.X = 5f;
                    }
                }

                spirit.LifeTime -= frameFactor;

                // Update corresponding dark light (main spirits at even indices: 0,2,4,6 -> light 0,1,2,3)
                if (i % 2 == 0)
                {
                    int lightIdx = i / 2;
                    if (_darkLights[lightIdx] != null)
                    {
                        _darkLights[lightIdx].Position = spirit.Position;

                        // Fade intensity with lifetime
                        float lifeFactor = MathHelper.Clamp(spirit.LifeTime / LifeTimeFrames, 0f, 1f);
                        _darkLights[lightIdx].Intensity = DarkLightIntensity * lifeFactor;
                    }
                }
            }

            UpdateBounds();

            if (!anyAlive)
            {
                RemoveSelf();
            }
        }

        /// <summary>
        /// Creates tail vertices at current position forming a cross shape.
        /// Based on original CreateTail function in ZzzEffectJoint.cpp
        /// </summary>
        private void CreateTail(ref SpiritBolt spirit)
        {
            float halfScale = spirit.Scale * 0.5f;

            // Build rotation matrix from angles
            Matrix rotation = Matrix.CreateRotationX(MathHelper.ToRadians(spirit.Angle.X)) *
                             Matrix.CreateRotationZ(MathHelper.ToRadians(spirit.Angle.Z));

            // Create 4 offset vectors forming a cross
            Vector3 left = Vector3.Transform(new Vector3(-halfScale, 0, 0), rotation);
            Vector3 right = Vector3.Transform(new Vector3(halfScale, 0, 0), rotation);
            Vector3 down = Vector3.Transform(new Vector3(0, 0, -halfScale), rotation);
            Vector3 up = Vector3.Transform(new Vector3(0, 0, halfScale), rotation);

            spirit.Tails[0] = new TailVertex
            {
                V0 = spirit.Position + left,
                V1 = spirit.Position + right,
                V2 = spirit.Position + down,
                V3 = spirit.Position + up
            };
        }

        private static void MoveHumming(ref Vector3 position, ref Vector3 angle, Vector3 targetPosition, float turn)
        {
            float dx = targetPosition.X - position.X;
            float dy = targetPosition.Y - position.Y;
            float targetYaw = MathHelper.ToDegrees(MathF.Atan2(dx, -dy));

            angle.Z = TurnAngle(angle.Z, targetYaw, turn);

            float horizontalDist = MathF.Sqrt(dx * dx + dy * dy);
            float dz = targetPosition.Z - position.Z;
            float targetPitch = MathHelper.ToDegrees(MathF.Atan2(dz, horizontalDist));

            angle.X = TurnAngle(angle.X, targetPitch, turn);
        }

        private static float TurnAngle(float current, float target, float maxTurn)
        {
            float diff = target - current;
            while (diff > 180f) diff -= 360f;
            while (diff < -180f) diff += 360f;

            if (MathF.Abs(diff) <= maxTurn)
                return target;

            return current + MathF.Sign(diff) * maxTurn;
        }

        private void UpdateBounds()
        {
            if (_spiritCount == 0)
                return;

            Vector3 min = _spirits[0].Position;
            Vector3 max = _spirits[0].Position;

            for (int i = 1; i < _spiritCount; i++)
            {
                if (_spirits[i].LifeTime > 0)
                {
                    min = Vector3.Min(min, _spirits[i].Position);
                    max = Vector3.Max(max, _spirits[i].Position);
                }
            }

            Position = (min + max) * 0.5f;
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _spiritTexture == null || _effect == null)
                return;

            var camera = Camera.Instance;
            if (camera == null)
                return;

            int quadIndex = 0;
            BuildVertices(ref quadIndex);

            if (quadIndex == 0)
                return;

            // Save states
            var prevBlend = GraphicsDevice.BlendState;
            var prevDepth = GraphicsDevice.DepthStencilState;
            var prevRaster = GraphicsDevice.RasterizerState;

            // Apply states for dark spirits
            GraphicsDevice.BlendState = SubtractiveBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.Identity;
            _effect.Texture = _spiritTexture;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertices, 0, quadIndex * 4,
                    _indices, 0, quadIndex * 2);
            }

            // Restore states
            GraphicsDevice.BlendState = prevBlend;
            GraphicsDevice.DepthStencilState = prevDepth;
            GraphicsDevice.RasterizerState = prevRaster;
        }

        private void BuildVertices(ref int quadIndex)
        {
            for (int i = 0; i < _spiritCount; i++)
            {
                ref var spirit = ref _spirits[i];

                if (spirit.LifeTime <= 0 || spirit.NumTails < 2)
                    continue;

                // Luminosity based on lifetime
                float luminosity = MathHelper.Clamp(spirit.LifeTime * 0.1f, 0f, 1f);

                for (int j = 0; j < spirit.NumTails - 1; j++)
                {
                    ref var current = ref spirit.Tails[j];
                    ref var next = ref spirit.Tails[j + 1];

                    // Light falloff along tail
                    float light1 = (spirit.NumTails - j) / (float)(MaxTails - 1);
                    float light2 = (spirit.NumTails - (j + 1)) / (float)(MaxTails - 1);

                    // Apply transparency multiplier for more ethereal look
                    float alpha1 = luminosity * light1 * AlphaMultiplier;
                    float alpha2 = luminosity * light2 * AlphaMultiplier;

                    // White color for subtractive blend -> appears dark
                    Color c1 = new Color(alpha1, alpha1, alpha1, alpha1);
                    Color c2 = new Color(alpha2, alpha2, alpha2, alpha2);

                    // UV coordinates
                    float u1 = light1;
                    float u2 = light2;

                    // FACE ONE: Quad from V2-V3 (vertical ribbon)
                    if (quadIndex * 4 + 4 <= _vertices.Length)
                    {
                        int vi = quadIndex * 4;
                        _vertices[vi + 0] = new VertexPositionColorTexture(current.V2, c1, new Vector2(u1, 1f));
                        _vertices[vi + 1] = new VertexPositionColorTexture(current.V3, c1, new Vector2(u1, 0f));
                        _vertices[vi + 2] = new VertexPositionColorTexture(next.V3, c2, new Vector2(u2, 0f));
                        _vertices[vi + 3] = new VertexPositionColorTexture(next.V2, c2, new Vector2(u2, 1f));
                        quadIndex++;
                    }

                    // FACE TWO: Quad from V0-V1 (horizontal ribbon)
                    if (quadIndex * 4 + 4 <= _vertices.Length)
                    {
                        int vi = quadIndex * 4;
                        _vertices[vi + 0] = new VertexPositionColorTexture(current.V0, c1, new Vector2(u1, 0f));
                        _vertices[vi + 1] = new VertexPositionColorTexture(current.V1, c1, new Vector2(u1, 1f));
                        _vertices[vi + 2] = new VertexPositionColorTexture(next.V1, c2, new Vector2(u2, 1f));
                        _vertices[vi + 3] = new VertexPositionColorTexture(next.V0, c2, new Vector2(u2, 0f));
                        quadIndex++;
                    }
                }
            }
        }

        private void RemoveSelf()
        {
            RemoveDarkLights();

            if (Parent != null)
                Parent.Children.Remove(this);
            else
                World?.RemoveObject(this);

            Dispose();
        }

        public override void Dispose()
        {
            RemoveDarkLights();
            _effect?.Dispose();
            _effect = null;
            base.Dispose();
        }
    }
}
