using Client.Data.BMD;
using Client.Data.Texture;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Client.Main.Objects.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Client.Main.Objects
{
    public abstract partial class ModelObject
    {
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
                if (!Visible)
                {
                    _invalidatedBufferFlags = 0;
                    return;
                }

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
                bool needArrayResize =
                    _boneVertexBuffers?.Length != meshCount ||
                    _boneIndexBuffers?.Length != meshCount ||
                    _gpuSkinVertexBuffers?.Length != meshCount ||
                    _gpuSkinIndexBuffers?.Length != meshCount ||
                    _gpuSkinBoneCounts?.Length != meshCount ||
                    _gpuSkinMeshEnabled?.Length != meshCount ||
                    _boneTextures?.Length != meshCount ||
                    _scriptTextures?.Length != meshCount ||
                    _dataTextures?.Length != meshCount ||
                    _meshIsRGBA?.Length != meshCount ||
                    _meshHiddenByScript?.Length != meshCount ||
                    _meshBlendByScript?.Length != meshCount ||
                    _meshTexturePath?.Length != meshCount ||
                    _blendMeshIndicesScratch?.Length != meshCount ||
                    _meshBufferCache?.Length != meshCount;
                if (needArrayResize)
                {
                    EnsureArraySize(ref _boneVertexBuffers, meshCount);
                    EnsureArraySize(ref _boneIndexBuffers, meshCount);
                    EnsureArraySize(ref _gpuSkinVertexBuffers, meshCount);
                    EnsureArraySize(ref _gpuSkinIndexBuffers, meshCount);
                    EnsureArraySize(ref _gpuSkinBoneCounts, meshCount);
                    EnsureArraySize(ref _gpuSkinMeshEnabled, meshCount);
                    EnsureArraySize(ref _boneTextures, meshCount);
                    EnsureArraySize(ref _scriptTextures, meshCount);
                    EnsureArraySize(ref _dataTextures, meshCount);
                    EnsureArraySize(ref _meshIsRGBA, meshCount);
                    EnsureArraySize(ref _meshHiddenByScript, meshCount);
                    EnsureArraySize(ref _meshBlendByScript, meshCount);
                    EnsureArraySize(ref _meshTexturePath, meshCount);
                    EnsureArraySize(ref _blendMeshIndicesScratch, meshCount);
                    EnsureArraySize(ref _meshBufferCache, meshCount);
                }

                // Bone transforms are expensive to prepare. Delay until a mesh actually needs CPU skinning.
                Matrix[] bones = null;

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
                bool textureDirty = (_invalidatedBufferFlags & BUFFER_FLAG_TEXTURE) != 0;

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

                        bool canUseGpuSkinning = SupportsGpuDynamicSkinning &&
                                                 Constants.ENABLE_GPU_SKINNING &&
                                                 !hasVertexDeformer &&
                                                 DetermineShaderForMesh(meshIndex).UseDynamicLighting;

                        bool gpuSkinReady = canUseGpuSkinning &&
                                            _gpuSkinMeshEnabled != null &&
                                            (uint)meshIndex < (uint)_gpuSkinMeshEnabled.Length &&
                                            _gpuSkinMeshEnabled[meshIndex] &&
                                            _gpuSkinVertexBuffers != null &&
                                            (uint)meshIndex < (uint)_gpuSkinVertexBuffers.Length &&
                                            _gpuSkinVertexBuffers[meshIndex] != null &&
                                            _gpuSkinIndexBuffers != null &&
                                            (uint)meshIndex < (uint)_gpuSkinIndexBuffers.Length &&
                                            _gpuSkinIndexBuffers[meshIndex] != null &&
                                            _gpuSkinBoneCounts != null &&
                                            (uint)meshIndex < (uint)_gpuSkinBoneCounts.Length &&
                                            _gpuSkinBoneCounts[meshIndex] > 0;

                        if (canUseGpuSkinning && (!gpuSkinReady && TryEnableGpuSkinnedMesh(meshIndex, mesh) || gpuSkinReady))
                        {
                            EnsureMeshTextureLoaded(meshIndex, mesh, allowLazyLoad: textureDirty);
                            cache.IsValid = false; // CPU cache path is bypassed for GPU-skinned mesh.
                            continue;
                        }

                        if (_gpuSkinMeshEnabled != null && (uint)meshIndex < (uint)_gpuSkinMeshEnabled.Length)
                        {
                            _gpuSkinMeshEnabled[meshIndex] = false;
                        }

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

                        if (bones == null)
                        {
                            bones = GetCachedBoneTransforms();
                            bones = GetRenderBoneTransforms(bones) ?? bones;
                            if (bones == null)
                            {
                                _logger?.LogDebug("SetDynamicBuffers: BoneTransform == null – skip");
                                return;
                            }
                        }

                        // Generate buffers only when necessary
                        bool animationDirty = (_invalidatedBufferFlags & BUFFER_FLAG_ANIMATION) != 0;
                        BMDLoader.Instance.GetModelBuffers(
                            Model, meshIndex, bodyColor, bones,
                            ref _boneVertexBuffers[meshIndex],
                            ref _boneIndexBuffers[meshIndex],
                            // Force bypassing internal cache when texture coordinates changed
                            animationDirty || textureDirty || hasVertexDeformer,
                            vertexDeformer);

                        // Update cache
                        cache.VertexBuffer = _boneVertexBuffers[meshIndex];
                        cache.IndexBuffer = _boneIndexBuffers[meshIndex];
                        cache.CachedLight = meshLight;
                        cache.CachedBodyColor = bodyColor;
                        cache.LastUpdateFrame = currentFrame;
                        cache.IsValid = true;

                        EnsureMeshTextureLoaded(
                            meshIndex,
                            mesh,
                            allowLazyLoad: textureDirty);
                    }
                    catch (Exception exMesh)
                    {
                        _logger?.LogError(exMesh, "SetDynamicBuffers - mesh {MeshIndex}", meshIndex);
                    }
                }

                _invalidatedBufferFlags = 0; // Clear all flags
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "SetDynamicBuffers FATAL");
            }
        }

        private bool TryEnableGpuSkinnedMesh(int meshIndex, BMDTextureMesh mesh)
        {
            if (_gpuSkinMeshEnabled == null ||
                _gpuSkinVertexBuffers == null ||
                _gpuSkinIndexBuffers == null ||
                _gpuSkinBoneCounts == null ||
                Model == null ||
                mesh == null ||
                (uint)meshIndex >= (uint)_gpuSkinMeshEnabled.Length)
            {
                return false;
            }

            // Blob-shadow path requires CPU-skinned buffers. Match DrawModel conditions
            // so GPU skinning is only blocked when blob shadows are actually rendered.
            bool useShadowMap = Constants.ENABLE_DYNAMIC_LIGHTING_SHADER &&
                                GraphicsManager.Instance.ShadowMapRenderer?.IsReady == true;
            bool isNight = Constants.ENABLE_DAY_NIGHT_CYCLE && SunCycleManager.IsNight;
            bool blobShadowPassActive = RenderShadow && !useShadowMap && !LowQuality && !isNight;
            if (blobShadowPassActive)
            {
                return false;
            }

            if (!BMDLoader.Instance.TryGetGpuSkinnedMeshBuffers(
                Model,
                meshIndex,
                out var vertexBuffer,
                out var indexBuffer,
                out var boneCount))
            {
                return false;
            }

            if (boneCount <= 0 || boneCount > MaxGpuSkinBones)
            {
                return false;
            }

            _gpuSkinVertexBuffers[meshIndex] = vertexBuffer;
            _gpuSkinIndexBuffers[meshIndex] = indexBuffer;
            _gpuSkinBoneCounts[meshIndex] = boneCount;
            _gpuSkinMeshEnabled[meshIndex] = true;

            // Free dynamic CPU buffers for this mesh - GPU skinning replaces this path.
            if (_boneVertexBuffers != null && (uint)meshIndex < (uint)_boneVertexBuffers.Length)
            {
                var cpuVB = _boneVertexBuffers[meshIndex];
                if (cpuVB != null)
                {
                    DynamicBufferPool.ReturnVertexBuffer(cpuVB);
                    _boneVertexBuffers[meshIndex] = null;
                }
            }

            if (_boneIndexBuffers != null && (uint)meshIndex < (uint)_boneIndexBuffers.Length)
            {
                var cpuIB = _boneIndexBuffers[meshIndex];
                if (cpuIB != null)
                {
                    DynamicBufferPool.ReturnIndexBuffer(cpuIB);
                    _boneIndexBuffers[meshIndex] = null;
                }
            }

            return true;
        }

        private void EnsureMeshTextureLoaded(int meshIndex, BMDTextureMesh mesh, bool allowLazyLoad)
        {
            if (_boneTextures == null ||
                _scriptTextures == null ||
                _dataTextures == null ||
                mesh == null ||
                Model == null ||
                (uint)meshIndex >= (uint)_boneTextures.Length ||
                (uint)meshIndex >= (uint)_scriptTextures.Length ||
                (uint)meshIndex >= (uint)_dataTextures.Length)
            {
                return;
            }

            string texturePath = null;
            if (_meshTexturePath != null && (uint)meshIndex < (uint)_meshTexturePath.Length)
            {
                texturePath = _meshTexturePath[meshIndex];
            }

            if (string.IsNullOrEmpty(texturePath))
            {
                texturePath = BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);
                if (_meshTexturePath != null && (uint)meshIndex < (uint)_meshTexturePath.Length)
                {
                    _meshTexturePath[meshIndex] = texturePath;
                }
            }

            if (string.IsNullOrEmpty(texturePath))
            {
                return;
            }

            if (allowLazyLoad && _boneTextures[meshIndex] == null)
            {
                _ = TextureLoader.Instance.Prepare(texturePath);
            }

            var resolvedTexture = TextureLoader.Instance.GetTexture2D(texturePath);
            if (!ReferenceEquals(_boneTextures[meshIndex], resolvedTexture))
            {
                _boneTextures[meshIndex] = resolvedTexture;
                _sortTextureHintDirty = true;
                _sortTextureHint = null;
            }

            bool needsMetadataRefresh = allowLazyLoad || _scriptTextures[meshIndex] == null || _dataTextures[meshIndex] == null;
            if (!needsMetadataRefresh)
            {
                return;
            }

            var script = TextureLoader.Instance.GetScript(texturePath);
            var data = TextureLoader.Instance.Get(texturePath);
            _scriptTextures[meshIndex] = script;
            _dataTextures[meshIndex] = data;

            if (_meshIsRGBA != null && (uint)meshIndex < (uint)_meshIsRGBA.Length)
            {
                _meshIsRGBA[meshIndex] = data?.Components == 4;
            }

            if (_meshHiddenByScript != null && (uint)meshIndex < (uint)_meshHiddenByScript.Length)
            {
                _meshHiddenByScript[meshIndex] = script?.HiddenMesh ?? false;
            }

            if (_meshBlendByScript != null && (uint)meshIndex < (uint)_meshBlendByScript.Length)
            {
                _meshBlendByScript[meshIndex] = script?.Bright ?? false;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureArraySize<T>(ref T[] array, int size)
        {
            if (array is null || array.Length != size)
                array = new T[size];
        }

        public void InvalidateBuffers(uint flags = BUFFER_FLAG_ALL)
        {
            _dynamicBuffersFrozen = false;
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
