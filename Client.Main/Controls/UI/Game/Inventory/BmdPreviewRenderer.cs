using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using System.Reflection;

namespace Client.Main.Controls.UI.Game.Inventory
{
    /// <summary>
    /// Utility for generating simple 3D previews of BMD models with proper BlendState support.
    /// </summary>
    public static class BmdPreviewRenderer
    {
        private readonly struct ItemRenderProperties
        {
            public static ItemRenderProperties Default => new(0, false, false);

            public ItemRenderProperties(int level, bool isExcellent, bool isAncient)
            {
                Level = Math.Clamp(level, 0, 15);
                IsExcellent = isExcellent;
                IsAncient = isAncient;
            }

            public int Level { get; }
            public bool IsExcellent { get; }
            public bool IsAncient { get; }

            public bool RequiresDistinctKey => Level != 0 || IsExcellent || IsAncient;
            public bool ShouldUseItemMaterial => Level >= 7 || IsExcellent || IsAncient;
            public int ItemOptions => (Level & 0x0F) | (IsExcellent ? 0x10 : 0);
        }

        private sealed class PreviewCacheEntry : IDisposable
        {
            public PreviewCacheEntry(RenderTarget2D texture, float lastUpdateTime, bool requiresAnimation)
            {
                Texture = texture;
                LastUpdateTime = lastUpdateTime;
                RequiresAnimation = requiresAnimation;
            }

            public RenderTarget2D Texture { get; private set; }
            public float LastUpdateTime { get; set; }
            public bool RequiresAnimation { get; set; }

            public void UpdateTexture(RenderTarget2D texture)
            {
                if (ReferenceEquals(Texture, texture))
                {
                    return;
                }

                Texture?.Dispose();
                Texture = texture;
            }

            public void Dispose()
            {
                Texture?.Dispose();
                Texture = null;
            }
        }

        private static readonly Dictionary<string, PreviewCacheEntry> _cache = new();
        private static readonly Dictionary<string, PreviewCacheEntry> _rotatingCache = new();
        private static readonly Queue<string> _rotatingCacheKeys = new();
        private static readonly HashSet<string> _failedRenders = new();
        private static readonly Dictionary<string, BlendState> _previewBlendStateCache = new();

        private const int MaxRotatingCacheSize = 96;
        private const float AnimatedUpdateInterval = 1f / 23f; // limit

        private static ItemRenderProperties CreateRenderProperties(InventoryItem item)
        {
            if (item == null)
            {
                return ItemRenderProperties.Default;
            }

            var details = item.Details;
            int level = Math.Max(details.Level, item.Level);
            bool isExcellent = details.IsExcellent;
            bool isAncient = details.IsAncient;

            return new ItemRenderProperties(level, isExcellent, isAncient);
        }

