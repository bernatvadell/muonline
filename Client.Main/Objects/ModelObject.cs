using Client.Data.BMD;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Player;
using System.Buffers;
using Client.Main.Controls.UI.Game.Inventory;

namespace Client.Main.Objects
{
    /// <summary>
    /// Base class for 3D model objects with skeletal animation support.
    /// Split into partial classes for maintainability:
    /// - ModelObject.cs (this file): Core fields, properties, lifecycle
    /// - ModelObject.Rendering.cs: Draw methods, shader selection, mesh grouping
    /// - ModelObject.Animation.cs: Animation, bone matrix generation, blending
    /// - ModelObject.Buffers.cs: Dynamic buffer management, caching
    /// - ModelObject.Lighting.cs: Dynamic lighting effect preparation
    /// - ModelObject.Shadow.cs: Shadow rendering and matrix calculation
    /// </summary>
    public abstract partial class ModelObject : WorldObject
    {
        #region Static Fields and Caches

        // Object pooling for Matrix arrays to reduce GC pressure
        // ArrayPool is thread-safe and extremely efficient for temporary arrays
        private static readonly ArrayPool<Matrix> _matrixArrayPool = ArrayPool<Matrix>.Shared;
        private static readonly Dictionary<string, BlendState> _blendStateCache = new Dictionary<string, BlendState>();

        // Cached arrays for dynamic lighting to avoid allocations
        private static readonly Vector3[] _cachedLightPositions = new Vector3[16];
        private static readonly Vector3[] _cachedLightColors = new Vector3[16];
        private static readonly float[] _cachedLightRadii = new float[16];
        private static readonly float[] _cachedLightIntensities = new float[16];
        private static readonly float[] _cachedLightScores = new float[16];
        private const float MinLightInfluence = 0.001f;

        // Cache for Environment.TickCount to reduce system calls
        private static float _cachedTime = 0f;
        private static int _lastTickCount = 0;

        // Cached common Vector3 instances to avoid allocations
        private static readonly Vector3 _ambientLightVector = new Vector3(0.8f, 0.8f, 0.8f);
        private static readonly Vector3 _redHighlight = new Vector3(1, 0, 0);
        private static readonly Vector3 _greenHighlight = new Vector3(0, 1, 0);
        private static readonly Vector3 _maxValueVector = new Vector3(float.MaxValue);
        private static readonly Vector3 _minValueVector = new Vector3(float.MinValue);
        private static readonly Vector3 _sunColor = new Vector3(1f, 0.95f, 0.85f);

        // Cache common graphics states to avoid repeated property access
        private static readonly RasterizerState _cullClockwise = RasterizerState.CullClockwise;
        private static readonly RasterizerState _cullNone = RasterizerState.CullNone;

        private static int _animationStrideSeed = 0;

        // Track per-pass preparation to avoid re-uploading shared effect parameters per mesh
        private static int _drawModelInvocationCounter = 0;

        public static ILoggerFactory AppLoggerFactory { get; private set; }

        public static void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            AppLoggerFactory = loggerFactory;
        }

        #endregion

        #region Instance Fields - Graphics Resources

        private DynamicVertexBuffer[] _boneVertexBuffers;
        private DynamicIndexBuffer[] _boneIndexBuffers;
        private Texture2D[] _boneTextures;
        private TextureScript[] _scriptTextures;
        private TextureData[] _dataTextures;

        // Cached hint for world-level batching/sorting (avoids scanning mesh textures during Sort comparisons)
        private Texture2D _sortTextureHint;
        private bool _sortTextureHintDirty = true;

        private bool[] _meshIsRGBA;
        private bool[] _meshHiddenByScript;
        private bool[] _meshBlendByScript;
        private string[] _meshTexturePath;

        private int[] _blendMeshIndicesScratch;

        #endregion

        #region Instance Fields - Animation State

