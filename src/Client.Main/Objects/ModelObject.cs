using Client.Data;
using Client.Data.BMD;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NAudio.CoreAudioApi;
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
        private Effect _effect;
        private bool _invalidatedBuffers = true;
        private float _blendMeshLight = 1f;
        private float _previousFrame = 0;
        public Color Color { get; set; } = Color.White;
        protected Matrix[] BoneTransform { get; set; }
        public int CurrentAction { get; set; }
        public virtual int OriginBoneIndex => 0;
        public BMD Model { get; set; }
        public Matrix BodyOrigin => OriginBoneIndex < BoneTransform.Length ? BoneTransform[OriginBoneIndex] : Matrix.Identity;
        public float BodyHeight { get; private set; }
        public int HiddenMesh { get; set; } = -1;
        public int BlendMesh { get; set; } = -1;
        public BlendState BlendMeshState { get; set; } = BlendState.Additive;
        public float BlendMeshLight { get => _blendMeshLight; set { _blendMeshLight = value; _invalidatedBuffers = true; } }
        public bool RenderShadow { get; set; }
        public float AnimationSpeed { get; set; } = 4f;

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
                // _effect = MuGame.Instance.AlphaRGBEffect.Clone();
            }

            UpdateWorldPosition();
            GenerateBoneMatrix(0, 0, 0, 0);

            await base.Load();
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Ready || OutOfView) return;

            if (_effect != null)
            {
                if (_effect is IEffectMatrices effectMatrices)
                {
                    effectMatrices.View = Camera.Instance.View;
                    effectMatrices.Projection = Camera.Instance.Projection;
                }
                else
                {
                    _effect.Parameters["View"].SetValue(Camera.Instance.View);
                    _effect.Parameters["Projection"].SetValue(Camera.Instance.Projection);
                }
            }

            Animation(gameTime);
            SetDynamicBuffers();
        }
        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _boneIndexBuffers == null) return;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            DrawModel(false);
            base.Draw(gameTime);
        }

        public virtual void DrawModel(bool isAfterDraw)
        {
            for (var i = 0; i < Model.Meshes.Length; i++)
            {
                if (_dataTextures[i] == null) continue;
                var isRGBA = _dataTextures[i].Components == 4;
                var isBlendMesh = BlendMesh == i;
                var draw = isAfterDraw
                    ? isRGBA || isBlendMesh
                    : !isRGBA && !isBlendMesh;

                if (!isAfterDraw && RenderShadow)
                {
                    GraphicsDevice.DepthStencilState = MuGame.Instance.DisableDepthMask;
                    DrawShadowMesh(i);
                }

                if (!draw) continue;

                if (isAfterDraw)
                {
                    GraphicsDevice.DepthStencilState = MuGame.Instance.DisableDepthMask;
                }
                else
                {
                    GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                }

                DrawMesh(i);
            }
        }

        public virtual void DrawMesh(int mesh)
        {
            if (HiddenMesh == mesh)
                return;

            if (_boneVertexBuffers == null)
                return;

            var texture = _boneTextures[mesh];

            var vertexBuffer = _boneVertexBuffers[mesh];
            var indexBuffer = _boneIndexBuffers[mesh];
            var primitiveCount = indexBuffer.IndexCount / 3;

            _effect.Parameters["Texture"].SetValue(texture);

            GraphicsDevice.BlendState = BlendMesh == mesh ? BlendMeshState : BlendState;
            if (_effect is AlphaTestEffect alphaTestEffect)
                alphaTestEffect.Alpha = TotalAlpha;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                GraphicsDevice.SetVertexBuffer(vertexBuffer);
                GraphicsDevice.Indices = indexBuffer;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
            }
        }

        public virtual void DrawShadowMesh(int mesh)
        {
            if (HiddenMesh == mesh)
                return;

            if (_boneVertexBuffers == null)
                return;

            var texture = _boneTextures[mesh];
            var vertexBuffer = _boneVertexBuffers[mesh];
            var indexBuffer = _boneIndexBuffers[mesh];
            var primitiveCount = indexBuffer.IndexCount / 3;

            _effect.Parameters["Texture"].SetValue(texture);

            var shadowVertices = new VertexPositionColorNormalTexture[vertexBuffer.VertexCount];

            GraphicsDevice.BlendState = BlendState.AlphaBlend;

            var effect = (AlphaTestEffect)_effect;
            vertexBuffer.GetData(shadowVertices);
            var originalWorld = effect.World;
            var originalView = effect.View;
            var originalProjection = effect.Projection;

            for (int i = 0; i < shadowVertices.Length; i++)
            {
                shadowVertices[i].Color = new Color(0, 0, 0, 128);
            }

            effect.Projection = originalProjection;

            var world = effect.World;

            world = Matrix.CreateRotationX(MathHelper.ToRadians(-45)) * world;
            world = Matrix.CreateScale(0.8f, 1.0f, 0.8f) * world;

            Vector3 lightDirection = new(-1, 0, 1);

            Vector3 shadowOffset = new(0.05f, 0, 0.1f);
            world.Translation += lightDirection * 0.3f + shadowOffset;

            effect.World = world;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, shadowVertices, 0, primitiveCount);
            }

            effect.World = originalWorld;
            effect.View = originalView;
            effect.Projection = originalProjection;
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

            GC.SuppressFinalize(this);
        }

        private void UpdateWorldPosition()
        {
            _invalidatedBuffers = true;

            if (_effect != null)
            {
                if (_effect is IEffectMatrices effectMatrices)
                {
                    effectMatrices.World = WorldPosition;
                }
                else
                {
                    _effect.Parameters["World"].SetValue(WorldPosition);
                }
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

            var currentAction = Model.Actions[CurrentAction];

            if (currentAction.NumAnimationKeys <= 1)
            {
                if (_priorAction != CurrentAction || BoneTransform == null)
                {
                    GenerateBoneMatrix(CurrentAction, 0, 0, 0);
                    _priorAction = CurrentAction;
                }
                return;
            }

            float currentFrame = (float)(gameTime.TotalGameTime.TotalSeconds * AnimationSpeed);

            int totalFrames = currentAction.NumAnimationKeys - 1;
            currentFrame %= totalFrames;

            Animation(currentFrame);

            _priorAction = CurrentAction;
        }

        private void Animation(float currentFrame)
        {
            if (LinkParent || Model == null || Model.Actions.Length <= 0) return;

            if (CurrentAction >= Model.Actions.Length) CurrentAction = 0;

            int currentAnimationFrame = (int)Math.Floor(currentFrame);
            float interpolationFactor = currentFrame - currentAnimationFrame;

            var currentActionData = Model.Actions[CurrentAction];
            int totalFrames = currentActionData.NumAnimationKeys - 1;
            int nextAnimationFrame = (currentAnimationFrame + 1) % totalFrames;

            GenerateBoneMatrix(CurrentAction, currentAnimationFrame, nextAnimationFrame, interpolationFactor);
        }


        private void GenerateBoneMatrix(int currentAction, int currentAnimationFrame, int nextAnimationFrame, float interpolationFactor)
        {
            BoneTransform ??= new Matrix[Model.Bones.Length];

            var currentActionData = Model.Actions[currentAction];
            var changed = false;

            for (int i = 0; i < Model.Bones.Length; i++)
            {
                var bone = Model.Bones[i];

                if (bone == BMDTextureBone.Dummy)
                    continue;

                var bm = bone.Matrixes[currentAction];

                var q1 = bm.Quaternion[currentAnimationFrame];
                var q2 = bm.Quaternion[nextAnimationFrame];

                var boneQuaternion = Quaternion.Slerp(q1, q2, interpolationFactor);

                Matrix matrix = Matrix.CreateFromQuaternion(boneQuaternion);

                Vector3 position1 = bm.Position[currentAnimationFrame];
                Vector3 position2 = bm.Position[nextAnimationFrame];
                Vector3 interpolatedPosition = Vector3.Lerp(position1, position2, interpolationFactor);

                if (i == 0 && currentActionData.LockPositions)
                {
                    matrix.M41 = bm.Position[0].X;
                    matrix.M42 = bm.Position[0].Y;
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
                    bodyLight = bodyLight * TotalAlpha;

                _boneVertexBuffers[meshIndex]?.Dispose();
                _boneIndexBuffers[meshIndex]?.Dispose();

                var bones = LinkParent && Parent is ModelObject parentModel ? parentModel.BoneTransform : BoneTransform;

                var bodyColor = new Color(Color.R * bodyLight.X, Color.G * bodyLight.Y, Color.B * bodyLight.Y);

                BMDLoader.Instance.GetModelBuffers(Model, meshIndex, bodyColor, bones, out var vertexBuffer, out var indexBuffer);

                _boneVertexBuffers[meshIndex] = vertexBuffer;
                _boneIndexBuffers[meshIndex] = indexBuffer;


                if (_boneTextures[meshIndex] == null)
                {
                    var texturePath = BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);
                    _boneTextures[meshIndex] = TextureLoader.Instance.GetTexture2D(texturePath);
                    _scriptTextures[meshIndex] = TextureLoader.Instance.GetScript(texturePath);
                    _dataTextures[meshIndex] = TextureLoader.Instance.Get(texturePath);

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

        protected void InvalidateBuffers()
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
