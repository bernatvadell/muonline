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
using System.Reflection;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Player;
using System.Buffers;
using Client.Main.Controls.UI.Game.Inventory;

namespace Client.Main.Objects
{
    public abstract class ModelObject : WorldObject
    {
        // Object pooling for Matrix arrays to reduce GC pressure
        // ArrayPool is thread-safe and extremely efficient for temporary arrays
        private static readonly ArrayPool<Matrix> _matrixArrayPool = ArrayPool<Matrix>.Shared;

#if DEBUG
        // Performance tracking for pooling (DEBUG only) - double buffered for accurate per-frame stats
        private static long _poolRentCount = 0;
        private static long _poolReturnCount = 0;
        private static long _lastFrameRentCount = 0;
        private static long _lastFrameReturnCount = 0;

        public static (long Rents, long Returns) GetPoolingStats() => (_lastFrameRentCount, _lastFrameReturnCount);

        public static void CaptureFrameStats()
        {
            // Capture current counts for display, then reset for next frame
            _lastFrameRentCount = Interlocked.Exchange(ref _poolRentCount, 0);
            _lastFrameReturnCount = Interlocked.Exchange(ref _poolReturnCount, 0);
        }
#endif

        private static readonly Dictionary<string, BlendState> _blendStateCache = new Dictionary<string, BlendState>();

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

        private bool _renderShadow = false;
        protected int _priorAction = 0;
        private uint _invalidatedBufferFlags = uint.MaxValue; // Start with all flags set
        private float _blendMeshLight = 1f;
        protected double _animTime = 0.0;
        private bool _contentLoaded = false;
        private bool _boundingComputed = false;
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

        // Hint for world-level batching: returns first visible mesh texture (if any)
        internal Texture2D GetSortTextureHint()
        {
            if (!_sortTextureHintDirty)
                return _sortTextureHint;

            _sortTextureHintDirty = false;
            _sortTextureHint = null;

            if (_boneTextures == null)
                return null;

            for (int i = 0; i < _boneTextures.Length; i++)
            {
                var tex = _boneTextures[i];
                if (tex != null && !IsHiddenMesh(i))
                {
                    _sortTextureHint = tex;
                    break;
                }
            }

            return _sortTextureHint;
        }

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
        public bool PreventLastFrameInterpolation { get; set; }
        /// <summary>
        /// When true, the animation will stop at the last frame instead of looping.
        /// Used for one-shot animations like skills or deaths.
        /// </summary>
        protected bool HoldOnLastFrame { get; set; }
        public static ILoggerFactory AppLoggerFactory { get; private set; }

        public static void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            AppLoggerFactory = loggerFactory;
        }
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

        // Cached arrays for dynamic lighting to avoid allocations
        private static readonly Vector3[] _cachedLightPositions = new Vector3[16];
        private static readonly Vector3[] _cachedLightColors = new Vector3[16];
        private static readonly float[] _cachedLightRadii = new float[16];
        private static readonly float[] _cachedLightIntensities = new float[16];
        private static readonly float[] _cachedLightScores = new float[16];
        private const float MinLightInfluence = 0.001f;

        // Per-object cached light selection (updated on throttled light snapshot version changes)
        private int _dynamicLightSelectionVersion = -1;
        private int _dynamicLightSelectionMaxLights = -1;
        private int _dynamicLightSelectionCount = 0;
        private int[] _dynamicLightSelectionIndices;

        // Track per-pass preparation to avoid re-uploading shared effect parameters per mesh
        private static int _drawModelInvocationCounter = 0;
        private int _drawModelInvocationId = 0;
        private int _dynamicLightingPreparedInvocationId = -1;

