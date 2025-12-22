using Client.Data;
using Client.Main;
using Client.Main.Content;
using Client.Main.Objects.Effects;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Client.Data.BMD;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class WeaponObject : ModelObject
    {
        private int _type;
        private new ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<WeaponObject>();
        private readonly WeaponTrailEffect _trail;
        private Vector3 _trailTipLocal;
        private int _trailTipBone = -1;
        private int _trailLevelCache = -1;
        private bool _trailExcellentCache;
        private bool _trailAncientCache;

        public new int Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    _ = OnChangeTypeAsync();
                }
            }
        }

        public bool IsRightHand { get; set; }
        public string TexturePath { get; set; }
        public byte ItemGroup { get; set; }

        public WeaponObject()
        {
            RenderShadow = true;
            LinkParentAnimation = true;

            if (Constants.ENABLE_WEAPON_TRAIL)
            {
                _trail = new WeaponTrailEffect
                {
                    Hidden = true
                };
                _trail.SamplePoint = SampleWeaponTip;
                Children.Add(_trail);
            }
        }

        private async Task OnChangeTypeAsync()
        {
            ParentBoneLink = IsRightHand ? 10 : 15;

            string modelPath = GetWeaponPath(Type);
            if (string.IsNullOrEmpty(modelPath))
            {
                Model = null;
                return;
            }

            Model = await BMDLoader.Instance.Prepare(modelPath);
            if (Model == null)
            {
                _logger?.LogWarning("WeaponObject: Failed to load model for Type {Type}. Path: {Path}", Type, modelPath);
                Status = Models.GameControlStatus.Error;
            }
            else
            {
                UpdateTrailFromModel();
            }
        }

        private static string GetWeaponPath(int type)
        {
            var modelType = (ModelType)type;
            int groupBase = type / 512 * 512;
            int id = type - groupBase;

            string category = ((ModelType)groupBase).ToString().Replace("ITEM_GROUP_", "").Split('_')[0];

            if (category == "MACE") category = "Mace";

            if (id >= 0)
            {
                return $"Item/{category}{id + 1:D2}.bmd";
            }

            return null;
        }

        public override void Update(GameTime gameTime)
        {
            if (_trail != null &&
                (_trailLevelCache != ItemLevel || _trailExcellentCache != IsExcellentItem || _trailAncientCache != IsAncientItem))
            {
                RefreshTrailColor();
            }

            base.Update(gameTime);
            // Force invalidation is now handled at parent level in ModelObject.Update()
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();
            UpdateTrailFromModel();
        }

        private void UpdateTrailFromModel()
        {
            if (_trail == null)
                return;

            _trail.SetTipFromModel(Model);
            ComputeTrailTip(Model);
            RefreshTrailColor();
        }

        private void RefreshTrailColor()
        {
            if (_trail == null)
                return;

            _trail.SetTrailColor(GetTrailColor());

            _trailLevelCache = ItemLevel;
            _trailExcellentCache = IsExcellentItem;
            _trailAncientCache = IsAncientItem;
        }

        private Color GetTrailColor()
        {
            if (IsAncientItem) return new Color(0.45f, 0.9f, 1f);
            if (IsExcellentItem) return new Color(0.5f, 1f, 0.6f);
            if (ItemLevel >= 7) return new Color(1f, 0.82f, 0.55f);
            return new Color(0.8f, 0.9f, 1f);
        }

        private Vector3 SampleWeaponTip()
        {
            if (_trailTipBone >= 0 && BoneTransform != null && _trailTipBone < BoneTransform.Length)
            {
                Vector3 animated = Vector3.Transform(_trailTipLocal, BoneTransform[_trailTipBone]);
                return Vector3.Transform(animated, WorldPosition);
            }

            // Fallback: use object origin (no tip data).
            return Vector3.Transform(_trailTipLocal, WorldPosition);
        }

        private void ComputeTrailTip(BMD model)
        {
            _trailTipBone = -1;
            _trailTipLocal = Vector3.Zero;

            if (model?.Meshes == null || model.Meshes.Length == 0)
                return;

            Matrix[] restBones = BuildRestPose(model);
            if (restBones == null)
                return;

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            Vector3 sum = Vector3.Zero;
            int count = 0;

            foreach (var mesh in model.Meshes)
            {
                var verts = mesh.Vertices;
                if (verts == null) continue;

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 pos = TransformVertex(restBones, verts[i]);
                    min = Vector3.Min(min, pos);
                    max = Vector3.Max(max, pos);
                    sum += pos;
                    count++;
                }
            }

            if (count == 0)
                return;

            Vector3 centroid = sum / count;
            Vector3 extents = max - min;

            int axis = 0;
            float axisLen = extents.X;
            if (extents.Y > axisLen) { axis = 1; axisLen = extents.Y; }
            if (extents.Z > axisLen) { axis = 2; axisLen = extents.Z; }

            Vector3 axisVec = axis switch
            {
                0 => Vector3.UnitX,
                1 => Vector3.UnitY,
                _ => Vector3.UnitZ
            };

            Vector3 refPoint = (restBones != null && restBones.Length > 0) ? restBones[0].Translation : Vector3.Zero;

            if (!SelectTipVertex(model, restBones, centroid, axisVec, refPoint, preferSkinned: true, out _trailTipLocal, out _trailTipBone))
            {
                SelectTipVertex(model, restBones, centroid, axisVec, refPoint, preferSkinned: false, out _trailTipLocal, out _trailTipBone);
            }
        }

        private static bool SelectTipVertex(BMD model, Matrix[] restBones, Vector3 centroid, Vector3 axisVec, Vector3 refPoint, bool preferSkinned, out Vector3 tipLocal, out int tipBone)
        {
            bool hasMin = false, hasMax = false;
            Vector3 minLocal = Vector3.Zero, maxLocal = Vector3.Zero;
            int minBone = -1, maxBone = -1;
            float minProj = 0f, maxProj = 0f;
            Vector3 minPosWorld = Vector3.Zero, maxPosWorld = Vector3.Zero;

            foreach (var mesh in model.Meshes)
            {
                var verts = mesh.Vertices;
                if (verts == null) continue;

                for (int i = 0; i < verts.Length; i++)
                {
                    var vert = verts[i];
                    if (preferSkinned && vert.Node < 0)
                        continue;

                    Vector3 pos = TransformVertex(restBones, vert);
                    float proj = Vector3.Dot(pos - centroid, axisVec);
                    if (!hasMax || proj > maxProj)
                    {
                        hasMax = true;
                        maxProj = proj;
                        maxLocal = ToXna(vert.Position);
                        maxBone = vert.Node;
                        maxPosWorld = pos;
                    }
                    if (!hasMin || proj < minProj)
                    {
                        hasMin = true;
                        minProj = proj;
                        minLocal = ToXna(vert.Position);
                        minBone = vert.Node;
                        minPosWorld = pos;
                    }
                }
            }

            if (!hasMin && !hasMax)
            {
                tipLocal = Vector3.Zero;
                tipBone = -1;
                return false;
            }

            // Choose the endpoint farther from the weapon's root/reference point to bias toward the blade tip.
            float maxDist = hasMax ? Vector3.DistanceSquared(refPoint, maxPosWorld) : float.MinValue;
            float minDist = hasMin ? Vector3.DistanceSquared(refPoint, minPosWorld) : float.MinValue;

            // Recompute world positions if we skipped skinning (posWorld would be local).
            if (hasMax && maxBone >= 0 && restBones != null && maxBone < restBones.Length)
            {
                maxDist = Vector3.DistanceSquared(refPoint, Vector3.Transform(maxLocal, restBones[maxBone]));
            }
            if (hasMin && minBone >= 0 && restBones != null && minBone < restBones.Length)
            {
                minDist = Vector3.DistanceSquared(refPoint, Vector3.Transform(minLocal, restBones[minBone]));
            }

            if (maxDist > minDist || !hasMin)
            {
                tipLocal = maxLocal;
                tipBone = maxBone;
            }
            else if (minDist > maxDist || !hasMax)
            {
                tipLocal = minLocal;
                tipBone = minBone;
            }
            else
            {
                // Distances tied; pick the side with larger absolute projection.
                if (MathF.Abs(maxProj) >= MathF.Abs(minProj))
                {
                    tipLocal = maxLocal;
                    tipBone = maxBone;
                }
                else
                {
                    tipLocal = minLocal;
                    tipBone = minBone;
                }
            }

            return true;
        }

        private static Matrix[] BuildRestPose(BMD model)
        {
            if (model?.Bones == null || model.Bones.Length == 0)
                return null;

            var bones = model.Bones;
            var result = new Matrix[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                Matrix local = Matrix.Identity;

                if (bone != BMDTextureBone.Dummy &&
                    bone.Matrixes != null &&
                    bone.Matrixes.Length > 0 &&
                    bone.Matrixes[0].Quaternion?.Length > 0 &&
                    bone.Matrixes[0].Position?.Length > 0)
                {
                    var bm = bone.Matrixes[0];
                    local = Matrix.CreateFromQuaternion(ToXna(bm.Quaternion[0]));
                    local.Translation = ToXna(bm.Position[0]);
                }

                if (bone.Parent >= 0 && bone.Parent < bones.Length)
                    result[i] = local * result[bone.Parent];
                else
                    result[i] = local;
            }

            return result;
        }

        private static Vector3 TransformVertex(Matrix[] bones, Client.Data.BMD.BMDTextureVertex vert)
        {
            Vector3 local = ToXna(vert.Position);
            if (vert.Node >= 0 && bones != null && vert.Node < bones.Length)
            {
                return Vector3.Transform(local, bones[vert.Node]);
            }
            return local;
        }

        private static Vector3 ToXna(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        private static Quaternion ToXna(System.Numerics.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
