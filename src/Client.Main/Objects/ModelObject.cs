using Client.Data.BMD;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class ModelObject : WorldObject
    {
        private VertexBuffer[] _boneVertexBuffers;
        private IndexBuffer[] _boneIndexBuffers;
        private Texture2D[] _boneTextures;
        private TextureScript[] _scriptTextures;
        private TextureData[] _dataTextures;

        private bool _renderShadow = false;
        private int _priorAction = 0;
        private bool _invalidatedBuffers = true;
        private float _blendMeshLight = 1f;
        private float _previousFrame = 0;
        public float ShadowOpacity { get; set; } = 1f;
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
        public Vector3 ShadowDirection { get; set; } = new Vector3(-1, -1, -1);
        public Color ShadowColor { get; set; } = Color.Black;
        public Vector3 ShadowOffset { get; set; } = new Vector3(0.05f, 0.01f, 0.1f);
        public float ShadowScale { get; set; } = 0.85f;
        public float ShadowRotationX { get; set; } = -20f;

        private Dictionary<string, float> _shadowOpacityCache = new Dictionary<string, float>();

        public float BlendMeshLight
        {
            get => _blendMeshLight;
            set
            {
                _blendMeshLight = value;
                _invalidatedBuffers = true;
            }
        }
        public bool RenderShadow { get => _renderShadow; set { _renderShadow = value; OnRenderShadowChanged(); } }
        public float AnimationSpeed { get; set; } = 4f;

        public ModelObject()
        {
            MatrixChanged += (_s, _e) => UpdateWorldPosition();
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            if (Model == null)
            {
                Debug.WriteLine($"Model is not assigned for {ObjectName} -> Type: {Type}");
                Status = Models.GameControlStatus.Error;
                return;
            }

            UpdateWorldPosition();
            GenerateBoneMatrix(0, 0, 0, 0);
        }

        public override void Update(GameTime gameTime)
        {
            if (World == null)
                return;

            base.Update(gameTime);

            if (!Visible) return;

            Animation(gameTime);
            SetDynamicBuffers();
        }

        public override void Draw(GameTime gameTime)
        {
            if (World == null)
                return;

            if (!Visible || _boneIndexBuffers == null) return;

            GraphicsManager.Instance.AlphaTestEffect3D.View = Camera.Instance.View;
            GraphicsManager.Instance.AlphaTestEffect3D.Projection = Camera.Instance.Projection;
            GraphicsManager.Instance.AlphaTestEffect3D.World = WorldPosition;

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            DrawModel(false);
            base.Draw(gameTime);
        }

        public virtual void DrawModel(bool isAfterDraw)
        {
            if (Model == null) return;

            int meshCount = Model.Meshes.Length;
            for (int i = 0; i < meshCount; i++)
            {
                if (_dataTextures[i] == null) continue;
                bool isRGBA = _dataTextures[i].Components == 4;
                bool isBlendMesh = IsBlendMesh(i);
                bool draw = (isAfterDraw ? isRGBA || isBlendMesh : !isRGBA && !isBlendMesh);

                if (!isAfterDraw && RenderShadow)
                {
                    DrawShadowMesh(i, Camera.Instance.View, Camera.Instance.Projection, new GameTime());
                }

                if (!isAfterDraw && !isBlendMesh && IsMouseHover)
                    DrawMeshHighlight(i);

                if (draw)
                {
                    GraphicsDevice.DepthStencilState = isAfterDraw
                        ? MuGame.Instance.DisableDepthMask
                        : DepthStencilState.Default;
                    DrawMesh(i);
                }
            }
        }

        private bool IsHiddenMesh(int mesh)
        {
            if (HiddenMesh == mesh || HiddenMesh == -2)
                return true;

            var script = _scriptTextures[mesh];

            if (script != null && script.HiddenMesh)
                return true;

            return false;
        }

        private bool IsBlendMesh(int mesh)
        {
            if (BlendMesh == mesh || BlendMesh == -2)
                return true;

            var script = _scriptTextures[mesh];

            if (script != null && script.Bright)
                return true;

            return false;
        }

        public virtual void DrawMesh(int mesh)
        {
            if (IsHiddenMesh(mesh) || _boneVertexBuffers == null)
                return;

            Texture2D texture = _boneTextures[mesh];
            VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
            IndexBuffer indexBuffer = _boneIndexBuffers[mesh];
            int primitiveCount = indexBuffer.IndexCount / 3;

            GraphicsManager.Instance.AlphaTestEffect3D.Texture = texture;
            GraphicsDevice.BlendState = IsBlendMesh(mesh) ? BlendMeshState : BlendState;
            GraphicsManager.Instance.AlphaTestEffect3D.Alpha = TotalAlpha;

            foreach (EffectPass pass in GraphicsManager.Instance.AlphaTestEffect3D.CurrentTechnique.Passes)
            {
                pass.Apply();

                GraphicsDevice.SetVertexBuffer(vertexBuffer);
                GraphicsDevice.Indices = indexBuffer;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
            }
        }

        public virtual void DrawMeshHighlight(int mesh)
        {
            if (IsHiddenMesh(mesh) || _boneVertexBuffers == null)
                return;

            Texture2D texture = _boneTextures[mesh];
            VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
            IndexBuffer indexBuffer = _boneIndexBuffers[mesh];
            int primitiveCount = indexBuffer.IndexCount / 3;

            float scaleFactor = Scale + 0.02f;

            Matrix highlightMatrix = Matrix.CreateScale(scaleFactor) * WorldPosition;

            GraphicsManager.Instance.AlphaTestEffect3D.World = highlightMatrix;
            GraphicsManager.Instance.AlphaTestEffect3D.DiffuseColor = new Vector3(0, 1, 0);

            GraphicsManager.Instance.AlphaTestEffect3D.Alpha = TotalAlpha;

            GraphicsDevice.DepthStencilState = MuGame.Instance.DisableDepthMask;
            GraphicsDevice.BlendState = BlendState.Additive;

            foreach (EffectPass pass in GraphicsManager.Instance.AlphaTestEffect3D.CurrentTechnique.Passes)
            {
                pass.Apply();

                GraphicsDevice.SetVertexBuffer(vertexBuffer);
                GraphicsDevice.Indices = indexBuffer;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
            }

            GraphicsManager.Instance.AlphaTestEffect3D.World = WorldPosition;
            GraphicsManager.Instance.AlphaTestEffect3D.DiffuseColor = Vector3.One;
        }

        private bool ValidateWorldMatrix(Matrix matrix)
        {
            for (int i = 0; i < 16; i++)
            {
                if (float.IsNaN(matrix[i]))
                    return false;
            }
            return true;
        }

        public virtual void DrawShadowMesh(int mesh, Matrix view, Matrix projection, GameTime gameTime)
        {
            try
            {
                if (IsHiddenMesh(mesh) || _boneVertexBuffers == null)
                    return;

                if (!ValidateWorldMatrix(WorldPosition))
                {
                    Debug.WriteLine("Invalid WorldPosition matrix detected - skipping shadow rendering");
                    return;
                }

                VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
                IndexBuffer indexBuffer = _boneIndexBuffers[mesh];

                if (vertexBuffer == null || indexBuffer == null)
                    return;

                var customBlendState = new BlendState
                {
                    ColorSourceBlend = Blend.SourceAlpha,
                    ColorDestinationBlend = Blend.InverseSourceAlpha,
                    AlphaSourceBlend = Blend.One,
                    AlphaDestinationBlend = Blend.One,
                    ColorBlendFunction = BlendFunction.Add,
                    AlphaBlendFunction = BlendFunction.Max
                };

                int primitiveCount = indexBuffer.IndexCount / 3;

                Matrix shadowWorld;

                float heightAboveTerrain = 0f;
                try
                {
                    Matrix originalWorld = WorldPosition;
                    Vector3 scale, translation;
                    Quaternion rotation;

                    originalWorld.Decompose(out scale, out rotation, out translation);

                    scale = new Vector3(
                        float.IsNaN(scale.X) ? 1.0f : scale.X,
                        float.IsNaN(scale.Y) ? 1.0f : scale.Y,
                        float.IsNaN(scale.Z) ? 1.0f : scale.Z
                    );

                    float modelRotationY = this.TotalAngle.Z;
                    Vector3 position = originalWorld.Translation;

                    float terrainHeight = World.Terrain.RequestTerrainHeight(position.X, position.Y);

                    float shadowHeightOffset = originalWorld.Translation.Z - terrainHeight + 5f;
                    Vector3 shadowPosition = new Vector3(
                        position.X,
                        position.Y,
                        terrainHeight + shadowHeightOffset
                    );

                    heightAboveTerrain = WorldPosition.Translation.Z - terrainHeight;
                    float sampleDistance = heightAboveTerrain + 60f;
                    float angleRad = MathHelper.ToRadians(-45);

                    float offsetX = sampleDistance * (float)Math.Cos(angleRad);
                    float offsetY = sampleDistance * (float)Math.Sin(angleRad);

                    float heightX1 = World.Terrain.RequestTerrainHeight(position.X - offsetX, position.Y - offsetY);
                    float heightX2 = World.Terrain.RequestTerrainHeight(position.X + offsetX, position.Y + offsetY);
                    float heightZ1 = World.Terrain.RequestTerrainHeight(position.X - offsetY, position.Y + offsetX);
                    float heightZ2 = World.Terrain.RequestTerrainHeight(position.X + offsetY, position.Y - offsetX);

                    float terrainSlopeX = (float)Math.Atan2(heightX2 - heightX1, sampleDistance * 0.4f);
                    float terrainSlopeZ = (float)Math.Atan2(heightZ2 - heightZ1, sampleDistance * 0.4f);

                    Matrix rotationMatrix = Matrix.CreateRotationZ(modelRotationY - MathHelper.ToRadians(45));

                    shadowWorld = Matrix.Identity;
                    shadowWorld *= rotationMatrix;
                    shadowWorld *= Matrix.CreateScale(1.0f + (Math.Min(0, terrainSlopeX / 2)), 0.01f, 1.0f + (Math.Min(0, terrainSlopeX / 2)));
                    shadowWorld *= Matrix.CreateRotationX(Math.Max(-MathHelper.PiOver2, -MathHelper.PiOver2 - terrainSlopeX));
                    shadowWorld *= Matrix.CreateRotationZ(MathHelper.ToRadians(45));
                    shadowWorld *= Matrix.CreateTranslation(shadowPosition);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating shadow matrix: {ex.Message}");
                    return;
                }

                var previousBlendState = GraphicsDevice.BlendState;
                var previousDepthStencilState = GraphicsDevice.DepthStencilState;

                try
                {
                    GraphicsDevice.BlendState = customBlendState;
                    GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

                    var effect = GraphicsManager.Instance.ShadowEffect;

                    Matrix worldViewProjection = shadowWorld * view * projection;

                    effect.Parameters["WorldViewProj"].SetValue(worldViewProjection);

                    float maxShadowHeight = 100f;
                    float shadowIntensity = Math.Max(0, 1 - (heightAboveTerrain / maxShadowHeight));

                    float baseOpacity = 0.3f;
                    float finalOpacity = Math.Min(baseOpacity, shadowIntensity * 0.4f);

                    string modelId = this.GetType().Name.Contains("Player") ? "Player" : this.GetType().Name;

                    if (!_shadowOpacityCache.ContainsKey(modelId))
                    {
                        baseOpacity = 0.3f;
                        shadowIntensity = Math.Max(0, 1 - (heightAboveTerrain / maxShadowHeight));
                        finalOpacity = Math.Min(baseOpacity, shadowIntensity * 0.6f);

                        // Add to cache with "Player" key if it's a player model
                        _shadowOpacityCache[modelId] = finalOpacity;
                    }

                    effect.Parameters["ShadowOpacity"].SetValue(_shadowOpacityCache[modelId]);
                    effect.Parameters["HeightAboveTerrain"].SetValue(heightAboveTerrain);

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.SetVertexBuffer(vertexBuffer);
                        GraphicsDevice.Indices = indexBuffer;
                        GraphicsDevice.DrawIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            0,
                            0,
                            primitiveCount
                        );
                    }
                }
                finally
                {
                    GraphicsDevice.BlendState = previousBlendState;
                    GraphicsDevice.DepthStencilState = previousDepthStencilState;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DrawShadowMesh: {ex.Message}");
            }
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible) return;

            GraphicsManager.Instance.AlphaTestEffect3D.View = Camera.Instance.View;
            GraphicsManager.Instance.AlphaTestEffect3D.Projection = Camera.Instance.Projection;
            GraphicsManager.Instance.AlphaTestEffect3D.World = WorldPosition;

            DrawModel(true);

            base.DrawAfter(gameTime);
        }

        public override void Dispose()
        {
            base.Dispose();

            Model = null;
            BoneTransform = null;
            _invalidatedBuffers = true;
        }

        private void OnRenderShadowChanged()
        {
            foreach (var obj in Children)
            {
                if (obj is ModelObject modelObj && modelObj.LinkParent)
                    modelObj.RenderShadow = RenderShadow;
            }
        }

        private void UpdateWorldPosition()
        {
            _invalidatedBuffers = true;
        }

        private void UpdateBoundings()
        {
            if (Model == null) return;
            if (Model.Meshes.Length == 0) return;

            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);

            Matrix[] boneTransforms = BoneTransform; // Cache BoneTransform

            foreach (var mesh in Model.Meshes)
            {
                foreach (var vertex in mesh.Vertices)
                {
                    int boneIndex = vertex.Node;

                    if (boneIndex < 0 || boneIndex >= boneTransforms.Length)
                        continue;

                    Matrix boneMatrix = boneTransforms[boneIndex];
                    Vector3 transformedPosition = Vector3.Transform(vertex.Position, boneMatrix);
                    min = Vector3.Min(min, transformedPosition);
                    max = Vector3.Max(max, transformedPosition);
                }
            }

            BoundingBoxLocal = new BoundingBox(min, max);
        }

        private void Animation(GameTime gameTime)
        {
            if (LinkParent || Model == null || Model.Actions.Length <= 0) return;

            var currentActionData = Model.Actions[CurrentAction];

            if (currentActionData.NumAnimationKeys <= 1)
            {
                if (_priorAction != CurrentAction || BoneTransform == null)
                {
                    GenerateBoneMatrix(CurrentAction, 0, 0, 0);
                    _priorAction = CurrentAction;
                }
                return;
            }

            float currentFrame = (float)(gameTime.TotalGameTime.TotalSeconds * AnimationSpeed);
            int totalFrames = currentActionData.NumAnimationKeys - 1;
            currentFrame %= totalFrames;

            Animation(currentFrame);

            _priorAction = CurrentAction;
        }

        private void Animation(float currentFrame)
        {
            if (LinkParent || Model == null || Model.Actions.Length <= 0) return;

            if (CurrentAction >= Model.Actions.Length)
                CurrentAction = 0;

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
            bool changed = false;

            Matrix[] boneTransforms = BoneTransform; // Cache BoneTransform
            BMDTextureBone[] modelBones = Model.Bones; // Cache Model.Bones

            for (int i = 0; i < modelBones.Length; i++)
            {
                var bone = modelBones[i];

                if (bone == BMDTextureBone.Dummy)
                    continue;

                var bm = bone.Matrixes[currentAction];

                Quaternion q1 = bm.Quaternion[currentAnimationFrame];
                Quaternion q2 = bm.Quaternion[nextAnimationFrame];

                Quaternion boneQuaternion = Quaternion.Slerp(q1, q2, interpolationFactor);
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

                Matrix newMatrix = bone.Parent != -1
                    ? matrix * boneTransforms[bone.Parent]
                    : matrix;

                if (!changed && boneTransforms[i] != newMatrix)
                    changed = true;

                boneTransforms[i] = newMatrix;
            }

            if (changed)
            {
                InvalidateBuffers();
                UpdateBoundings();
            }
        }

        private void SetDynamicBuffers()
        {
            if (!_invalidatedBuffers || Model == null)
                return;

            int meshCount = Model.Meshes.Length; // Cache mesh count

            _boneVertexBuffers ??= new VertexBuffer[meshCount];
            _boneIndexBuffers ??= new IndexBuffer[meshCount];
            _boneTextures ??= new Texture2D[meshCount];
            _scriptTextures ??= new TextureScript[meshCount];
            _dataTextures ??= new TextureData[meshCount];

            for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
            {
                var mesh = Model.Meshes[meshIndex];
                // Resolve the body light conflict

                Vector3 bodyLight;

                if (LightEnabled && World.Terrain != null)
                {
                    Vector3 terrainLight = World.Terrain.RequestTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y);
                    terrainLight += Light;
                    bodyLight = terrainLight;
                }
                else
                {
                    bodyLight = Light;
                }

                bodyLight = IsBlendMesh(meshIndex)
                    ? bodyLight * BlendMeshLight
                    : bodyLight * TotalAlpha;

                _boneVertexBuffers[meshIndex]?.Dispose();
                _boneIndexBuffers[meshIndex]?.Dispose();

                Matrix[] bones = (LinkParent && Parent is ModelObject parentModel) ? parentModel.BoneTransform : BoneTransform;

                // Use the updated color calculation method from the improvements branch
                byte r = Color.R;
                byte g = Color.G;
                byte b = Color.B;

                // Calculate color components considering lighting and clamping values
                byte bodyR = (byte)Math.Min(r * bodyLight.X, 255);
                byte bodyG = (byte)Math.Min(g * bodyLight.Y, 255);
                byte bodyB = (byte)Math.Min(b * bodyLight.Z, 255);

                Color bodyColor = new Color(bodyR, bodyG, bodyB);

                BMDLoader.Instance.GetModelBuffers(Model, meshIndex, bodyColor, bones, out var vertexBuffer, out var indexBuffer);

                _boneVertexBuffers[meshIndex] = vertexBuffer;
                _boneIndexBuffers[meshIndex] = indexBuffer;

                if (_boneTextures[meshIndex] == null)
                {
                    string texturePath = BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);
                    _boneTextures[meshIndex] = TextureLoader.Instance.GetTexture2D(texturePath);
                    _scriptTextures[meshIndex] = TextureLoader.Instance.GetScript(texturePath);
                    _dataTextures[meshIndex] = TextureLoader.Instance.Get(texturePath);
                }
            }

            _invalidatedBuffers = false;
        }

        protected void InvalidateBuffers()
        {
            _invalidatedBuffers = true;

            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is ModelObject modelObject && modelObject.LinkParent)
                {
                    modelObject.InvalidateBuffers();
                }
            }
        }

        protected override void RecalculateWorldPosition()
        {
            Matrix localMatrix = Matrix.CreateScale(Scale) *
                                 Matrix.CreateFromQuaternion(MathUtils.AngleQuaternion(Angle)) *
                                 Matrix.CreateTranslation(Position);

            if (Parent != null)
            {
                Matrix worldMatrix = localMatrix * Parent.WorldPosition;

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
