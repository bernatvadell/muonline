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

        private Vector3 _lastFrameLight = Vector3.Zero;

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

            // Initialize lighting cache for static objects
            _lastFrameLight = LightEnabled && World?.Terrain != null
                ? World.Terrain.RequestTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y) + Light
                : Light;

            _invalidatedBuffers = true;
            _contentLoaded = true;
            GenerateBoneMatrix(0, 0, 0, 0);
        }

        public override void Update(GameTime gameTime)
        {
            if (World == null) return;
            base.Update(gameTime);

            if (!Visible) return;

            // Only calculate lighting for non-animated objects or every few frames for animated ones
            if (!LinkParentAnimation && _contentLoaded)
            {
                if (LightEnabled && World?.Terrain != null)
                {
                    Vector3 currentLight = World.Terrain.RequestTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y) + Light;

                    // Use squared distance with higher threshold to reduce sensitivity
                    if (Vector3.DistanceSquared(currentLight, _lastFrameLight) > 0.0001f) // Increased threshold
                    {
                        _invalidatedBuffers = true;
                        _lastFrameLight = currentLight;
                    }
                }
            }

            Animation(gameTime);

            if (_contentLoaded && _invalidatedBuffers)
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
            if (Model?.Meshes == null || _boneVertexBuffers == null) return;

            int meshCount = Model.Meshes.Length;
            var camPos = Camera.Instance.Position; // Cache camera position

            // First pass - draw non-blend meshes
            for (int i = 0; i < meshCount; i++)
            {
                if (IsHiddenMesh(i) || IsBlendMesh(i)) continue;

                bool isRGBA = _meshIsRGBA[i];
                bool draw = isAfterDraw ? isRGBA : !isRGBA;
                if (!draw) continue;

                // Batch shadow and highlight rendering with main mesh
                if (!isAfterDraw)
                {
                    if (RenderShadow)
                        DrawShadowMesh(i, Camera.Instance.View, Camera.Instance.Projection, MuGame.Instance.GameTime);

                    bool highlightAllowed = !(this is Monsters.MonsterObject m && m.IsDead);
                    if (IsMouseHover && highlightAllowed)
                        DrawMeshHighlight(i);
                }

                DrawMesh(i);
            }

            // Second pass - blend meshes (only count once)
            int blendCount = 0;
            for (int i = 0; i < meshCount; i++)
            {
                if (!IsHiddenMesh(i) && IsBlendMesh(i))
                    _blendMeshIndicesScratch[blendCount++] = i;
            }

            if (blendCount == 0) return;

            // Simplified sorting - distance sorting removed as it was ineffective
            // The original comparison always returned 0, so we skip expensive sorting

            for (int n = 0; n < blendCount; n++)
            {
                int i = _blendMeshIndicesScratch[n];
                bool isRGBA = _meshIsRGBA[i];
                bool draw = isAfterDraw ? isRGBA || true : false;

                if (!isAfterDraw)
                {
                    if (RenderShadow)
                        DrawShadowMesh(i, Camera.Instance.View, Camera.Instance.Projection, MuGame.Instance.GameTime);

                    bool highlightAllowed = !(this is Monsters.MonsterObject m && m.IsDead);
                    if (IsMouseHover && highlightAllowed)
                        DrawMeshHighlight(i);
                }

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

        /// <summary>
        /// Preloads textures for this model and all child models so that the first
        /// render does not trigger loading stalls.
        /// </summary>
        public virtual async Task PreloadTexturesAsync()
        {
            if (Model?.Meshes != null)
            {
                foreach (var mesh in Model.Meshes)
                {
                    string texturePath = BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);
                    if (!string.IsNullOrEmpty(texturePath))
                        await TextureLoader.Instance.PrepareAndGetTexture(texturePath);
                }
            }

            foreach (var child in Children)
            {
                if (child is ModelObject mo)
                    await mo.PreloadTexturesAsync();
                else if (child is SpriteObject so)
                    await so.PreloadTexturesAsync();
            }
        }

        public virtual void DrawMesh(int mesh)
        {
            if (_boneVertexBuffers?[mesh] == null ||
                _boneIndexBuffers?[mesh] == null ||
                _boneTextures?[mesh] == null ||
                IsHiddenMesh(mesh))
                return;

            try
            {
                var gd = GraphicsDevice;
                var effect = GraphicsManager.Instance.AlphaTestEffect3D;

                // Cache frequently used values
                bool isBlendMesh = IsBlendMesh(mesh);
                bool isTwoSided = _meshIsRGBA[mesh] || isBlendMesh;

                // Batch state changes
                var prevCull = gd.RasterizerState;
                var prevBlend = gd.BlendState;
                float prevAlpha = effect.Alpha;

                gd.RasterizerState = isTwoSided ? RasterizerState.CullNone : RasterizerState.CullClockwise;
                gd.BlendState = isBlendMesh ? BlendMeshState : BlendState;

                // Set effect properties
                effect.Texture = _boneTextures[mesh];
                effect.Alpha = TotalAlpha;

                // Draw with single pass application
                int primitiveCount = _boneIndexBuffers[mesh].IndexCount / 3;

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.SetVertexBuffer(_boneVertexBuffers[mesh]);
                    gd.Indices = _boneIndexBuffers[mesh];
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
                }

                // Restore states in batch
                effect.Alpha = prevAlpha;
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
                    _logger?.LogDebug("Invalid WorldPosition matrix detected – skipping shadow rendering");
                    return;
                }

                VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
                IndexBuffer indexBuffer = _boneIndexBuffers[mesh];
                if (vertexBuffer == null || indexBuffer == null)
                    return;

                int primitiveCount = indexBuffer.IndexCount / 3;

                Matrix shadowWorld;
                const float shadowBias = 0.1f;
                try
                {
                    Vector3 position = WorldPosition.Translation;
                    float terrainH = World.Terrain.RequestTerrainHeight(position.X, position.Y);
                    terrainH += terrainH * 0.5f;

                    float heightAboveTerrain = position.Z - terrainH;
                    float sampleDist = heightAboveTerrain + 10f;
                    float angleRad = MathHelper.ToRadians(45);

                    float offX = sampleDist * (float)Math.Cos(angleRad);
                    float offY = sampleDist * (float)Math.Sin(angleRad);

                    float hX1 = World.Terrain.RequestTerrainHeight(position.X - offX, position.Y - offY);
                    float hX2 = World.Terrain.RequestTerrainHeight(position.X + offX, position.Y + offY);
                    float hZ1 = World.Terrain.RequestTerrainHeight(position.X - offY, position.Y + offX);
                    float hZ2 = World.Terrain.RequestTerrainHeight(position.X + offY, position.Y - offX);

                    float slopeX = (float)Math.Atan2(hX2 - hX1, sampleDist * 0.4f);
                    float slopeZ = (float)Math.Atan2(hZ2 - hZ1, sampleDist * 0.4f);

                    Vector3 shadowPos = new Vector3(
                        position.X - (heightAboveTerrain / 2),
                        position.Y - (heightAboveTerrain / 4.5f),
                        terrainH + 1f
                    );

                    float yaw = TotalAngle.Y + MathHelper.ToRadians(110) - slopeX / 2;
                    float pitch = TotalAngle.X + MathHelper.ToRadians(120);
                    float roll = TotalAngle.Z + MathHelper.ToRadians(90);

                    Quaternion rotQ = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);

                    shadowWorld =
                          Matrix.CreateFromQuaternion(rotQ)
                        * Matrix.CreateScale(1.0f * TotalScale, 0.01f * TotalScale, 1.0f * TotalScale)
                        * Matrix.CreateRotationX(Math.Max(-MathHelper.PiOver2, -MathHelper.PiOver2 - slopeX))
                        * Matrix.CreateRotationZ(angleRad)
                        * Matrix.CreateTranslation(shadowPos + new Vector3(0f, 0f, shadowBias));
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug($"Error creating shadow matrix: {ex.Message}");
                    return;
                }

                var prevBlendState = GraphicsDevice.BlendState;
                var prevDepthStencilState = GraphicsDevice.DepthStencilState;

                GraphicsDevice.BlendState = Blendings.ShadowBlend;
                GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

                try
                {
                    var effect = GraphicsManager.Instance.ShadowEffect;
                    if (effect == null || _boneTextures?[mesh] == null)
                        return;

                    effect.Parameters["World"]?.SetValue(shadowWorld);
                    effect.Parameters["ViewProjection"]?.SetValue(view * projection);
                    effect.Parameters["ShadowTint"]?.SetValue(
                        new Vector4(0f, 0f, 0f, 1f * ShadowOpacity));       // pół-przezroczyste czarne
                    effect.Parameters["ShadowTexture"]?.SetValue(_boneTextures[mesh]);

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.SetVertexBuffer(vertexBuffer);
                        GraphicsDevice.Indices = indexBuffer;
                        GraphicsDevice.DrawIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            baseVertex: 0,
                            startIndex: 0,
                            primitiveCount: primitiveCount);
                    }
                }
                finally
                {
                    GraphicsDevice.BlendState = prevBlendState;
                    GraphicsDevice.DepthStencilState = prevDepthStencilState;
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
            if (Model?.Meshes == null || Model.Meshes.Length == 0 || BoneTransform == null) return;

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            bool hasValidVertices = false;

            foreach (var mesh in Model.Meshes)
            {
                if (mesh.Vertices == null) continue;

                foreach (var vertex in mesh.Vertices)
                {
                    int boneIndex = vertex.Node;
                    if (boneIndex < 0 || boneIndex >= BoneTransform.Length) continue;

                    Vector3 transformedPosition = Vector3.Transform(vertex.Position, BoneTransform[boneIndex]);

                    // Optimized min/max calculation
                    if (transformedPosition.X < min.X) min.X = transformedPosition.X;
                    if (transformedPosition.Y < min.Y) min.Y = transformedPosition.Y;
                    if (transformedPosition.Z < min.Z) min.Z = transformedPosition.Z;

                    if (transformedPosition.X > max.X) max.X = transformedPosition.X;
                    if (transformedPosition.Y > max.Y) max.Y = transformedPosition.Y;
                    if (transformedPosition.Z > max.Z) max.Z = transformedPosition.Z;

                    hasValidVertices = true;
                }
            }

            if (hasValidVertices)
                BoundingBoxLocal = new BoundingBox(min, max);
        }

        private void Animation(GameTime gameTime)
        {
            if (LinkParentAnimation || Model?.Actions == null || Model.Actions.Length == 0) return;

            int currentActionIndex = Math.Clamp(CurrentAction, 0, Model.Actions.Length - 1);
            var action = Model.Actions[currentActionIndex];
            int totalFrames = Math.Max(action.NumAnimationKeys, 1);

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Handle single frame animations early
            if (totalFrames == 1)
            {
                if (_priorAction != currentActionIndex)
                    GenerateBoneMatrix(currentActionIndex, 0, 0, 0);
                _priorAction = currentActionIndex;
                return;
            }

            // Handle action changes
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

            // Update blend timing
            if (_isBlending && _blendFromAction >= 0 && _blendFromAction < Model.Actions.Length)
            {
                var prevAction = Model.Actions[_blendFromAction];
                float prevMul = prevAction.PlaySpeed == 0 ? 1.0f : prevAction.PlaySpeed;
                float prevFps = Math.Max(0.01f, AnimationSpeed * prevMul);
                _blendFromTime += delta * prevFps;
                _blendElapsed += delta;

                if (_blendElapsed >= _blendDuration)
                {
                    _blendElapsed = _blendDuration;
                    _isBlending = false;
                }
            }

            // Update main animation
            float playMul = action.PlaySpeed == 0 ? 1.0f : action.PlaySpeed;
            float effectiveFps = Math.Max(0.01f, AnimationSpeed * playMul);
            _animTime += delta * effectiveFps;

            double framePos = _animTime % totalFrames;
            int f0 = (int)framePos;
            int f1 = (f0 + 1) % totalFrames;
            float t = (float)(framePos - f0);

            GenerateBoneMatrix(currentActionIndex, f0, f1, t);

            // Handle blending with optimized matrix interpolation
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

                int boneCount = Math.Min(BoneTransform.Length, _blendFromBones.Length);
                for (int i = 0; i < boneCount; i++)
                {
                    // Optimized matrix blending
                    ref Matrix current = ref BoneTransform[i];
                    ref Matrix previous = ref _blendFromBones[i];

                    if (blendFactor >= 0.99f) // Near complete blend
                    {
                        if (current != BoneTransform[i])
                            changed = true;
                        continue;
                    }

                    // Extract and interpolate components more efficiently
                    Vector3 curPos = current.Translation;
                    Vector3 prevPos = previous.Translation;
                    Vector3 pos = Vector3.Lerp(prevPos, curPos, blendFactor);

                    // Simplified rotation blending for better performance
                    Quaternion curRot = Quaternion.CreateFromRotationMatrix(current);
                    Quaternion prevRot = Quaternion.CreateFromRotationMatrix(previous);
                    Quaternion rot = Quaternion.Slerp(prevRot, curRot, blendFactor);

                    Matrix newMatrix = Matrix.CreateFromQuaternion(rot);
                    newMatrix.Translation = pos;

                    if (newMatrix != current)
                    {
                        BoneTransform[i] = newMatrix;
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
            if (Model?.Bones == null) return;

            Matrix[] transforms = BoneTransform ??= new Matrix[Model.Bones.Length];
            var bones = Model.Bones;

            actionIdx = Math.Clamp(actionIdx, 0, Model.Actions.Length - 1);
            var action = Model.Actions[actionIdx];

            bool changedAny = false;
            bool lockPositions = action.LockPositions;
            float bodyHeight = BodyHeight;

            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];

                if (bone == BMDTextureBone.Dummy || bone.Matrixes == null || actionIdx >= bone.Matrixes.Length)
                    continue;

                var bm = bone.Matrixes[actionIdx];
                int numPosKeys = bm.Position?.Length ?? 0;
                int numQuatKeys = bm.Quaternion?.Length ?? 0;

                if (numPosKeys == 0 || numQuatKeys == 0) continue;

                // Clamp frame indices
                int maxValidIndex = Math.Min(numPosKeys, numQuatKeys) - 1;
                frame0 = Math.Clamp(frame0, 0, maxValidIndex);
                frame1 = Math.Clamp(frame1, 0, maxValidIndex);

                if (frame0 == frame1) t = 0f;

                // Optimized quaternion and position interpolation
                Quaternion q = (t == 0f) ? bm.Quaternion[frame0] : Quaternion.Slerp(bm.Quaternion[frame0], bm.Quaternion[frame1], t);
                Matrix m = Matrix.CreateFromQuaternion(q);

                if (t == 0f)
                {
                    m.Translation = bm.Position[frame0];
                }
                else
                {
                    Vector3 p0 = bm.Position[frame0];
                    Vector3 p1 = bm.Position[frame1];
                    m.Translation = Vector3.Lerp(p0, p1, t);
                }

                // Apply position locking for root bone
                if (i == 0 && lockPositions)
                {
                    var basePos = bm.Position[0];
                    m.Translation = new Vector3(basePos.X, basePos.Y, m.Translation.Z + bodyHeight);
                }

                // Apply parent transformation
                Matrix world = (bone.Parent != -1 && bone.Parent < transforms.Length)
                    ? m * transforms[bone.Parent]
                    : m;

                if (world != transforms[i])
                {
                    transforms[i] = world;
                    changedAny = true;
                }
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
            if (!_invalidatedBuffers || Model?.Meshes == null)
                return;

            try
            {
                int meshCount = Model.Meshes.Length;
                if (meshCount == 0) return;

                // Ensure arrays only once
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

                Matrix[] bones = (LinkParentAnimation && Parent is ModelObject parentModel && parentModel.BoneTransform != null)
                    ? parentModel.BoneTransform
                    : BoneTransform;

                if (bones == null)
                {
                    _logger?.LogDebug("SetDynamicBuffers: BoneTransform == null – skip");
                    return;
                }

                // Calculate lighting once for all meshes
                Vector3 baseLight = LightEnabled && World?.Terrain != null
                    ? World.Terrain.RequestTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y) + Light
                    : Light;

                // Pre-calculate common color components
                float colorR = Color.R;
                float colorG = Color.G;
                float colorB = Color.B;
                float totalAlpha = TotalAlpha;
                float blendMeshLight = BlendMeshLight;

                for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
                {
                    try
                    {
                        bool isBlend = IsBlendMesh(meshIndex);
                        Vector3 meshLight = isBlend ? baseLight * blendMeshLight : baseLight * totalAlpha;

                        // Optimized color calculation
                        byte r = (byte)Math.Min(colorR * meshLight.X, 255f);
                        byte g = (byte)Math.Min(colorG * meshLight.Y, 255f);
                        byte b = (byte)Math.Min(colorB * meshLight.Z, 255f);
                        Color bodyColor = new Color(r, g, b);

                        BMDLoader.Instance.GetModelBuffers(
                            Model, meshIndex, bodyColor, bones,
                            ref _boneVertexBuffers[meshIndex],
                            ref _boneIndexBuffers[meshIndex]);

                        // Load textures only if needed
                        if (_boneTextures[meshIndex] == null)
                        {
                            string texturePath = _meshTexturePath[meshIndex]
                                ?? BMDLoader.Instance.GetTexturePath(Model, Model.Meshes[meshIndex].TexturePath);

                            _meshTexturePath[meshIndex] = texturePath;
                            _boneTextures[meshIndex] = TextureLoader.Instance.GetTexture2D(texturePath);
                            _scriptTextures[meshIndex] = TextureLoader.Instance.GetScript(texturePath);
                            _dataTextures[meshIndex] = TextureLoader.Instance.Get(texturePath);

                            // Cache texture properties
                            _meshIsRGBA[meshIndex] = _dataTextures[meshIndex]?.Components == 4;
                            _meshHiddenByScript[meshIndex] = _scriptTextures[meshIndex]?.HiddenMesh ?? false;
                            _meshBlendByScript[meshIndex] = _scriptTextures[meshIndex]?.Bright ?? false;
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