        private int _blendFromAction = -1;
        private double _blendFromTime = 0.0;
        private Matrix[] _blendFromBones = null;
        private bool _isBlending = false;
        private float _blendElapsed = 0f;
        private float _blendDuration = 0.25f;

        protected int _priorActionIndex = 0;
        protected double _animTime = 0.0;

        private LocalAnimationState _lastAnimationState;
        private bool _animationStateValid = false;

        #endregion

        #region Instance Fields - Buffer State

        private bool _renderShadow = false;
        private uint _invalidatedBufferFlags = uint.MaxValue; // Start with all flags set
        private float _blendMeshLight = 1f;
        private bool _contentLoaded = false;
        private bool _boundingComputed = false;

        // Buffer invalidation flags
        private const uint BUFFER_FLAG_ANIMATION = 1u << 0;      // Animation/bones changed
        private const uint BUFFER_FLAG_LIGHTING = 1u << 1;       // Lighting changed
        private const uint BUFFER_FLAG_TRANSFORM = 1u << 2;      // World transform changed
        private const uint BUFFER_FLAG_MATERIAL = 1u << 3;       // Material properties changed
        private const uint BUFFER_FLAG_TEXTURE = 1u << 4;        // Texture changed
        private const uint BUFFER_FLAG_ALL = uint.MaxValue;      // Force full rebuild

        // Exposed equivalents for derived classes (to avoid magic numbers)
        protected const uint BufferFlagAnimation = BUFFER_FLAG_ANIMATION;
        protected const uint BufferFlagLighting = BUFFER_FLAG_LIGHTING;
        protected const uint BufferFlagTransform = BUFFER_FLAG_TRANSFORM;
        protected const uint BufferFlagMaterial = BUFFER_FLAG_MATERIAL;
        protected const uint BufferFlagTexture = BUFFER_FLAG_TEXTURE;
        protected const uint BufferFlagAll = BUFFER_FLAG_ALL;

        // Bounding box update optimization
        private int _boundingFrameCounter = BoundingUpdateInterval;
        private const int BoundingUpdateInterval = 4;

        // Enhanced animation caching system
        private Matrix[] _cachedBoneMatrix = null;
        private int _lastCachedAction = -1;
        private float _lastCachedAnimTime = -1;
        private bool _boneMatrixCacheValid = false;

        private MeshBufferCache[] _meshBufferCache;

        #endregion

        #region Instance Fields - Lighting State

        private Vector3 _lastFrameLight = Vector3.Zero;
        private double _lastLightUpdateTime = 0;

        // Quantized lighting sample (reduces CPU work without visible change)
        private const float _LIGHT_SAMPLE_GRID = 8f; // world units per cell
        private Vector2 _lastLightSampleCell = new Vector2(float.MaxValue);
        private Vector3 _lastSampledLight = Vector3.Zero;

        // Per-object cached light selection (updated on throttled light snapshot version changes)
        private int _dynamicLightSelectionVersion = -1;
        private int _dynamicLightSelectionMaxLights = -1;
        private int _dynamicLightSelectionCount = 0;
        private int[] _dynamicLightSelectionIndices;

        private int _drawModelInvocationId = 0;
        private int _dynamicLightingPreparedInvocationId = -1;

        #endregion

        #region Instance Fields - Cached State

        // Cached ModelObject children to avoid per-frame type checks and allocations
        private ModelObject[] _cachedModelChildren = Array.Empty<ModelObject>();
        private int _cachedChildrenCount = -1;

        private double _lastAnimationUpdateTime = 0;
        private double _lastFrameTimeMs = 0; // To track timing in methods without GameTime
        private double _lastStrideAnimationBufferUpdateTimeMs = double.NegativeInfinity;

        private readonly int _animationStrideOffset;

        #endregion

        #region Properties

        public float ShadowOpacity { get; set; } = 1f;
        public Color Color { get; set; } = Color.White;
        public ItemDefinition ItemDefinition { get; set; }
        protected Matrix[] BoneTransform { get; set; }
        public Matrix[] GetBoneTransforms() => BoneTransform;
        public int CurrentAction { get; set; }
        public int CurrentFrame { get; private set; }
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