        private static string BuildCacheKey(ItemDefinition definition, int width, int height, float rotationAngle, in ItemRenderProperties props)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.TexturePath))
            {
                return string.Empty;
            }

            string key = $"{definition.TexturePath}:{width}x{height}";
            if (rotationAngle != 0f)
            {
                key = $"{key}:{rotationAngle:F0}";
            }

            if (props.RequiresDistinctKey)
            {
                key = $"{key}:lvl{props.Level:X2}:ex{(props.IsExcellent ? 1 : 0)}:an{(props.IsAncient ? 1 : 0)}";
            }

            return key;
        }

        private static PreviewCacheEntry GetCacheEntry(string key, bool isRotating)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (!isRotating)
            {
                return _cache.TryGetValue(key, out var cached) ? cached : null;
            }

            return _rotatingCache.TryGetValue(key, out var cachedRot) ? cachedRot : null;
        }

        private static void StoreCacheEntry(string key, PreviewCacheEntry entry, bool isRotating)
        {
            if (string.IsNullOrEmpty(key) || entry == null)
            {
                return;
            }

            if (!isRotating)
            {
                if (_cache.TryGetValue(key, out var existing) && !ReferenceEquals(existing, entry))
                {
                    existing.Dispose();
                }
                _cache[key] = entry;
                return;
            }

            if (!_rotatingCache.ContainsKey(key))
            {
                _rotatingCacheKeys.Enqueue(key);
            }
            else if (!ReferenceEquals(_rotatingCache[key], entry))
            {
                _rotatingCache[key]?.Dispose();
            }

            _rotatingCache[key] = entry;

            while (_rotatingCacheKeys.Count > MaxRotatingCacheSize)
            {
                string oldestKey = _rotatingCacheKeys.Dequeue();
                if (_rotatingCache.TryGetValue(oldestKey, out var removed))
                {
                    removed.Dispose();
                    _rotatingCache.Remove(oldestKey);
                }
            }
        }

        private static float ResolveEffectTime(GameTime gameTime)
        {
            if (gameTime != null)
            {
                return (float)gameTime.TotalGameTime.TotalSeconds;
            }

            var muTime = MuGame.Instance?.GameTime;
            if (muTime != null)
            {
                return (float)muTime.TotalGameTime.TotalSeconds;
            }

            return Environment.TickCount * 0.001f;
        }

        public static Texture2D GetPreview(ItemDefinition definition, int width, int height, float rotationAngle = 0f)
        {
            return GetPreviewInternal(definition, width, height, rotationAngle, ItemRenderProperties.Default, gameTime: null, useCache: true);
        }

        public static Texture2D GetPreview(InventoryItem item, int width, int height, float rotationAngle = 0f)
        {
            return GetPreviewInternal(item?.Definition, width, height, rotationAngle, CreateRenderProperties(item), gameTime: null, useCache: true);
        }

        /// <summary>
        /// Tries to retrieve a cached preview without rendering a new one. Returns null if not cached.
        /// </summary>
        public static Texture2D TryGetCachedPreview(ItemDefinition definition, int width, int height, float rotationAngle = 0f)
        {
            var key = BuildCacheKey(definition, width, height, rotationAngle, ItemRenderProperties.Default);
            return GetCacheEntry(key, rotationAngle != 0f)?.Texture;
        }

        public static Texture2D TryGetCachedPreview(InventoryItem item, int width, int height, float rotationAngle = 0f)
        {
            var key = BuildCacheKey(item?.Definition, width, height, rotationAngle, CreateRenderProperties(item));
            return GetCacheEntry(key, rotationAngle != 0f)?.Texture;
        }

        /// <summary>
        /// Creates an animated rotating preview for mouse hover effect
        /// </summary>
        public static Texture2D GetAnimatedPreview(ItemDefinition definition, int width, int height, GameTime gameTime)
        {
            if (gameTime == null)
                return GetPreview(definition, width, height, 0f);

            float rotationAngle = CalculateCachedRotationAngle(gameTime.TotalGameTime.TotalSeconds, 120f);
            return GetPreviewInternal(definition, width, height, rotationAngle, ItemRenderProperties.Default, gameTime, useCache: true);
        }

        public static Texture2D GetAnimatedPreview(InventoryItem item, int width, int height, GameTime gameTime)
        {
            if (gameTime == null)
                return GetPreview(item, width, height, 0f);

            float rotationAngle = CalculateCachedRotationAngle(gameTime.TotalGameTime.TotalSeconds, 120f);
            return GetPreviewInternal(item?.Definition, width, height, rotationAngle, CreateRenderProperties(item), gameTime, useCache: true);
        }

        /// <summary>
        /// Test function with more obvious rotation for debugging
        /// </summary>
        public static Texture2D GetTestRotatingPreview(ItemDefinition definition, int width, int height, GameTime gameTime)
        {
            if (gameTime == null)
                return GetPreview(definition, width, height, 0f);

            float rotationAngle = CalculateCachedRotationAngle(gameTime.TotalGameTime.TotalSeconds, 90f);
            return GetPreviewInternal(definition, width, height, rotationAngle, ItemRenderProperties.Default, gameTime, useCache: true);
        }

        public static Texture2D GetTestRotatingPreview(InventoryItem item, int width, int height, GameTime gameTime)
        {
            if (gameTime == null)
                return GetPreview(item, width, height, 0f);

            float rotationAngle = CalculateCachedRotationAngle(gameTime.TotalGameTime.TotalSeconds, 90f);
            return GetPreviewInternal(item?.Definition, width, height, rotationAngle, CreateRenderProperties(item), gameTime, useCache: true);
        }

        /// <summary>
        /// Smooth preview without any caching (for perfectly smooth animation)
        /// </summary>
        public static Texture2D GetSmoothRotatingPreview(ItemDefinition definition, int width, int height, GameTime gameTime)
        {
            if (gameTime == null)
                return GetPreview(definition, width, height, 0f);

            float rotationAngle = CalculateRawRotationAngle(gameTime.TotalGameTime.TotalSeconds, 120f);
            return Render(definition, width, height, rotationAngle, ItemRenderProperties.Default, gameTime);
        }

        public static Texture2D GetSmoothRotatingPreview(InventoryItem item, int width, int height, GameTime gameTime)
        {
            if (gameTime == null)
                return GetPreview(item, width, height, 0f);

            float rotationAngle = CalculateRawRotationAngle(gameTime.TotalGameTime.TotalSeconds, 120f);
            return Render(item?.Definition, width, height, rotationAngle, CreateRenderProperties(item), gameTime);
        }

        /// <summary>
        /// Retrieves a preview that allows item material shader animation to advance over time without requiring hover.
        /// </summary>
        public static Texture2D GetMaterialAnimatedPreview(InventoryItem item, int width, int height, GameTime gameTime)
        {
            return GetPreviewInternal(item?.Definition, width, height, 0f, CreateRenderProperties(item), gameTime, useCache: true);
        }

        private static float CalculateCachedRotationAngle(double totalSeconds, float speedDegreesPerSecond)
        {
            float angle = CalculateRawRotationAngle(totalSeconds, speedDegreesPerSecond);
            return MathF.Round(angle / 5f) * 5f;
        }

        private static float CalculateRawRotationAngle(double totalSeconds, float speedDegreesPerSecond)
        {
            return (float)(totalSeconds * speedDegreesPerSecond) % 360f;
        }

        private static Texture2D GetPreviewInternal(ItemDefinition definition, int width, int height, float rotationAngle, in ItemRenderProperties props, GameTime gameTime, bool useCache)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.TexturePath))
                return null;

            bool isRotating = rotationAngle != 0f;
            string key = BuildCacheKey(definition, width, height, rotationAngle, props);
            float now = ResolveEffectTime(gameTime);
            bool requiresAnimation = props.ShouldUseItemMaterial && Constants.ENABLE_ITEM_MATERIAL_ANIMATION;

            PreviewCacheEntry entry = useCache ? GetCacheEntry(key, isRotating) : null;

            if (!useCache && _failedRenders.Contains(key) && entry == null)
            {
                return null;
            }

            if (entry != null && (!requiresAnimation || now - entry.LastUpdateTime < AnimatedUpdateInterval))
            {
                return entry.Texture;
            }

            if (entry == null && _failedRenders.Contains(key))
            {
                return null;
            }

            try
            {
                var target = entry?.Texture;
                var rendered = Render(definition, width, height, rotationAngle, props, gameTime, target);

                if (rendered == null)
                {
                    if (entry == null)
                    {
                        _failedRenders.Add(key);
                        return null;
                    }

                    return entry.Texture;
                }

                if (!useCache)
                {
                    _failedRenders.Remove(key);
                    return rendered;
                }

                if (entry == null)
                {
                    entry = new PreviewCacheEntry(rendered, now, requiresAnimation);
                    StoreCacheEntry(key, entry, isRotating);
                }
                else
                {
                    entry.UpdateTexture(rendered);
                    entry.LastUpdateTime = now;
                    entry.RequiresAnimation = requiresAnimation;
                }

                _failedRenders.Remove(key);
                return entry.Texture;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("UI thread"))
            {
                if (entry == null)
                {
                    _failedRenders.Add(key);
                }
                return entry?.Texture;
            }
            catch (Exception)
            {
                if (entry == null)
                {
                    _failedRenders.Add(key);
                }
                return entry?.Texture;
            }
        }

        private static RenderTarget2D Render(ItemDefinition def, int width, int height, float rotationAngle, in ItemRenderProperties props, GameTime gameTime, RenderTarget2D target = null)
        {
            RenderTarget2D rt = target;
            bool createdNewTarget = false;
            try
            {
                var gd = GraphicsManager.Instance.GraphicsDevice;
                if (gd == null)
                    return target;

                var modelTask = BMDLoader.Instance.Prepare(def.TexturePath);
                var bmd = modelTask.ConfigureAwait(false).GetAwaiter().GetResult();
                if (bmd == null)
                    return target;

                var bones = BuildBoneMatrices(bmd);
                var originalBounds = ComputeBounds(bmd, bones);

                if (rt == null || rt.IsDisposed || rt.Width != width || rt.Height != height)
                {
                    target?.Dispose();
                    rt = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24);
                    createdNewTarget = true;
                }

                var prevTargets = gd.GetRenderTargets();
                var originalBlendState = gd.BlendState;
                var originalDepthStencilState = gd.DepthStencilState;
                var originalRasterizerState = gd.RasterizerState;

                gd.SetRenderTarget(rt);
                gd.Clear(Color.Transparent);

                gd.BlendState = BlendState.AlphaBlend;
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone;

                Matrix view = Matrix.CreateLookAt(new Vector3(0, 0, 40f), Vector3.Zero, Vector3.Up);
                Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(30f), (float)width / height, 1f, 100f);

                Matrix baseRotation = ItemOrientationHelper.GetInventoryBaseRotation(def);

                Matrix mouseRotation = Matrix.CreateRotationY(MathHelper.ToRadians(rotationAngle));

                Vector3[] originalCorners = originalBounds.GetCorners();
                Vector3 rotatedMin = new Vector3(float.MaxValue);
                Vector3 rotatedMax = new Vector3(float.MinValue);

                foreach (Vector3 corner in originalCorners)
                {
                    Vector3 rotatedCorner = Vector3.Transform(corner, baseRotation);
                    rotatedMin = Vector3.Min(rotatedMin, rotatedCorner);
                    rotatedMax = Vector3.Max(rotatedMax, rotatedCorner);
                }

                BoundingBox rotatedBounds = new BoundingBox(rotatedMin, rotatedMax);
                Vector3 rotatedSize = rotatedBounds.Max - rotatedBounds.Min;

                float scale = 15f / Math.Max(rotatedSize.X, Math.Max(rotatedSize.Y, rotatedSize.Z));

                Vector3 originalCenter = (originalBounds.Min + originalBounds.Max) * 0.5f;

                Matrix finalRotation = rotationAngle != 0f ? baseRotation * mouseRotation : baseRotation;

                Matrix worldBase = Matrix.CreateScale(scale) * finalRotation * Matrix.CreateTranslation(-originalCenter * scale);

                Vector3[] transformedCorners = new Vector3[8];
                for (int i = 0; i < originalCorners.Length; i++)
                {
                    transformedCorners[i] = Vector3.Transform(originalCorners[i], worldBase);
                }

                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;

                foreach (Vector3 corner in transformedCorners)
                {
                    if (corner.X < minX) minX = corner.X;
                    if (corner.X > maxX) maxX = corner.X;
                    if (corner.Y < minY) minY = corner.Y;
                    if (corner.Y > maxY) maxY = corner.Y;
                    if (corner.Z < minZ) minZ = corner.Z;
                    if (corner.Z > maxZ) maxZ = corner.Z;
                }

                float xOffset = -(minX + maxX) * 0.5f;
                float yOffset = -(minY + maxY) * 0.5f;
                float zOffset = -(minZ + maxZ) * 0.5f;

                Matrix world = worldBase * Matrix.CreateTranslation(xOffset, yOffset, zOffset);
                Matrix worldViewProjection = world * view * projection;
                Vector3 eyePosition = new Vector3(0f, 0f, 40f);

                bool useItemMaterial = props.ShouldUseItemMaterial &&
                                       Constants.ENABLE_ITEM_MATERIAL_SHADER &&
                                       GraphicsManager.Instance.ItemMaterialEffect != null;

                var meshOrder = GetMeshRenderOrder(bmd);

                if (!useItemMaterial)
                {
                    var effect = GraphicsManager.Instance.BasicEffect3D;
                    Matrix oldV = effect.View;
                    Matrix oldP = effect.Projection;
                    Matrix oldW = effect.World;

                    effect.View = view;
                    effect.Projection = projection;
                    effect.World = world;

                    foreach (int meshIdx in meshOrder)
                    {
                        RenderMeshWithBlendState(gd, effect, bmd, meshIdx, bones);
                    }

                    effect.View = oldV;
                    effect.Projection = oldP;
                    effect.World = oldW;
                }
                else
                {
                    float shaderTime = ResolveEffectTime(gameTime);
                    foreach (int meshIdx in meshOrder)
                    {
                        RenderMeshWithItemMaterialPreview(gd, bmd, meshIdx, bones, world, view, projection, worldViewProjection, eyePosition, props, shaderTime);
                    }
                }

                gd.SetRenderTarget(null);
                if (prevTargets != null && prevTargets.Length > 0)
                {
                    gd.SetRenderTargets(prevTargets);
                }
                gd.BlendState = originalBlendState;
                gd.DepthStencilState = originalDepthStencilState;
                gd.RasterizerState = originalRasterizerState;

                return rt;
            }
            catch
            {
                if (createdNewTarget && rt != null && !rt.IsDisposed)
                {
                    rt.Dispose();
                }
                return null;
            }
        }

        private static List<int> GetMeshRenderOrder(Client.Data.BMD.BMD bmd)
        {
            var opaqueOrder = new List<int>();
            var transparentOrder = new List<int>();

            for (int i = 0; i < bmd.Meshes.Length; i++)
            {
                var mesh = bmd.Meshes[i];
                bool isTransparent = !string.IsNullOrEmpty(mesh.BlendingMode) &&
                                   mesh.BlendingMode != "Opaque";

                if (isTransparent)
                    transparentOrder.Add(i);
                else
                    opaqueOrder.Add(i);
            }

            // Render opaque first, then transparent
            var result = new List<int>(opaqueOrder.Count + transparentOrder.Count);
            result.AddRange(opaqueOrder);
            result.AddRange(transparentOrder);

            return result;
        }

        private static void RenderMeshWithBlendState(GraphicsDevice gd, BasicEffect effect,
                                                   Client.Data.BMD.BMD bmd, int meshIdx,
                                                   Matrix[] bones)
        {
            var mesh = bmd.Meshes[meshIdx];

            // Save current blend state
            var currentBlendState = gd.BlendState;
            var currentRasterizerState = gd.RasterizerState;

            try
            {
                // Get custom BlendState from mesh configuration
                BlendState customBlendState = GetBlendStateForMesh(mesh);

                // Apply BlendState
                if (customBlendState != null)
                {
                    gd.BlendState = customBlendState;
                }

                // Check if mesh needs two-sided rendering (for transparent/blend meshes)
                bool isTwoSided = customBlendState != null && customBlendState != BlendState.Opaque;
                if (isTwoSided)
                {
                    gd.RasterizerState = RasterizerState.CullNone;
                }

                // Generate vertex and index buffers
                DynamicVertexBuffer vb = null;
                DynamicIndexBuffer ib = null;

                BMDLoader.Instance.GetModelBuffers(bmd, meshIdx, Color.White,
                                                 bones, ref vb, ref ib, false);

                if (vb != null && ib != null)
                {
                    // Set texture
                    effect.Texture = TextureLoader.Instance.GetTexture2D(
                                         BMDLoader.Instance.GetTexturePath(
                                             bmd, mesh.TexturePath));

                    // Render the mesh
                    foreach (var pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        gd.SetVertexBuffer(vb);
                        gd.Indices = ib;
                        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                                               0, 0, ib.IndexCount / 3);
                    }
                }
            }
            finally
            {
                // Always restore original states
                gd.BlendState = currentBlendState;
                gd.RasterizerState = currentRasterizerState;
            }
        }

        private static void RenderMeshWithItemMaterialPreview(GraphicsDevice gd,
                                                              Client.Data.BMD.BMD bmd,
                                                              int meshIdx,
                                                              Matrix[] bones,
                                                              Matrix world,
                                                              Matrix view,
                                                              Matrix projection,
                                                              Matrix worldViewProjection,
                                                              Vector3 eyePosition,
                                                              in ItemRenderProperties props,
                                                              float shaderTime)
        {
            var effect = GraphicsManager.Instance.ItemMaterialEffect;
            if (effect == null)
            {
                return;
            }

            var mesh = bmd.Meshes[meshIdx];

            var currentBlendState = gd.BlendState;
            var currentRasterizerState = gd.RasterizerState;

            try
            {
                BlendState customBlendState = GetBlendStateForMesh(mesh);
                if (customBlendState != null)
                {
                    gd.BlendState = customBlendState;
                }

                bool isTwoSided = customBlendState != null && customBlendState != BlendState.Opaque;
                if (isTwoSided)
                {
                    gd.RasterizerState = RasterizerState.CullNone;
                }

                DynamicVertexBuffer vb = null;
                DynamicIndexBuffer ib = null;

                BMDLoader.Instance.GetModelBuffers(bmd, meshIdx, Color.White, bones, ref vb, ref ib, skipCache: false);

                if (vb == null || ib == null)
                {
                    return;
                }

                var texturePath = BMDLoader.Instance.GetTexturePath(bmd, mesh.TexturePath);
                if (string.IsNullOrEmpty(texturePath))
                {
                    return;
                }

                var texture = TextureLoader.Instance.GetTexture2D(texturePath);
                if (texture == null)
                {
                    return;
                }

                effect.Parameters["World"]?.SetValue(world);
                effect.Parameters["View"]?.SetValue(view);
                effect.Parameters["Projection"]?.SetValue(projection);
                effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
                effect.Parameters["EyePosition"]?.SetValue(eyePosition);
                effect.Parameters["DiffuseTexture"]?.SetValue(texture);
                effect.Parameters["ItemOptions"]?.SetValue(props.ItemOptions);
                effect.Parameters["IsExcellent"]?.SetValue(props.IsExcellent);
                effect.Parameters["IsAncient"]?.SetValue(props.IsAncient);
                effect.Parameters["Time"]?.SetValue(shaderTime);
                effect.Parameters["Alpha"]?.SetValue(1f);

                gd.SetVertexBuffer(vb);
                gd.Indices = ib;

                int primitiveCount = ib.IndexCount / 3;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
                }
            }
            finally
            {
                gd.BlendState = currentBlendState;
                gd.RasterizerState = currentRasterizerState;
            }
        }

        private static BlendState GetBlendStateForMesh(Client.Data.BMD.BMDTextureMesh mesh)
        {
            if (string.IsNullOrEmpty(mesh.BlendingMode))
                return null;

            // Check cache first
            if (_previewBlendStateCache.TryGetValue(mesh.BlendingMode, out var cachedState))
                return cachedState;

            // Use reflection to get BlendState from Blendings class
            try
            {
                var field = typeof(Blendings).GetField(mesh.BlendingMode,
                                                      BindingFlags.Public | BindingFlags.Static);
                if (field != null && field.FieldType == typeof(BlendState))
                {
                    var blendState = (BlendState)field.GetValue(null);
                    _previewBlendStateCache[mesh.BlendingMode] = blendState;
                    return blendState;
                }
            }
            catch (Exception)
            {
                // If reflection fails, cache null to avoid repeated attempts
                _previewBlendStateCache[mesh.BlendingMode] = null;
            }

            return null;
        }

        private static Matrix[] BuildBoneMatrices(Client.Data.BMD.BMD bmd)
        {
            var bones = bmd.Bones;
            var result = new Matrix[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                Matrix local = Matrix.Identity;
                if (bone.Matrixes != null && bone.Matrixes.Length > 0)
                {
                    var bm = bone.Matrixes[0];
                    if (bm.Position?.Length > 0 && bm.Quaternion?.Length > 0)
                    {
                        var q = bm.Quaternion[0];
                        local = Matrix.CreateFromQuaternion(new Microsoft.Xna.Framework.Quaternion(q.X, q.Y, q.Z, q.W));
                        var p = bm.Position[0];
                        local.Translation = new Vector3(p.X, p.Y, p.Z);
                    }
                }
                if (bone.Parent >= 0 && bone.Parent < result.Length)
                    result[i] = local * result[bone.Parent];
                else
                    result[i] = local;
            }
            return result;
        }

        private static BoundingBox ComputeBounds(Client.Data.BMD.BMD bmd, Matrix[] bones)
        {
            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);
            foreach (var mesh in bmd.Meshes)
            {
                foreach (var vert in mesh.Vertices)
                {
                    Matrix m = vert.Node < bones.Length ? bones[vert.Node] : Matrix.Identity;
                    Vector3 pos = Vector3.Transform(new Vector3(vert.Position.X, vert.Position.Y, vert.Position.Z), m);
                    min = Vector3.Min(min, pos);
                    max = Vector3.Max(max, pos);
                }
            }
            return new BoundingBox(min, max);
        }
    }

    internal static class ItemOrientationHelper
    {
        private const float MinAxisLengthSq = 1e-6f;

        public static Matrix GetInventoryBaseRotation(ItemDefinition definition)
        {
            if (definition == null)
            {
                return MuRotationConverter.ConvertToMonoGame(25f, 45f, 0f);
            }

            short group = (short)definition.Group;
            if (group == 6)
            {
                return MuRotationConverter.ConvertToMonoGame(270f, 270f, 0f);
            }

            if (group == 13 || group == 14)
            {
                return MuRotationConverter.ConvertToMonoGame(270f, 0f, 0f);
            }

            if (group == 0 || group == 1 || group == 2 || group == 3 || group == 5)
            {
                return MuRotationConverter.ConvertToMonoGame(25f, 45f, 0f);
            }

            return MuRotationConverter.ConvertToMonoGame(270f, -10f, 0f);
        }

        public static Quaternion GetInventoryOrientation(ItemDefinition definition)
        {
            Matrix rotation = GetInventoryBaseRotation(definition);
            return Quaternion.CreateFromRotationMatrix(rotation);
        }

        public static Vector3 GetWorldDropEuler(ItemDefinition definition)
        {
            // Get the same MU rotation values used in inventory
            (float muX, float muY, float muZ) = GetMuRotationValues(definition);

            // Apply EXACTLY the same conversion as MuRotationConverter.ConvertToMonoGame
            bool hasLargeAngle = (MathF.Abs(muX) >= 180f || MathF.Abs(muY) >= 180f || MathF.Abs(muZ) >= 180f);

            float monoX = muX;
            float monoY = -muY;
            float monoZ = hasLargeAngle ? muZ : muZ + 180f;

            // Convert to radians (ModelObject.Angle expects radians)
            Vector3 inventoryAngle = new Vector3(
                MathHelper.ToRadians(monoX),
                MathHelper.ToRadians(monoY),
                MathHelper.ToRadians(monoZ)
            );

            // Apply camera space transformation offset
            // This rotates from inventory camera view to world space isometric view
            inventoryAngle.X += -MathHelper.PiOver2; // -90° pitch
            inventoryAngle.Y += MathHelper.PiOver2;  // +90° yaw

            return inventoryAngle;
        }

        /// <summary>
        /// Returns the MU Online rotation values for each item group
        /// These are the same values used in GetInventoryBaseRotation
        /// </summary>
        private static (float muX, float muY, float muZ) GetMuRotationValues(ItemDefinition definition)
        {
            if (definition == null)
            {
                return (25f, 45f, 0f); // Default weapons
            }

            short group = (short)definition.Group;

            // Shields
            if (group == 6)
            {
                return (270f, 270f, 0f);
            }

            // Wings
            if (group == 13 || group == 14)
            {
                return (270f, 0f, 0f);
            }

            // Weapons (swords, axes, maces, spears, bows)
            if (group == 0 || group == 1 || group == 2 || group == 3 || group == 5)
            {
                return (25f, 45f, 0f);
            }

            // Default for other groups
            return (270f, -10f, 0f);
        }

        private static float NormalizeAngle(float angle)
        {
            const float twoPi = MathF.PI * 2f;
            angle %= twoPi;
            if (angle <= -MathF.PI)
            {
                angle += twoPi;
            }
            else if (angle > MathF.PI)
            {
                angle -= twoPi;
            }
            return angle;
        }
    }

    /// <summary>
    /// Smart MU Online to MonoGame rotation converter - Final Version
    /// Based on discovered patterns: Small angles need Z+180°, Large angles don't
    /// </summary>
    public static class MuRotationConverter
    {
        /// <summary>
        /// Converts MU Online rotation to MonoGame with automatic correction detection
        /// </summary>
        /// <param name="muX">MU Online X rotation in degrees</param>
        /// <param name="muY">MU Online Y rotation in degrees</param>
        /// <param name="muZ">MU Online Z rotation in degrees</param>
        /// <returns>MonoGame rotation matrix</returns>
        public static Matrix ConvertToMonoGame(float muX, float muY, float muZ)
        {
            // Rule: If any angle >= 180°, don't add Z flip. Otherwise, add Z+180°
            bool hasLargeAngle = (Math.Abs(muX) >= 180f || Math.Abs(muY) >= 180f || Math.Abs(muZ) >= 180f);

            float monoX = muX;
            float monoY = -muY;
            float monoZ = hasLargeAngle ? muZ : muZ + 180f;

            var m = CreateRotationMatrix(monoX, monoY, monoZ);

            if (muX >= 180f && muY >= 180f && Math.Abs(muZ) < 1f)
                m *= Matrix.CreateRotationY(MathHelper.Pi);

            return m;
        }

        /// <summary>
        /// Converts with explicit control over corrections
        /// </summary>
        public static Matrix ConvertToMonoGame(float muX, float muY, float muZ,
                                             float xCorrection = 0f,
                                             float yCorrection = 0f,
                                             float zCorrection = float.NaN) // NaN = auto-detect
        {
            float monoX = muX + xCorrection;
            float monoY = -muY + yCorrection;

            float monoZ;
            if (float.IsNaN(zCorrection))
            {
                // Auto-detect Z correction
                bool hasLargeAngle = (Math.Abs(muX) >= 180f || Math.Abs(muY) >= 180f || Math.Abs(muZ) >= 180f);
                monoZ = hasLargeAngle ? muZ : muZ + 180f;
            }
            else
            {
                monoZ = muZ + zCorrection;
            }

            return CreateRotationMatrix(monoX, monoY, monoZ);
        }

        /// <summary>
        /// Creates rotation matrix in correct order
        /// </summary>
        private static Matrix CreateRotationMatrix(float x, float y, float z)
        {
            return Matrix.CreateRotationX(MathHelper.ToRadians(x)) *
                   Matrix.CreateRotationY(MathHelper.ToRadians(y)) *
                   Matrix.CreateRotationZ(MathHelper.ToRadians(z));
        }

        /// <summary>
        /// Predefined rotations for verified items
        /// </summary>
        public static class Presets
        {
            /// <summary>
            /// Default MU rotation (0, 0, 0) → MonoGame (0°, 0°, 180°)
            /// </summary>
            public static Matrix Default => ConvertToMonoGame(0f, 0f, 0f);

            /// <summary>
            /// Small Axe (25, 45, 0) → MonoGame (25°, -45°, 180°) ✅ VERIFIED
            /// </summary>
            public static Matrix SmallAxe => ConvertToMonoGame(25f, 45f, 0f);

            /// <summary>
            /// Shield (270, 270, 0) → MonoGame (270°, -270°, 0°) ✅ VERIFIED
            /// </summary>
            public static Matrix Shield => ConvertToMonoGame(270f, 270f, 0f);

            /// <summary>
            /// Common sword rotation (0, 90, 0) → MonoGame (0°, -90°, 180°)
            /// </summary>
            public static Matrix Sword => ConvertToMonoGame(0f, 90f, 0f);
        }

        /// <summary>
        /// Debug method to show conversion logic
        /// </summary>
        public static void DebugConversion(float muX, float muY, float muZ)
        {
            bool hasLargeAngle = (Math.Abs(muX) >= 180f || Math.Abs(muY) >= 180f || Math.Abs(muZ) >= 180f);

            float monoX = muX;
            float monoY = -muY;
            float monoZ = hasLargeAngle ? muZ : muZ + 180f;

            Console.WriteLine($"MU Rotation: ({muX:F0}°, {muY:F0}°, {muZ:F0}°)");
            Console.WriteLine($"Has large angle (≥180°): {hasLargeAngle}");
            Console.WriteLine($"MonoGame: ({monoX:F0}°, {monoY:F0}°, {monoZ:F0}°)");

            if (hasLargeAngle)
                Console.WriteLine("  → No Z correction applied (large angle rule)");
            else
                Console.WriteLine("  → Z+180° correction applied (small angle rule)");
        }

        /// <summary>
        /// Verify known working combinations
        /// </summary>
        public static void VerifyKnownRotations()
        {
            Console.WriteLine("=== VERIFYING KNOWN WORKING ROTATIONS ===");

            Console.WriteLine("\n--- Small Axe (25, 45, 0) ---");
            DebugConversion(25f, 45f, 0f);
            Console.WriteLine("Expected: (25°, -45°, 180°) ✅");

            Console.WriteLine("\n--- Shield (270, 270, 0) ---");
            DebugConversion(270f, 270f, 0f);
            Console.WriteLine("Expected: (270°, -270°, 0°) ✅");

            Console.WriteLine("\n--- Default (0, 0, 0) ---");
            DebugConversion(0f, 0f, 0f);
            Console.WriteLine("Expected: (0°, 0°, 180°)");

            Console.WriteLine("\n--- Sword example (0, 90, 0) ---");
            DebugConversion(0f, 90f, 0f);
            Console.WriteLine("Expected: (0°, -90°, 180°)");
        }
    }

    /// <summary>
    /// Extension methods for easier usage
    /// </summary>
    public static class MuRotationExtensions
    {
        /// <summary>
        /// Convert Vector3 MU rotation to MonoGame matrix
        /// </summary>
        public static Matrix ToMonoGameRotation(this Vector3 muRotation)
        {
            return MuRotationConverter.ConvertToMonoGame(muRotation.X, muRotation.Y, muRotation.Z);
        }

        /// <summary>
        /// Create MU rotation vector
        /// </summary>
        public static Vector3 MuRotation(float x, float y, float z) => new Vector3(x, y, z);
    }

    /// <summary>
    /// Item-specific rotation configurations
    /// </summary>
    public class ItemRotationConfig
    {
        public float MuX { get; set; }
        public float MuY { get; set; }
        public float MuZ { get; set; }
        public string ItemName { get; set; } = "";

        /// <summary>
        /// Convert this config to MonoGame rotation matrix
        /// </summary>
        public Matrix ToMonoGameMatrix()
        {
            return MuRotationConverter.ConvertToMonoGame(MuX, MuY, MuZ);
        }

        /// <summary>
        /// Debug this rotation
        /// </summary>
        public void Debug()
        {
            Console.WriteLine($"--- {ItemName} ({MuX}, {MuY}, {MuZ}) ---");
            MuRotationConverter.DebugConversion(MuX, MuY, MuZ);
        }
    }
}