        // Cached ModelObject children to avoid per-frame type checks and allocations
        private ModelObject[] _cachedModelChildren = Array.Empty<ModelObject>();
        private int _cachedChildrenCount = -1;

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
        private BlendState GetMeshBlendState(int mesh, bool isBlendMesh)
        {
            if (Model?.Meshes == null || mesh < 0 || mesh >= Model.Meshes.Length)
                return isBlendMesh ? BlendMeshState : BlendState;

            var meshConf = Model.Meshes[mesh];

            // Check for custom blend state from JSON config
            if (meshConf.BlendingMode != null && _blendStateCache.TryGetValue(meshConf.BlendingMode, out var customBlendState))
                return customBlendState;

            // Cache custom blend states dynamically
            if (meshConf.BlendingMode != null && meshConf.BlendingMode != "Opaque")
            {
                var field = typeof(Blendings).GetField(meshConf.BlendingMode, BindingFlags.Public | BindingFlags.Static);
                if (field != null)
                {
                    customBlendState = (BlendState)field.GetValue(null);
                    _blendStateCache[meshConf.BlendingMode] = customBlendState;
                    return customBlendState;
                }
            }

            // Default to instance properties which can be changed dynamically by code
            // IMPORTANT: Use instance properties, not cached states, as they can be modified at runtime!
            return isBlendMesh ? BlendMeshState : BlendState;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsMeshTwoSided(int mesh, bool isBlendMesh)
        {
            if (_meshIsRGBA == null || mesh < 0 || mesh >= _meshIsRGBA.Length)
                return false;

            if (_meshIsRGBA[mesh] || isBlendMesh)
                return true;

            if (Model?.Meshes != null && mesh < Model.Meshes.Length)
            {
                var meshConf = Model.Meshes[mesh];
                return meshConf.BlendingMode != null && meshConf.BlendingMode != "Opaque";
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsTransparentMesh(int mesh, bool isBlendMesh)
        {
            if (isBlendMesh)
                return true;

            return _meshIsRGBA != null && (uint)mesh < (uint)_meshIsRGBA.Length && _meshIsRGBA[mesh];
        }

        private int _blendFromAction = -1;
        private double _blendFromTime = 0.0;
        private Matrix[] _blendFromBones = null;
        private bool _isBlending = false;
        private float _blendElapsed = 0f;
        private float _blendDuration = 0.25f;

        // Bounding box update optimization
        private int _boundingFrameCounter = BoundingUpdateInterval;
        private const int BoundingUpdateInterval = 4;

        // Animation and buffer optimization

        // Enhanced animation caching system
        private Matrix[] _cachedBoneMatrix = null;
        private int _lastCachedAction = -1;
        private float _lastCachedAnimTime = -1;
        private bool _boneMatrixCacheValid = false;

        // Local animation optimization - per object only
        private struct LocalAnimationState : IEquatable<LocalAnimationState>
        {
            public int ActionIndex;
            public int Frame0;
            public int Frame1;
            public float InterpolationFactor;

            public bool Equals(LocalAnimationState other)
            {
                return ActionIndex == other.ActionIndex &&
                       Frame0 == other.Frame0 &&
                       Frame1 == other.Frame1 &&
                       MathF.Abs(InterpolationFactor - other.InterpolationFactor) < 0.001f; // More strict, use MathF for float
            }

            public override bool Equals(object obj) => obj is LocalAnimationState other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(ActionIndex, Frame0, Frame1, InterpolationFactor);
        }

        private LocalAnimationState _lastAnimationState;
        private bool _animationStateValid = false;
        // Note: _tempBoneTransforms removed - now using ArrayPool for better memory management


        // Buffer invalidation flags
        private const uint BUFFER_FLAG_ANIMATION = 1u << 0;      // Animation/bones changed
        private const uint BUFFER_FLAG_LIGHTING = 1u << 1;      // Lighting changed  
        private const uint BUFFER_FLAG_TRANSFORM = 1u << 2;     // World transform changed
        private const uint BUFFER_FLAG_MATERIAL = 1u << 3;      // Material properties changed
        private const uint BUFFER_FLAG_TEXTURE = 1u << 4;       // Texture changed
        private const uint BUFFER_FLAG_ALL = uint.MaxValue;     // Force full rebuild

        // Exposed equivalents for derived classes (to avoid magic numbers)
        protected const uint BufferFlagAnimation = BUFFER_FLAG_ANIMATION;
        protected const uint BufferFlagLighting = BUFFER_FLAG_LIGHTING;
        protected const uint BufferFlagTransform = BUFFER_FLAG_TRANSFORM;
        protected const uint BufferFlagMaterial = BUFFER_FLAG_MATERIAL;
        protected const uint BufferFlagTexture = BUFFER_FLAG_TEXTURE;
        protected const uint BufferFlagAll = BUFFER_FLAG_ALL;

        // State grouping optimization
        private readonly struct MeshStateKey : IEquatable<MeshStateKey>
        {
            public readonly Texture2D Texture;
            public readonly BlendState BlendState;
            public readonly bool TwoSided;

            public MeshStateKey(Texture2D tex, BlendState blend, bool twoSided)
            {
                Texture = tex;
                BlendState = blend;
                TwoSided = twoSided;
            }

            public bool Equals(MeshStateKey other) =>
                ReferenceEquals(Texture, other.Texture) &&
                ReferenceEquals(BlendState, other.BlendState) &&
                TwoSided == other.TwoSided;

            public override bool Equals(object obj) => obj is MeshStateKey o && Equals(o);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + (Texture?.GetHashCode() ?? 0);
                    h = h * 31 + (BlendState?.GetHashCode() ?? 0);
                    h = h * 31 + (TwoSided ? 1 : 0);
                    return h;
                }
            }
        }

        // Reuse for grouping to avoid allocations
        private readonly Dictionary<MeshStateKey, List<int>> _meshGroups = new Dictionary<MeshStateKey, List<int>>(32);
        private readonly Stack<List<int>> _meshGroupPool = new Stack<List<int>>(32);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<int> RentMeshList()
            => _meshGroupPool.Count > 0 ? _meshGroupPool.Pop() : new List<int>(8);

        private void ReleaseMeshGroups()
        {
            if (_meshGroups.Count == 0)
                return;

            foreach (var list in _meshGroups.Values)
            {
                list.Clear();
                // Avoid unbounded growth in extreme cases
                if (list.Capacity > 128)
                    list.Capacity = 128;
                _meshGroupPool.Push(list);
            }

            _meshGroups.Clear();
        }
        private MeshBufferCache[] _meshBufferCache;

        private readonly int _animationStrideOffset;
        public int AnimationUpdateStride { get; private set; } = 1;
        protected virtual bool RequiresPerFrameAnimation => false;

        public ModelObject()
        {
            _logger = AppLoggerFactory?.CreateLogger(GetType());
            MatrixChanged += (_s, _e) => UpdateWorldPosition();
            _animationStrideOffset = Interlocked.Increment(ref _animationStrideSeed) & 31;
        }

        private Vector3 _lastFrameLight = Vector3.Zero;
        private double _lastLightUpdateTime = 0;

        // Quantized lighting sample (reduces CPU work without visible change)
        private const float _LIGHT_SAMPLE_GRID = 8f; // world units per cell
        private Vector2 _lastLightSampleCell = new Vector2(float.MaxValue);
        private Vector3 _lastSampledLight = Vector3.Zero;
        private double _lastAnimationUpdateTime = 0;
        private double _lastFrameTimeMs = 0; // To track timing in methods without GameTime
        private double _lastStrideAnimationBufferUpdateTimeMs = double.NegativeInfinity;

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

        public void SetAnimationUpdateStride(int stride)
        {
            int newStride = Math.Max(1, stride);
            if (AnimationUpdateStride == newStride)
                return;

            AnimationUpdateStride = newStride;
            _lastStrideAnimationBufferUpdateTimeMs = double.NegativeInfinity;
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
            {
                ReleaseMeshGroups();
                return;
            }

            int meshCount = Model.Meshes.Length;
            if (meshCount == 0)
            {
                ReleaseMeshGroups();
                return;
            }

            _drawModelInvocationId = ++_drawModelInvocationCounter;

            // Cache commonly used values
            var view = Camera.Instance.View;
            var projection = Camera.Instance.Projection;
            var worldPos = WorldPosition;

            // Pre-calculate shadow and highlight states at object level
            bool doShadow = false;
            Matrix shadowMatrix = Matrix.Identity;
            bool useShadowMap = Constants.ENABLE_DYNAMIC_LIGHTING_SHADER &&
                                GraphicsManager.Instance.ShadowMapRenderer?.IsReady == true;
            // Skip blob shadows at night when day-night cycle is active
            bool isNight = Constants.ENABLE_DAY_NIGHT_CYCLE && SunCycleManager.IsNight;
            if (!isAfterDraw && RenderShadow && !LowQuality && !useShadowMap && !isNight)
                doShadow = TryGetShadowMatrix(out shadowMatrix);
            float shadowOpacity = ShadowOpacity;
            if (doShadow && World?.Terrain != null)
            {
                // Fade blob shadow slightly in strong local light so ground illumination stays visible.
                var dyn = World.Terrain.EvaluateDynamicLight(new Vector2(worldPos.Translation.X, worldPos.Translation.Y));
                float lum = (0.2126f * dyn.X + 0.7152f * dyn.Y + 0.0722f * dyn.Z) / 255f;
                shadowOpacity *= MathHelper.Clamp(1f - lum * 0.6f, 0.35f, 1f);
            }

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

            // Group meshes by render state to minimize state changes
            GroupMeshesByState(isAfterDraw);

            // Render each group with minimal state changes
            try
            {
                var gd = GraphicsDevice;
                var effect = GraphicsManager.Instance.AlphaTestEffect3D;
                // Object-level alpha is constant; set once for the pass
                if (effect != null && effect.Alpha != TotalAlpha)
                    effect.Alpha = TotalAlpha;

                foreach (var kvp in _meshGroups)
                {
                    var stateKey = kvp.Key;
                    var meshIndices = kvp.Value;
                    if (meshIndices.Count == 0) continue;

                    // Apply render state once per group (with object depth bias)
                    if (gd.BlendState != stateKey.BlendState)
                        gd.BlendState = stateKey.BlendState;
                    float depthBias = GetDepthBias();
                    RasterizerState targetRasterizer;
                    if (depthBias != 0f)
                    {
                        var cm = stateKey.TwoSided ? CullMode.None : CullMode.CullClockwiseFace;
                        targetRasterizer = GraphicsManager.GetCachedRasterizerState(depthBias, cm);
                    }
                    else
                    {
                        targetRasterizer = stateKey.TwoSided ? RasterizerState.CullNone : RasterizerState.CullClockwise;
                    }
                    if (gd.RasterizerState != targetRasterizer)
                        gd.RasterizerState = targetRasterizer;
                    if (effect != null && effect.Texture != stateKey.Texture)
                        effect.Texture = stateKey.Texture;

                    // Bind effect once per group
                    if (effect != null)
                    {
                        var passes = effect.CurrentTechnique.Passes;
                        for (int p = 0; p < passes.Count; p++)
                            passes[p].Apply();
                    }

                    // Object-level shadow and highlight passes
                    if (doShadow && !useShadowMap)
                        DrawMeshesShadow(meshIndices, shadowMatrix, view, projection, shadowOpacity);
                    if (highlightAllowed)
                        DrawMeshesHighlight(meshIndices, highlightMatrix, highlightColor);

                    // Shadow/highlight passes change the active shader; reapply the main effect before fast draws.
                    if (effect != null)
                    {
                        var passes = effect.CurrentTechnique.Passes;
                        for (int p = 0; p < passes.Count; p++)
                            passes[p].Apply();
                    }

                    // Draw all meshes in this state group
                    // When dynamic lighting is disabled and blend state is non-opaque, force per-mesh path
                    // to ensure proper DepthStencilState handling and BasicEffect usage for alpha blending
                    bool forcePerMeshTransparency = !Constants.ENABLE_DYNAMIC_LIGHTING_SHADER &&
                                                    stateKey.BlendState != BlendState.Opaque;
                    for (int n = 0; n < meshIndices.Count; n++)
                    {
                        int mi = meshIndices[n];
                        if (NeedsSpecialShaderForMesh(mi) || forcePerMeshTransparency)
                        {
                            DrawMesh(mi); // Falls back to full per-mesh path for special shaders or forced transparency

                            // Per-mesh draws can change the active shader; reapply the group effect
                            // before any fast draws that follow.
                            if (!forcePerMeshTransparency && effect != null)
                            {
                                var passes = effect.CurrentTechnique.Passes;
                                for (int p = 0; p < passes.Count; p++)
                                    passes[p].Apply();
                            }
                        }
                        else
                        {
                            DrawMeshFastAlpha(mi); // Fast path: VB/IB bind + draw only
                        }
                    }
                }
            }
            finally
            {
                // Drop state groups promptly to avoid retaining stale texture references between frames/passes.
                ReleaseMeshGroups();
            }
        }

        // Fast path draw for standard alpha-tested meshes (no special shaders)
        private void DrawMeshFastAlpha(int mesh)
        {
            if (_boneVertexBuffers == null || _boneIndexBuffers == null || _boneTextures == null)
                return;
            if (mesh < 0 ||
                mesh >= _boneVertexBuffers.Length ||
                mesh >= _boneIndexBuffers.Length ||
                mesh >= _boneTextures.Length ||
                _boneVertexBuffers[mesh] == null ||
                _boneIndexBuffers[mesh] == null ||
                _boneTextures[mesh] == null ||
                IsHiddenMesh(mesh))
                return;

            var gd = GraphicsDevice;
            gd.SetVertexBuffer(_boneVertexBuffers[mesh]);
            gd.Indices = _boneIndexBuffers[mesh];
            int primitiveCount = gd.Indices.IndexCount / 3;
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
        }

        // Struct to hold shader selection results
        private readonly struct ShaderSelection
        {
            public readonly bool UseDynamicLighting;
            public readonly bool UseItemMaterial;
            public readonly bool UseMonsterMaterial;
            public readonly bool NeedsSpecialShader;

            public ShaderSelection(bool useDynamicLighting, bool useItemMaterial, bool useMonsterMaterial)
            {
                UseDynamicLighting = useDynamicLighting;
                UseItemMaterial = useItemMaterial;
                UseMonsterMaterial = useMonsterMaterial;
                NeedsSpecialShader = useItemMaterial || useMonsterMaterial || useDynamicLighting;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ShaderSelection DetermineShaderForMesh(int mesh)
        {
            // Only force standard path for fading monsters (to guarantee alpha/darken visibility)
            if (this is MonsterObject mo && mo.IsDead)
                return new ShaderSelection(false, false, false);

            // Item material shader (for excellent/ancient/high level items)
            bool useItemMaterial = Constants.ENABLE_ITEM_MATERIAL_SHADER &&
                                   (ItemLevel >= 7 || IsExcellentItem || IsAncientItem) &&
                                   GraphicsManager.Instance.ItemMaterialEffect != null &&
                                   ShouldApplyItemMaterial(mesh);

            // Monster material shader
            bool useMonsterMaterial = Constants.ENABLE_MONSTER_MATERIAL_SHADER &&
                                      EnableCustomShader &&
                                      GraphicsManager.Instance.MonsterMaterialEffect != null;

            // Dynamic lighting shader (used when no special material is active)
            bool useDynamicLighting = !useItemMaterial && !useMonsterMaterial &&
                                      Constants.ENABLE_DYNAMIC_LIGHTING_SHADER &&
                                      GraphicsManager.Instance.DynamicLightingEffect != null;

            return new ShaderSelection(useDynamicLighting, useItemMaterial, useMonsterMaterial);
        }

        // Determines if this mesh needs special shader path and cannot use fast alpha path
        private bool NeedsSpecialShaderForMesh(int mesh)
        {
            return DetermineShaderForMesh(mesh).NeedsSpecialShader;
        }

        private void GroupMeshesByState(bool isAfterDraw)
        {
            // Release previous frame state to avoid retaining textures/blend states longer than needed
            ReleaseMeshGroups();

            if (Model?.Meshes == null)
                return;

            int meshCount = Model.Meshes.Length;

            for (int i = 0; i < meshCount; i++)
            {
                if (IsHiddenMesh(i)) continue;

                bool isBlend = IsBlendMesh(i);
                bool isRGBA = _meshIsRGBA != null && i < _meshIsRGBA.Length && _meshIsRGBA[i];

                // Skip based on pass and low quality settings
                if (LowQuality && isBlend) continue;
                bool shouldDraw = isAfterDraw ? (isRGBA || isBlend) : (!isRGBA && !isBlend);
                if (!shouldDraw) continue;

                if (_boneTextures == null || i >= _boneTextures.Length)
                    continue;

                var tex = _boneTextures[i];
                bool twoSided = IsMeshTwoSided(i, isBlend);
                BlendState blend = GetMeshBlendState(i, isBlend);

                var key = new MeshStateKey(tex, blend, twoSided);
                if (!_meshGroups.TryGetValue(key, out var list))
                {
                    list = RentMeshList();
                    _meshGroups[key] = list;
                }

                list.Add(i);
            }
        }

        private void DrawMeshesShadow(List<int> meshIndices, Matrix shadowMatrix, Matrix view, Matrix projection, float shadowOpacity)
        {
            for (int n = 0; n < meshIndices.Count; n++)
                DrawShadowMesh(meshIndices[n], view, projection, shadowMatrix, shadowOpacity);
        }

        private void DrawMeshesHighlight(List<int> meshIndices, Matrix highlightMatrix, Vector3 highlightColor)
        {
            for (int n = 0; n < meshIndices.Count; n++)
            {
                int mi = meshIndices[n];
                if (_boneVertexBuffers == null || _boneIndexBuffers == null || _boneTextures == null)
                    return;
                if (mi < 0 ||
                    mi >= _boneVertexBuffers.Length ||
                    mi >= _boneIndexBuffers.Length ||
                    mi >= _boneTextures.Length)
                {
                    continue;
                }
                DrawMeshHighlight(mi, highlightMatrix, highlightColor);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHiddenMesh(int mesh)
        {
            if (_meshHiddenByScript == null || (uint)mesh >= (uint)_meshHiddenByScript.Length)
                return false;

            return HiddenMesh == mesh || HiddenMesh == -2 || _meshHiddenByScript[mesh];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual bool IsBlendMesh(int mesh)
        {
            if (_meshBlendByScript == null || (uint)mesh >= (uint)_meshBlendByScript.Length)
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
            if (Model?.Meshes == null || mesh < 0 || mesh >= Model.Meshes.Length)
                return;
            if (_boneVertexBuffers?[mesh] == null ||
                _boneIndexBuffers?[mesh] == null ||
                _boneTextures?[mesh] == null ||
                IsHiddenMesh(mesh))
                return;

            try
            {
                var gd = GraphicsDevice;
                var prevDepthState = gd.DepthStencilState;
                bool depthStateChanged = false;

                try
                {
                    // Apply small depth bias based on object type to reduce Z-fighting
                    var prevRasterizer = gd.RasterizerState;
                    var depthBias = GetDepthBias();
                    if (depthBias != 0f)
                    {
                        // PERFORMANCE: Use cached RasterizerState to avoid per-mesh allocation
                        gd.RasterizerState = GraphicsManager.GetCachedRasterizerState(depthBias, prevRasterizer.CullMode, prevRasterizer);
                    }

                    // Determine which shader to use (if any)
                    var shaderSelection = DetermineShaderForMesh(mesh);

                    if (shaderSelection.UseItemMaterial)
                    {
                        DrawMeshWithItemMaterial(mesh);
                        return;
                    }

                    if (shaderSelection.UseMonsterMaterial)
                    {
                        DrawMeshWithMonsterMaterial(mesh);
                        return;
                    }

                    if (shaderSelection.UseDynamicLighting)
                    {
                        DrawMeshWithDynamicLighting(mesh);
                        return;
                    }

                    var alphaEffect = GraphicsManager.Instance.AlphaTestEffect3D;

                    // Cache frequently used values
                    bool isBlendMesh = IsBlendMesh(mesh);
                    BlendState blendState = GetMeshBlendState(mesh, isBlendMesh);
                    // Always use AlphaTestEffect - it has ReferenceAlpha=2 which discards very low alpha
                    // pixels similar to DynamicLightingEffect's clip(finalAlpha - 0.01), preventing
                    // black outlines and depth buffer issues with semi-transparent meshes
                    var vertexBuffer = _boneVertexBuffers[mesh];
                    var indexBuffer = _boneIndexBuffers[mesh];
                    var texture = _boneTextures[mesh];

                    // Batch state changes - save current states
                    var originalRasterizer = gd.RasterizerState;
                    var prevBlend = gd.BlendState;
                    float prevAlpha = alphaEffect?.Alpha ?? 1f;

                    // Get mesh rendering states using helper methods
                    bool isTwoSided = IsMeshTwoSided(mesh, isBlendMesh);

                    // Apply final rasterizer state (considering depth bias and culling)
                    if (depthBias != 0f)
                    {
                        // PERFORMANCE: Use cached RasterizerState to avoid per-mesh allocation
                        CullMode cullMode = isTwoSided ? CullMode.None : CullMode.CullClockwiseFace;
                        gd.RasterizerState = GraphicsManager.GetCachedRasterizerState(depthBias, cullMode, originalRasterizer);
                    }
                    else
                    {
                        gd.RasterizerState = isTwoSided ? _cullNone : _cullClockwise;
                    }

                    if (isBlendMesh)
                    {
                        gd.DepthStencilState = GraphicsManager.ReadOnlyDepth;
                        depthStateChanged = true;
                    }

                    gd.BlendState = blendState;

                    // Set buffers once
                    gd.SetVertexBuffer(vertexBuffer);
                    gd.Indices = indexBuffer;

                    // Draw with optimized primitive count calculation
                    int primitiveCount = indexBuffer.IndexCount / 3;

                    // Always use AlphaTestEffect - it discards very low alpha pixels (ReferenceAlpha=2)
                    // similar to DynamicLightingEffect's clip(finalAlpha - 0.01), preventing black
                    // outlines and depth issues while still allowing proper alpha blending
                    if (alphaEffect != null)
                    {
                        alphaEffect.Texture = texture;
                        alphaEffect.Alpha = TotalAlpha;

                        var technique = alphaEffect.CurrentTechnique;
                        var passes = technique.Passes;
                        int passCount = passes.Count;

                        for (int p = 0; p < passCount; p++)
                        {
                            passes[p].Apply();
                            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
                        }

                        alphaEffect.Alpha = prevAlpha;
                    }

                    gd.BlendState = prevBlend;
                    gd.RasterizerState = originalRasterizer;
                }
                finally
                {
                    if (depthStateChanged)
                        gd.DepthStencilState = prevDepthState;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error in DrawMesh: {Message}", ex.Message);
            }
        }

        public virtual void DrawMeshWithItemMaterial(int mesh)
        {
            if (Model?.Meshes == null || mesh < 0 || mesh >= Model.Meshes.Length)
                return;
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

                effect.CurrentTechnique = effect.Techniques[0];
                GraphicsManager.Instance.ShadowMapRenderer?.ApplyShadowParameters(effect);

                var prevDepthState = gd.DepthStencilState;
                bool depthStateChanged = false;

                try
                {
                    bool isBlendMesh = IsBlendMesh(mesh);
                    var vertexBuffer = _boneVertexBuffers[mesh];
                    var indexBuffer = _boneIndexBuffers[mesh];
                    var texture = _boneTextures[mesh];

                    var prevCull = gd.RasterizerState;
                    var prevBlend = gd.BlendState;

                    // Get mesh rendering states using helper methods
                    bool isTwoSided = IsMeshTwoSided(mesh, isBlendMesh);
                    BlendState blendState = GetMeshBlendState(mesh, isBlendMesh);

                    gd.RasterizerState = isTwoSided ? _cullNone : _cullClockwise;

                    if (isBlendMesh)
                    {
                        gd.DepthStencilState = GraphicsManager.ReadOnlyDepth;
                        depthStateChanged = true;
                    }

                    gd.BlendState = blendState;

                    Vector3 sunDir = GraphicsManager.Instance.ShadowMapRenderer?.LightDirection ?? Constants.SUN_DIRECTION;
                    if (sunDir.LengthSquared() < 0.0001f)
                        sunDir = new Vector3(1f, 0f, -0.6f);
                    sunDir = Vector3.Normalize(sunDir);
                    bool worldAllowsSun = World is WorldControl wc ? wc.IsSunWorld : true;
                    bool sunEnabled = Constants.SUN_ENABLED && worldAllowsSun && UseSunLight && !HasWalkerAncestor();

                    // Set world view projection matrix
                    Matrix worldViewProjection = WorldPosition * Camera.Instance.View * Camera.Instance.Projection;
                    effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
                    effect.Parameters["World"]?.SetValue(WorldPosition);
                    effect.Parameters["View"]?.SetValue(Camera.Instance.View);
                    effect.Parameters["Projection"]?.SetValue(Camera.Instance.Projection);
                    effect.Parameters["EyePosition"]?.SetValue(Camera.Instance.Position);
                    effect.Parameters["LightDirection"]?.SetValue(sunDir);
                    effect.Parameters["ShadowStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveShadowStrength() : 0f);

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
                    effect.Parameters["Alpha"]?.SetValue(TotalAlpha);
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
                finally
                {
                    if (depthStateChanged)
                        gd.DepthStencilState = prevDepthState;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error in DrawMeshWithItemMaterial: {Message}", ex.Message);
                DrawMesh(mesh);
            }
        }

        public virtual void DrawMeshWithMonsterMaterial(int mesh)
        {
            if (Model?.Meshes == null || mesh < 0 || mesh >= Model.Meshes.Length)
                return;
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

                effect.CurrentTechnique = effect.Techniques[0];
                GraphicsManager.Instance.ShadowMapRenderer?.ApplyShadowParameters(effect);

                var prevDepthState = gd.DepthStencilState;
                bool depthStateChanged = false;

                try
                {
                    bool isBlendMesh = IsBlendMesh(mesh);
                    var vertexBuffer = _boneVertexBuffers[mesh];
                    var indexBuffer = _boneIndexBuffers[mesh];
                    var texture = _boneTextures[mesh];

                    var prevCull = gd.RasterizerState;
                    var prevBlend = gd.BlendState;

                    // Get mesh rendering states using helper methods
                    bool isTwoSided = IsMeshTwoSided(mesh, isBlendMesh);
                    BlendState blendState = GetMeshBlendState(mesh, isBlendMesh);

                    gd.RasterizerState = isTwoSided ? _cullNone : _cullClockwise;

                    if (isBlendMesh)
                    {
                        gd.DepthStencilState = GraphicsManager.ReadOnlyDepth;
                        depthStateChanged = true;
                    }

                    gd.BlendState = blendState;

                    Vector3 sunDir = GraphicsManager.Instance.ShadowMapRenderer?.LightDirection ?? Constants.SUN_DIRECTION;
                    if (sunDir.LengthSquared() < 0.0001f)
                        sunDir = new Vector3(1f, 0f, -0.6f);
                    sunDir = Vector3.Normalize(sunDir);
                    bool worldAllowsSun = World is WorldControl wc ? wc.IsSunWorld : true;
                    bool sunEnabled = Constants.SUN_ENABLED && worldAllowsSun && UseSunLight && !HasWalkerAncestor();

                    // Set matrices
                    effect.Parameters["World"]?.SetValue(WorldPosition);
                    effect.Parameters["View"]?.SetValue(Camera.Instance.View);
                    effect.Parameters["Projection"]?.SetValue(Camera.Instance.Projection);
                    effect.Parameters["EyePosition"]?.SetValue(Camera.Instance.Position);
                    effect.Parameters["LightDirection"]?.SetValue(sunDir);
                    effect.Parameters["ShadowStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveShadowStrength() : 0f);

                    // Set texture
                    effect.Parameters["DiffuseTexture"]?.SetValue(texture);

                    // Set monster-specific properties
                    effect.Parameters["GlowColor"]?.SetValue(GlowColor);
                    effect.Parameters["GlowIntensity"]?.SetValue(GlowIntensity);
                    effect.Parameters["EnableGlow"]?.SetValue(GlowIntensity > 0.0f && !SimpleColorMode);
                    effect.Parameters["SimpleColorMode"]?.SetValue(SimpleColorMode);
                    effect.Parameters["Time"]?.SetValue(GetCachedTime());
                    effect.Parameters["Alpha"]?.SetValue(TotalAlpha);

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
                finally
                {
                    if (depthStateChanged)
                        gd.DepthStencilState = prevDepthState;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error in DrawMeshWithMonsterMaterial: {Message}", ex.Message);
                DrawMesh(mesh);
            }
        }

        public virtual void DrawMeshWithDynamicLighting(int mesh)
        {
            if (Model?.Meshes == null || mesh < 0 || mesh >= Model.Meshes.Length)
                return;
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

                var prevDepthState = gd.DepthStencilState;
                bool depthStateChanged = false;

                try
                {
                    bool isBlendMesh = IsBlendMesh(mesh);
                    var vertexBuffer = _boneVertexBuffers[mesh];
                    var indexBuffer = _boneIndexBuffers[mesh];
                    var texture = _boneTextures[mesh];

                    var prevCull = gd.RasterizerState;
                    var prevBlend = gd.BlendState;

                    // Get mesh rendering states using helper methods
                    bool isTwoSided = IsMeshTwoSided(mesh, isBlendMesh);
                    BlendState blendState = GetMeshBlendState(mesh, isBlendMesh);

                    gd.RasterizerState = isTwoSided ? _cullNone : _cullClockwise;

                    if (isBlendMesh)
                    {
                        gd.DepthStencilState = GraphicsManager.ReadOnlyDepth;
                        depthStateChanged = true;
                    }

                    gd.BlendState = blendState;

                    if (_dynamicLightingPreparedInvocationId != _drawModelInvocationId)
                    {
                        PrepareDynamicLightingEffect(effect);
                        _dynamicLightingPreparedInvocationId = _drawModelInvocationId;
                    }

                    // Set texture
                    effect.Parameters["DiffuseTexture"]?.SetValue(texture);

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
                finally
                {
                    if (depthStateChanged)
                        gd.DepthStencilState = prevDepthState;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error in DrawMeshWithDynamicLighting: {Message}", ex.Message);
                DrawMesh(mesh); // Fallback to standard rendering
            }
        }

        private void PrepareDynamicLightingEffect(Effect effect)
        {
            effect.CurrentTechnique = effect.Techniques["DynamicLighting"];
            GraphicsManager.Instance.ShadowMapRenderer?.ApplyShadowParameters(effect);

            var camera = Camera.Instance;
            if (camera == null)
                return;

            // Set transformation matrices
            effect.Parameters["World"]?.SetValue(WorldPosition);
            effect.Parameters["View"]?.SetValue(camera.View);
            effect.Parameters["Projection"]?.SetValue(camera.Projection);
            Matrix worldViewProjection = WorldPosition * camera.View * camera.Projection;
            effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
            effect.Parameters["EyePosition"]?.SetValue(camera.Position);

            Vector3 sunDir = GraphicsManager.Instance.ShadowMapRenderer?.LightDirection ?? Constants.SUN_DIRECTION;
            if (sunDir.LengthSquared() < 0.0001f)
                sunDir = new Vector3(1f, 0f, -0.6f);
            sunDir = Vector3.Normalize(sunDir);
            bool worldAllowsSun = World is WorldControl wc ? wc.IsSunWorld : true;
            bool sunEnabled = Constants.SUN_ENABLED && worldAllowsSun && UseSunLight && !HasWalkerAncestor();
            effect.Parameters["SunDirection"]?.SetValue(sunDir);
            effect.Parameters["SunColor"]?.SetValue(_sunColor);
            effect.Parameters["SunStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveSunStrength() : 0f);
            effect.Parameters["ShadowStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveShadowStrength() : 0f);

            effect.Parameters["Alpha"]?.SetValue(TotalAlpha);
            // Use objects technique instead of setting uniforms (better performance, no shader branches)
            effect.CurrentTechnique = effect.Techniques["DynamicLighting"];
            effect.Parameters["TerrainDynamicIntensityScale"]?.SetValue(1.5f);
            effect.Parameters["AmbientLight"]?.SetValue(_ambientLightVector * SunCycleManager.AmbientMultiplier);
            effect.Parameters["DebugLightingAreas"]?.SetValue(Constants.DEBUG_LIGHTING_AREAS ? 1.0f : 0.0f);

            // Set terrain lighting (cached per draw pass)
            Vector3 worldTranslation = WorldPosition.Translation;
            Vector3 terrainLight = Vector3.One;
            if (LightEnabled && World?.Terrain != null)
                terrainLight = World.Terrain.EvaluateTerrainLight(worldTranslation.X, worldTranslation.Y);
            terrainLight = Vector3.Clamp(terrainLight / 255f, Vector3.Zero, Vector3.One);
            effect.Parameters["TerrainLight"]?.SetValue(terrainLight);

            // Select dynamic lights (cached per object, updated on throttled light version changes)
            if (!Constants.ENABLE_DYNAMIC_LIGHTS)
            {
                effect.Parameters["ActiveLightCount"]?.SetValue(0);
                effect.Parameters["MaxLightsToProcess"]?.SetValue(0);
                return;
            }

            int maxLights = Constants.OPTIMIZE_FOR_INTEGRATED_GPU ? 4 : 16;
            int lightCount = 0;
            var terrain = World?.Terrain;
            if (terrain != null)
            {
                int version = terrain.ActiveLightsVersion;
                var activeLights = terrain.ActiveLights;

                if (_dynamicLightSelectionVersion != version || _dynamicLightSelectionMaxLights != maxLights)
                {
                    _dynamicLightSelectionCount = 0;
                    if (activeLights != null && activeLights.Count > 0)
                    {
                        EnsureDynamicLightSelectionBuffer();
                        _dynamicLightSelectionCount = SelectRelevantLightIndices(activeLights, worldTranslation, maxLights, _dynamicLightSelectionIndices);
                    }

                    _dynamicLightSelectionVersion = version;
                    _dynamicLightSelectionMaxLights = maxLights;
                }

                if (activeLights != null && _dynamicLightSelectionCount > 0)
                {
                    lightCount = FillSelectedLightArrays(activeLights, _dynamicLightSelectionIndices, _dynamicLightSelectionCount);
                }
            }

            effect.Parameters["ActiveLightCount"]?.SetValue(lightCount);
            effect.Parameters["MaxLightsToProcess"]?.SetValue(maxLights);
            if (lightCount > 0)
            {
                effect.Parameters["LightPositions"]?.SetValue(_cachedLightPositions);
                effect.Parameters["LightColors"]?.SetValue(_cachedLightColors);
                effect.Parameters["LightRadii"]?.SetValue(_cachedLightRadii);
                effect.Parameters["LightIntensities"]?.SetValue(_cachedLightIntensities);
            }
        }

        private void EnsureDynamicLightSelectionBuffer()
        {
            if (_dynamicLightSelectionIndices == null || _dynamicLightSelectionIndices.Length != _cachedLightPositions.Length)
            {
                _dynamicLightSelectionIndices = new int[_cachedLightPositions.Length];
            }
        }

        private int FillSelectedLightArrays(IReadOnlyList<DynamicLightSnapshot> activeLights, int[] selectedIndices, int count)
        {
            int filled = 0;
            int max = Math.Min(count, _cachedLightPositions.Length);
            for (int i = 0; i < max; i++)
            {
                int idx = selectedIndices[i];
                if ((uint)idx >= (uint)activeLights.Count)
                    continue;

                var light = activeLights[idx];
                _cachedLightPositions[filled] = light.Position;
                _cachedLightColors[filled] = light.Color;
                _cachedLightRadii[filled] = light.Radius;
                _cachedLightIntensities[filled] = light.Intensity;
                filled++;
            }
            return filled;
        }

        private int SelectRelevantLightIndices(IReadOnlyList<DynamicLightSnapshot> activeLights, Vector3 worldTranslation, int maxLights, int[] selectedIndices)
        {
            maxLights = Math.Min(maxLights, _cachedLightPositions.Length);
            maxLights = Math.Min(maxLights, selectedIndices.Length);
            if (activeLights == null || activeLights.Count == 0 || maxLights <= 0)
                return 0;

            int selected = 0;
            float weakestScore = float.MaxValue;
            int weakestIndex = 0;
            var obj2D = new Vector2(worldTranslation.X, worldTranslation.Y);

            for (int i = 0; i < activeLights.Count; i++)
            {
                var light = activeLights[i];
                float radius = light.Radius;
                float radiusSq = radius * radius;

                var diff = new Vector2(light.Position.X, light.Position.Y) - obj2D;
                float distSq = diff.LengthSquared();
                if (distSq >= radiusSq)
                    continue;

                float influence = (1f - distSq / radiusSq) * light.Intensity;
                if (influence <= MinLightInfluence)
                    continue;

                if (selected < maxLights)
                {
                    _cachedLightScores[selected] = influence;
                    selectedIndices[selected] = i;

                    if (influence < weakestScore)
                    {
                        weakestScore = influence;
                        weakestIndex = selected;
                    }

                    selected++;
                }
                else if (influence > weakestScore)
                {
                    _cachedLightScores[weakestIndex] = influence;
                    selectedIndices[weakestIndex] = i;

                    weakestScore = _cachedLightScores[0];
                    weakestIndex = 0;
                    for (int j = 1; j < selected; j++)
                    {
                        float score = _cachedLightScores[j];
                        if (score < weakestScore)
                        {
                            weakestScore = score;
                            weakestIndex = j;
                        }
                    }
                }
            }

            return selected;
        }

        public virtual void DrawMeshHighlight(int mesh, Matrix highlightMatrix, Vector3 highlightColor)
        {
            if (IsHiddenMesh(mesh) || _boneVertexBuffers == null || _boneIndexBuffers == null || _boneTextures == null)
                return;

            // Defensive range checks to avoid races when buffers are swapped during async loads
            if (mesh < 0 ||
                mesh >= _boneVertexBuffers.Length ||
                mesh >= _boneIndexBuffers.Length ||
                mesh >= _boneTextures.Length)
            {
                return;
            }

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
            GraphicsDevice.DepthStencilState = GraphicsManager.ReadOnlyDepth;
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
                // For bone-attached models (weapons, wings, etc.) reuse the parent's blob-shadow basis
                // so attachments share the same shadow anchor/orientation as the character.
                if (ParentBoneLink >= 0 && Parent is ModelObject parentModel)
                {
                    if (!parentModel.TryGetShadowMatrix(out Matrix parentShadowWorld))
                        return false;

                    Matrix localMatrix = Matrix.CreateScale(Scale) *
                                         Matrix.CreateFromQuaternion(MathUtils.AngleQuaternion(Angle)) *
                                         Matrix.CreateTranslation(Position);

                    shadowWorld = localMatrix * ParentBodyOrigin * parentShadowWorld;
                    return true;
                }

                Vector3 position = WorldPosition.Translation;
                float terrainH = World.Terrain.RequestTerrainHeight(position.X, position.Y);
                terrainH += terrainH * 0.5f;

                float heightAboveTerrain = position.Z - terrainH;
                float angleRad = MathHelper.ToRadians(45);

                Vector3 shadowPos = new(
                    position.X - (heightAboveTerrain / 2),
                    position.Y - (heightAboveTerrain / 4.5f),
                    terrainH + 1f);

                float yaw = TotalAngle.Y + MathHelper.ToRadians(110);
                float pitch = TotalAngle.X + MathHelper.ToRadians(120);
                float roll = TotalAngle.Z + MathHelper.ToRadians(90);

                Quaternion rotQ = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);

                const float shadowBias = 0.1f;
                shadowWorld =
                      Matrix.CreateFromQuaternion(rotQ)
                    * Matrix.CreateScale(1.0f * TotalScale, 0.01f * TotalScale, 1.0f * TotalScale)
                    * Matrix.CreateRotationX(-MathHelper.PiOver2) // keep shadow flat; skip extra terrain samples
                    * Matrix.CreateRotationZ(angleRad)
                    * Matrix.CreateTranslation(shadowPos + new Vector3(0f, 0f, shadowBias));

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error creating shadow matrix: {Message}", ex.Message);
                return false;
            }
        }

        public virtual void DrawShadowMesh(int mesh, Matrix view, Matrix projection, Matrix shadowWorld, float shadowOpacity)
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

                // PERFORMANCE: Use cached RasterizerState to avoid per-mesh allocation
                RasterizerState ShadowRasterizer = GraphicsManager.GetCachedRasterizerState(constBias * -20, CullMode.None);

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
                    effect.Parameters["ShadowTint"]?.SetValue(new Vector4(0, 0, 0, shadowOpacity));
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
                _logger?.LogDebug("Error in DrawShadowMesh: {Message}", ex.Message);
            }
        }

        public virtual void DrawShadowCaster(Effect shadowEffect, Matrix lightViewProjection)
        {
            if (shadowEffect == null)
                return;

            int shadowSize = GraphicsManager.Instance.ShadowMapRenderer?.ShadowMap?.Width ?? Math.Max(256, Constants.SHADOW_MAP_SIZE);
            Vector2 shadowTexel = new Vector2(1f / shadowSize, 1f / shadowSize);

            // Draw own meshes if available
            if (Model?.Meshes != null && _boneVertexBuffers != null && _boneIndexBuffers != null && _boneTextures != null)
            {
                try
                {
                    var gd = GraphicsDevice;
                    var prevBlend = gd.BlendState;
                    var prevDepth = gd.DepthStencilState;
                    var prevRaster = gd.RasterizerState;
                    var prevTechnique = shadowEffect?.CurrentTechnique;

                    shadowEffect.CurrentTechnique = shadowEffect?.Techniques["ShadowCaster"];
                    shadowEffect?.Parameters["World"]?.SetValue(WorldPosition);
                    shadowEffect?.Parameters["LightViewProjection"]?.SetValue(lightViewProjection);
                    shadowEffect?.Parameters["ShadowMapTexelSize"]?.SetValue(shadowTexel);
                    shadowEffect?.Parameters["ShadowBias"]?.SetValue(Constants.SHADOW_BIAS);
                    shadowEffect?.Parameters["ShadowNormalBias"]?.SetValue(Constants.SHADOW_NORMAL_BIAS);
                    shadowEffect?.Parameters["SunDirection"]?.SetValue(GraphicsManager.Instance.ShadowMapRenderer?.LightDirection ?? Constants.SUN_DIRECTION);
                    shadowEffect?.Parameters["UseProceduralTerrainUV"]?.SetValue(0.0f);
                    shadowEffect?.Parameters["IsWaterTexture"]?.SetValue(0.0f);

                    gd.BlendState = BlendState.Opaque;
                    gd.DepthStencilState = DepthStencilState.Default;

                    int meshCount = Model.Meshes.Length;
                    for (int i = 0; i < meshCount; i++)
                    {
                        if (IsHiddenMesh(i))
                            continue;

                        var vb = _boneVertexBuffers[i];
                        var ib = _boneIndexBuffers[i];
                        var tex = _boneTextures[i];
                        if (vb == null || ib == null || tex == null)
                            continue;

                        bool isTwoSided = IsMeshTwoSided(i, IsBlendMesh(i));
                        gd.RasterizerState = isTwoSided ? _cullNone : _cullClockwise;

                        shadowEffect?.Parameters["DiffuseTexture"]?.SetValue(tex);

                        foreach (var pass in shadowEffect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            gd.SetVertexBuffer(vb);
                            gd.Indices = ib;
                            gd.DrawIndexedPrimitives(
                                PrimitiveType.TriangleList,
                                0, 0, ib.IndexCount / 3);
                        }
                    }

                    gd.BlendState = prevBlend;
                    gd.DepthStencilState = prevDepth;
                    gd.RasterizerState = prevRaster;
                    if (prevTechnique != null)
                        shadowEffect.CurrentTechnique = prevTechnique;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("Error drawing shadow caster: {Message}", ex.Message);
                }
            }

            // Recursively draw shadow casters for all children (armor, weapons, helm, etc.)
            // Note: We don't use modelChild.Visible here because it includes OutOfView check,
            // and children may not have their OutOfView properly updated since they're not in World.Objects directly.
            // Instead, we check Status, Hidden, and RenderShadow directly.
            int childCount = Children.Count;
            bool skipSmallParts = Constants.SHADOW_SKIP_SMALL_PARTS;
            for (int i = 0; i < childCount; i++)
            {
                var child = Children[i];
                if (child is ModelObject modelChild &&
                    modelChild.Status == GameControlStatus.Ready &&
                    !modelChild.Hidden &&
                    modelChild.RenderShadow)
                {
                    // Skip small parts (weapons, gloves, boots) for performance if enabled
                    if (skipSmallParts && IsSmallShadowPart(modelChild))
                        continue;

                    modelChild.DrawShadowCaster(shadowEffect, lightViewProjection);
                }
            }
        }

        /// <summary>
        /// Checks if a model child is a small part that can be skipped for shadow casting.
        /// Small parts like weapons, gloves, and boots don't contribute much to shadow silhouette.
        /// </summary>
        private static bool IsSmallShadowPart(ModelObject modelChild)
        {
            return modelChild is Player.WeaponObject ||
                   modelChild is Player.PlayerGloveObject ||
                   modelChild is Player.PlayerBootObject ||
                   modelChild is Player.PlayerMaskHelmObject;
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
            // _tempBoneTransforms removed - using ArrayPool now
            _animationStateValid = false;

            ReleaseMeshGroups();
            _meshGroupPool.Clear();
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

        private void Animation(GameTime gameTime)
        {
            if (LinkParentAnimation || Model?.Actions == null || Model.Actions.Length == 0) return;

            int currentActionIndex = Math.Clamp(CurrentAction, 0, Model.Actions.Length - 1);
            var action = Model.Actions[currentActionIndex];
            if (action == null) return; // Skip animation if action is null

            int totalFrames = Math.Max(action.NumAnimationKeys, 1);
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Detect death action for walkers to clamp on second-to-last key
            bool isDeathAction = false;
            if (this is WalkerObject)
            {
                if (this is PlayerObject)
                {
                    var pa = (PlayerAction)currentActionIndex;
                    isDeathAction = pa == PlayerAction.PlayerDie1 || pa == PlayerAction.PlayerDie2;
                }
                else if (this is MonsterObject)
                {
                    isDeathAction = currentActionIndex == (int)Client.Main.Models.MonsterActionType.Die;
                }
                else if (this is NPCObject)
                {
                    var pa = (PlayerAction)currentActionIndex;
                    isDeathAction = pa == PlayerAction.PlayerDie1 || pa == PlayerAction.PlayerDie2;
                }
            }

            if (totalFrames == 1 && !ContinuousAnimation)
            {
                if (_priorAction != currentActionIndex)
                {
                    GenerateBoneMatrix(currentActionIndex, 0, 0, 0);
                    _priorAction = currentActionIndex;
                    InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                }
                CurrentFrame = 0;
                return;
            }

            if (_priorAction != currentActionIndex)
            {
                _blendFromAction = _priorAction;
                _blendFromTime = _animTime;
                _blendElapsed = 0f;
                _isBlending = true;
                _animTime = 0.0;

                // _blendFromBones is pre-allocated in LoadContent - no need to allocate here
            }

            _animTime += delta * (action.PlaySpeed == 0 ? 1.0f : action.PlaySpeed) * AnimationSpeed;
            double framePos;
            if (isDeathAction || HoldOnLastFrame)
            {
                int endIdx = Math.Max(0, totalFrames - 2);
                _animTime = Math.Min(_animTime, endIdx + 0.0001f);
                framePos = _animTime;
            }
            else if (this is WalkerObject walker && walker.IsOneShotPlaying)
            {
                int endIdx = Math.Max(0, totalFrames - 1);
                if (_animTime >= endIdx)
                {
                    _animTime = endIdx;
                    framePos = _animTime;
                    walker.NotifyOneShotAnimationCompleted();
                }
                else
                {
                    framePos = _animTime;
                }
            }
            else
            {
                framePos = _animTime % totalFrames;
            }
            int f0 = (int)framePos;
            int f1 = (f0 + 1) % totalFrames;
            float t = (float)(framePos - f0);
            CurrentFrame = f0;

            // Only applies to objects that specifically request it (e.g., portals with stuttering)
            if (PreventLastFrameInterpolation && totalFrames > 1 && f0 == totalFrames - 1)
            {
                // Instead of interpolating lastFrame->firstFrame, restart smoothly
                // This eliminates the visual "jump" that causes animation stuttering
                f0 = 0;
                f1 = 1;
                t = 0.0f;
                framePos = 0.0;
                _animTime = _animTime - (totalFrames - 1); // Adjust time to maintain continuity
            }

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
            var bones = Model?.Bones;

            if (bones == null || bones.Length == 0)
            {
                // Reset animation cache for invalid models
                _animationStateValid = false;
                return;
            }

            // Armor items use the player's idle pose so they match equipped visuals
            if (TryApplyPlayerIdlePose(bones))
            {
                _animationStateValid = true;
                _lastAnimationState = default;
                return;
            }

            if (Model.Actions == null || Model.Actions.Length == 0)
            {
                _animationStateValid = false;
                return;
            }

            actionIdx = Math.Clamp(actionIdx, 0, Model.Actions.Length - 1);
            var action = Model.Actions[actionIdx];

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
                    InterpolationFactor = t
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

            // Rent temp array from pool for safer hierarchical calculations
            // ArrayPool may return larger array, so we use bones.Length for actual operations
            Matrix[] tempBoneTransforms = _matrixArrayPool.Rent(bones.Length);
#if DEBUG
            Interlocked.Increment(ref _poolRentCount);
#endif
            try
            {
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
                        tempBoneTransforms[i] = Matrix.Identity;
                        if (BoneTransform[i] != Matrix.Identity)
                            anyBoneChanged = true;
                        continue;
                    }

                    var bm = bone.Matrixes[actionIdx];
                    int numPosKeys = bm.Position?.Length ?? 0;
                    int numQuatKeys = bm.Quaternion?.Length ?? 0;

                    if (numPosKeys == 0 || numQuatKeys == 0)
                    {
                        tempBoneTransforms[i] = Matrix.Identity;
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
                        // Interpolated keyframe - use fast normalized lerp instead of costly Slerp
                        Quaternion q = Nlerp(bm.Quaternion[boneFrame0], bm.Quaternion[boneFrame1], boneT);
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
                    if (bone.Parent >= 0 && bone.Parent < bones.Length)
                    {
                        worldTransform = localTransform * tempBoneTransforms[bone.Parent];
                    }
                    else
                    {
                        worldTransform = localTransform;
                    }

                    // Store in temp array
                    tempBoneTransforms[i] = worldTransform;

                    // Check if this bone actually changed (simple comparison for performance)
                    if (BoneTransform[i] != worldTransform)
                    {
                        anyBoneChanged = true;
                    }
                }

                // For static objects (single frame) or first-time setup, always update
                bool forceUpdate = action.NumAnimationKeys <= 1 || !_animationStateValid;

                // Allow derived objects to apply procedural bone post-processing (e.g., head look-at).
                // Must run on the temp array so the result also propagates to children using LinkParentAnimation.
                if (PostProcessBoneTransforms(bones, tempBoneTransforms))
                {
                    anyBoneChanged = true;
                }

                // Only update final transforms and invalidate if something actually changed OR force update
                if (anyBoneChanged || forceUpdate)
                {
                    Array.Copy(tempBoneTransforms, BoneTransform, bones.Length);

                    // Always invalidate animation for walkers (players/monsters/NPCs) to preserve smooth pacing
                    bool isImportantObject = RequiresPerFrameAnimation;
                    if (forceUpdate || isImportantObject)
                    {
                        InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                    }
                    else
                    {
                        // Only throttle animation updates for non-critical objects (NPCs, monsters)
                        const double ANIMATION_UPDATE_INTERVAL_MS = 20; // Max 20 Hz for non-critical objects

                        if (_lastFrameTimeMs - _lastAnimationUpdateTime > ANIMATION_UPDATE_INTERVAL_MS)
                        {
                            InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                            _lastAnimationUpdateTime = _lastFrameTimeMs;
                        }
                    }
                    UpdateBoundings();
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
            finally
            {
                // CRITICAL: Always return rented array to pool to prevent memory leaks
                // clearArray: false because we don't need to zero out Matrix structs (performance)
                _matrixArrayPool.Return(tempBoneTransforms, clearArray: false);
#if DEBUG
                Interlocked.Increment(ref _poolReturnCount);
#endif
            }
        }

        /// <summary>
        /// Allows derived objects to procedurally adjust the computed bone transforms (in-place).
        /// Return true if any bone was modified.
        /// </summary>
        protected virtual bool PostProcessBoneTransforms(BMDTextureBone[] bones, Matrix[] boneTransforms)
        {
            return false;
        }

        private static Quaternion Nlerp(in Quaternion q1, in Quaternion q2, float t)
        {
            var target = q2;
            if (Quaternion.Dot(q1, q2) < 0f)
            {
                target.X = -target.X;
                target.Y = -target.Y;
                target.Z = -target.Z;
                target.W = -target.W;
            }

            var blended = new Quaternion(
                q1.X + (target.X - q1.X) * t,
                q1.Y + (target.Y - q1.Y) * t,
                q1.Z + (target.Z - q1.Z) * t,
                q1.W + (target.W - q1.W) * t);

            return Quaternion.Normalize(blended);
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

                Quaternion q = Nlerp(bm.Quaternion[frame0], bm.Quaternion[frame1], t);
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

        /// <summary>
        /// Allows derived objects to provide modified bone transforms for rendering.
        /// Default returns the input bones unchanged.
        /// Useful for lightweight procedural deformations (e.g., cape flutter).
        /// </summary>
        /// <param name="bones">Current bone transforms used for skinning.</param>
        /// <returns>Bone transforms to use for rendering.</returns>
        protected virtual Matrix[] GetRenderBoneTransforms(Matrix[] bones)
        {
            return bones;
        }

        /// <summary>
        /// Allows derived objects to deform vertices procedurally during buffer generation.
        /// Default returns null (no deformation).
        /// </summary>
        protected virtual IVertexDeformer GetVertexDeformer()
        {
            return null;
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

                // (Reverted) No frame-based throttling here to maintain smooth animations.

                uint currentFrame = (uint)(MuGame.Instance.GameTime.TotalGameTime.TotalMilliseconds / 16.67f);

                // If we only have transform updates we can skip heavy CPU skinning work.
                if ((_invalidatedBufferFlags & ~BUFFER_FLAG_TRANSFORM) == 0)
                {
                    _invalidatedBufferFlags &= ~BUFFER_FLAG_TRANSFORM;
                    return;
                }

                // Allow attachments to update at a reduced frequency when only animation is dirty.
                if ((_invalidatedBufferFlags & BUFFER_FLAG_ANIMATION) != 0 &&
                    (_invalidatedBufferFlags & ~(BUFFER_FLAG_ANIMATION | BUFFER_FLAG_TRANSFORM)) == 0 &&
                    AnimationUpdateStride > 1)
                {
                    const double strideFrameMs = 1000.0 / 60.0;
                    double nowMs = _lastFrameTimeMs;
                    double intervalMs = strideFrameMs * AnimationUpdateStride;

                    // Time-based throttling avoids aliasing at low FPS where frame-based modulo
                    // can reduce updates far below the intended rate (causing visible stutter).
                    if (double.IsNegativeInfinity(_lastStrideAnimationBufferUpdateTimeMs))
                    {
                        double phaseMs = (_animationStrideOffset % AnimationUpdateStride) * strideFrameMs;
                        _lastStrideAnimationBufferUpdateTimeMs = nowMs - intervalMs + phaseMs;
                    }

                    if (nowMs - _lastStrideAnimationBufferUpdateTimeMs < intervalMs)
                    {
                        _invalidatedBufferFlags &= ~BUFFER_FLAG_TRANSFORM;
                        return;
                    }

                    _lastStrideAnimationBufferUpdateTimeMs = nowMs;
                }

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
                bones = GetRenderBoneTransforms(bones) ?? bones;
                if (bones == null)
                {
                    _logger?.LogDebug("SetDynamicBuffers: BoneTransform == null – skip");
                    return;
                }

                IVertexDeformer vertexDeformer = GetVertexDeformer();
                bool hasVertexDeformer = vertexDeformer != null;

                // Calculate lighting only once if lighting flags are set
                bool needLightCalculation = (_invalidatedBufferFlags & BUFFER_FLAG_LIGHTING) != 0;
                Vector3 baseLight = Vector3.Zero;
                Vector3 worldTranslation = WorldPosition.Translation;

                if (needLightCalculation && LightEnabled && World?.Terrain != null)
                {
                    baseLight = World.Terrain.EvaluateTerrainLight(worldTranslation.X, worldTranslation.Y) + Light;
                }
                else if (needLightCalculation)
                {
                    baseLight = Light;
                }

                // Pre-calculate common color components (cache to avoid property access)
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

                        // Check if this specific mesh needs update - only on real changes
                        bool meshNeedsUpdate = !cache.IsValid ||
                                             (needLightCalculation && Vector3.DistanceSquared(meshLight, cache.CachedLight) > 0.01f) ||
                                             (_invalidatedBufferFlags & (BUFFER_FLAG_ANIMATION | BUFFER_FLAG_TRANSFORM | BUFFER_FLAG_LIGHTING | BUFFER_FLAG_TEXTURE)) != 0;

                        if (!meshNeedsUpdate)
                            continue;

                        // Optimized color calculation with clamping - use byte directly to avoid float→int→byte conversion
                        float r = MathF.Min(colorR * meshLight.X, 255f);
                        float g = MathF.Min(colorG * meshLight.Y, 255f);
                        float b = MathF.Min(colorB * meshLight.Z, 255f);
                        Color bodyColor = new Color((byte)r, (byte)g, (byte)b);

                        // Skip expensive buffer generation if color hasn't changed
                        bool colorChanged = cache.CachedBodyColor.PackedValue != bodyColor.PackedValue;
                        if (!colorChanged && cache.IsValid && (_invalidatedBufferFlags & BUFFER_FLAG_ANIMATION) == 0)
                            continue;

                        // Generate buffers only when necessary
                        BMDLoader.Instance.GetModelBuffers(
                            Model, meshIndex, bodyColor, bones,
                            ref _boneVertexBuffers[meshIndex],
                            ref _boneIndexBuffers[meshIndex],
                            // Force bypassing internal cache when texture coordinates changed
                            ((_invalidatedBufferFlags & BUFFER_FLAG_TEXTURE) != 0) || hasVertexDeformer,
                            vertexDeformer);

                        // Update cache
                        cache.VertexBuffer = _boneVertexBuffers[meshIndex];
                        cache.IndexBuffer = _boneIndexBuffers[meshIndex];
                        cache.CachedLight = meshLight;
                        cache.CachedBodyColor = bodyColor;
                        cache.LastUpdateFrame = currentFrame;
                        cache.IsValid = true;

                        // PERFORMANCE: Textures are now preloaded in LoadContent - only reload on explicit texture change
                        if (_boneTextures[meshIndex] == null && (_invalidatedBufferFlags & BUFFER_FLAG_TEXTURE) != 0)
                        {
                            // This should rarely happen since textures are preloaded in LoadContent
                            _logger?.LogDebug("Lazy loading texture for mesh {MeshIndex} - this may cause frame stutter", meshIndex);
                            string texturePath = _meshTexturePath[meshIndex]
                                ?? BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);

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
                        _logger?.LogDebug("SetDynamicBuffers – mesh {MeshIndex}: {Message}", meshIndex, exMesh.Message);
                    }
                }

                _invalidatedBufferFlags = 0; // Clear all flags
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("SetDynamicBuffers FATAL: {Message}", ex.Message);
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
            // Also always use fresh transforms for PlayerObjects to avoid rendering issues
            if (LinkParentAnimation || ParentBoneLink >= 0 || this is PlayerObject)
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

        private bool TryApplyPlayerIdlePose(BMDTextureBone[] bones)
        {
            var def = ItemDefinition;
            int group = def?.Group ?? -1;
            bool isArmor = group >= 7 && group <= 11;
            if (!isArmor)
                return false;

            var playerBones = PlayerIdlePoseProvider.GetIdleBoneMatrices();
            if (playerBones == null || playerBones.Length == 0)
                return false;

            if (BoneTransform == null || BoneTransform.Length != bones.Length)
                BoneTransform = new Matrix[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                BoneTransform[i] = (i < playerBones.Length)
                    ? playerBones[i]
                    : BuildBoneFromBmd(bones[i], BoneTransform);
            }

            InvalidateBuffers(BUFFER_FLAG_ANIMATION);
            return true;
        }

        private static Matrix BuildBoneFromBmd(BMDTextureBone bone, Matrix[] parentResults)
        {
            Matrix local = Matrix.Identity;

            if (bone?.Matrixes != null && bone.Matrixes.Length > 0)
            {
                var bm = bone.Matrixes[0];
                if (bm.Position?.Length > 0 && bm.Quaternion?.Length > 0)
                {
                    var q = bm.Quaternion[0];
                    local = Matrix.CreateFromQuaternion(new Quaternion(q.X, q.Y, q.Z, q.W));
                    var p = bm.Position[0];
                    local.Translation = new Vector3(p.X, p.Y, p.Z);
                }
            }

            if (bone != null && bone.Parent >= 0 && bone.Parent < parentResults.Length)
                return local * parentResults[bone.Parent];

            return local;
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
            if ((flags & BUFFER_FLAG_TEXTURE) != 0)
            {
                _sortTextureHintDirty = true;
                _sortTextureHint = null;
            }

            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is not ModelObject modelObject)
                    continue;

                uint childFlags = flags;

                if ((childFlags & BUFFER_FLAG_TRANSFORM) != 0 &&
                    (modelObject.LinkParentAnimation || modelObject.ParentBoneLink >= 0))
                {
                    childFlags &= ~BUFFER_FLAG_TRANSFORM;
                }

                if (childFlags != 0)
                {
                    modelObject.InvalidateBuffers(childFlags);
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

        private void ReleaseDynamicBuffers()
        {
            var vertexBuffers = Interlocked.Exchange(ref _boneVertexBuffers, null);
            if (vertexBuffers != null)
            {
                for (int i = 0; i < vertexBuffers.Length; i++)
                {
                    var buffer = vertexBuffers[i];
                    if (buffer == null)
                        continue;

                    DynamicBufferPool.ReturnVertexBuffer(buffer);
                    vertexBuffers[i] = null;
                }
            }

            var indexBuffers = Interlocked.Exchange(ref _boneIndexBuffers, null);
            if (indexBuffers != null)
            {
                for (int i = 0; i < indexBuffers.Length; i++)
                {
                    var buffer = indexBuffers[i];
                    if (buffer == null)
                        continue;

                    DynamicBufferPool.ReturnIndexBuffer(buffer);
                    indexBuffers[i] = null;
                }
            }

            var meshCache = _meshBufferCache;
            if (meshCache != null)
            {
                for (int i = 0; i < meshCache.Length; i++)
                {
                    ref var cache = ref meshCache[i];
                    cache.VertexBuffer = null;
                    cache.IndexBuffer = null;
                    cache.IsValid = false;
                }
            }
        }
    }
}