        private int _hiddenMesh = -1;
        private int _blendMesh = -1;

        public int HiddenMesh
        {
            get => _hiddenMesh;
            set
            {
                if (_hiddenMesh == value)
                    return;

                _hiddenMesh = value;
                _sortTextureHintDirty = true;
                _sortTextureHint = null;
            }
        }

        public int BlendMesh
        {
            get => _blendMesh;
            set => _blendMesh = value;
        }

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
        public bool ContinuousAnimation { get; set; }

        /// <summary>
        /// Indicates if this object can be rendered to a static cache surface.
        /// Objects with skeletal animation, continuous animation, or per-frame effects should return false.
        /// Override in subclasses that have dynamic visuals (flickering, fading, etc.).
        /// </summary>
        public virtual bool IsStaticForCaching => !ContinuousAnimation
            && (Model?.Bones == null || Model.Bones.Length == 0)
            && !RequiresPerFrameAnimation;

        /// <summary>
        /// When true, the animation will stop at the last frame instead of looping.
        /// Used for one-shot animations like skills or deaths.
        /// </summary>
        protected bool HoldOnLastFrame { get; set; }

        protected ILogger _logger;

        public int ItemLevel { get; set; } = 0;
        public bool IsExcellentItem { get; set; } = false;
        public bool IsAncientItem { get; set; } = false;

        // Monster/NPC glow properties
        public Vector3 GlowColor { get; set; } = new Vector3(1.0f, 0.8f, 0.0f); // Default gold
        public float GlowIntensity { get; set; } = 0.0f;
        public bool EnableCustomShader { get; set; } = false;
        public bool SimpleColorMode { get; set; } = false;
        public bool UseSunLight { get; set; } = true;

        public int AnimationUpdateStride { get; private set; } = 1;
        protected virtual bool RequiresPerFrameAnimation => false;

        #endregion

        #region Constructor

        public ModelObject()
        {
            _logger = AppLoggerFactory?.CreateLogger(GetType());
            MatrixChanged += (_s, _e) => UpdateWorldPosition();
            _animationStrideOffset = Interlocked.Increment(ref _animationStrideSeed) & 31;
        }

        #endregion

        #region Lifecycle Methods

