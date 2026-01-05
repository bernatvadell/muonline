using Client.Main.Content;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects.Effects;
using Client.Data.BMD;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Client.Main.Controls.UI.Game.Inventory;

namespace Client.Main.Objects.Wings
{
    public class CustomEffect
    {
        public int BoneID { get; set; }
        public EffectType EffectID { get; set; }
        public Vector3 Angle { get; set; }
        public Vector3 Position { get; set; }
        public float Scale { get; set; }
        public Vector3 Color { get; set; }
        public SpriteObject Effect { get; set; }
    }

    public class WingObject : ModelObject, IVertexDeformer
    {
        public List<CustomEffect> _effects { get; set; } = new List<CustomEffect>();

        private const int DefaultWingBoneLink = 47;
        // Strength is a fraction of cape height used as lateral offset.
        private const float CapeFlutterStrength = 0.1f;
        private const float CapeFlutterFrequency = 2.5f;  // radians/sec multiplier
        private const float TwoPi = MathF.PI * 2f;
        // Manual tuning for cape placement/orientation (local space, radians).
        // Adjust these if the robe needs to sit closer/further or rotated.
        private static readonly Vector3 CapePositionOffset = new Vector3(-5f, 20f, -50f);
        private static readonly Vector3 CapeAngleOffset = new Vector3(MathHelper.ToRadians(30f), 0f, MathHelper.ToRadians(10f));

        private bool _isCapeLike;
        private bool _savedBaseTransform;
        private Vector3 _savedPosition;
        private Vector3 _savedAngle;
        private float _capeTimeSeconds;
        private readonly float _flutterSeed1;
        private readonly float _flutterSeed2;
        private readonly float _flutterSeed3;
        private int _capeVerticalAxis = 2; // 0=X, 1=Y, 2=Z
        private float _capeMinAxis;
        private float _capeMaxAxis;
        private bool _capeHasExtents;

