using Client.Data.BMD;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class ModelObject : WorldObject
    {
        private DynamicVertexBuffer[] _boneVertexBuffers;
        private DynamicIndexBuffer[] _boneIndexBuffers;
        private Texture2D[] _boneTextures;
        private TextureScript[] _scriptTextures;
        private TextureData[] _dataTextures;

        private bool[] _meshIsRGBA;
        private bool[] _meshHiddenByScript;
        private bool[] _meshBlendByScript;
        private string[] _meshTexturePath;

        private int[] _blendMeshIndicesScratch;

        private bool _renderShadow = false;
        protected int _priorAction = 0;
        private bool _invalidatedBuffers = true;
        private float _blendMeshLight = 1f;
        protected double _animTime = 0.0;
        private bool _contentLoaded = false;
        public float ShadowOpacity { get; set; } = 1f;
        public Color Color { get; set; } = Color.White;
        protected Matrix[] BoneTransform { get; set; }
        public Matrix[] GetBoneTransforms() => BoneTransform;
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
        public static ILoggerFactory AppLoggerFactory { get; private set; }
        private ILogger _logger => AppLoggerFactory?.CreateLogger<ModelObject>();

        private int _blendFromAction = -1;
        private double _blendFromTime = 0.0;
        private Matrix[] _blendFromBones = null;
        private bool _isBlending = false;
        private float _blendElapsed = 0f;
        private float _blendDuration = 0.25f;

        public ModelObject()
        {
            MatrixChanged += (_s, _e) => UpdateWorldPosition();
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            if (Model == null)
            {
                _logger?.LogDebug($"Model is not assigned for {ObjectName} -> Type: {Type}");
                Status = Models.GameControlStatus.Error;
                return;
            }

            int meshCount = Model.Meshes.Length;
            _boneVertexBuffers = new DynamicVertexBuffer[meshCount];
            _boneIndexBuffers = new DynamicIndexBuffer[meshCount];
            _boneTextures = new Texture2D[meshCount];
            _scriptTextures = new TextureScript[meshCount];
            _dataTextures = new TextureData[meshCount];

            UpdateWorldPosition();

            _meshIsRGBA = new bool[meshCount];
            _meshHiddenByScript = new bool[meshCount];
            _meshBlendByScript = new bool[meshCount];
            _meshTexturePath = new string[meshCount];

            for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
            {
                var mesh = Model.Meshes[meshIndex];
                string texturePath = BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);

                _meshTexturePath[meshIndex] = texturePath;

                _boneTextures[meshIndex] = TextureLoader.Instance.GetTexture2D(texturePath);
                _scriptTextures[meshIndex] = TextureLoader.Instance.GetScript(texturePath);
                _dataTextures[meshIndex] = TextureLoader.Instance.Get(texturePath);

                _meshIsRGBA[meshIndex] = _dataTextures[meshIndex]?.Components == 4;
                _meshHiddenByScript[meshIndex] = _scriptTextures[meshIndex]?.HiddenMesh ?? false;
                _meshBlendByScript[meshIndex] = _scriptTextures[meshIndex]?.Bright ?? false;
            }

            _blendMeshIndicesScratch = new int[meshCount];

            _invalidatedBuffers = true;
            _contentLoaded = true;
            GenerateBoneMatrix(0, 0, 0, 0);
        }

        public override void Update(GameTime gameTime)
        {
            if (World == null) return;
            base.Update(gameTime);

            if (!Visible) return;

            Animation(gameTime);

            if (_contentLoaded)
                SetDynamicBuffers();
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _boneIndexBuffers == null) return;

            var gd = GraphicsDevice;
            var prevCull = gd.RasterizerState;                      // zapisz
            gd.RasterizerState = RasterizerState.CullClockwise;

            GraphicsManager.Instance.AlphaTestEffect3D.View = Camera.Instance.View;
            GraphicsManager.Instance.AlphaTestEffect3D.Projection = Camera.Instance.Projection;
            GraphicsManager.Instance.AlphaTestEffect3D.World = WorldPosition;

            DrawModel(false);   // solid pass
            base.Draw(gameTime);

            gd.RasterizerState = prevCull;
        }

        public virtual void DrawModel(bool isAfterDraw)
        {
            if (Model == null || _boneVertexBuffers == null) return;

            int meshCount = Model.Meshes.Length;

            for (int i = 0; i < meshCount; i++)
            {
                if (IsHiddenMesh(i) || IsBlendMesh(i)) continue;

                bool isRGBA = _meshIsRGBA[i];
                bool draw = isAfterDraw ? isRGBA : !isRGBA;
                if (!draw) continue;

                if (!isAfterDraw && RenderShadow)
                    DrawShadowMesh(i, Camera.Instance.View, Camera.Instance.Projection, MuGame.Instance.GameTime);

                bool highlightAllowed = !(this is Monsters.MonsterObject m && m.IsDead);
                if (!isAfterDraw && IsMouseHover && highlightAllowed)
                    DrawMeshHighlight(i);

                DrawMesh(i);
            }

            int blendCount = 0;
            for (int i = 0; i < meshCount; i++)
                if (!IsHiddenMesh(i) && IsBlendMesh(i))
                    _blendMeshIndicesScratch[blendCount++] = i;

            if (blendCount == 0) return;

            Vector3 camPos = Camera.Instance.Position;
            Array.Sort(_blendMeshIndicesScratch, 0, blendCount,
                Comparer<int>.Create((a, b) =>
                {
                    float da = Vector3.DistanceSquared(camPos, WorldPosition.Translation);
                    float db = da;
                    return 0;
                }));

            for (int n = 0; n < blendCount; n++)
            {
                int i = _blendMeshIndicesScratch[n];
                bool isRGBA = _meshIsRGBA[i];
                bool draw = isAfterDraw ? isRGBA || true : false;

                if (!isAfterDraw && RenderShadow)
                    DrawShadowMesh(i, Camera.Instance.View, Camera.Instance.Projection, MuGame.Instance.GameTime);

                bool highlightAllowed = !(this is Monsters.MonsterObject m && m.IsDead);
                if (!isAfterDraw && IsMouseHover && highlightAllowed)
                    DrawMeshHighlight(i);

                if (draw) DrawMesh(i);
            }
        }

        private bool IsHiddenMesh(int mesh)
        {
            if (_meshHiddenByScript == null || mesh < 0 || mesh >= _meshHiddenByScript.Length)
                return false;

            return HiddenMesh == mesh || HiddenMesh == -2 || _meshHiddenByScript[mesh];
        }

        private bool IsBlendMesh(int mesh)
        {
            if (_meshBlendByScript == null || mesh < 0 || mesh >= _meshBlendByScript.Length)
                return false;

            return BlendMesh == mesh || BlendMesh == -2 || _meshBlendByScript[mesh];
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

                // GraphicsManager.Instance.AlphaTestEffect3D.Texture = texture;
                // 
                // GraphicsManager.Instance.AlphaTestEffect3D.Alpha = TotalAlpha;

                var gd = GraphicsDevice;
                var prevCull = gd.RasterizerState;
                var prevBlend = gd.BlendState;
                float prevAlpha = GraphicsManager.Instance.AlphaTestEffect3D.Alpha;

                bool isTwoSided = _meshIsRGBA[mesh] || IsBlendMesh(mesh);
                gd.RasterizerState = isTwoSided
                    ? RasterizerState.CullNone
                    : RasterizerState.CullClockwise;

                GraphicsDevice.BlendState = IsBlendMesh(mesh) ? BlendMeshState : BlendState;
                GraphicsManager.Instance.AlphaTestEffect3D.Texture = texture;
                GraphicsManager.Instance.AlphaTestEffect3D.Alpha = TotalAlpha;

                foreach (var pass in GraphicsManager.Instance.AlphaTestEffect3D.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.SetVertexBuffer(vertexBuffer);
                    gd.Indices = indexBuffer;
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
                }

                GraphicsManager.Instance.AlphaTestEffect3D.Alpha = prevAlpha;

                // ❹ przywrócenie stanów
                gd.BlendState = prevBlend;
                gd.RasterizerState = prevCull;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Error in DrawMesh: {ex.Message}");
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
            float prevAlpha = GraphicsManager.Instance.AlphaTestEffect3D.Alpha;

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

            GraphicsManager.Instance.AlphaTestEffect3D.Alpha = prevAlpha;

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
                    _logger?.LogDebug("Invalid WorldPosition matrix detected - skipping shadow rendering");
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
                    _logger?.LogDebug($"Error creating shadow matrix: {ex.Message}");
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
                _logger?.LogDebug($"Error in DrawShadowMesh: {ex.Message}");
            }
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible) return;

            var gd = GraphicsDevice;
            var prevCull = gd.RasterizerState;
            // ZMIANA: Użyj CullCounterClockwise
            gd.RasterizerState = RasterizerState.CullCounterClockwise;

            GraphicsManager.Instance.AlphaTestEffect3D.View = Camera.Instance.View;
            GraphicsManager.Instance.AlphaTestEffect3D.Projection = Camera.Instance.Projection;
            GraphicsManager.Instance.AlphaTestEffect3D.World = WorldPosition;

            DrawModel(true);    // RGBA / blend mesh
            base.DrawAfter(gameTime);

            gd.RasterizerState = prevCull;
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
            if (LinkParentAnimation) return;
            if (Model?.Actions == null || Model.Actions.Length == 0) return;

            int currentActionIndex = CurrentAction;

            if (currentActionIndex < 0 || currentActionIndex >= Model.Actions.Length)
            {
                _logger?.LogError("Animation Error: invalid CurrentAction {Index} for {Obj}", currentActionIndex, ObjectName);
                currentActionIndex = 0;
                if (currentActionIndex >= Model.Actions.Length) return;
            }

            var action = Model.Actions[currentActionIndex];
            int totalFrames = Math.Max(action.NumAnimationKeys, 1);

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float objFps = AnimationSpeed;
            if (totalFrames == 1)
            {
                if (_priorAction != currentActionIndex)
                    GenerateBoneMatrix(currentActionIndex, 0, 0, 0);
                _priorAction = currentActionIndex;
                return;
            }

            if (_priorAction != currentActionIndex)
            {
                _blendFromAction = _priorAction;
                _blendFromTime = _animTime;
                _blendElapsed = 0f;
                _isBlending = true;
                _animTime = 0.0;
                EnsureArray(ref _blendFromBones, Model.Bones.Length, Matrix.Identity);

                if (_blendFromAction >= 0 && _blendFromAction < Model.Actions.Length)
                {
                    var prevAction = Model.Actions[_blendFromAction];
                    int prevTotal = Math.Max(prevAction.NumAnimationKeys, 1);
                    double pf = _blendFromTime % prevTotal;
                    int pf0 = (int)pf;
                    int pf1 = (pf0 + 1) % prevTotal;
                    float pt = (float)(pf - pf0);
                    ComputeBoneMatrixTo(_blendFromAction, pf0, pf1, pt, _blendFromBones);
                }
            }

            if (_isBlending && _blendFromAction >= 0 && _blendFromAction < Model.Actions.Length)
            {
                var prevAction = Model.Actions[_blendFromAction];
                float prevMul = prevAction.PlaySpeed == 0 ? 1.0f : prevAction.PlaySpeed;
                float prevFps = Math.Max(0.01f, objFps * prevMul);
                _blendFromTime += delta * prevFps;
                _blendElapsed += delta;
                if (_blendElapsed >= _blendDuration)
                {
                    _blendElapsed = _blendDuration;
                    _isBlending = false;
                }
            }

            float playMul = action.PlaySpeed == 0 ? 1.0f : action.PlaySpeed;
            float effectiveFps = Math.Max(0.01f, objFps * playMul);

            _animTime += delta * effectiveFps;
            double framePos = _animTime % totalFrames;

            int f0 = (int)framePos;
            int f1 = (f0 + 1) % totalFrames;
            float t = (float)(framePos - f0);

            GenerateBoneMatrix(currentActionIndex, f0, f1, t);

            if (_isBlending && _blendFromBones != null && _blendFromAction >= 0 && _blendFromAction < Model.Actions.Length)
            {
                var prevAction = Model.Actions[_blendFromAction];
                int prevTotal = Math.Max(prevAction.NumAnimationKeys, 1);
                double pf = _blendFromTime % prevTotal;
                int pf0 = (int)pf;
                int pf1 = (pf0 + 1) % prevTotal;
                float pt = (float)(pf - pf0);
                ComputeBoneMatrixTo(_blendFromAction, pf0, pf1, pt, _blendFromBones);

                float blendFactor = Math.Clamp(_blendElapsed / _blendDuration, 0f, 1f);
                bool changed = false;
                for (int i = 0; i < BoneTransform.Length && i < _blendFromBones.Length; i++)
                {
                    Quaternion curRot = Quaternion.CreateFromRotationMatrix(BoneTransform[i]);
                    Quaternion prevRot = Quaternion.CreateFromRotationMatrix(_blendFromBones[i]);
                    Vector3 curPos = BoneTransform[i].Translation;
                    Vector3 prevPos = _blendFromBones[i].Translation;

                    Quaternion rot = Quaternion.Slerp(prevRot, curRot, blendFactor);
                    Vector3 pos = Vector3.Lerp(prevPos, curPos, blendFactor);

                    Matrix m = Matrix.CreateFromQuaternion(rot);
                    m.Translation = pos;

                    if (m != BoneTransform[i])
                    {
                        BoneTransform[i] = m;
                        changed = true;
                    }
                }

                if (!_isBlending || blendFactor >= 1f)
                {
                    _isBlending = false;
                    _blendFromBones = null;
                    _blendFromAction = -1;
                }

                if (changed)
                {
                    InvalidateBuffers();
                    UpdateBoundings();
                }
            }

            _priorAction = currentActionIndex;
        }

        protected void GenerateBoneMatrix(int actionIdx, int frame0, int frame1, float t)
        {
            if (Model?.Bones == null)
            {
                _logger?.LogWarning("GenerateBoneMatrix called with null Model or Model.Bones for {ObjectName}", this.ObjectName ?? GetType().Name);
                return;
            }

            Matrix[] transforms = BoneTransform ??= new Matrix[Model.Bones.Length];
            var bones = Model.Bones;

            if (actionIdx < 0 || actionIdx >= Model.Actions.Length)
            {
                _logger?.LogError("GenerateBoneMatrix: Invalid actionIdx {ActionIndex} for model {ModelName}. Max actions: {MaxActions}. Defaulting to action 0.", actionIdx, Model.Name ?? "Unknown", Model.Actions.Length);
                actionIdx = 0;
                if (actionIdx >= Model.Actions.Length)
                {
                    _logger?.LogError("GenerateBoneMatrix: Default action 0 is also invalid for model {ModelName}. Cannot generate bone matrix.", Model.Name ?? "Unknown");
                    return;
                }
            }

            var action = Model.Actions[actionIdx];

            bool changedAny = false;

            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];

                if (bone == BMDTextureBone.Dummy || bone.Matrixes == null || actionIdx >= bone.Matrixes.Length)
                    continue;

                var bm = bone.Matrixes[actionIdx];

                int numPosKeys = bm.Position?.Length ?? 0;
                int numQuatKeys = bm.Quaternion?.Length ?? 0;

                if (numPosKeys == 0 || numQuatKeys == 0)
                {
                    _logger?.LogWarning("GenerateBoneMatrix: Bone {BoneIndex} (Name: {BoneName}), Action {ActionIndex} has 0 position ({PosLen}) or quaternion ({QuatLen}) keys. Skipping animation update for this bone.", i, bone.Name ?? "N/A", actionIdx, numPosKeys, numQuatKeys);
                    continue;
                }

                if (frame0 < 0 || frame1 < 0 || frame0 >= numPosKeys || frame1 >= numPosKeys || frame0 >= numQuatKeys || frame1 >= numQuatKeys)
                {
                    _logger?.LogWarning("GenerateBoneMatrix: Frame index out of bounds for Bone {BoneIndex} (Name: {BoneName}), Action {ActionIndex}. frame0={f0}, frame1={f1}, PosKeys={PosLen}, QuatKeys={QuatLen}. Clamping frames.", i, bone.Name ?? "N/A", actionIdx, frame0, frame1, numPosKeys, numQuatKeys);

                    int maxValidIndex = Math.Min(numPosKeys, numQuatKeys) - 1;
                    if (maxValidIndex < 0) maxValidIndex = 0;

                    frame0 = Math.Clamp(frame0, 0, maxValidIndex);
                    frame1 = Math.Clamp(frame1, 0, maxValidIndex);

                    if (frame0 == frame1) t = 0f;
                }

                Quaternion q = Quaternion.Slerp(bm.Quaternion[frame0], bm.Quaternion[frame1], t);
                Matrix m = Matrix.CreateFromQuaternion(q);

                Vector3 p0 = bm.Position[frame0];
                Vector3 p1 = bm.Position[frame1];

                m.M41 = p0.X + (p1.X - p0.X) * t;
                m.M42 = p0.Y + (p1.Y - p0.Y) * t;
                m.M43 = p0.Z + (p1.Z - p0.Z) * t;

                if (i == 0 && action.LockPositions)
                    m.Translation = new Vector3(bm.Position[0].X, bm.Position[0].Y, m.M43 + BodyHeight);

                Matrix world = bone.Parent != -1 && bone.Parent < transforms.Length
                    ? m * transforms[bone.Parent]
                    : m;

                if (world != transforms[i]) { transforms[i] = world; changedAny = true; }
            }

            if (changedAny)
            {
                InvalidateBuffers();
                UpdateBoundings();
            }
        }

        private void ComputeBoneMatrixTo(int actionIdx, int frame0, int frame1, float t, Matrix[] output)
        {
            if (Model?.Bones == null || output == null)
                return;

            var bones = Model.Bones;
            if (actionIdx < 0 || actionIdx >= Model.Actions.Length)
                actionIdx = 0;

            var action = Model.Actions[actionIdx];

            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];

                if (bone == BMDTextureBone.Dummy || bone.Matrixes == null || actionIdx >= bone.Matrixes.Length)
                    continue;

                var bm = bone.Matrixes[actionIdx];

                int numPosKeys = bm.Position?.Length ?? 0;
                int numQuatKeys = bm.Quaternion?.Length ?? 0;
                if (numPosKeys == 0 || numQuatKeys == 0)
                    continue;

                if (frame0 < 0 || frame1 < 0 || frame0 >= numPosKeys || frame1 >= numPosKeys || frame0 >= numQuatKeys || frame1 >= numQuatKeys)
                {
                    int maxValidIndex = Math.Min(numPosKeys, numQuatKeys) - 1;
                    if (maxValidIndex < 0) maxValidIndex = 0;
                    frame0 = Math.Clamp(frame0, 0, maxValidIndex);
                    frame1 = Math.Clamp(frame1, 0, maxValidIndex);
                    if (frame0 == frame1) t = 0f;
                }

                Quaternion q = Quaternion.Slerp(bm.Quaternion[frame0], bm.Quaternion[frame1], t);
                Matrix m = Matrix.CreateFromQuaternion(q);

                Vector3 p0 = bm.Position[frame0];
                Vector3 p1 = bm.Position[frame1];

                m.M41 = p0.X + (p1.X - p0.X) * t;
                m.M42 = p0.Y + (p1.Y - p0.Y) * t;
                m.M43 = p0.Z + (p1.Z - p0.Z) * t;

                if (i == 0 && action.LockPositions)
                    m.Translation = new Vector3(bm.Position[0].X, bm.Position[0].Y, m.M43 + BodyHeight);

                Matrix world = bone.Parent != -1 && bone.Parent < output.Length
                    ? m * output[bone.Parent]
                    : m;

                output[i] = world;
            }
        }

        private void SetDynamicBuffers()
        {
            if (!_invalidatedBuffers || Model == null)
                return;

            try
            {
                int meshCount = Model.Meshes?.Length ?? 0;
                if (meshCount == 0) return;

                EnsureArray(ref _boneVertexBuffers, meshCount);
                EnsureArray(ref _boneIndexBuffers, meshCount);
                EnsureArray(ref _boneTextures, meshCount);
                EnsureArray(ref _scriptTextures, meshCount);
                EnsureArray(ref _dataTextures, meshCount);

                EnsureArray(ref _meshIsRGBA, meshCount, false);
                EnsureArray(ref _meshHiddenByScript, meshCount, false);
                EnsureArray(ref _meshBlendByScript, meshCount, false);
                EnsureArray(ref _meshTexturePath, meshCount, string.Empty);
                EnsureArray(ref _blendMeshIndicesScratch, meshCount, 0);

                Matrix[] bones =
                    (LinkParentAnimation && Parent is ModelObject parentModel && parentModel.BoneTransform != null)
                        ? parentModel.BoneTransform
                        : BoneTransform;

                if (bones == null)
                {
                    _logger?.LogDebug("SetDynamicBuffers: BoneTransform == null – skip");
                    return;
                }

                Vector3 bodyLight = LightEnabled && World?.Terrain != null
                    ? World.Terrain.RequestTerrainLight(WorldPosition.Translation.X,
                                                        WorldPosition.Translation.Y) + Light
                    : Light;

                for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
                {
                    try
                    {
                        bool isBlend = IsBlendMesh(meshIndex);
                        Vector3 meshLight = isBlend ? bodyLight * BlendMeshLight
                                                    : bodyLight * TotalAlpha;

                        byte r = (byte)MathF.Min(Color.R * meshLight.X, 255f);
                        byte g = (byte)MathF.Min(Color.G * meshLight.Y, 255f);
                        byte b = (byte)MathF.Min(Color.B * meshLight.Z, 255f);
                        Color bodyColor = new(r, g, b);

                        BMDLoader.Instance.GetModelBuffers(
                            Model, meshIndex, bodyColor, bones,
                            ref _boneVertexBuffers[meshIndex],
                            ref _boneIndexBuffers[meshIndex]);


                        if (_boneTextures[meshIndex] == null)
                        {
                            string texturePath = _meshTexturePath?[meshIndex]
                                             ?? BMDLoader.Instance.GetTexturePath(Model,
                                                    Model.Meshes[meshIndex].TexturePath);

                            _boneTextures[meshIndex] = TextureLoader.Instance.GetTexture2D(texturePath);
                            _scriptTextures[meshIndex] = TextureLoader.Instance.GetScript(texturePath);
                            _dataTextures[meshIndex] = TextureLoader.Instance.Get(texturePath);
                        }
                    }
                    catch (Exception exMesh)
                    {
                        _logger?.LogDebug($"SetDynamicBuffers – mesh {meshIndex}: {exMesh.Message}");
                    }
                }

                _invalidatedBuffers = false;
                RecalculateWorldPosition();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"SetDynamicBuffers FATAL: {ex.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureArray<T>(ref T[] array, int size, T defaultValue = default)
        {
            if (array is null)
                array = new T[size];
            else if (array.Length != size)
                Array.Resize(ref array, size);

            for (int i = 0; i < size; i++)
                if (array[i] == null || array[i].Equals(default(T)))
                    array[i] = defaultValue;
        }

        public void InvalidateBuffers()
        {
            _invalidatedBuffers = true;

            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is ModelObject modelObject && modelObject.LinkParentAnimation)
                    modelObject.InvalidateBuffers();
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