        public override async Task LoadContent()
        {
            await base.LoadContent();

            ReleaseDynamicBuffers();

            if (Model == null)
            {
                // This is a valid state, e.g., when an item is unequipped.
                // Clear out graphics resources to ensure it becomes invisible.
                _boneVertexBuffers = null;
                _boneIndexBuffers = null;
                _boneTextures = null;
                _scriptTextures = null;
                _dataTextures = null;
                _logger?.LogDebug("Model is null for {ObjectName}. Clearing buffers. This is likely an unequip action.", ObjectName);
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

            // PERFORMANCE: Preload all textures during LoadContent to avoid SetData during gameplay
            var texturePreloadTasks = new List<Task>();

            for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
            {
                var mesh = Model.Meshes[meshIndex];
                string texturePath = BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);

                _meshTexturePath[meshIndex] = texturePath;

                // Preload texture data asynchronously to avoid lazy loading during render
                if (!string.IsNullOrEmpty(texturePath))
                {
                    texturePreloadTasks.Add(TextureLoader.Instance.Prepare(texturePath));
                }

                _boneTextures[meshIndex] = TextureLoader.Instance.GetTexture2D(texturePath);
                _scriptTextures[meshIndex] = TextureLoader.Instance.GetScript(texturePath);
                _dataTextures[meshIndex] = TextureLoader.Instance.Get(texturePath);

                _meshIsRGBA[meshIndex] = _dataTextures[meshIndex]?.Components == 4;
                _meshHiddenByScript[meshIndex] = _scriptTextures[meshIndex]?.HiddenMesh ?? false;
                _meshBlendByScript[meshIndex] = _scriptTextures[meshIndex]?.Bright ?? false;
            }

            // Wait for all textures to be preloaded
            if (texturePreloadTasks.Count > 0)
            {
                await Task.WhenAll(texturePreloadTasks);
            }

            _sortTextureHintDirty = true;
            _sortTextureHint = null;

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

                // Pre-allocate blend buffer to avoid allocations during action transitions
                if (_blendFromBones == null || _blendFromBones.Length != Model.Bones.Length)
                    _blendFromBones = new Matrix[Model.Bones.Length];

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
            // Centralized animation (includes cross-action blending). LinkParentAnimation skips.
            if (isVisible && !LinkParentAnimation)
            {
                Animation(gameTime);
            }

            base.Update(gameTime);

            if (isVisible)
            {
                // Update cached children if collection changed
                EnsureModelChildrenCache();

                // Use cached array to avoid per-frame type checks
                for (int i = 0; i < _cachedModelChildren.Length; i++)
                {
                    var childModel = _cachedModelChildren[i];
                    // if (!childModel.Visible) continue;

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

            // Throttle CPU skinning / buffer rebuild frequency for distant walkers (monsters/NPC/remote players).
            // This affects SetDynamicBuffers() later in this Update call via AnimationUpdateStride.
            if (this is WalkerObject walker)
            {
                int desiredStride = 1;
                if (!walker.IsMainWalker)
                {
                    // Keep nearby animations smooth; only throttle when low-quality is active.
                    desiredStride = walker.IsOneShotPlaying ? 1 : (LowQuality ? 4 : 1);
                }

                if (AnimationUpdateStride != desiredStride)
                    SetAnimationUpdateStride(desiredStride);

                // Apply the same stride to linked child models (equipment/attachments) to avoid rebuilding all parts every frame.
                if (isVisible && _cachedModelChildren.Length > 0)
                {
                    for (int i = 0; i < _cachedModelChildren.Length; i++)
                    {
                        var child = _cachedModelChildren[i];
                        if (child.ParentBoneLink < 0 && !child.LinkParentAnimation)
                            continue;

                        if (child.AnimationUpdateStride != desiredStride)
                            child.SetAnimationUpdateStride(desiredStride);
                    }
                }
            }

            if (!isVisible) return;

            // Like old code: Check if lighting has changed significantly (for static objects)
            bool hasDynamicLightingShader = Constants.ENABLE_DYNAMIC_LIGHTING_SHADER &&
                                           GraphicsManager.Instance.DynamicLightingEffect != null;

            Vector3 currentLight;

            // CPU lighting path (shader disabled): sample terrain light on a small grid
            if (!hasDynamicLightingShader && LightEnabled && World?.Terrain != null)
            {
                var pos = WorldPosition.Translation;
                var cell = new Vector2(
                    MathF.Floor(pos.X / _LIGHT_SAMPLE_GRID),
                    MathF.Floor(pos.Y / _LIGHT_SAMPLE_GRID));

                if (_lastLightSampleCell != cell)
                {
                    // Terrain base light
                    _lastSampledLight = World.Terrain.EvaluateTerrainLight(pos.X, pos.Y);
                    // Include dynamic lights on CPU path only
                    _lastSampledLight += World.Terrain.EvaluateDynamicLight(new Vector2(pos.X, pos.Y));
                    _lastLightSampleCell = cell;
                }

                currentLight = _lastSampledLight + Light;
            }
            else
            {
                currentLight = LightEnabled && World?.Terrain != null
                    ? World.Terrain.EvaluateTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y) + Light
                    : Light;
            }

            if (!LinkParentAnimation && _contentLoaded)
            {
                // PERFORMANCE: Only invalidate lighting for CPU lighting path - shader lighting doesn't need buffer rebuilds
                if (!hasDynamicLightingShader)
                {
                    // Reduce throttling for PlayerObjects to ensure proper rendering
                    bool isMainPlayer = this is PlayerObject p && p.IsMainWalker;
                    double lightUpdateInterval = isMainPlayer
                        ? 16.67
                        : (RequiresPerFrameAnimation ? 50 : 1000); // throttle static objects heavily
                    float lightThreshold = isMainPlayer ? 0.001f : 0.01f;   // More sensitive for main player

                    double currentTime = gameTime.TotalGameTime.TotalMilliseconds;
                    bool shouldCheckLight = currentTime - _lastLightUpdateTime > lightUpdateInterval;

                    if (shouldCheckLight)
                    {
                        bool lightChanged = Vector3.DistanceSquared(currentLight, _lastFrameLight) > lightThreshold;
                        if (lightChanged)
                        {
                            InvalidateBuffers(BUFFER_FLAG_LIGHTING);
                            _lastFrameLight = currentLight;
                        }
                        _lastLightUpdateTime = currentTime;
                    }
                }
            }

            // Track frame time for methods that need it
            _lastFrameTimeMs = gameTime.TotalGameTime.TotalMilliseconds;

            // Like old code: always call SetDynamicBuffers when content is loaded
            if (_contentLoaded)
            {
                SetDynamicBuffers();
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            Model = null;
            BoneTransform = null;
            _invalidatedBufferFlags = 0;
            ReleaseDynamicBuffers();

            // Release graphics resources and mark content as unloaded
            _boneVertexBuffers = null;
            _boneIndexBuffers = null;
            _boneTextures = null;
            _scriptTextures = null;
            _dataTextures = null;
            _meshIsRGBA = null;
            _meshHiddenByScript = null;
            _meshBlendByScript = null;
            _meshTexturePath = null;
            _blendMeshIndicesScratch = null;
            _contentLoaded = false;
            _boundingComputed = false;

            // Clear cache references
            _cachedBoneMatrix = null;
            _boneMatrixCacheValid = false;
            _meshBufferCache = null;
            _animationStateValid = false;

            ReleaseMeshGroups();
            _meshGroupPool.Clear();
        }

        #endregion

        #region Helper Methods

        private bool HasWalkerAncestor()
        {
            // Disable sun-lighting for objects that belong to player/NPC/monster hierarchies
            ModelObject current = this;
            while (current != null)
            {
                if (current is WalkerObject)
                    return true;
                current = current.Parent as ModelObject;
            }
            return false;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureModelChildrenCache()
        {
            // Check if children collection changed by comparing count
            int currentCount = Children.Count;
            if (_cachedChildrenCount == currentCount && _cachedModelChildren.Length > 0)
                return;

            // Rebuild cache - filter to ModelObject children only
            int modelObjectCount = 0;
            for (int i = 0; i < currentCount; i++)
            {
                if (Children[i] is ModelObject)
                    modelObjectCount++;
            }

            if (_cachedModelChildren.Length != modelObjectCount)
                _cachedModelChildren = new ModelObject[modelObjectCount];

            int index = 0;
            for (int i = 0; i < currentCount; i++)
            {
                if (Children[i] is ModelObject modelChild)
                    _cachedModelChildren[index++] = modelChild;
            }

            _cachedChildrenCount = currentCount;
        }

        public void SetAnimationUpdateStride(int stride)
        {
            int newStride = Math.Max(1, stride);
            if (AnimationUpdateStride == newStride)
                return;

            AnimationUpdateStride = newStride;
            _lastStrideAnimationBufferUpdateTimeMs = double.NegativeInfinity;
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
            if (!RequiresPerFrameAnimation && _contentLoaded && _boundingComputed)
                return;

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

            // Optimized: Only sample every 4th vertex for performance while maintaining accuracy
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                var mesh = meshes[meshIndex];
                var vertices = mesh.Vertices;
                if (vertices == null) continue;

                int step = Math.Max(1, vertices.Length / 32); // Sample max 32 vertices per mesh
                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex += step)
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
            {
                BoundingBoxLocal = new BoundingBox(min, max);
                _boundingComputed = true;
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

        #endregion
    }
}
