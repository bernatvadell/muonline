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

        // Bounding box update optimization
        private int _boundingFrameCounter = BoundingUpdateInterval;
        private const int BoundingUpdateInterval = 5;

        // Animation
        private bool _needsBufferUpdate = true;
        private float _lastAnimationTime = 0;

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
            UpdateBoundings();
        }

        public override void Update(GameTime gameTime)
        {
            if (World == null) return;

            bool isVisible = Visible;
            bool isContentLoaded = _contentLoaded;

            // Track animation changes for buffer optimization
            float currentAnimTime = (float)gameTime.TotalGameTime.TotalSeconds;
            bool animationChanged = Math.Abs(currentAnimTime - _lastAnimationTime) > 0.001f;

            if (animationChanged)
            {
                _lastAnimationTime = currentAnimTime;
                _needsBufferUpdate = true;
            }

            // Process animation BEFORE updating children so they get fresh bone transforms
            if (isContentLoaded && isVisible)
            {
                Animation(gameTime);
            }

            // Now update children - they will use the updated bone transforms
            base.Update(gameTime);

            if (!isVisible || !isContentLoaded) return;

            // Only calculate lighting for non-animated objects or when needed
            if (!LinkParentAnimation)
            {
                if (LightEnabled && World?.Terrain != null)
                {
                    Vector3 worldPos = WorldPosition.Translation;
                    Vector3 currentLight = World.Terrain.RequestTerrainLight(worldPos.X, worldPos.Y) + Light;

                    // Use squared distance with higher threshold to reduce sensitivity
                    float lightDeltaSq = Vector3.DistanceSquared(currentLight, _lastFrameLight);
                    if (lightDeltaSq > 0.0001f)
                    {
                        _needsBufferUpdate = true;
                        _lastFrameLight = currentLight;
                    }
                }
            }

            // Update buffers only when necessary
            if (_invalidatedBuffers || _needsBufferUpdate)
            {
                SetDynamicBuffers();
                _needsBufferUpdate = false;
            }
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
            if (Model?.Meshes == null || _boneVertexBuffers == null)
                return;

            int meshCount = Model.Meshes.Length;
            if (meshCount == 0) return;

            // Cache commonly used values
            var view = Camera.Instance.View;
            var projection = Camera.Instance.Projection;
            var worldPos = WorldPosition;

            // Pre-calculate shadow and highlight states
            bool doShadow = false;
            Matrix shadowMatrix = Matrix.Identity;
            if (!isAfterDraw && RenderShadow && !LowQuality)
                doShadow = TryGetShadowMatrix(out shadowMatrix);

            bool highlightAllowed = !isAfterDraw && !LowQuality && IsMouseHover &&
                                   !(this is Monsters.MonsterObject m && m.IsDead);
            Matrix highlightMatrix = Matrix.Identity;
            Vector3 highlightColor = Vector3.One;

            if (highlightAllowed)
            {
                const float scaleHighlight = 0.015f;
                const float scaleFactor = 1f + scaleHighlight;
                highlightMatrix = Matrix.CreateScale(scaleFactor) *
                    Matrix.CreateTranslation(-scaleHighlight, -scaleHighlight, -scaleHighlight) *
                    worldPos;
                highlightColor = this is Monsters.MonsterObject ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
            }

            // First pass - draw non-blend meshes (batch similar operations)
            for (int i = 0; i < meshCount; i++)
            {
                // Early exit conditions grouped together
                if (IsHiddenMesh(i) || IsBlendMesh(i) || (LowQuality && IsBlendMesh(i)))
                    continue;

                bool isRGBA = _meshIsRGBA[i];
                bool shouldDraw = isAfterDraw ? isRGBA : !isRGBA;
                if (!shouldDraw) continue;

                // Draw effects first (shadow, highlight) then main mesh
                if (!isAfterDraw)
                {
                    if (doShadow)
                        DrawShadowMesh(i, view, projection, shadowMatrix);
                    if (highlightAllowed)
                        DrawMeshHighlight(i, highlightMatrix, highlightColor);
                }

                DrawMesh(i);
            }

            // Second pass - blend meshes (optimized count and draw)
            if (LowQuality) return;

            int blendCount = 0;
            // Count and store blend indices in one pass
            for (int i = 0; i < meshCount; i++)
            {
                if (!IsHiddenMesh(i) && IsBlendMesh(i))
                    _blendMeshIndicesScratch[blendCount++] = i;
            }

            if (blendCount == 0) return;

            // Draw blend meshes
            for (int n = 0; n < blendCount; n++)
            {
                int i = _blendMeshIndicesScratch[n];
                bool isRGBA = _meshIsRGBA[i];
                bool shouldDraw = isAfterDraw ? isRGBA || true : false;

                if (!isAfterDraw)
                {
                    if (doShadow)
                        DrawShadowMesh(i, view, projection, shadowMatrix);
                    if (highlightAllowed)
                        DrawMeshHighlight(i, highlightMatrix, highlightColor);
                }

                if (shouldDraw)
                    DrawMesh(i);
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
                    {
                        await TextureLoader.Instance.PrepareAndGetTexture(texturePath);
                        await Task.Yield();
                    }
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
                var vertexBuffer = _boneVertexBuffers[mesh];
                var indexBuffer = _boneIndexBuffers[mesh];
                var texture = _boneTextures[mesh];

                // Batch state changes - save current states
                var prevCull = gd.RasterizerState;
                var prevBlend = gd.BlendState;
                float prevAlpha = effect.Alpha;

                // Apply new states
                gd.RasterizerState = isTwoSided ? RasterizerState.CullNone : RasterizerState.CullClockwise;
                gd.BlendState = isBlendMesh ? BlendMeshState : BlendState;

                // Set effect properties
                effect.Texture = texture;
                effect.Alpha = TotalAlpha;

                // Set buffers once
                gd.SetVertexBuffer(vertexBuffer);
                gd.Indices = indexBuffer;

                // Draw with optimized primitive count calculation
                int primitiveCount = indexBuffer.IndexCount / 3;

                // Single pass application with minimal overhead
                var technique = effect.CurrentTechnique;
                var passes = technique.Passes;
                int passCount = passes.Count;

                for (int p = 0; p < passCount; p++)
                {
                    passes[p].Apply();
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

        public virtual void DrawMeshHighlight(int mesh, Matrix highlightMatrix, Vector3 highlightColor)
        {
            if (IsHiddenMesh(mesh) || _boneVertexBuffers == null)
                return;

            VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
            IndexBuffer indexBuffer = _boneIndexBuffers[mesh];
            int primitiveCount = indexBuffer.IndexCount / 3;

            // Save previous graphics states
            var previousDepthState = GraphicsDevice.DepthStencilState;
            var previousBlendState = GraphicsDevice.BlendState;
            float prevAlpha = GraphicsManager.Instance.AlphaTestEffect3D.Alpha;

            GraphicsManager.Instance.AlphaTestEffect3D.World = highlightMatrix;
            GraphicsManager.Instance.AlphaTestEffect3D.Texture = _boneTextures[mesh];
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

        private bool TryGetShadowMatrix(out Matrix shadowWorld)
        {
            shadowWorld = Matrix.Identity;

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

                float slopeX = (float)Math.Atan2(hX2 - hX1, sampleDist * 0.4f);

                Vector3 shadowPos = new(
                    position.X - (heightAboveTerrain / 2),
                    position.Y - (heightAboveTerrain / 4.5f),
                    terrainH + 1f);

                float yaw = TotalAngle.Y + MathHelper.ToRadians(110) - slopeX / 2;
                float pitch = TotalAngle.X + MathHelper.ToRadians(120);
                float roll = TotalAngle.Z + MathHelper.ToRadians(90);

                Quaternion rotQ = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);

                const float shadowBias = 0.1f;
                shadowWorld =
                      Matrix.CreateFromQuaternion(rotQ)
                    * Matrix.CreateScale(1.0f * TotalScale, 0.01f * TotalScale, 1.0f * TotalScale)
                    * Matrix.CreateRotationX(Math.Max(-MathHelper.PiOver2, -MathHelper.PiOver2 - slopeX))
                    * Matrix.CreateRotationZ(angleRad)
                    * Matrix.CreateTranslation(shadowPos + new Vector3(0f, 0f, shadowBias));

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Error creating shadow matrix: {ex.Message}");
                return false;
            }
        }

        public virtual void DrawShadowMesh(int mesh, Matrix view, Matrix projection, Matrix shadowWorld)
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

                int primitiveCount = indexBuffer.IndexCount / 3;

                var prevBlendState = GraphicsDevice.BlendState;
                var prevDepthState = GraphicsDevice.DepthStencilState;
                var prevRasterizerState = GraphicsDevice.RasterizerState;

                float constBias = 1f / (1 << 24);
                float slopeBias = -1.0f;

                RasterizerState ShadowRasterizer = new RasterizerState
                {
                    CullMode = CullMode.None,
                    DepthBias = constBias * -20,
                    SlopeScaleDepthBias = slopeBias
                };

                GraphicsDevice.BlendState = Blendings.ShadowBlend;
                GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                GraphicsDevice.RasterizerState = ShadowRasterizer;

                try
                {
                    var effect = GraphicsManager.Instance.ShadowEffect;
                    if (effect == null || _boneTextures?[mesh] == null)
                        return;

                    effect.Parameters["World"]?.SetValue(shadowWorld);
                    effect.Parameters["ViewProjection"]?.SetValue(view * projection);
                    effect.Parameters["ShadowTint"]?.SetValue(new Vector4(0, 0, 0, 1f * ShadowOpacity));
                    effect.Parameters["ShadowTexture"]?.SetValue(_boneTextures[mesh]);

                    foreach (var pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.SetVertexBuffer(vertexBuffer);
                        GraphicsDevice.Indices = indexBuffer;
                        GraphicsDevice.DrawIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            0, 0, primitiveCount);
                    }
                }
                finally
                {
                    GraphicsDevice.BlendState = prevBlendState;
                    GraphicsDevice.DepthStencilState = prevDepthState;
                    GraphicsDevice.RasterizerState = prevRasterizerState;
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
            // World transformation changes no longer force buffer rebuilds.
            // Lighting updates will trigger invalidation when needed.
        }

        private void UpdateBoundings()
        {
            // Recalculate bounding box only every few frames
            if (_boundingFrameCounter++ < BoundingUpdateInterval)
                return;

            _boundingFrameCounter = 0;

            if (Model?.Meshes == null || Model.Meshes.Length == 0 || BoneTransform == null) return;

            // Use faster min/max calculation
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            bool hasValidVertices = false;
            var meshes = Model.Meshes;
            var bones = BoneTransform;
            int boneCount = bones.Length;

            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                var mesh = meshes[meshIndex];
                var vertices = mesh.Vertices;
                if (vertices == null) continue;

                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    var vertex = vertices[vertexIndex];
                    int boneIndex = vertex.Node;

                    if (boneIndex < 0 || boneIndex >= boneCount) continue;

                    Vector3 transformedPosition = Vector3.Transform(vertex.Position, bones[boneIndex]);

                    // Optimized min/max calculation - avoid method calls
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
                {
                    GenerateBoneMatrix(currentActionIndex, 0, 0, 0);
                    _priorAction = currentActionIndex;
                }
                return;
            }

            // Handle action changes with optimized blending setup
            if (_priorAction != currentActionIndex)
            {
                _blendFromAction = _priorAction;
                _blendFromTime = _animTime;
                _blendElapsed = 0f;
                _isBlending = true;
                _animTime = 0.0;

                // Ensure blend bones array only when needed
                int boneCount = Model.Bones.Length;
                if (_blendFromBones == null || _blendFromBones.Length != boneCount)
                    _blendFromBones = new Matrix[boneCount];

                // Pre-compute blend-from matrices if valid
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

            // Optimized blend timing update
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

            // Update main animation with cached calculations
            float playMul = action.PlaySpeed == 0 ? 1.0f : action.PlaySpeed;
            float effectiveFps = Math.Max(0.01f, AnimationSpeed * playMul);
            _animTime += delta * effectiveFps;

            double framePos = _animTime % totalFrames;
            int f0 = (int)framePos;
            int f1 = (f0 + 1) % totalFrames;
            float t = (float)(framePos - f0);

            GenerateBoneMatrix(currentActionIndex, f0, f1, t);

            // Optimized blending with early exit conditions
            if (_isBlending && _blendFromBones != null && _blendFromAction >= 0 && _blendFromAction < Model.Actions.Length)
            {
                float blendFactor = Math.Clamp(_blendElapsed / _blendDuration, 0f, 1f);

                // Early exit for nearly complete blend
                if (blendFactor >= 0.99f)
                {
                    _isBlending = false;
                    _blendFromBones = null;
                    _blendFromAction = -1;
                    _priorAction = currentActionIndex;
                    return;
                }

                // Compute blend-from matrices
                var prevAction = Model.Actions[_blendFromAction];
                int prevTotal = Math.Max(prevAction.NumAnimationKeys, 1);
                double pf = _blendFromTime % prevTotal;
                int pf0 = (int)pf;
                int pf1 = (pf0 + 1) % prevTotal;
                float pt = (float)(pf - pf0);

                ComputeBoneMatrixTo(_blendFromAction, pf0, pf1, pt, _blendFromBones);

                // Optimized matrix blending
                bool changed = false;
                int boneCount = Math.Min(BoneTransform.Length, _blendFromBones.Length);

                for (int i = 0; i < boneCount; i++)
                {
                    ref Matrix current = ref BoneTransform[i];
                    ref Matrix previous = ref _blendFromBones[i];

                    // Decompose matrices efficiently
                    Vector3 curPos = current.Translation;
                    Vector3 prevPos = previous.Translation;
                    Vector3 pos = Vector3.Lerp(prevPos, curPos, blendFactor);

                    // Optimized rotation extraction and blending
                    Quaternion curRot = Quaternion.CreateFromRotationMatrix(current);
                    Quaternion prevRot = Quaternion.CreateFromRotationMatrix(previous);
                    Quaternion rot = Quaternion.Slerp(prevRot, curRot, blendFactor);

                    // Reconstruct matrix
                    Matrix newMatrix = Matrix.CreateFromQuaternion(rot);
                    newMatrix.Translation = pos;

                    // Only update if changed
                    if (newMatrix != current)
                    {
                        BoneTransform[i] = newMatrix;
                        changed = true;
                    }
                }

                // Clean up blending state
                if (blendFactor >= 1f)
                {
                    _isBlending = false;
                    _blendFromBones = null;
                    _blendFromAction = -1;
                }

                // Update dependent systems only when needed
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

                // Ensure arrays only once with optimized allocation
                EnsureArraySize(ref _boneVertexBuffers, meshCount);
                EnsureArraySize(ref _boneIndexBuffers, meshCount);
                EnsureArraySize(ref _boneTextures, meshCount);
                EnsureArraySize(ref _scriptTextures, meshCount);
                EnsureArraySize(ref _dataTextures, meshCount);
                EnsureArraySize(ref _meshIsRGBA, meshCount);
                EnsureArraySize(ref _meshHiddenByScript, meshCount);
                EnsureArraySize(ref _meshBlendByScript, meshCount);
                EnsureArraySize(ref _meshTexturePath, meshCount);
                EnsureArraySize(ref _blendMeshIndicesScratch, meshCount);

                // Get bone transforms with single check
                Matrix[] bones = (LinkParentAnimation && Parent is ModelObject parentModel && parentModel.BoneTransform != null)
                    ? parentModel.BoneTransform
                    : BoneTransform;

                if (bones == null)
                {
                    _logger?.LogDebug("SetDynamicBuffers: BoneTransform == null – skip");
                    return;
                }

                // Calculate lighting once for all meshes
                Vector3 worldTranslation = WorldPosition.Translation;
                Vector3 baseLight = LightEnabled && World?.Terrain != null
                    ? World.Terrain.RequestTerrainLight(worldTranslation.X, worldTranslation.Y) + Light
                    : Light;

                // Pre-calculate common color components
                float colorR = Color.R;
                float colorG = Color.G;
                float colorB = Color.B;
                float totalAlpha = TotalAlpha;
                float blendMeshLight = BlendMeshLight;

                // Process meshes with optimized loops
                for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
                {
                    try
                    {
                        var mesh = Model.Meshes[meshIndex];

                        // Calculate mesh-specific lighting
                        bool isBlend = IsBlendMesh(meshIndex);
                        Vector3 meshLight = isBlend ? baseLight * blendMeshLight : baseLight * totalAlpha;

                        // Optimized color calculation with clamping
                        byte r = (byte)Math.Min(255f, colorR * meshLight.X);
                        byte g = (byte)Math.Min(255f, colorG * meshLight.Y);
                        byte b = (byte)Math.Min(255f, colorB * meshLight.Z);
                        Color bodyColor = new Color(r, g, b);

                        // Generate buffers
                        BMDLoader.Instance.GetModelBuffers(
                            Model, meshIndex, bodyColor, bones,
                            ref _boneVertexBuffers[meshIndex],
                            ref _boneIndexBuffers[meshIndex]);

                        // Load textures only if needed (avoid redundant loading)
                        if (_boneTextures[meshIndex] == null)
                        {
                            string texturePath = _meshTexturePath[meshIndex]
                                ?? BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);

                            _meshTexturePath[meshIndex] = texturePath;

                            // Batch texture loading
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureArraySize<T>(ref T[] array, int size)
        {
            if (array is null || array.Length != size)
                array = new T[size];
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
                Matrix worldMatrix = localMatrix * ParentBodyOrigin * Parent.WorldPosition;

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
