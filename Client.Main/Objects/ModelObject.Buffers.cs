using Client.Data.Texture;
using Client.Main.Content;
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
                if (!Visible || OutOfView)
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
                        _logger?.LogDebug("SetDynamicBuffers - mesh {MeshIndex}: {Message}", meshIndex, exMesh.Message);
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
