using Client.Data.BMD;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Reflection;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Player;

namespace Client.Main.Objects
{
    public abstract class ModelObject : WorldObject
    {
        private static readonly Dictionary<string, BlendState> _blendStateCache = new Dictionary<string, BlendState>();

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
        private uint _invalidatedBufferFlags = uint.MaxValue; // Start with all flags set
        private float _blendMeshLight = 1f;
        protected double _animTime = 0.0;
        private bool _contentLoaded = false;
        public float ShadowOpacity { get; set; } = 1f;
        public Color Color { get; set; } = Color.White;
        protected Matrix[] BoneTransform { get; set; }
        public Matrix[] GetBoneTransforms() => BoneTransform;
        public int CurrentAction { get; set; }
        public int ParentBoneLink { get; set; } = -1;
        private BMD _model;
        public BMD Model
        {
            get => _model;
            set
            {
                if (_model != value)
                {
                    _model = value;
                    // If the model changes after the object has already been loaded,
                    // we need to re-run the content loading logic to update buffers, textures, etc.
                    if (Status != GameControlStatus.Disposed)
                    {
                        _ = LoadContent();
                    }
                }
            }
        }

        public Matrix ParentBodyOrigin
        {
            get
            {
                if (ParentBoneLink >= 0 && Parent != null && Parent is ModelObject modelObject)
                {
                    if (modelObject.BoneTransform != null && ParentBoneLink < modelObject.BoneTransform.Length)
                    {
                        return modelObject.BoneTransform[ParentBoneLink];
                    }
                }
                return Matrix.Identity;
            }
        }

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
                InvalidateBuffers(BUFFER_FLAG_MATERIAL);
            }
        }
        public bool RenderShadow { get => _renderShadow; set { _renderShadow = value; OnRenderShadowChanged(); } }
        public float AnimationSpeed { get; set; } = 4f;
        public static ILoggerFactory AppLoggerFactory { get; private set; }
        protected ILogger _logger;

        public int ItemLevel { get; set; } = 0;
        public bool IsExcellentItem { get; set; } = false;
        public bool IsAncientItem { get; set; } = false;

        // Monster/NPC glow properties
        public Vector3 GlowColor { get; set; } = new Vector3(1.0f, 0.8f, 0.0f); // Default gold
        public float GlowIntensity { get; set; } = 0.0f;
        public bool EnableCustomShader { get; set; } = false;

        // Cached arrays for dynamic lighting to avoid allocations
        private static readonly Vector3[] _cachedLightPositions = new Vector3[16];
        private static readonly Vector3[] _cachedLightColors = new Vector3[16];
        private static readonly float[] _cachedLightRadii = new float[16];
        private static readonly float[] _cachedLightIntensities = new float[16];

        // Cache for Environment.TickCount to reduce system calls
        private static float _cachedTime = 0f;
        private static int _lastTickCount = 0;

        // Cached common Vector3 instances to avoid allocations
        private static readonly Vector3 _ambientLightVector = new Vector3(0.8f, 0.8f, 0.8f);
        private static readonly Vector3 _redHighlight = new Vector3(1, 0, 0);
        private static readonly Vector3 _greenHighlight = new Vector3(0, 1, 0);
        private static readonly Vector3 _maxValueVector = new Vector3(float.MaxValue);
        private static readonly Vector3 _minValueVector = new Vector3(float.MinValue);

        // Cache common graphics states to avoid repeated property access
        private static readonly RasterizerState _cullClockwise = RasterizerState.CullClockwise;
        private static readonly RasterizerState _cullNone = RasterizerState.CullNone;

        private static float GetCachedTime()
        {
            int currentTick = Environment.TickCount;
            if (currentTick != _lastTickCount)
            {
                _lastTickCount = currentTick;
                _cachedTime = currentTick * 0.001f;
            }
            return _cachedTime;
        }

        private int _blendFromAction = -1;
        private double _blendFromTime = 0.0;
        private Matrix[] _blendFromBones = null;
        private bool _isBlending = false;
        private float _blendElapsed = 0f;
        private float _blendDuration = 0.25f;

        // Bounding box update optimization
        private int _boundingFrameCounter = BoundingUpdateInterval;
        private const int BoundingUpdateInterval = 10;

        // Animation and buffer optimization

        // Enhanced animation caching system
        private Matrix[] _cachedBoneMatrix = null;
        private int _lastCachedAction = -1;
        private float _lastCachedAnimTime = -1;
        private bool _boneMatrixCacheValid = false;
        
        // Local animation optimization - per object only
        private struct LocalAnimationState
        {
            public int ActionIndex;
            public int Frame0;
            public int Frame1; 
            public float InterpolationFactor;
            public double AnimTime;
            
            public bool Equals(LocalAnimationState other)
            {
                return ActionIndex == other.ActionIndex &&
                       Frame0 == other.Frame0 &&
                       Frame1 == other.Frame1 &&
                       Math.Abs(InterpolationFactor - other.InterpolationFactor) < 0.001f; // More strict
            }
        }
        
        private LocalAnimationState _lastAnimationState;
        private bool _animationStateValid = false;
        private Matrix[] _tempBoneTransforms = null; // Reusable temp array
        

        // Buffer invalidation flags
        private const uint BUFFER_FLAG_ANIMATION = 1u << 0;      // Animation/bones changed
        private const uint BUFFER_FLAG_LIGHTING = 1u << 1;      // Lighting changed  
        private const uint BUFFER_FLAG_TRANSFORM = 1u << 2;     // World transform changed
        private const uint BUFFER_FLAG_MATERIAL = 1u << 3;      // Material properties changed
        private const uint BUFFER_FLAG_TEXTURE = 1u << 4;       // Texture changed
        private const uint BUFFER_FLAG_ALL = uint.MaxValue;     // Force full rebuild

        // Per-mesh buffer cache
        private struct MeshBufferCache
        {
            public DynamicVertexBuffer VertexBuffer;
            public DynamicIndexBuffer IndexBuffer;
            public Vector3 CachedLight;
            public Color CachedBodyColor;
            public uint LastUpdateFrame;
            public bool IsValid;
        }
        private MeshBufferCache[] _meshBufferCache;

        public ModelObject()
        {
            _logger = AppLoggerFactory?.CreateLogger(GetType());
            MatrixChanged += (_s, _e) => UpdateWorldPosition();
        }

        private Vector3 _lastFrameLight = Vector3.Zero;

        public override async Task LoadContent()
        {
            await base.LoadContent();

            if (Model == null)
            {
                // This is a valid state, e.g., when an item is unequipped.
                // Clear out graphics resources to ensure it becomes invisible.
                _boneVertexBuffers = null;
                _boneIndexBuffers = null;
                _boneTextures = null;
                _scriptTextures = null;
                _dataTextures = null;
                _logger?.LogDebug($"Model is null for {ObjectName}. Clearing buffers. This is likely an unequip action.");
                // Set to Ready because it's a valid, though non-renderable, state.
                Status = GameControlStatus.Ready;
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
            
            // Initialize mesh buffer cache
            _meshBufferCache = new MeshBufferCache[meshCount];
            for (int i = 0; i < meshCount; i++)
            {
                _meshBufferCache[i] = new MeshBufferCache { IsValid = false };
            }

            InvalidateBuffers(BUFFER_FLAG_ALL);
            _contentLoaded = true;

            if (Model?.Bones != null && Model.Bones.Length > 0)
            {
                BoneTransform = new Matrix[Model.Bones.Length];

                if (Model.Actions != null && Model.Actions.Length > 0)
                {
                    GenerateBoneMatrix(0, 0, 0, 0);
                }
                else
                {
                    for (int i = 0; i < Model.Bones.Length; i++)
                    {
                        var bone = Model.Bones[i];
                        var localMatrix = Matrix.Identity;

                        BoneTransform[i] = (bone.Parent != -1 && bone.Parent < BoneTransform.Length)
                            ? localMatrix * BoneTransform[bone.Parent]
                            : localMatrix;
                    }
                }
            }

            UpdateBoundings();
        }

        public override void Update(GameTime gameTime)
        {
            if (World == null || !_contentLoaded) return;

            bool isVisible = Visible;

            // Process animation for the parent first. This ensures its BoneTransform is up-to-date.
            if (isVisible && !LinkParentAnimation)
            {
                Animation(gameTime);
            }

            base.Update(gameTime);

            if (isVisible)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i] is ModelObject childModel)
                    {
                        if (childModel.ParentBoneLink >= 0 || childModel.LinkParentAnimation)
                        {
                            childModel.CurrentAction = this.CurrentAction;
                            childModel._animTime = this._animTime;
                            childModel._isBlending = this._isBlending;
                            childModel._blendElapsed = this._blendElapsed;

                            childModel.RecalculateWorldPosition();

                            if (this._isBlending || this.BoneTransform != null)
                            {
                                childModel.InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                            }
                        }
                    }
                }
            }

            if (!isVisible) return;

            // Like old code: Check if lighting has changed significantly (for static objects)
            Vector3 currentLight = LightEnabled && World?.Terrain != null
                ? World.Terrain.EvaluateTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y) + Light
                : Light;

            // Add dynamic lighting to currentLight for CPU-based lighting
            bool hasDynamicLightingShader = Constants.ENABLE_DYNAMIC_LIGHTING_SHADER &&
                                           GraphicsManager.Instance.DynamicLightingEffect != null;

            if (!hasDynamicLightingShader && LightEnabled && World?.Terrain != null)
            {
                currentLight += World.Terrain.EvaluateDynamicLight(new Vector2(WorldPosition.Translation.X, WorldPosition.Translation.Y));
            }

            if (!LinkParentAnimation && _contentLoaded)
            {
                bool lightChanged = Vector3.DistanceSquared(currentLight, _lastFrameLight) > 0.0001f;
                if (lightChanged)
                {
                    InvalidateBuffers(BUFFER_FLAG_LIGHTING);
                    _lastFrameLight = currentLight;
                }
            }

            // Like old code: always call SetDynamicBuffers when content is loaded
            if (_contentLoaded)
            {
                SetDynamicBuffers();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _boneIndexBuffers == null) return;

            var gd = GraphicsDevice;
            var prevCull = gd.RasterizerState;
            gd.RasterizerState = _cullClockwise;

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
                                   !(this is MonsterObject m && m.IsDead);
            Matrix highlightMatrix = Matrix.Identity;
            Vector3 highlightColor = Vector3.One;

            if (highlightAllowed)
            {
                const float scaleHighlight = 0.015f;
                const float scaleFactor = 1f + scaleHighlight;
                highlightMatrix = Matrix.CreateScale(scaleFactor) *
                    Matrix.CreateTranslation(-scaleHighlight, -scaleHighlight, -scaleHighlight) *
                    worldPos;
                highlightColor = this is MonsterObject ? _redHighlight : _greenHighlight;
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
        /// Gets depth bias for different object types to reduce Z-fighting
        /// </summary>
        protected virtual float GetDepthBias()
        {
            // Small bias values - negative values bring objects closer to camera
            var objectType = GetType();
            
            if (objectType == typeof(PlayerObject))
                return -0.00001f;  // Players slightly closer
            if (objectType == typeof(DroppedItemObject))
                return -0.00002f;  // Items even closer  
            if (objectType == typeof(NPCObject))
                return -0.000005f; // NPCs slightly closer than terrain
            
            return 0f; // Default - no bias for terrain and other objects
        }

        /// <summary>
        /// Determines if item material effect should be applied to a specific mesh
        /// </summary>
        protected virtual bool ShouldApplyItemMaterial(int meshIndex)
        {
            // By default, apply to all meshes
            // Override in specific classes to exclude certain meshes
            return true;
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
                
                // Apply small depth bias based on object type to reduce Z-fighting
                var prevRasterizer = gd.RasterizerState;
                var depthBias = GetDepthBias();
                if (depthBias != 0f)
                {
                    var customRasterizer = new RasterizerState
                    {
                        CullMode = prevRasterizer.CullMode,
                        FillMode = prevRasterizer.FillMode,
                        DepthBias = depthBias,
                        SlopeScaleDepthBias = depthBias * 0.1f
                    };
                    gd.RasterizerState = customRasterizer;
                }

                // Use dynamic lighting effect if shader is enabled and available
                bool useDynamicLighting = Constants.ENABLE_DYNAMIC_LIGHTING_SHADER &&
                                        GraphicsManager.Instance.DynamicLightingEffect != null;

                // Debug: Force dynamic lighting for testing pulsing
                // useDynamicLighting = useDynamicLighting || (GraphicsManager.Instance.DynamicLightingEffect != null);

                // Use item material effect only for items with level 7+, excellent, or ancient
                // but exclude certain meshes (like face mesh on helmets)
                bool useItemMaterial = Constants.ENABLE_ITEM_MATERIAL_SHADER &&
                                     (ItemLevel >= 7 || IsExcellentItem || IsAncientItem) &&
                                     GraphicsManager.Instance.ItemMaterialEffect != null &&
                                     ShouldApplyItemMaterial(mesh);

                // Use monster material effect if custom shader is enabled
                bool useMonsterMaterial = Constants.ENABLE_MONSTER_MATERIAL_SHADER &&
                                        EnableCustomShader &&
                                        GraphicsManager.Instance.MonsterMaterialEffect != null;

                if (useDynamicLighting && !useItemMaterial && !useMonsterMaterial)
                {
                    DrawMeshWithDynamicLighting(mesh);
                    return;
                }

                if (useItemMaterial)
                {
                    DrawMeshWithItemMaterial(mesh);
                    return;
                }

                if (useMonsterMaterial)
                {
                    DrawMeshWithMonsterMaterial(mesh);
                    return;
                }

                var effect = GraphicsManager.Instance.AlphaTestEffect3D;

                // Cache frequently used values
                bool isBlendMesh = IsBlendMesh(mesh);
                var vertexBuffer = _boneVertexBuffers[mesh];
                var indexBuffer = _boneIndexBuffers[mesh];
                var texture = _boneTextures[mesh];

                // Batch state changes - save current states  
                var originalRasterizer = gd.RasterizerState;
                var prevBlend = gd.BlendState;
                float prevAlpha = effect.Alpha;

                // for custom blending from json
                // Apply new states from config first
                var meshConf = Model.Meshes[mesh];
                
                // Check if mesh should be two-sided based on RGBA, blend mesh, or JSON config
                bool isTwoSided = _meshIsRGBA[mesh] || isBlendMesh;
                if (meshConf.BlendingMode != null && meshConf.BlendingMode != "Opaque")
                {
                    isTwoSided = true;
                }
                BlendState customBlendState = null;
                if (meshConf.BlendingMode != null)
                {
                    if (!_blendStateCache.TryGetValue(meshConf.BlendingMode, out customBlendState))
                    {
                        var field = typeof(Blendings).GetField(meshConf.BlendingMode, BindingFlags.Public | BindingFlags.Static);
                        if (field != null)
                        {
                            customBlendState = (BlendState)field.GetValue(null);
                            _blendStateCache[meshConf.BlendingMode] = customBlendState;
                        }
                    }
                }

                // Apply final rasterizer state (considering depth bias and culling)
                if (depthBias != 0f)
                {
                    // Keep the custom rasterizer with depth bias but update culling
                    var finalRasterizer = new RasterizerState
                    {
                        CullMode = isTwoSided ? CullMode.None : CullMode.CullClockwiseFace,
                        FillMode = originalRasterizer.FillMode,
                        DepthBias = depthBias,
                        SlopeScaleDepthBias = depthBias * 0.1f
                    };
                    gd.RasterizerState = finalRasterizer;
                }
                else
                {
                    gd.RasterizerState = isTwoSided ? _cullNone : _cullClockwise;
                }
                gd.BlendState = customBlendState ?? (isBlendMesh ? BlendMeshState : BlendState);
                //

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
                gd.RasterizerState = originalRasterizer;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error in DrawMesh: {Message}", ex.Message);
            }
        }

        public virtual void DrawMeshWithItemMaterial(int mesh)
        {
            if (_boneVertexBuffers?[mesh] == null ||
                _boneIndexBuffers?[mesh] == null ||
                _boneTextures?[mesh] == null ||
                IsHiddenMesh(mesh))
                return;

            try
            {
                var gd = GraphicsDevice;
                var effect = GraphicsManager.Instance.ItemMaterialEffect;

                if (effect == null)
                {
                    DrawMesh(mesh);
                    return;
                }

                bool isBlendMesh = IsBlendMesh(mesh);
                var vertexBuffer = _boneVertexBuffers[mesh];
                var indexBuffer = _boneIndexBuffers[mesh];
                var texture = _boneTextures[mesh];

                var prevCull = gd.RasterizerState;
                var prevBlend = gd.BlendState;

                var meshConf = Model.Meshes[mesh];
                
                // Check if mesh should be two-sided based on RGBA, blend mesh, or JSON config
                bool isTwoSided = _meshIsRGBA[mesh] || isBlendMesh;
                if (meshConf.BlendingMode != null && meshConf.BlendingMode != "Opaque")
                {
                    isTwoSided = true;
                }
                BlendState customBlendState = null;
                if (meshConf.BlendingMode != null)
                {
                    if (!_blendStateCache.TryGetValue(meshConf.BlendingMode, out customBlendState))
                    {
                        var field = typeof(Blendings).GetField(meshConf.BlendingMode, BindingFlags.Public | BindingFlags.Static);
                        if (field != null)
                        {
                            customBlendState = (BlendState)field.GetValue(null);
                            _blendStateCache[meshConf.BlendingMode] = customBlendState;
                        }
                    }
                }

                gd.RasterizerState = isTwoSided ? _cullNone : _cullClockwise;
                gd.BlendState = customBlendState ?? (isBlendMesh ? BlendMeshState : BlendState);

                // Set world view projection matrix
                Matrix worldViewProjection = WorldPosition * Camera.Instance.View * Camera.Instance.Projection;
                effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
                effect.Parameters["World"]?.SetValue(WorldPosition);
                effect.Parameters["View"]?.SetValue(Camera.Instance.View);
                effect.Parameters["Projection"]?.SetValue(Camera.Instance.Projection);
                effect.Parameters["EyePosition"]?.SetValue(Camera.Instance.Position);

                // Set texture
                effect.Parameters["DiffuseTexture"]?.SetValue(texture);

                // Set item properties
                int itemOptions = ItemLevel & 0x0F;
                if (IsExcellentItem)
                    itemOptions |= 0x10;

                effect.Parameters["ItemOptions"]?.SetValue(itemOptions);
                effect.Parameters["Time"]?.SetValue(GetCachedTime());
                effect.Parameters["IsAncient"]?.SetValue(IsAncientItem);
                effect.Parameters["IsExcellent"]?.SetValue(IsExcellentItem);
                //effect.Parameters["GlowColor"]?.SetValue(GlowColor);

                gd.SetVertexBuffer(vertexBuffer);
                gd.Indices = indexBuffer;

                int primitiveCount = indexBuffer.IndexCount / 3;

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
                }

                gd.BlendState = prevBlend;
                gd.RasterizerState = prevCull;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error in DrawMeshWithItemMaterial: {Message}", ex.Message);
                DrawMesh(mesh);
            }
        }

        public virtual void DrawMeshWithMonsterMaterial(int mesh)
        {
            if (_boneVertexBuffers?[mesh] == null ||
                _boneIndexBuffers?[mesh] == null ||
                _boneTextures?[mesh] == null ||
                IsHiddenMesh(mesh))
                return;

            try
            {
                var gd = GraphicsDevice;
                var effect = GraphicsManager.Instance.MonsterMaterialEffect;

                if (effect == null)
                {
                    DrawMesh(mesh);
                    return;
                }

                bool isBlendMesh = IsBlendMesh(mesh);
                var vertexBuffer = _boneVertexBuffers[mesh];
                var indexBuffer = _boneIndexBuffers[mesh];
                var texture = _boneTextures[mesh];

                var prevCull = gd.RasterizerState;
                var prevBlend = gd.BlendState;

                var meshConf = Model.Meshes[mesh];
                
                // Check if mesh should be two-sided based on RGBA, blend mesh, or JSON config
                bool isTwoSided = _meshIsRGBA[mesh] || isBlendMesh;
                if (meshConf.BlendingMode != null && meshConf.BlendingMode != "Opaque")
                {
                    isTwoSided = true;
                }
                BlendState customBlendState = null;
                if (meshConf.BlendingMode != null)
                {
                    if (!_blendStateCache.TryGetValue(meshConf.BlendingMode, out customBlendState))
                    {
                        var field = typeof(Blendings).GetField(meshConf.BlendingMode, BindingFlags.Public | BindingFlags.Static);
                        if (field != null)
                        {
                            customBlendState = (BlendState)field.GetValue(null);
                            _blendStateCache[meshConf.BlendingMode] = customBlendState;
                        }
                    }
                }

                gd.RasterizerState = isTwoSided ? _cullNone : _cullClockwise;
                gd.BlendState = customBlendState ?? (isBlendMesh ? BlendMeshState : BlendState);

                // Set matrices
                effect.Parameters["World"]?.SetValue(WorldPosition);
                effect.Parameters["View"]?.SetValue(Camera.Instance.View);
                effect.Parameters["Projection"]?.SetValue(Camera.Instance.Projection);
                effect.Parameters["EyePosition"]?.SetValue(Camera.Instance.Position);

                // Set texture
                effect.Parameters["DiffuseTexture"]?.SetValue(texture);

                // Set monster-specific properties
                effect.Parameters["GlowColor"]?.SetValue(GlowColor);
                effect.Parameters["GlowIntensity"]?.SetValue(GlowIntensity);
                effect.Parameters["EnableGlow"]?.SetValue(GlowIntensity > 0.0f);
                effect.Parameters["Time"]?.SetValue(GetCachedTime());

                gd.SetVertexBuffer(vertexBuffer);
                gd.Indices = indexBuffer;

                int primitiveCount = indexBuffer.IndexCount / 3;

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
                }

                gd.BlendState = prevBlend;
                gd.RasterizerState = prevCull;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error in DrawMeshWithMonsterMaterial: {Message}", ex.Message);
                DrawMesh(mesh);
            }
        }

        public virtual void DrawMeshWithDynamicLighting(int mesh)
        {
            if (_boneVertexBuffers?[mesh] == null ||
                _boneIndexBuffers?[mesh] == null ||
                _boneTextures?[mesh] == null ||
                IsHiddenMesh(mesh))
                return;

            try
            {
                var gd = GraphicsDevice;
                var effect = GraphicsManager.Instance.DynamicLightingEffect;

                if (effect == null)
                {
                    DrawMesh(mesh); // Fallback to standard rendering
                    return;
                }

                bool isBlendMesh = IsBlendMesh(mesh);
                var vertexBuffer = _boneVertexBuffers[mesh];
                var indexBuffer = _boneIndexBuffers[mesh];
                var texture = _boneTextures[mesh];

                var prevCull = gd.RasterizerState;
                var prevBlend = gd.BlendState;

                var meshConf = Model.Meshes[mesh];
                
                // Check if mesh should be two-sided based on RGBA, blend mesh, or JSON config
                bool isTwoSided = _meshIsRGBA[mesh] || isBlendMesh;
                if (meshConf.BlendingMode != null && meshConf.BlendingMode != "Opaque")
                {
                    isTwoSided = true;
                }
                BlendState customBlendState = null;
                if (meshConf.BlendingMode != null)
                {
                    if (!_blendStateCache.TryGetValue(meshConf.BlendingMode, out customBlendState))
                    {
                        var field = typeof(Blendings).GetField(meshConf.BlendingMode, BindingFlags.Public | BindingFlags.Static);
                        if (field != null)
                        {
                            customBlendState = (BlendState)field.GetValue(null);
                            _blendStateCache[meshConf.BlendingMode] = customBlendState;
                        }
                    }
                }

                gd.RasterizerState = isTwoSided ? _cullNone : _cullClockwise;
                gd.BlendState = customBlendState ?? (isBlendMesh ? BlendMeshState : BlendState);

                // Set transformation matrices
                effect.Parameters["World"]?.SetValue(WorldPosition);
                effect.Parameters["View"]?.SetValue(Camera.Instance.View);
                effect.Parameters["Projection"]?.SetValue(Camera.Instance.Projection);
                Matrix worldViewProjection = WorldPosition * Camera.Instance.View * Camera.Instance.Projection;
                effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
                effect.Parameters["EyePosition"]?.SetValue(Camera.Instance.Position);

                // Set texture
                effect.Parameters["DiffuseTexture"]?.SetValue(texture);
                effect.Parameters["Alpha"]?.SetValue(TotalAlpha);

                // Set terrain lighting
                Vector3 worldTranslation = WorldPosition.Translation;
                Vector3 terrainLight = Vector3.One;
                if (LightEnabled && World?.Terrain != null)
                {
                    terrainLight = World.Terrain.EvaluateTerrainLight(worldTranslation.X, worldTranslation.Y);
                }
                // Set ambient lighting for dynamic lighting shader
                effect.Parameters["AmbientLight"]?.SetValue(_ambientLightVector);

                // Ensure terrain light is reasonable - don't divide by 255 as it makes it too dark
                terrainLight = Vector3.Clamp(terrainLight / 255f, Vector3.Zero, Vector3.One);
                effect.Parameters["TerrainLight"]?.SetValue(terrainLight);

                // Set dynamic lights
                var activeLights = World?.Terrain?.ActiveLights;
                if (activeLights != null && activeLights.Count > 0)
                {
                    int maxLights = Constants.OPTIMIZE_FOR_INTEGRATED_GPU ? 4 : 16;
                    int lightCount = Math.Min(activeLights.Count, maxLights);
                    effect.Parameters["ActiveLightCount"]?.SetValue(lightCount);
                    effect.Parameters["MaxLightsToProcess"]?.SetValue(maxLights);

                    // Use cached arrays to avoid allocations
                    for (int i = 0; i < lightCount; i++)
                    {
                        var light = activeLights[i];
                        _cachedLightPositions[i] = light.Position;
                        _cachedLightColors[i] = light.Color; // Already in 0-1 range
                        _cachedLightRadii[i] = light.Radius;
                        _cachedLightIntensities[i] = light.Intensity;
                    }

                    effect.Parameters["LightPositions"]?.SetValue(_cachedLightPositions);
                    effect.Parameters["LightColors"]?.SetValue(_cachedLightColors);
                    effect.Parameters["LightRadii"]?.SetValue(_cachedLightRadii);
                    effect.Parameters["LightIntensities"]?.SetValue(_cachedLightIntensities);
                }
                else
                {
                    effect.Parameters["ActiveLightCount"]?.SetValue(0);
                    effect.Parameters["MaxLightsToProcess"]?.SetValue(Constants.OPTIMIZE_FOR_INTEGRATED_GPU ? 4 : 16);
                }

                // Set debug lighting areas parameter
                effect.Parameters["DebugLightingAreas"]?.SetValue(Constants.DEBUG_LIGHTING_AREAS);

                gd.SetVertexBuffer(vertexBuffer);
                gd.Indices = indexBuffer;

                int primitiveCount = indexBuffer.IndexCount / 3;

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
                }

                gd.BlendState = prevBlend;
                gd.RasterizerState = prevCull;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error in DrawMeshWithDynamicLighting: {Message}", ex.Message);
                DrawMesh(mesh); // Fallback to standard rendering
            }
        }

        public virtual void DrawMeshHighlight(int mesh, Matrix highlightMatrix, Vector3 highlightColor)
        {
            if (IsHiddenMesh(mesh) || _boneVertexBuffers == null || _boneIndexBuffers == null || _boneTextures == null)
                return;

            VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
            IndexBuffer indexBuffer = _boneIndexBuffers[mesh];

            if (vertexBuffer == null || indexBuffer == null)
                return;

            int primitiveCount = indexBuffer.IndexCount / 3;

            // Save previous graphics states
            var previousDepthState = GraphicsDevice.DepthStencilState;
            var previousBlendState = GraphicsDevice.BlendState;

            var alphaTestEffect = GraphicsManager.Instance.AlphaTestEffect3D;
            if (alphaTestEffect == null || alphaTestEffect.CurrentTechnique == null) return; // Ensure effect and technique are not null

            float prevAlpha = alphaTestEffect.Alpha;

            alphaTestEffect.World = highlightMatrix;
            alphaTestEffect.Texture = _boneTextures[mesh];
            alphaTestEffect.DiffuseColor = highlightColor;
            alphaTestEffect.Alpha = 1f;

            // Configure depth and blend states for drawing the highlight
            GraphicsDevice.DepthStencilState = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = false
            };
            GraphicsDevice.BlendState = BlendState.Additive;

            // Draw the mesh highlight
            foreach (EffectPass pass in alphaTestEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.SetVertexBuffer(vertexBuffer);
                GraphicsDevice.Indices = indexBuffer;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
            }

            alphaTestEffect.Alpha = prevAlpha;

            // Restore previous graphics states
            GraphicsDevice.DepthStencilState = previousDepthState;
            GraphicsDevice.BlendState = previousBlendState;

            alphaTestEffect.World = WorldPosition;
            alphaTestEffect.DiffuseColor = Vector3.One;
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
                // Skip shadow rendering if shadows are disabled for this world
                if (MuGame.Instance.ActiveScene?.World is WorldControl world && !world.EnableShadows)
                    return;

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
            _invalidatedBufferFlags = 0;

            // Clear cache references
            _cachedBoneMatrix = null;
            _boneMatrixCacheValid = false;
            _meshBufferCache = null;
            _tempBoneTransforms = null;
            _animationStateValid = false;
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

            // Use faster min/max calculation with cached vectors
            Vector3 min = _maxValueVector;
            Vector3 max = _minValueVector;

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

            if (totalFrames == 1)
            {
                if (_priorAction != currentActionIndex)
                {
                    GenerateBoneMatrix(currentActionIndex, 0, 0, 0);
                    _priorAction = currentActionIndex;
                    InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                }
                return;
            }

            if (_priorAction != currentActionIndex)
            {
                _blendFromAction = _priorAction;
                _blendFromTime = _animTime;
                _blendElapsed = 0f;
                _isBlending = true;
                _animTime = 0.0;

                int boneCount = Model.Bones.Length;
                if (_blendFromBones == null || _blendFromBones.Length != boneCount)
                    _blendFromBones = new Matrix[boneCount];
            }

            _animTime += delta * (action.PlaySpeed == 0 ? 1.0f : action.PlaySpeed) * AnimationSpeed;
            double framePos = _animTime % totalFrames;
            int f0 = (int)framePos;
            int f1 = (f0 + 1) % totalFrames;
            float t = (float)(framePos - f0);

            GenerateBoneMatrix(currentActionIndex, f0, f1, t);

            if (_isBlending)
            {
                _blendElapsed += delta;
                float blendFactor = MathHelper.Clamp(_blendElapsed / _blendDuration, 0f, 1f);

                if (_blendFromAction >= 0 && _blendFromBones != null)
                {
                    var prevAction = Model.Actions[_blendFromAction];
                    _blendFromTime += delta * (prevAction.PlaySpeed == 0 ? 1.0f : prevAction.PlaySpeed) * AnimationSpeed;
                    int prevTotal = Math.Max(prevAction.NumAnimationKeys, 1);
                    double pf = _blendFromTime % prevTotal;
                    int pf0 = (int)pf;
                    int pf1 = (pf0 + 1) % prevTotal;
                    float pt = (float)(pf - pf0);
                    ComputeBoneMatrixTo(_blendFromAction, pf0, pf1, pt, _blendFromBones);

                    // blending
                    for (int i = 0; i < BoneTransform.Length; i++)
                    {
                        Matrix.Lerp(ref _blendFromBones[i], ref BoneTransform[i], blendFactor, out BoneTransform[i]);
                    }
                }

                if (blendFactor >= 1.0f)
                {
                    _isBlending = false;
                    _blendFromAction = -1;
                }

                InvalidateBuffers(BUFFER_FLAG_ANIMATION);
            }

            _priorAction = currentActionIndex;
        }

        protected void GenerateBoneMatrix(int actionIdx, int frame0, int frame1, float t)
        {
            if (Model?.Bones == null || Model.Actions == null || Model.Actions.Length == 0) 
            {
                // Reset animation cache for invalid models
                _animationStateValid = false;
                return;
            }

            actionIdx = Math.Clamp(actionIdx, 0, Model.Actions.Length - 1);
            var action = Model.Actions[actionIdx];
            var bones = Model.Bones;

            // Create animation state for comparison - only for animated objects
            LocalAnimationState currentAnimState = default;
            bool shouldCheckCache = !LinkParentAnimation && ParentBoneLink < 0 && 
                                   action.NumAnimationKeys > 1; // Only cache animated objects
            
            if (shouldCheckCache)
            {
                currentAnimState = new LocalAnimationState
                {
                    ActionIndex = actionIdx,
                    Frame0 = frame0,
                    Frame1 = frame1,
                    InterpolationFactor = t,
                    AnimTime = _animTime
                };

                // Check if we can skip expensive calculation using local cache
                // But be more conservative - only skip if frames and interpolation are identical
                if (_animationStateValid && currentAnimState.Equals(_lastAnimationState) && 
                    BoneTransform != null && BoneTransform.Length == bones.Length)
                {
                    // Animation state hasn't changed - no need to recalculate
                    return;
                }
            }

            // Initialize or resize bone transform array if needed
            if (BoneTransform == null || BoneTransform.Length != bones.Length)
                BoneTransform = new Matrix[bones.Length];

            // Initialize temp array for safer calculations - only when needed
            if (_tempBoneTransforms == null || _tempBoneTransforms.Length != bones.Length)
                _tempBoneTransforms = new Matrix[bones.Length];

            bool lockPositions = action.LockPositions;
            float bodyHeight = BodyHeight;
            bool anyBoneChanged = false;

            // Pre-clamp frame indices to valid ranges
            int maxFrameIndex = action.NumAnimationKeys - 1;
            frame0 = Math.Clamp(frame0, 0, maxFrameIndex);
            frame1 = Math.Clamp(frame1, 0, maxFrameIndex);
            
            // If frames are the same, no interpolation needed
            if (frame0 == frame1) t = 0f;

            // Process bones in order (parents before children)
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];

                // Skip invalid bones
                if (bone == BMDTextureBone.Dummy || bone.Matrixes == null || actionIdx >= bone.Matrixes.Length)
                {
                    _tempBoneTransforms[i] = Matrix.Identity;
                    if (BoneTransform[i] != Matrix.Identity)
                        anyBoneChanged = true;
                    continue;
                }

                var bm = bone.Matrixes[actionIdx];
                int numPosKeys = bm.Position?.Length ?? 0;
                int numQuatKeys = bm.Quaternion?.Length ?? 0;
                
                if (numPosKeys == 0 || numQuatKeys == 0)
                {
                    _tempBoneTransforms[i] = Matrix.Identity;
                    if (BoneTransform[i] != Matrix.Identity)
                        anyBoneChanged = true;
                    continue;
                }

                // Ensure frame indices are valid for this specific bone
                int boneMaxFrame = Math.Min(numPosKeys, numQuatKeys) - 1;
                int boneFrame0 = Math.Min(frame0, boneMaxFrame);
                int boneFrame1 = Math.Min(frame1, boneMaxFrame);
                float boneT = (boneFrame0 == boneFrame1) ? 0f : t;

                Matrix localTransform;

                // Optimize for common case: no interpolation needed
                if (boneT == 0f)
                {
                    // Direct keyframe - no interpolation
                    localTransform = Matrix.CreateFromQuaternion(bm.Quaternion[boneFrame0]);
                    localTransform.Translation = bm.Position[boneFrame0];
                }
                else
                {
                    // Interpolated keyframe - use more efficient linear interpolation for position
                    Quaternion q = Quaternion.Slerp(bm.Quaternion[boneFrame0], bm.Quaternion[boneFrame1], boneT);
                    Vector3 p0 = bm.Position[boneFrame0];
                    Vector3 p1 = bm.Position[boneFrame1];
                    
                    localTransform = Matrix.CreateFromQuaternion(q);
                    localTransform.M41 = p0.X + (p1.X - p0.X) * boneT;
                    localTransform.M42 = p0.Y + (p1.Y - p0.Y) * boneT;
                    localTransform.M43 = p0.Z + (p1.Z - p0.Z) * boneT;
                }

                // Apply position locking for root bone
                if (i == 0 && lockPositions && bm.Position.Length > 0)
                {
                    var rootPos = bm.Position[0];
                    localTransform.Translation = new Vector3(rootPos.X, rootPos.Y, localTransform.M43 + bodyHeight);
                }

                // Apply parent transformation with safety checks
                Matrix worldTransform;
                if (bone.Parent >= 0 && bone.Parent < _tempBoneTransforms.Length)
                {
                    worldTransform = localTransform * _tempBoneTransforms[bone.Parent];
                }
                else
                {
                    worldTransform = localTransform;
                }

                // Store in temp array
                _tempBoneTransforms[i] = worldTransform;
                
                // Check if this bone actually changed (simple comparison for performance)
                if (BoneTransform[i] != worldTransform)
                {
                    anyBoneChanged = true;
                }
            }

            // For static objects (single frame) or first-time setup, always update
            bool forceUpdate = action.NumAnimationKeys <= 1 || !_animationStateValid;
            
            // Only update final transforms and invalidate if something actually changed OR force update
            if (anyBoneChanged || forceUpdate)
            {
                Array.Copy(_tempBoneTransforms, BoneTransform, bones.Length);
                
                // Always invalidate for static objects or when forced
                if (anyBoneChanged || forceUpdate)
                {
                    InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                    UpdateBoundings();
                }
            }

            // Always update cache for objects that should use it
            if (shouldCheckCache)
            {
                _lastAnimationState = currentAnimState;
                _animationStateValid = true;
            }
            else if (action.NumAnimationKeys <= 1)
            {
                // Mark static objects as having valid animation state
                _animationStateValid = true;
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
            if (_invalidatedBufferFlags == 0 || Model?.Meshes == null)
                return;

            try
            {
                int meshCount = Model.Meshes.Length;
                if (meshCount == 0) return;

                // Early exit if not visible - huge optimization
                if (!Visible || OutOfView)
                {
                    _invalidatedBufferFlags = 0;
                    return;
                }

                uint currentFrame = (uint)(MuGame.Instance.GameTime.TotalGameTime.TotalMilliseconds / 16.67f);
                
                // Ensure arrays only when needed
                bool needArrayResize = _boneVertexBuffers?.Length != meshCount;
                if (needArrayResize)
                {
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
                }

                // Get bone transforms with caching
                Matrix[] bones = GetCachedBoneTransforms();
                if (bones == null)
                {
                    _logger?.LogDebug("SetDynamicBuffers: BoneTransform == null – skip");
                    return;
                }

                // Calculate lighting only once if lighting flags are set
                Vector3 baseLight = Vector3.Zero;
                bool needLightCalculation = (_invalidatedBufferFlags & BUFFER_FLAG_LIGHTING) != 0;
                
                if (needLightCalculation)
                {
                    Vector3 worldTranslation = WorldPosition.Translation;
                    baseLight = LightEnabled && World?.Terrain != null
                        ? World.Terrain.EvaluateTerrainLight(worldTranslation.X, worldTranslation.Y) + Light
                        : Light;
                }

                // Pre-calculate common color components
                float colorR = Color.R;
                float colorG = Color.G;
                float colorB = Color.B;
                float totalAlpha = TotalAlpha;
                float blendMeshLight = BlendMeshLight;

                // Process only meshes that need updates
                for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
                {
                    try
                    {
                        ref var cache = ref _meshBufferCache[meshIndex];
                        var mesh = Model.Meshes[meshIndex];

                        // Skip if mesh is hidden and we're not doing texture updates
                        if (IsHiddenMesh(meshIndex) && (_invalidatedBufferFlags & BUFFER_FLAG_TEXTURE) == 0)
                            continue;

                        // Calculate mesh-specific lighting
                        bool isBlend = IsBlendMesh(meshIndex);
                        Vector3 meshLight = needLightCalculation 
                            ? (isBlend ? baseLight * blendMeshLight : baseLight * totalAlpha)
                            : cache.CachedLight;

                        // Check if this specific mesh needs update
                        bool meshNeedsUpdate = !cache.IsValid ||
                                             currentFrame - cache.LastUpdateFrame > 2 || // Refresh every few frames
                                             (needLightCalculation && Vector3.DistanceSquared(meshLight, cache.CachedLight) > 0.01f) ||
                                             (_invalidatedBufferFlags & (BUFFER_FLAG_ANIMATION | BUFFER_FLAG_TRANSFORM)) != 0;

                        if (!meshNeedsUpdate)
                            continue;

                        // Optimized color calculation with clamping
                        byte r = (byte)MathF.Min(colorR * meshLight.X, 255f);
                        byte g = (byte)MathF.Min(colorG * meshLight.Y, 255f);
                        byte b = (byte)MathF.Min(colorB * meshLight.Z, 255f);
                        Color bodyColor = new Color(r, g, b);

                        // Skip expensive buffer generation if color hasn't changed much
                        bool colorChanged = Math.Abs(cache.CachedBodyColor.PackedValue - bodyColor.PackedValue) > 0;
                        if (!colorChanged && cache.IsValid && (_invalidatedBufferFlags & BUFFER_FLAG_ANIMATION) == 0)
                            continue;

                        // Generate buffers only when necessary
                        BMDLoader.Instance.GetModelBuffers(
                            Model, meshIndex, bodyColor, bones,
                            ref _boneVertexBuffers[meshIndex],
                            ref _boneIndexBuffers[meshIndex],
                            false);

                        // Update cache
                        cache.VertexBuffer = _boneVertexBuffers[meshIndex];
                        cache.IndexBuffer = _boneIndexBuffers[meshIndex];
                        cache.CachedLight = meshLight;
                        cache.CachedBodyColor = bodyColor;
                        cache.LastUpdateFrame = currentFrame;
                        cache.IsValid = true;

                        // Load textures only if needed (avoid redundant loading)
                        if (_boneTextures[meshIndex] == null || (_invalidatedBufferFlags & BUFFER_FLAG_TEXTURE) != 0)
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

                _invalidatedBufferFlags = 0; // Clear all flags
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"SetDynamicBuffers FATAL: {ex.Message}");
            }
        }

        private Matrix[] GetCachedBoneTransforms()
        {
            Matrix[] bones = (LinkParentAnimation && Parent is ModelObject parentModel && parentModel.BoneTransform != null)
                ? parentModel.BoneTransform
                : BoneTransform;

            if (bones == null) return null;

            float currentAnimTime = (float)_animTime;

            // For child objects that link to parent animation OR have ParentBoneLink, always use fresh bone transforms
            // This ensures weapons and accessories animate properly during blending
            if (LinkParentAnimation || ParentBoneLink >= 0)
            {
                return bones;
            }

            // Check if we can use cached bone matrix for main objects
            // But be more conservative - only cache if animation time hasn't changed at all
            if (_boneMatrixCacheValid &&
                _lastCachedAction == CurrentAction &&
                Math.Abs(_lastCachedAnimTime - currentAnimTime) < 0.0001f &&
                _cachedBoneMatrix != null &&
                _cachedBoneMatrix.Length == bones.Length)
            {
                return _cachedBoneMatrix;
            }

            // Update cache
            if (_cachedBoneMatrix == null || _cachedBoneMatrix.Length != bones.Length)
            {
                _cachedBoneMatrix = new Matrix[bones.Length];
            }

            Array.Copy(bones, _cachedBoneMatrix, bones.Length);

            _lastCachedAction = CurrentAction;
            _lastCachedAnimTime = currentAnimTime;
            _boneMatrixCacheValid = true;

            return _cachedBoneMatrix;
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

        public void InvalidateBuffers(uint flags = BUFFER_FLAG_ALL)
        {
            _invalidatedBufferFlags |= flags;

            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is ModelObject modelObject && (modelObject.LinkParentAnimation || modelObject.ParentBoneLink >= 0))
                {
                    modelObject.InvalidateBuffers(flags);
                }
            }
        }

        protected override void RecalculateWorldPosition()
        {
            Matrix localMatrix = Matrix.CreateScale(Scale) *
                                 Matrix.CreateFromQuaternion(MathUtils.AngleQuaternion(Angle)) *
                                 Matrix.CreateTranslation(Position);

            Matrix newWorldPosition;
            if (Parent != null)
            {
                newWorldPosition = localMatrix * ParentBodyOrigin * Parent.WorldPosition;
            }
            else
            {
                newWorldPosition = localMatrix;
            }

            if (WorldPosition != newWorldPosition)
            {
                WorldPosition = newWorldPosition;
                InvalidateBuffers(BUFFER_FLAG_TRANSFORM);
            }
        }
    }
}
