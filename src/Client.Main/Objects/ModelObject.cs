using Client.Data;
using Client.Data.BMD;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace Client.Main.Objects
{
    public abstract class ModelObject : WorldObject
    {
        private VertexBuffer[] _boneVertexBuffers;
        private IndexBuffer[] _boneIndexBuffers;
        private Texture2D[] _boneTextures;
        private TextureScript[] _scriptTextures;
        private TextureData[] _dataTextures;

        private int _priorAction = 0;
        private AlphaTestEffect _effect;
        private bool _invalidatedBuffers = true;
        private float _blendMeshLight = 1f;
        private float _previousFrame = 0;

        protected Matrix[] BoneTransform { get; set; }
        public int CurrentAction { get; set; }
        public virtual int OriginBoneIndex => 0;
        public BMD Model { get; set; }
        public float BodyHeight { get; private set; }
        public int HiddenMesh { get; set; } = -1;
        public int BlendMesh { get; set; } = -1;
        public BlendState BlendMeshState { get; set; } = BlendState.AlphaBlend;
        public float BlendMeshLight { get => _blendMeshLight; set { _blendMeshLight = value; _invalidatedBuffers = true; } }

        public ModelObject()
        {
            MatrixChanged += (_s, _e) => UpdateWorldPosition();
        }

        public override async Task Load()
        {
            if (Model == null)
            {
                Debug.WriteLine($"Model is not assigned for {ObjectName} -> Type: {Type}");
                return;
            }

            lock (GraphicsDevice)
            {
                _effect = new AlphaTestEffect(GraphicsDevice)
                {
                    VertexColorEnabled = true,
                    World = Matrix.Identity,
                    AlphaFunction = CompareFunction.Greater,
                    ReferenceAlpha = (int)(255 * 0.25f)
                };
            }

            UpdateWorldPosition();
            Animation(0, 0, 0);

            await base.Load();
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Ready || OutOfView) return;

            if (_effect != null)
            {
                _effect.View = Camera.Instance.View;
                _effect.Projection = Camera.Instance.Projection;
            }

            Animation(gameTime);
            SetDynamicBuffers();
        }
        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _boneIndexBuffers == null) return;

            DrawModel(false);
            base.Draw(gameTime);
        }
        public virtual void DrawModel(bool isAfterDraw)
        {
            for (var i = 0; i < Model.Meshes.Length; i++)
            {
                var isRGBA = _dataTextures[i].Components == 4;
                var isBlendMesh = BlendMesh == i;
                var draw = isAfterDraw
                    ? isRGBA || isBlendMesh
                    : !isRGBA && !isBlendMesh;

                if (!draw) continue;
                DrawMesh(i);
            }
        }
        public virtual void DrawMesh(int mesh)
        {
            if (HiddenMesh == mesh)
                return;

            if (_boneVertexBuffers == null)
                return;

            GraphicsDevice.BlendState = BlendMesh == mesh ? BlendMeshState : BlendState;

            var texture = _boneTextures[mesh];

            var vertexBuffer = _boneVertexBuffers[mesh];
            var indexBuffer = _boneIndexBuffers[mesh];
            var primitiveCount = indexBuffer.IndexCount / 3;

            _effect.Alpha = Alpha;
            _effect.Texture = texture;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                GraphicsDevice.SetVertexBuffer(vertexBuffer);
                GraphicsDevice.Indices = indexBuffer;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
            }
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible) return;
            DrawModel(true);
            base.DrawAfter(gameTime);
        }

        public override void Dispose()
        {
            base.Dispose();

            _effect?.Dispose();
            Model = null;
            BoneTransform = null;
            _invalidatedBuffers = true;
        }

        private void UpdateWorldPosition()
        {
            _invalidatedBuffers = true;

            if (_effect != null)
            {
                _effect.World = WorldPosition;
            }
        }
        private void UpdateBoundings()
        {
            if (Model == null) return;

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var mesh in Model.Meshes)
            {
                foreach (var vertex in mesh.Vertices)
                {
                    int boneIndex = vertex.Node;

                    if (boneIndex < 0 || boneIndex >= BoneTransform.Length)
                        continue;

                    Matrix boneMatrix = BoneTransform[boneIndex];
                    Vector3 transformedPosition = Vector3.Transform(vertex.Position, boneMatrix);
                    min = Vector3.Min(min, transformedPosition);
                    max = Vector3.Max(max, transformedPosition);
                }
            }

            BoundingBoxLocal = new BoundingBox(min, max);
        }
        private void Animation(GameTime gameTime)
        {
            if (LinkParent || Model.Actions.Length <= 0) return;

            float animationSpeed = 3f;
            float currentFrame = (float)(gameTime.TotalGameTime.TotalSeconds * animationSpeed);

            currentFrame %= Model.Actions[CurrentAction].NumAnimationKeys;
            var priorFrame = currentFrame - 1;
            if (priorFrame < 0) priorFrame = Model.Actions[CurrentAction].NumAnimationKeys - 1 - priorFrame;

            Animation(currentFrame, priorFrame, _priorAction);

            _priorAction = CurrentAction;
        }
        private void SetDynamicBuffers()
        {
            if (!_invalidatedBuffers)
                return;

            _boneVertexBuffers ??= new VertexBuffer[Model.Meshes.Length];
            _boneIndexBuffers ??= new IndexBuffer[Model.Meshes.Length];
            _boneTextures ??= new Texture2D[Model.Meshes.Length];
            _scriptTextures ??= new TextureScript[Model.Meshes.Length];
            _dataTextures ??= new TextureData[Model.Meshes.Length];

            for (int meshIndex = 0; meshIndex < Model.Meshes.Length; meshIndex++)
            {
                var mesh = Model.Meshes[meshIndex];

                var bodyLight = Vector3.Zero;

                if (LightEnabled && World.Terrain != null)
                {
                    var terrainLight = World.Terrain.RequestTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y);
                    terrainLight += Light;
                    bodyLight = terrainLight;
                }

                if (meshIndex == BlendMesh)
                    bodyLight = bodyLight * BlendMeshLight;
                else
                    bodyLight = bodyLight * Alpha;

                _boneVertexBuffers[meshIndex]?.Dispose();
                _boneIndexBuffers[meshIndex]?.Dispose();

                var bones = LinkParent && Parent is ModelObject parentModel ? parentModel.BoneTransform : BoneTransform;

                BMDLoader.Instance.GetModelBuffers(Model, meshIndex, Color.White, bones, out var vertexBuffer, out var indexBuffer);

                _boneVertexBuffers[meshIndex] = vertexBuffer;
                _boneIndexBuffers[meshIndex] = indexBuffer;


                if (_boneTextures[meshIndex] == null)
                {
                    var texturePath = BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);
                    _boneTextures[meshIndex] = TextureLoader.Instance.GetTexture2D(texturePath);
                    _scriptTextures[meshIndex] = TextureLoader.Instance.GetScript(texturePath);
                    _dataTextures[meshIndex] = TextureLoader.Instance.Get(texturePath);

                    if (BlendState == BlendState.Opaque && _dataTextures[meshIndex].Components == 4)
                        BlendState = Blendings.Alpha;

                    var script = TextureLoader.Instance.GetScript(texturePath);

                    if (script != null)
                    {
                        if (script.HiddenMesh)
                            HiddenMesh = meshIndex;

                        if (script.Bright)
                            BlendMesh = meshIndex;
                    }
                }

                _invalidatedBuffers = false;
            }
        }

        private void Animation(float currentFrame, float priorFrame, int priorAction)
        {
            if (LinkParent || Model == null || Model.Actions.Length <= 0) return;

            if (priorAction >= Model.Actions.Length) priorAction = 0;
            if (CurrentAction >= Model.Actions.Length) CurrentAction = 0;

            int currentAnimationFrame = (int)currentFrame;
            float interpolationFactor = currentFrame - currentAnimationFrame;

            int priorAnimationFrame = (int)priorFrame;
            if (priorAnimationFrame < 0) priorAnimationFrame = 0;
            if (currentAnimationFrame < 0) currentAnimationFrame = 0;

            var priorActionData = Model.Actions[priorAction];
            var currentActionData = Model.Actions[CurrentAction];

            if (priorAnimationFrame >= priorActionData.NumAnimationKeys) priorAnimationFrame = 0;
            if (currentAnimationFrame >= currentActionData.NumAnimationKeys) currentAnimationFrame = 0;

            GenerateBoneMatrix(priorAction, CurrentAction, priorAnimationFrame, currentAnimationFrame, interpolationFactor);
        }

        private void GenerateBoneMatrix(int priorAction, int currentAction, int priorAnimationFrame, int currentAnimationFrame, float interpolationFactor)
        {
            BoneTransform ??= new Matrix[Model.Bones.Length];

            var priorActionData = Model.Actions[priorAction];
            var currentActionData = Model.Actions[currentAction];
            var changed = false;

            for (int i = 0; i < Model.Bones.Length; i++)
            {
                var bone = Model.Bones[i];

                if (bone == BMDTextureBone.Dummy)
                    continue;

                var bm1 = bone.Matrixes[priorAction];
                var bm2 = bone.Matrixes[CurrentAction];

                var q1 = bm1.Quaternion[priorAnimationFrame];
                var q2 = bm2.Quaternion[currentAnimationFrame];

                var boneQuaternion = q1 != q2
                    ? Quaternion.Slerp(q1, q2, interpolationFactor)
                    : q1;

                Matrix matrix = Matrix.CreateFromQuaternion(boneQuaternion);

                Vector3 position1 = bm1.Position[priorAnimationFrame];
                Vector3 position2 = bm2.Position[currentAnimationFrame];
                Vector3 interpolatedPosition = Vector3.Lerp(position1, position2, interpolationFactor);

                if (i == 0 && (priorActionData.LockPositions || currentActionData.LockPositions))
                {
                    matrix.M41 = bm2.Position[0].X;
                    matrix.M42 = bm2.Position[0].Y;
                    matrix.M43 = position1.Z * (1 - interpolationFactor) + position2.Z * interpolationFactor + BodyHeight;
                }
                else
                {
                    matrix.Translation = interpolatedPosition;
                }

                Matrix newMatrix;

                if (bone.Parent != -1)
                    newMatrix = matrix * BoneTransform[bone.Parent];
                else
                    newMatrix = matrix;

                if (!changed && BoneTransform[i] != newMatrix)
                    changed = true;

                BoneTransform[i] = newMatrix;
            }

            if (changed)
            {
                InvalidateBuffers();
                UpdateBoundings();
            }
        }

        public void InvalidateBuffers()
        {
            _invalidatedBuffers = true;

            for (var i = 0; i < Children.Count; i++)
                if (Children[i] is ModelObject modelObject && modelObject.LinkParent)
                    modelObject.InvalidateBuffers();
        }

        protected override void RecalculateWorldPosition()
        {
            var localMatrix = Matrix.CreateScale(Scale)
                                * Matrix.CreateFromQuaternion(MathUtils.AngleQuaternion(Angle))
                                * Matrix.CreateTranslation(Position);

            if (Parent != null)
            {
                var worldMatrix = localMatrix * Parent.WorldPosition;

                if (WorldPosition != worldMatrix)
                {
                    WorldPosition = worldMatrix;
                }
            }
            else if (WorldPosition != localMatrix)
            {
                WorldPosition = localMatrix;
            }
        }
    }
}