        private short _type;
        public new short Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    _ = OnChangeType();
                }
            }
        }

        private short itemIndex = -1;
        public short ItemIndex
        {
            get => itemIndex;
            set
            {
                if (itemIndex == value)
                {
                    return;
                }

                itemIndex = value;
                _ = OnChangeIndex();
            }
        }

        public WingObject()
        {
            RenderShadow = true;
            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.AlphaBlend;
            BlendMesh = -1;
            BlendMeshState = BlendState.Additive;
            Alpha = 1f;
            LinkParentAnimation = false;
            ParentBoneLink = DefaultWingBoneLink;

            int hash = GetHashCode();
            _flutterSeed1 = ((hash >> 0) & 0xFF) / 255f * TwoPi;
            _flutterSeed2 = ((hash >> 8) & 0xFF) / 255f * TwoPi;
            _flutterSeed3 = ((hash >> 16) & 0xFF) / 255f * TwoPi;
        }

        protected override float GetDepthBias()
        {
            // Wings/capes sit very close to the player body. Without a small bias they can be fully
            // occluded by the body depth, especially for capes (e.g., Dark Lord robe).
            return -0.000012f;
        }

        private async Task OnChangeType()
        {
            if (Type <= 0)
            {
                // Only clear the model if we don't have a valid ItemIndex
                // (ItemIndex is the preferred source when set)
                if (ItemIndex < 0)
                {
                    Model = null;
                }

                return;
            }

            string modelPath = Path.Combine("Item", $"Wing{Type:D2}.bmd");

            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model == null)
            {
                modelPath = Path.Combine("Item", $"Wing{Type}.bmd");
                Model = await BMDLoader.Instance.Prepare(modelPath);
            }

            Status = Model == null ? GameControlStatus.Error : GameControlStatus.Ready;
        }

        private async Task OnChangeIndex()
        {
            if (ItemIndex < 0)
            {
                // Only clear the model if we don't have a valid Type
                // (Type is the fallback source when ItemIndex is not set)
                if (Type <= 0)
                {
                    Model = null;
                }

                return;
            }

            ItemDefinition itemDefinition = ItemDatabase.GetItemDefinition(12, itemIndex);
            string modelPath = itemDefinition?.TexturePath;
            bool isCape = ItemIndex == 30;

            if (string.IsNullOrWhiteSpace(modelPath))
            {
                // Only Cape of Lord needs a hardcoded fallback model.
                if (ItemIndex == 30)
                {
                    Model = await BMDLoader.Instance.Prepare("Item/DarkLordRobe.bmd")
                        ?? await BMDLoader.Instance.Prepare("Item/DarkLordRobe02.bmd");
                }
                else
                {
                    Model = null;
                }
            }
            else
            {
                string normalized = modelPath.Replace("\\", "/");

                if (normalized.Contains("DarkLordRobe", StringComparison.OrdinalIgnoreCase))
                {
                    isCape = true;
                }

                if (normalized.Contains("Item/Wing/", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = Path.Combine("Item", Path.GetFileName(normalized)).Replace("\\", "/");
                }

                if (normalized.Contains("DarkLordRobe01", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Replace("DarkLordRobe01", "DarkLordRobe");
                }

                Model = await BMDLoader.Instance.Prepare(normalized);

                if (Model == null && normalized.EndsWith("DarkLordRobe.bmd", StringComparison.OrdinalIgnoreCase))
                {
                    string robe02Path = normalized.Replace("DarkLordRobe.bmd", "DarkLordRobe02.bmd");
                    Model = await BMDLoader.Instance.Prepare(robe02Path);
                }
            }

            if (isCape && Model != null)
            {
                UpdateCapeExtents();
            }

            ApplyCapeState(isCape);
            Status = Model == null ? GameControlStatus.Error : GameControlStatus.Ready;
        }

        private void ApplyCapeState(bool isCape)
        {
            if (isCape == _isCapeLike)
            {
                return;
            }

            if (isCape)
            {
                // Keep the standard back anchor for correct orientation.
                // We add flutter and small offsets in local space instead of switching bones,
                // because DarkLordRobe.bmd has no usable skeleton for cloth simulation.
                ParentBoneLink = DefaultWingBoneLink;

                // Save current transform so we can restore when cape is removed.
                _savedPosition = Position;
                _savedAngle = Angle;
                _savedBaseTransform = true;

                Position = _savedPosition + CapePositionOffset;
                Angle = _savedAngle + CapeAngleOffset;
            }
            else
            {
                ParentBoneLink = DefaultWingBoneLink;

                if (_savedBaseTransform)
                {
                    Position = _savedPosition;
                    Angle = _savedAngle;
                }

                _capeHasExtents = false;
            }

            _isCapeLike = isCape;
        }

        protected override IVertexDeformer GetVertexDeformer()
        {
            return _isCapeLike ? this : null;
        }

        public Vector3 DeformVertex(in BMDTextureVertex vertex, in Vector3 transformedPosition)
        {
            if (!_isCapeLike || !_capeHasExtents)
            {
                return transformedPosition;
            }

            float range = _capeMaxAxis - _capeMinAxis;
            if (range <= 0.0001f)
            {
                return transformedPosition;
            }

            // Top of the cape (higher Z) stays pinned; bottom (lower Z) flutters.
            float coord = GetAxisComponent(vertex.Position, _capeVerticalAxis);
            float t = (_capeMaxAxis - coord) / range;
            t = Math.Clamp(t, 0f, 1f);
            if (t <= 0f)
            {
                return transformedPosition;
            }

            GetLateralAxes(_capeVerticalAxis, out int axisA, out int axisB);
            float lateralA = GetAxisComponent(vertex.Position, axisA);
            float lateralB = GetAxisComponent(vertex.Position, axisB);

            float localSeed = Hash01(vertex.Position, vertex.Node);
            float baseTime = _capeTimeSeconds;

            // Slow-varying gust factor to avoid perfectly periodic motion.
            float gust =
                0.75f +
                0.18f * MathF.Sin(baseTime * 0.55f + _flutterSeed1) +
                0.12f * MathF.Sin(baseTime * 1.05f + _flutterSeed2);

            float localPhase =
                (baseTime * CapeFlutterFrequency * (0.7f + localSeed * 0.6f)) +
                (localSeed * TwoPi) +
                _flutterSeed3 +
                (lateralA * 0.03f) +
                (lateralB * 0.08f);

            float wave1 = MathF.Sin(localPhase);
            float wave2 = MathF.Sin(localPhase * 1.7f + _flutterSeed2 + lateralA * 0.06f) * 0.35f;
            float wave3 = MathF.Sin(localPhase * 0.5f + _flutterSeed1 + lateralB * 0.02f) * 0.2f;
            float wave = (wave1 + wave2 + wave3) * gust;
            float baseAmp = Math.Max(range * CapeFlutterStrength, 1.5f);
            float amp = baseAmp * t;

            // Push mostly backwards (Y) with a bit of side movement (X).
            Vector3 offset = Vector3.Zero;
            offset = AddAxisOffset(offset, axisA, wave * amp * 0.4f);
            offset = AddAxisOffset(offset, axisB, wave * amp);
            return transformedPosition + offset;
        }

        private void UpdateCapeExtents()
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var mesh in Model.Meshes)
            {
                foreach (var v in mesh.Vertices)
                {
                    float x = v.Position.X;
                    float y = v.Position.Y;
                    float z = v.Position.Z;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    if (z < minZ) minZ = z;
                    if (z > maxZ) maxZ = z;
                }
            }

            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            float rangeZ = maxZ - minZ;

            _capeVerticalAxis = 2;
            _capeMinAxis = minZ;
            _capeMaxAxis = maxZ;

            if (rangeY > rangeZ && rangeY >= rangeX)
            {
                _capeVerticalAxis = 1;
                _capeMinAxis = minY;
                _capeMaxAxis = maxY;
            }
            else if (rangeX > rangeZ && rangeX > rangeY)
            {
                _capeVerticalAxis = 0;
                _capeMinAxis = minX;
                _capeMaxAxis = maxX;
            }

            if (_capeMinAxis < _capeMaxAxis)
            {
                _capeHasExtents = true;
            }
            else
            {
                _capeHasExtents = false;
            }
        }

        private static float GetAxisComponent(System.Numerics.Vector3 v, int axis)
        {
            return axis switch
            {
                0 => v.X,
                1 => v.Y,
                _ => v.Z
            };
        }

        private static void GetLateralAxes(int verticalAxis, out int axisA, out int axisB)
        {
            switch (verticalAxis)
            {
                case 0:
                    axisA = 1;
                    axisB = 2;
                    break;
                case 1:
                    axisA = 0;
                    axisB = 2;
                    break;
                default:
                    axisA = 0;
                    axisB = 1;
                    break;
            }
        }

        private static Vector3 AddAxisOffset(Vector3 v, int axis, float delta)
        {
            switch (axis)
            {
                case 0:
                    v.X += delta;
                    break;
                case 1:
                    v.Y += delta;
                    break;
                default:
                    v.Z += delta;
                    break;
            }
            return v;
        }

        private static float Hash01(System.Numerics.Vector3 p, short node)
        {
            float h = p.X * 0.137f + p.Y * 0.713f + p.Z * 1.371f + node * 0.17f;
            return Fract(MathF.Sin(h) * 43758.5453f);
        }

        private static float Fract(float v)
        {
            return v - MathF.Floor(v);
        }

        public override async Task Load()
        {
            if (_effects.Count > 0)
            {
                foreach (var effect in _effects)
                {
                    effect.Effect = Utils.GetEffectByCode(effect.EffectID);
                    effect.Effect.Light = effect.Color;
                    effect.Effect.Scale = effect.Scale;
                    Children.Add(effect.Effect);
                }
            }

            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            _capeTimeSeconds = (float)gameTime.TotalGameTime.TotalSeconds;

            if (_isCapeLike)
            {
                // Force per-frame buffer rebuild so DeformVertex is applied continuously.
                InvalidateBuffers(BufferFlagAnimation);
            }

            base.Update(gameTime);

            if (_effects.Count > 0 && BoneTransform != null)
            {
                foreach (var effect in _effects)
                {
                    effect.Effect.Position = effect.Position + BoneTransform[effect.BoneID].Translation;
                    effect.Effect.Angle = effect.Angle + Angle;
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
