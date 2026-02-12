using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Graphics;
using Client.Main.Objects.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Client.Main.Objects
{
    public abstract partial class ModelObject
    {
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
            bool useDynamicLighting = AllowDynamicLightingShader &&
                                      !useItemMaterial && !useMonsterMaterial &&
                                      Constants.ENABLE_DYNAMIC_LIGHTING_SHADER &&
                                      GraphicsManager.Instance.DynamicLightingEffect != null;

            return new ShaderSelection(useDynamicLighting, useItemMaterial, useMonsterMaterial);
        }

        // Determines if this mesh needs special shader path and cannot use fast alpha path
        private bool NeedsSpecialShaderForMesh(int mesh)
        {
            return DetermineShaderForMesh(mesh).NeedsSpecialShader;
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

        public virtual void DrawMesh(int mesh)
        {
            if (Model?.Meshes == null || mesh < 0 || mesh >= Model.Meshes.Length)
                return;
            if (_boneTextures?[mesh] == null || IsHiddenMesh(mesh))
                return;

            bool hasCpuBuffers = _boneVertexBuffers?[mesh] != null && _boneIndexBuffers?[mesh] != null;
            bool hasGpuDynamicBuffers = _gpuSkinMeshEnabled != null &&
                                        (uint)mesh < (uint)_gpuSkinMeshEnabled.Length &&
                                        _gpuSkinMeshEnabled[mesh] &&
                                        _gpuSkinVertexBuffers != null &&
                                        (uint)mesh < (uint)_gpuSkinVertexBuffers.Length &&
                                        _gpuSkinVertexBuffers[mesh] != null &&
                                        _gpuSkinIndexBuffers != null &&
                                        (uint)mesh < (uint)_gpuSkinIndexBuffers.Length &&
                                        _gpuSkinIndexBuffers[mesh] != null;

            var shaderSelection = DetermineShaderForMesh(mesh);

            if (shaderSelection.UseDynamicLighting)
            {
                if (!hasCpuBuffers && !hasGpuDynamicBuffers)
                    return;

                DrawMeshWithDynamicLighting(mesh);
                return;
            }

            if (!hasCpuBuffers)
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
            if (_boneTextures?[mesh] == null || IsHiddenMesh(mesh))
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
                    var texture = _boneTextures[mesh];
                    bool useGpuSkinning = _gpuSkinMeshEnabled != null &&
                                          (uint)mesh < (uint)_gpuSkinMeshEnabled.Length &&
                                          _gpuSkinMeshEnabled[mesh] &&
                                          _gpuSkinVertexBuffers != null &&
                                          (uint)mesh < (uint)_gpuSkinVertexBuffers.Length &&
                                          _gpuSkinVertexBuffers[mesh] != null &&
                                          _gpuSkinIndexBuffers != null &&
                                          (uint)mesh < (uint)_gpuSkinIndexBuffers.Length &&
                                          _gpuSkinIndexBuffers[mesh] != null;

                    VertexBuffer vertexBuffer = useGpuSkinning ? _gpuSkinVertexBuffers[mesh] : _boneVertexBuffers?[mesh];
                    IndexBuffer indexBuffer = useGpuSkinning ? _gpuSkinIndexBuffers[mesh] : _boneIndexBuffers?[mesh];
                    if (vertexBuffer == null || indexBuffer == null)
                        return;

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

                    int requiredBoneCount = useGpuSkinning &&
                                            _gpuSkinBoneCounts != null &&
                                            (uint)mesh < (uint)_gpuSkinBoneCounts.Length
                        ? _gpuSkinBoneCounts[mesh]
                        : 0;

                    bool needsGpuBoneRefresh = useGpuSkinning &&
                                               requiredBoneCount > _dynamicLightingPreparedGpuBoneCount;

                    if (_dynamicLightingPreparedInvocationId != _drawModelInvocationId ||
                        _dynamicLightingPreparedWithGpuSkinning != useGpuSkinning ||
                        needsGpuBoneRefresh)
                    {
                        PrepareDynamicLightingEffect(effect, useGpuSkinning, requiredBoneCount);
                        _dynamicLightingPreparedInvocationId = _drawModelInvocationId;
                        _dynamicLightingPreparedWithGpuSkinning = useGpuSkinning;
                        _dynamicLightingPreparedGpuBoneCount = useGpuSkinning ? requiredBoneCount : 0;
                    }

                    if (useGpuSkinning && !string.Equals(effect.CurrentTechnique?.Name, "DynamicLighting_Skinned", StringComparison.Ordinal))
                    {
                        _dynamicLightingPreparedWithGpuSkinning = false;
                        _dynamicLightingPreparedGpuBoneCount = 0;
                        vertexBuffer = _boneVertexBuffers?[mesh];
                        indexBuffer = _boneIndexBuffers?[mesh];
                        if (vertexBuffer == null || indexBuffer == null)
                            return;
                        useGpuSkinning = false;
                    }

                    if (useGpuSkinning)
                        RegisterGpuSkinnedMeshDraw();

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
            if (alphaTestEffect == null || alphaTestEffect.CurrentTechnique == null) return;

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
    }
}
