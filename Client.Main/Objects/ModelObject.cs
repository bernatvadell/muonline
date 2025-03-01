using Client.Data.BMD;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using System;

using System.Diagnostics;
using System.Linq;
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
        public int ParentBoneLink { get; set; } = -1;
        public BMD Model { get; set; }

        public Matrix ParentBodyOrigin => ParentBoneLink >= 0
            && Parent != null
            && Parent is ModelObject modelObject
            && modelObject.BoneTransform != null
            && ParentBoneLink < modelObject.BoneTransform.Length
                ? modelObject.BoneTransform[ParentBoneLink]
                : Matrix.Identity;

        public float BodyHeight { get; private set; }
        public int HiddenMesh { get; set; } = -1;
        public int BlendMesh { get; set; } = -1;
        public BlendState BlendMeshState { get; set; } = BlendState.Additive;

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

            int meshCount = Model.Meshes.Length;
            _boneVertexBuffers = new VertexBuffer[meshCount];
            _boneIndexBuffers = new IndexBuffer[meshCount];
            _boneTextures = new Texture2D[meshCount];
            _scriptTextures = new TextureScript[meshCount];
            _dataTextures = new TextureData[meshCount];

            UpdateWorldPosition();

            for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
            {
                var mesh = Model.Meshes[meshIndex];
                string texturePath = BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);
                _boneTextures[meshIndex] = TextureLoader.Instance.GetTexture2D(texturePath);
                _scriptTextures[meshIndex] = TextureLoader.Instance.GetScript(texturePath);
                _dataTextures[meshIndex] = TextureLoader.Instance.Get(texturePath);
            }

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

            DrawModel(false);
            base.Draw(gameTime);
        }

        public virtual void DrawModel(bool isAfterDraw)
        {
            if (Model == null || _boneVertexBuffers == null || _scriptTextures == null || _dataTextures == null) return;

            try
            {
                var validMeshes = Enumerable.Range(0, Model.Meshes.Length)
                    .Where(idx =>
                        idx < _dataTextures.Length &&
                        _dataTextures[idx] != null &&
                        !IsHiddenMesh(idx))
                    .Select(idx => new
                    {
                        Index = idx,
                        IsBlend = IsBlendMesh(idx),
                        IsRGBA = _dataTextures[idx].Components == 4
                    })
                    .ToList();

                foreach (var meshData in validMeshes.Where(m => !m.IsBlend))
                {
                    int i = meshData.Index;
                    bool draw = isAfterDraw ? meshData.IsRGBA : !meshData.IsRGBA;

                    if (!isAfterDraw && RenderShadow)
                    {
                        DrawShadowMesh(i, Camera.Instance.View, Camera.Instance.Projection, MuGame.Instance.GameTime);
                    }

                    if (!isAfterDraw && IsMouseHover)
                    {
                        DrawMeshHighlight(i);
                    }

                    if (draw)
                    {
                        DrawMesh(i);
                    }
                }

                foreach (var meshData in validMeshes
                    .Where(m => m.IsBlend)
                    .OrderByDescending(m => Vector3.Distance(Camera.Instance.Position, WorldPosition.Translation)))
                {
                    int i = meshData.Index;
                    bool draw = isAfterDraw ? meshData.IsRGBA || true : false;

                    if (!isAfterDraw && RenderShadow)
                    {
                        DrawShadowMesh(i, Camera.Instance.View, Camera.Instance.Projection, MuGame.Instance.GameTime);
                    }

                    if (!isAfterDraw && IsMouseHover)
                    {
                        DrawMeshHighlight(i);
                    }

                    if (draw)
                    {
                        DrawMesh(i);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DrawModel: {ex.Message}");
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
            try
            {
                if (_boneVertexBuffers == null || mesh < 0 || mesh >= _boneVertexBuffers.Length ||
                    _boneIndexBuffers == null || mesh >= _boneIndexBuffers.Length ||
                    _boneTextures == null || mesh >= _boneTextures.Length ||
                    IsHiddenMesh(mesh))
                    return;

                Texture2D texture = _boneTextures[mesh];
                VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
                IndexBuffer indexBuffer = _boneIndexBuffers[mesh];

                if (texture == null || vertexBuffer == null || indexBuffer == null)
                    return;

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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DrawMesh: {ex.Message}");
            }
        }

        public virtual void DrawMeshHighlight(int mesh)
        {
            if (IsHiddenMesh(mesh) || _boneVertexBuffers == null)
                return;

            VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
            IndexBuffer indexBuffer = _boneIndexBuffers[mesh];
            int primitiveCount = indexBuffer.IndexCount / 3;

            // Define a small highlight offset
            float scaleHighlight = 0.015f;
            // Use fixed multiplier independent of the object's Scale, since WorldPosition already includes it
            float scaleFactor = 1f + scaleHighlight;

            // Create the highlight transformation matrix
            var highlightMatrix = Matrix.CreateScale(scaleFactor)
                * Matrix.CreateTranslation(-scaleHighlight, -scaleHighlight, -scaleHighlight)
                * WorldPosition;

            // Save previous graphics states
            var previousDepthState = GraphicsDevice.DepthStencilState;
            var previousBlendState = GraphicsDevice.BlendState;

            GraphicsManager.Instance.AlphaTestEffect3D.World = highlightMatrix;
            GraphicsManager.Instance.AlphaTestEffect3D.Texture = _boneTextures[mesh];

            // Set highlight color: red for Monster objects, green for others
            Vector3 highlightColor = new Vector3(0, 1, 0); // default green
            if (this is Monsters.MonsterObject)
            {
                highlightColor = new Vector3(1, 0, 0); // red for monster
            }
            GraphicsManager.Instance.AlphaTestEffect3D.DiffuseColor = highlightColor;
            GraphicsManager.Instance.AlphaTestEffect3D.Alpha = 1f;

            // Configure depth and blend states for drawing the highlight
            GraphicsDevice.DepthStencilState = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = false
            };
            GraphicsDevice.BlendState = BlendState.Additive;

            // Draw the mesh highlight
            foreach (EffectPass pass in GraphicsManager.Instance.AlphaTestEffect3D.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.SetVertexBuffer(vertexBuffer);
                GraphicsDevice.Indices = indexBuffer;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
            }

            // Restore previous graphics states
            GraphicsDevice.DepthStencilState = previousDepthState;
            GraphicsDevice.BlendState = previousBlendState;

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

                var originalWorld = GraphicsManager.Instance.BasicEffect3D.World;
                var originalProjection = GraphicsManager.Instance.BasicEffect3D.Projection;
                var originalView = GraphicsManager.Instance.BasicEffect3D.View;

                int primitiveCount = indexBuffer.IndexCount / 3;

                Matrix shadowWorld;
                const float shadowBias = 0.1f; // Constant bias to offset the shadow and prevent z-fighting

                float heightAboveTerrain = 0f;
                try
                {
                    Vector3 position = WorldPosition.Translation;

                    float terrainHeight = World.Terrain.RequestTerrainHeight(position.X, position.Y);
                    terrainHeight += terrainHeight * 0.5f;

                    float shadowHeightOffset = position.Z;
                    float relativeHeightOverTerrain = shadowHeightOffset - terrainHeight;

                    // Calculate shadow position
                    Vector3 shadowPosition = new Vector3(
                        position.X - (relativeHeightOverTerrain / 2),
                        position.Y - (relativeHeightOverTerrain / 4.5f),
                        terrainHeight + 1f
                    );

                    heightAboveTerrain = position.Z - terrainHeight;
                    float sampleDistance = heightAboveTerrain + 10f;
                    float angleRad = MathHelper.ToRadians(45);

                    float offsetX = sampleDistance * (float)Math.Cos(angleRad);
                    float offsetY = sampleDistance * (float)Math.Sin(angleRad);

                    float heightX1 = World.Terrain.RequestTerrainHeight(position.X - offsetX, position.Y - offsetY);
                    float heightX2 = World.Terrain.RequestTerrainHeight(position.X + offsetX, position.Y + offsetY);
                    float heightZ1 = World.Terrain.RequestTerrainHeight(position.X - offsetY, position.Y + offsetX);
                    float heightZ2 = World.Terrain.RequestTerrainHeight(position.X + offsetY, position.Y - offsetX);

                    float terrainSlopeX = (float)Math.Atan2(heightX2 - heightX1, sampleDistance * 0.4f);
                    float terrainSlopeZ = (float)Math.Atan2(heightZ2 - heightZ1, sampleDistance * 0.4f);

                    float adjustedYaw = TotalAngle.Y + MathHelper.ToRadians(110) - terrainSlopeX / 2;
                    float adjustedPitch = TotalAngle.X + MathHelper.ToRadians(120);
                    float adjustedRoll = TotalAngle.Z + MathHelper.ToRadians(90);

                    Quaternion rotationQuat = Quaternion.CreateFromYawPitchRoll(adjustedYaw, adjustedPitch, adjustedRoll);
                    Matrix rotationMatrix = Matrix.CreateFromQuaternion(rotationQuat);

                    shadowWorld = rotationMatrix
                        * Matrix.CreateScale(1.0f * TotalScale, 0.01f * TotalScale, 1.0f * TotalScale)
                        * Matrix.CreateRotationX(Math.Max(-MathHelper.PiOver2, -MathHelper.PiOver2 - terrainSlopeX))
                        * Matrix.CreateRotationZ(angleRad)
                        // Add a small bias to the shadow position to prevent z-fighting
                        * Matrix.CreateTranslation(shadowPosition + new Vector3(0f, 0f, shadowBias));
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
                    GraphicsDevice.BlendState = Blendings.ShadowBlend;
                    GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

                    var effect = GraphicsManager.Instance.BasicEffect3D;
                    effect.World = shadowWorld;
                    effect.View = view;
                    effect.Projection = projection;
                    effect.Texture = _boneTextures[mesh];
                    effect.DiffuseColor = Vector3.Zero;

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

                    effect.DiffuseColor = Vector3.One;
                }
                finally
                {
                    GraphicsDevice.BlendState = previousBlendState;
                    GraphicsDevice.DepthStencilState = previousDepthStencilState;
                    GraphicsManager.Instance.BasicEffect3D.World = originalWorld;
                    GraphicsManager.Instance.BasicEffect3D.View = originalView;
                    GraphicsManager.Instance.BasicEffect3D.Projection = originalProjection;
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
                if (obj is ModelObject modelObj && modelObj.LinkParentAnimation)
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

                    min.X = Math.Min(min.X, transformedPosition.X);
                    min.Y = Math.Min(min.Y, transformedPosition.Y);
                    min.Z = Math.Min(min.Z, transformedPosition.Z);

                    max.X = Math.Max(max.X, transformedPosition.X);
                    max.Y = Math.Max(max.Y, transformedPosition.Y);
                    max.Z = Math.Max(max.Z, transformedPosition.Z);
                }
            }

            BoundingBoxLocal = new BoundingBox(min, max);
        }

        private void Animation(GameTime gameTime)
        {
            if (LinkParentAnimation || Model == null || Model.Actions.Length <= 0) return;

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
            if (LinkParentAnimation || Model == null || Model.Actions.Length <= 0) return;

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
            if (Model == null || Model.Bones == null) return;

            try
            {
                BoneTransform ??= new Matrix[Model.Bones.Length];

                if (currentAction >= Model.Actions.Length)
                {
                    Debug.WriteLine($"Invalid action index: {currentAction}, max: {Model.Actions.Length - 1}");
                    currentAction = 0;
                }

                var currentActionData = Model.Actions[currentAction];
                bool changed = false;

                Matrix[] boneTransforms = BoneTransform;
                BMDTextureBone[] modelBones = Model.Bones;
                var boneCount = modelBones.Length;

                for (int i = 0; i < boneCount; i++)
                {
                    var bone = modelBones[i];

                    if (bone == BMDTextureBone.Dummy || bone.Matrixes == null ||
                        currentAction >= bone.Matrixes.Length)
                        continue;

                    var bm = bone.Matrixes[currentAction];

                    if (currentAnimationFrame >= bm.Quaternion.Length ||
                        nextAnimationFrame >= bm.Quaternion.Length ||
                        currentAnimationFrame >= bm.Position.Length ||
                        nextAnimationFrame >= bm.Position.Length)
                    {
                        Debug.WriteLine($"Animation frame index out of bounds: current={currentAnimationFrame}, next={nextAnimationFrame}, max={bm.Quaternion.Length - 1}");
                        continue;
                    }

                    Quaternion q1 = bm.Quaternion[currentAnimationFrame];
                    Quaternion q2 = bm.Quaternion[nextAnimationFrame];
                    Quaternion boneQuaternion = Quaternion.Slerp(q1, q2, interpolationFactor);

                    Matrix matrix = Matrix.CreateFromQuaternion(boneQuaternion);

                    Vector3 position1 = bm.Position[currentAnimationFrame];
                    Vector3 position2 = bm.Position[nextAnimationFrame];

                    if (i == 0 && currentActionData.LockPositions)
                    {
                        matrix.M41 = bm.Position[0].X;
                        matrix.M42 = bm.Position[0].Y;
                        matrix.M43 = position1.Z + (position2.Z - position1.Z) * interpolationFactor + BodyHeight;
                    }
                    else
                    {
                        matrix.M41 = position1.X + (position2.X - position1.X) * interpolationFactor;
                        matrix.M42 = position1.Y + (position2.Y - position1.Y) * interpolationFactor;
                        matrix.M43 = position1.Z + (position2.Z - position1.Z) * interpolationFactor;
                    }

                    Matrix newMatrix;
                    if (bone.Parent != -1 && bone.Parent < boneTransforms.Length)
                    {
                        newMatrix = matrix * boneTransforms[bone.Parent];
                    }
                    else
                    {
                        newMatrix = matrix;
                    }

                    if (!changed && i < boneTransforms.Length)
                    {
                        Matrix oldMatrix = boneTransforms[i];

                        changed = oldMatrix.M11 != newMatrix.M11 ||
                                oldMatrix.M22 != newMatrix.M22 ||
                                oldMatrix.M33 != newMatrix.M33 ||
                                oldMatrix.M44 != newMatrix.M44 ||
                                oldMatrix.M41 != newMatrix.M41 ||
                                oldMatrix.M42 != newMatrix.M42 ||
                                oldMatrix.M43 != newMatrix.M43;
                    }

                    if (i < boneTransforms.Length)
                    {
                        boneTransforms[i] = newMatrix;
                    }
                }

                if (changed)
                {
                    InvalidateBuffers();
                    UpdateBoundings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GenerateBoneMatrix: {ex.Message}");
            }
        }

        private void SetDynamicBuffers()
        {
            if (!_invalidatedBuffers || Model == null)
                return;

            try
            {
                int meshCount = Model.Meshes.Length;

                if (_boneVertexBuffers == null || _boneVertexBuffers.Length != meshCount)
                {
                    _boneVertexBuffers = new VertexBuffer[meshCount];
                    _boneIndexBuffers = new IndexBuffer[meshCount];
                    _boneTextures = new Texture2D[meshCount];
                    _scriptTextures = new TextureScript[meshCount];
                    _dataTextures = new TextureData[meshCount];
                }

                Matrix[] bones = (LinkParentAnimation && Parent is ModelObject parentModel && parentModel.BoneTransform != null)
                    ? parentModel.BoneTransform
                    : BoneTransform;

                if (bones == null)
                {
                    Debug.WriteLine("SetDynamicBuffers: bones reference is null");
                    return;
                }

                Vector3 bodyLight = LightEnabled && World != null && World.Terrain != null
                    ? World.Terrain.RequestTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y) + Light
                    : Light;

                for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
                {
                    var mesh = Model.Meshes[meshIndex];

                    bool isBlendMesh = false;
                    try
                    {
                        isBlendMesh = IsBlendMesh(meshIndex);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking blend mesh: {ex.Message}");
                    }

                    Vector3 meshLight = isBlendMesh
                        ? bodyLight * BlendMeshLight
                        : bodyLight * TotalAlpha;

                    byte r = Color.R;
                    byte g = Color.G;
                    byte b = Color.B;
                    byte bodyR = (byte)Math.Min(r * meshLight.X, 255);
                    byte bodyG = (byte)Math.Min(g * meshLight.Y, 255);
                    byte bodyB = (byte)Math.Min(b * meshLight.Z, 255);
                    Color bodyColor = new(bodyR, bodyG, bodyB);

                    _boneVertexBuffers[meshIndex]?.Dispose();
                    _boneIndexBuffers[meshIndex]?.Dispose();

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
                RecalculateWorldPosition();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SetDynamicBuffers: {ex.Message}");
            }
        }

        protected void InvalidateBuffers()
        {
            _invalidatedBuffers = true;

            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is ModelObject modelObject && modelObject.LinkParentAnimation)
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
                Matrix worldMatrix = ParentBodyOrigin * localMatrix * Parent.WorldPosition;

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
