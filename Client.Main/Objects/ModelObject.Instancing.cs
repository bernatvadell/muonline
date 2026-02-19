using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Client.Main.Objects
{
    public abstract partial class ModelObject
    {
        private readonly struct StaticMapInstancingBatchKey : IEquatable<StaticMapInstancingBatchKey>
        {
            public StaticMapInstancingBatchKey(BMD model, int meshIndex, Texture2D texture, bool twoSided)
            {
                Model = model;
                MeshIndex = meshIndex;
                Texture = texture;
                TwoSided = twoSided;
            }

            public BMD Model { get; }
            public int MeshIndex { get; }
            public Texture2D Texture { get; }
            public bool TwoSided { get; }

            public bool Equals(StaticMapInstancingBatchKey other)
            {
                return ReferenceEquals(Model, other.Model)
                    && MeshIndex == other.MeshIndex
                    && ReferenceEquals(Texture, other.Texture)
                    && TwoSided == other.TwoSided;
            }

            public override bool Equals(object obj) => obj is StaticMapInstancingBatchKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + RuntimeHelpers.GetHashCode(Model);
                    hash = (hash * 31) + MeshIndex;
                    hash = (hash * 31) + RuntimeHelpers.GetHashCode(Texture);
                    hash = (hash * 31) + (TwoSided ? 1 : 0);
                    return hash;
                }
            }
        }

        private sealed class StaticMapInstancingBatch : IDisposable
        {
            public VertexBuffer GeometryVertexBuffer;
            public IndexBuffer GeometryIndexBuffer;
            public int PrimitiveCount;
            public int BoneCount;
            public bool TwoSided;
            public Texture2D Texture;
            public ModelObject PoseSource;
            public readonly List<StaticModelInstanceData> Instances = new List<StaticModelInstanceData>(64);
            public DynamicVertexBuffer InstanceBuffer;
            public int InstanceBufferCapacity;
            public StaticModelInstanceData[] UploadBuffer = Array.Empty<StaticModelInstanceData>();
            public readonly VertexBufferBinding[] VertexBindings = new VertexBufferBinding[2];

            public void Dispose()
            {
                InstanceBuffer?.Dispose();
                InstanceBuffer = null;
                InstanceBufferCapacity = 0;
                UploadBuffer = Array.Empty<StaticModelInstanceData>();
                Instances.Clear();
            }
        }

        private static readonly Dictionary<StaticMapInstancingBatchKey, StaticMapInstancingBatch> _staticMapInstancingBatches = new Dictionary<StaticMapInstancingBatchKey, StaticMapInstancingBatch>(128);
        private static readonly List<StaticMapInstancingBatch> _staticMapInstancingActiveBatches = new List<StaticMapInstancingBatch>(128);
        private static readonly Vector3[] _staticInstancingLightPositions = new Vector3[32];
        private static readonly Vector3[] _staticInstancingLightColors = new Vector3[32];
        private static readonly float[] _staticInstancingLightRadii = new float[32];
        private static readonly float[] _staticInstancingLightIntensities = new float[32];
        private static readonly float[] _staticInstancingLightScores = new float[32];
        private static int _staticInstancingLastLightsVersion = -1;
        private static int _staticInstancingLastLightCount = 0;
        private static bool _staticMapInstancingFailed = false;
        private static EffectTechnique _cachedStaticMapInstancingTechnique;
        private static readonly Matrix _identity = Matrix.Identity;

        private static int _staticMapInstancedObjectsThisFrame = 0;
        private static int _staticMapInstancedMeshInstancesThisFrame = 0;
        private static int _staticMapInstancedBatchesThisFrame = 0;
        private static int _staticMapInstancedDrawCallsThisFrame = 0;
        private static int _staticMapInstancingFallbacksThisFrame = 0;

        public static int LastFrameStaticMapInstancedObjects { get; private set; }
        public static int LastFrameStaticMapInstancedMeshInstances { get; private set; }
        public static int LastFrameStaticMapInstancedBatches { get; private set; }
        public static int LastFrameStaticMapInstancedDrawCalls { get; private set; }
        public static int LastFrameStaticMapInstancingFallbacks { get; private set; }
        public static bool IsStaticMapInstancingBackendSupported => SupportsGpuDynamicSkinning;
        public static bool IsStaticMapInstancingRuntimeDisabled => _staticMapInstancingFailed;

        private static void BeginFrameStaticMapInstancingMetrics()
        {
            LastFrameStaticMapInstancedObjects = _staticMapInstancedObjectsThisFrame;
            LastFrameStaticMapInstancedMeshInstances = _staticMapInstancedMeshInstancesThisFrame;
            LastFrameStaticMapInstancedBatches = _staticMapInstancedBatchesThisFrame;
            LastFrameStaticMapInstancedDrawCalls = _staticMapInstancedDrawCallsThisFrame;
            LastFrameStaticMapInstancingFallbacks = _staticMapInstancingFallbacksThisFrame;

            _staticMapInstancedObjectsThisFrame = 0;
            _staticMapInstancedMeshInstancesThisFrame = 0;
            _staticMapInstancedBatchesThisFrame = 0;
            _staticMapInstancedDrawCallsThisFrame = 0;
            _staticMapInstancingFallbacksThisFrame = 0;
        }

        internal static void RegisterStaticMapInstancingFallback()
        {
            _staticMapInstancingFallbacksThisFrame++;
        }

        internal static bool IsStaticMapInstancingPathAvailable()
        {
            return IsStaticMapInstancingSupported();
        }

        internal static bool TryQueueStaticMapObjectForInstancing(WorldObject obj)
        {
            if (obj is not ModelObject modelObject)
                return false;

            return modelObject.TryQueueStaticMapObjectForInstancing();
        }

        internal static void FlushStaticMapInstancingBatches(WorldControl world)
        {
            if (_staticMapInstancingActiveBatches.Count == 0)
                return;

            if (_staticMapInstancingFailed || !IsStaticMapInstancingSupported())
            {
                ClearStaticMapInstancingQueues();
                return;
            }

            var graphicsManager = GraphicsManager.Instance;
            var effect = graphicsManager.DynamicLightingEffect;
            if (effect == null || _cachedStaticMapInstancingTechnique == null)
            {
                ClearStaticMapInstancingQueues();
                return;
            }

            var gd = graphicsManager.GraphicsDevice;
            var prevBlend = gd.BlendState;
            var prevRaster = gd.RasterizerState;
            var prevSampler = gd.SamplerStates[0];

            try
            {
                PrepareStaticMapInstancingEffect(effect, world);

                gd.BlendState = BlendState.Opaque;
                gd.SamplerStates[0] = GraphicsManager.GetQualityLinearSamplerState();

                for (int i = 0; i < _staticMapInstancingActiveBatches.Count; i++)
                {
                    var batch = _staticMapInstancingActiveBatches[i];
                    int instanceCount = batch.Instances.Count;
                    if (instanceCount <= 0 ||
                        batch.GeometryVertexBuffer == null ||
                        batch.GeometryIndexBuffer == null ||
                        batch.Texture == null ||
                        batch.PoseSource == null)
                    {
                        continue;
                    }

                    if (!batch.PoseSource.TryUploadGpuSkinBoneMatrices(effect, batch.BoneCount))
                        continue;

                    EnsureInstanceUploadBuffer(batch, instanceCount);
                    for (int j = 0; j < instanceCount; j++)
                        batch.UploadBuffer[j] = batch.Instances[j];

                    EnsureInstanceVertexBuffer(gd, batch, instanceCount);
                    batch.InstanceBuffer.SetData(batch.UploadBuffer, 0, instanceCount, SetDataOptions.Discard);

                    gd.RasterizerState = batch.TwoSided ? RasterizerState.CullNone : RasterizerState.CullClockwise;
                    effect.Parameters["DiffuseTexture"]?.SetValue(batch.Texture);

                    batch.VertexBindings[0] = new VertexBufferBinding(batch.GeometryVertexBuffer);
                    batch.VertexBindings[1] = new VertexBufferBinding(batch.InstanceBuffer, 0, 1);
                    gd.SetVertexBuffers(batch.VertexBindings);
                    gd.Indices = batch.GeometryIndexBuffer;

                    _staticMapInstancedBatchesThisFrame++;
                    int passCount = effect.CurrentTechnique.Passes.Count;
                    for (int p = 0; p < passCount; p++)
                    {
                        effect.CurrentTechnique.Passes[p].Apply();
                        _staticMapInstancedDrawCallsThisFrame++;
                        gd.DrawInstancedPrimitives(
                            PrimitiveType.TriangleList,
                            0,
                            0,
                            batch.PrimitiveCount,
                            instanceCount);
                    }
                }
            }
            catch (Exception ex)
            {
                _staticMapInstancingFailed = true;
                MuGame.AppLoggerFactory?.CreateLogger<ModelObject>()?.LogWarning(ex, "Static map hardware instancing disabled after runtime failure.");
            }
            finally
            {
                gd.BlendState = prevBlend;
                gd.RasterizerState = prevRaster;
                gd.SamplerStates[0] = prevSampler;
                ClearStaticMapInstancingQueues();
            }
        }

        internal static bool HasPendingStaticMapInstancingBatches() => _staticMapInstancingActiveBatches.Count > 0;

        private static void EnsureInstanceUploadBuffer(StaticMapInstancingBatch batch, int instanceCount)
        {
            if (batch.UploadBuffer.Length >= instanceCount)
                return;

            int newSize = Math.Max(instanceCount, batch.UploadBuffer.Length == 0 ? 64 : batch.UploadBuffer.Length * 2);
            batch.UploadBuffer = new StaticModelInstanceData[newSize];
        }

        private static void EnsureInstanceVertexBuffer(GraphicsDevice gd, StaticMapInstancingBatch batch, int instanceCount)
        {
            if (batch.InstanceBuffer != null &&
                !batch.InstanceBuffer.IsDisposed &&
                batch.InstanceBufferCapacity >= instanceCount)
            {
                return;
            }

            batch.InstanceBuffer?.Dispose();
            int capacity = Math.Max(instanceCount, 64);
            batch.InstanceBuffer = new DynamicVertexBuffer(
                gd,
                StaticModelInstanceData.VertexDeclaration,
                capacity,
                BufferUsage.WriteOnly);
            batch.InstanceBufferCapacity = capacity;
        }

        private static void ClearStaticMapInstancingQueues()
        {
            for (int i = 0; i < _staticMapInstancingActiveBatches.Count; i++)
                _staticMapInstancingActiveBatches[i].Instances.Clear();

            _staticMapInstancingActiveBatches.Clear();
        }

        private static bool IsStaticMapInstancingSupported()
        {
            if (_staticMapInstancingFailed ||
                !Constants.ENABLE_MAP_OBJECT_INSTANCING ||
                !SupportsGpuDynamicSkinning)
            {
                return false;
            }

            var effect = GraphicsManager.Instance.DynamicLightingEffect;
            if (effect == null)
                return false;

            _cachedStaticMapInstancingTechnique ??= TryGetTechnique(effect, "DynamicLighting_SkinnedInstanced");
            return _cachedStaticMapInstancingTechnique != null;
        }

        private bool TryQueueStaticMapObjectForInstancing()
        {
            if (!CanUseStaticMapInstancing())
                return false;

            if (Model?.Meshes == null || _boneTextures == null)
                return false;

            int meshCount = Model.Meshes.Length;
            byte alpha = (byte)MathHelper.Clamp(TotalAlpha * 255f, 0f, 255f);
            var instanceData = new StaticModelInstanceData(WorldPosition, new Color((byte)255, (byte)255, (byte)255, alpha));

            for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
            {
                if (!CanUseStaticMapMeshForInstancing(meshIndex))
                    return false;
            }

            for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
            {
                if (!BMDLoader.Instance.TryGetGpuSkinnedMeshBuffers(
                    Model,
                    meshIndex,
                    out _,
                    out _,
                    out _))
                {
                    return false;
                }
            }

            for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
            {
                if (!BMDLoader.Instance.TryGetGpuSkinnedMeshBuffers(
                    Model,
                    meshIndex,
                    out var geometryVB,
                    out var geometryIB,
                    out var boneCount))
                {
                    return false;
                }

                bool twoSided = IsMeshTwoSided(meshIndex, false);
                Texture2D texture = _boneTextures[meshIndex];
                var key = new StaticMapInstancingBatchKey(Model, meshIndex, texture, twoSided);
                if (!_staticMapInstancingBatches.TryGetValue(key, out var batch))
                {
                    batch = new StaticMapInstancingBatch();
                    _staticMapInstancingBatches[key] = batch;
                }

                batch.GeometryVertexBuffer = geometryVB;
                batch.GeometryIndexBuffer = geometryIB;
                batch.PrimitiveCount = geometryIB.IndexCount / 3;
                batch.BoneCount = boneCount;
                batch.TwoSided = twoSided;
                batch.Texture = texture;

                if (batch.PoseSource == null || !ReferenceEquals(batch.PoseSource.Model, Model))
                    batch.PoseSource = this;

                if (batch.Instances.Count == 0)
                    _staticMapInstancingActiveBatches.Add(batch);

                batch.Instances.Add(instanceData);
                _staticMapInstancedMeshInstancesThisFrame++;
            }

            _staticMapInstancedObjectsThisFrame++;
            return true;
        }

        private bool CanUseStaticMapInstancing()
        {
            if (!IsStaticMapInstancingSupported())
                return false;

            if (!IsMapPlacementObject || !AllowMapObjectInstancing)
                return false;

            if (HasMultiFrameAnimation())
                return false;

            if (!Visible || Children.Count > 0 || Model?.Meshes == null || Model.Meshes.Length == 0)
                return false;

            if (LinkParentAnimation || ParentBoneLink >= 0 || RequiresPerFrameAnimation || ContinuousAnimation)
                return false;

            if (TotalAlpha < 0.999f)
                return false;

            return true;
        }

        private bool HasMultiFrameAnimation()
        {
            if (Model?.Actions == null || Model.Actions.Length == 0)
                return false;

            var actions = Model.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action != null && action.NumAnimationKeys > 1)
                    return true;
            }

            return false;
        }

        private bool CanUseStaticMapMeshForInstancing(int meshIndex)
        {
            if (Model?.Meshes == null || meshIndex < 0 || meshIndex >= Model.Meshes.Length)
                return false;

            if (_boneTextures == null || meshIndex >= _boneTextures.Length || _boneTextures[meshIndex] == null)
                return false;

            // Keep RGBA meshes on classic path (transparent/alpha-tested ordering).
            // Instanced static path is opaque-only and can cause vegetation flicker.
            if (_meshIsRGBA != null &&
                (uint)meshIndex < (uint)_meshIsRGBA.Length &&
                _meshIsRGBA[meshIndex])
            {
                return false;
            }

            if (IsHiddenMesh(meshIndex))
                return false;

            if (IsBlendMesh(meshIndex))
                return false;

            var blendState = GetMeshBlendState(meshIndex, false);
            if (!ReferenceEquals(blendState, BlendState.Opaque))
                return false;

            return true;
        }

        private static void PrepareStaticMapInstancingEffect(Effect effect, WorldControl world)
        {
            if (effect == null || _cachedStaticMapInstancingTechnique == null)
                return;

            effect.CurrentTechnique = _cachedStaticMapInstancingTechnique;

            var camera = Camera.Instance;
            if (camera == null)
                return;

            effect.Parameters["World"]?.SetValue(_identity);
            effect.Parameters["View"]?.SetValue(camera.View);
            effect.Parameters["Projection"]?.SetValue(camera.Projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(camera.View * camera.Projection);
            effect.Parameters["EyePosition"]?.SetValue(camera.Position);
            effect.Parameters["Alpha"]?.SetValue(1f);
            effect.Parameters["TerrainDynamicIntensityScale"]?.SetValue(1.5f);
            effect.Parameters["DebugLightingAreas"]?.SetValue(Constants.DEBUG_LIGHTING_AREAS ? 1.0f : 0.0f);

            Vector3 sunDir = GraphicsManager.Instance.ShadowMapRenderer?.LightDirection ?? Constants.SUN_DIRECTION;
            if (sunDir.LengthSquared() < 0.0001f)
                sunDir = new Vector3(1f, 0f, -0.6f);
            sunDir = Vector3.Normalize(sunDir);
            bool sunEnabled = Constants.SUN_ENABLED && (world?.IsSunWorld ?? true);

            effect.Parameters["SunDirection"]?.SetValue(sunDir);
            effect.Parameters["SunColor"]?.SetValue(_sunColor);
            effect.Parameters["SunStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveSunStrength() : 0f);
            effect.Parameters["ShadowStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveShadowStrength() : 0f);
            effect.Parameters["AmbientLight"]?.SetValue(_ambientLightVector * SunCycleManager.AmbientMultiplier);

            GraphicsManager.Instance.ShadowMapRenderer?.ApplyShadowParameters(effect);
            UploadStaticMapInstancingDynamicLights(effect, world);
        }

        private static void UploadStaticMapInstancingDynamicLights(Effect effect, WorldControl world)
        {
            if (!Constants.ENABLE_DYNAMIC_LIGHTS || world?.Terrain == null)
            {
                effect.Parameters["ActiveLightCount"]?.SetValue(0);
                effect.Parameters["MaxLightsToProcess"]?.SetValue(0);
                return;
            }

            int maxLights = Constants.OPTIMIZE_FOR_INTEGRATED_GPU ? 8 : 16;
            maxLights = Math.Min(maxLights, _staticInstancingLightPositions.Length);

            int version = world.Terrain.ActiveLightsVersion;
            if (version != _staticInstancingLastLightsVersion)
            {
                _staticInstancingLastLightsVersion = version;
                _staticInstancingLastLightCount = 0;

                var lights = world.Terrain.ActiveLights;
                if (lights != null && lights.Count > 0)
                {
                    Vector2 focus = Camera.Instance != null
                        ? new Vector2(Camera.Instance.Target.X, Camera.Instance.Target.Y)
                        : Vector2.Zero;

                    _staticInstancingLastLightCount = SelectInstancingLightsByProximity(lights, focus, maxLights);
                }
            }

            effect.Parameters["ActiveLightCount"]?.SetValue(_staticInstancingLastLightCount);
            effect.Parameters["MaxLightsToProcess"]?.SetValue(maxLights);
            if (_staticInstancingLastLightCount > 0)
            {
                effect.Parameters["LightPositions"]?.SetValue(_staticInstancingLightPositions);
                effect.Parameters["LightColors"]?.SetValue(_staticInstancingLightColors);
                effect.Parameters["LightRadii"]?.SetValue(_staticInstancingLightRadii);
                effect.Parameters["LightIntensities"]?.SetValue(_staticInstancingLightIntensities);
            }
        }

        private static int SelectInstancingLightsByProximity(
            IReadOnlyList<DynamicLightSnapshot> lights,
            Vector2 focus,
            int maxLights)
        {
            int selected = 0;
            float weakestScore = float.MaxValue;
            int weakestIndex = 0;

            for (int i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                float radius = light.Radius;
                float radiusSq = radius * radius;
                if (radiusSq <= 0.001f)
                    continue;

                var lightPos2 = new Vector2(light.Position.X, light.Position.Y);
                float distSq = Vector2.DistanceSquared(lightPos2, focus);
                float score = light.Intensity * radiusSq / (radiusSq + distSq + 1f);

                if (selected < maxLights)
                {
                    _staticInstancingLightScores[selected] = score;
                    _staticInstancingLightPositions[selected] = light.Position;
                    _staticInstancingLightColors[selected] = light.Color;
                    _staticInstancingLightRadii[selected] = light.Radius;
                    _staticInstancingLightIntensities[selected] = light.Intensity;

                    if (score < weakestScore)
                    {
                        weakestScore = score;
                        weakestIndex = selected;
                    }

                    selected++;
                }
                else if (score > weakestScore)
                {
                    _staticInstancingLightScores[weakestIndex] = score;
                    _staticInstancingLightPositions[weakestIndex] = light.Position;
                    _staticInstancingLightColors[weakestIndex] = light.Color;
                    _staticInstancingLightRadii[weakestIndex] = light.Radius;
                    _staticInstancingLightIntensities[weakestIndex] = light.Intensity;

                    weakestScore = _staticInstancingLightScores[0];
                    weakestIndex = 0;
                    for (int j = 1; j < selected; j++)
                    {
                        float s = _staticInstancingLightScores[j];
                        if (s < weakestScore)
                        {
                            weakestScore = s;
                            weakestIndex = j;
                        }
                    }
                }
            }

            return selected;
        }
    }
}
