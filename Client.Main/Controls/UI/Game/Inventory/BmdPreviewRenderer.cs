using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Content;
using Client.Main.Controllers;
using System.Reflection;

namespace Client.Main.Controls.UI.Game.Inventory
{
    /// <summary>
    /// Utility for generating simple 3D previews of BMD models with proper BlendState support.
    /// </summary>
    public static class BmdPreviewRenderer
    {
        private static readonly Dictionary<string, Texture2D> _cache = new();
        private static readonly Dictionary<string, Texture2D> _rotatingCache = new();
        private static readonly HashSet<string> _failedRenders = new();
        private static readonly Dictionary<string, BlendState> _previewBlendStateCache = new();

        // Rotating cache cleanup
        private static int _rotatingCacheCleanupCounter = 0;
        private const int MaxRotatingCacheSize = 50;

        public static Texture2D GetPreview(ItemDefinition definition, int width, int height, float rotationAngle = 0f)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.TexturePath))
                return null;

            string baseKey = $"{definition.TexturePath}:{width}x{height}";
            string key = rotationAngle == 0f ? baseKey : $"{baseKey}:{rotationAngle:F0}";

            // Check appropriate cache
            if (rotationAngle == 0f)
            {
                if (_cache.TryGetValue(key, out var cachedTex))
                    return cachedTex;
            }
            else
            {
                if (_rotatingCache.TryGetValue(key, out var cachedTex))
                    return cachedTex;

                // Clean up rotating cache periodically
                if (++_rotatingCacheCleanupCounter > MaxRotatingCacheSize)
                {
                    _rotatingCache.Clear();
                    _rotatingCacheCleanupCounter = 0;
                }
            }

            // Skip if we know this render failed before
            if (_failedRenders.Contains(key))
                return null;

            Texture2D tex = null;
            try
            {
                tex = Render(definition, width, height, rotationAngle);
                if (tex != null)
                {
                    if (rotationAngle == 0f)
                    {
                        // Cache static previews permanently
                        _cache[key] = tex;
                    }
                    else
                    {
                        // Cache rotating previews temporarily
                        _rotatingCache[key] = tex;
                    }
                }
                else
                {
                    // Mark as failed to avoid repeated attempts
                    _failedRenders.Add(key);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("UI thread"))
            {
                // We're not on the UI thread, mark as failed and return null
                _failedRenders.Add(key);
                return null;
            }
            catch (Exception)
            {
                // Other rendering errors, mark as failed
                _failedRenders.Add(key);
                return null;
            }

            return tex;
        }

        /// <summary>
        /// Creates an animated rotating preview for mouse hover effect
        /// </summary>
        public static Texture2D GetAnimatedPreview(ItemDefinition definition, int width, int height, GameTime gameTime)
        {
            if (gameTime == null)
                return GetPreview(definition, width, height, 0f);

            // Create smooth rotation based on game time (360° over 3 seconds)
            float rotationSpeed = 120f; // degrees per second (slower for smoother look)
            float rotationAngle = (float)(gameTime.TotalGameTime.TotalSeconds * rotationSpeed) % 360f;

            // Round to 5-degree increments for smoother animation but still cache-friendly
            rotationAngle = MathF.Round(rotationAngle);

            return GetPreview(definition, width, height, rotationAngle);
        }

        /// <summary>
        /// Test function with more obvious rotation for debugging
        /// </summary>
        public static Texture2D GetTestRotatingPreview(ItemDefinition definition, int width, int height, GameTime gameTime)
        {
            if (gameTime == null)
                return GetPreview(definition, width, height, 0f);

            // Create very obvious rotation (90 degrees every second for testing)
            float rotationSpeed = 90f; // degrees per second
            float rotationAngle = (float)(gameTime.TotalGameTime.TotalSeconds * rotationSpeed) % 360f;

            return GetPreview(definition, width, height, rotationAngle);
        }

        /// <summary>
        /// Smooth preview without any caching (for perfectly smooth animation)
        /// </summary>
        public static Texture2D GetSmoothRotatingPreview(ItemDefinition definition, int width, int height, GameTime gameTime)
        {
            if (gameTime == null)
                return GetPreview(definition, width, height, 0f);

            // Perfectly smooth rotation (no rounding for cache)
            float rotationSpeed = 120f; // degrees per second
            float rotationAngle = (float)(gameTime.TotalGameTime.TotalSeconds * rotationSpeed) % 360f;

            // Don't use cache for perfectly smooth animation
            return Render(definition, width, height, rotationAngle);
        }

        private static Texture2D Render(ItemDefinition def, int width, int height, float rotationAngle = 0f)
        {
            try
            {
                var gd = GraphicsManager.Instance.GraphicsDevice;
                if (gd == null)
                    return null;

                var modelTask = BMDLoader.Instance.Prepare(def.TexturePath);
                // Use ConfigureAwait(false) to avoid deadlocks and run on thread pool
                var bmd = modelTask.ConfigureAwait(false).GetAwaiter().GetResult();
                if (bmd == null)
                    return null;

                var bones = BuildBoneMatrices(bmd);
                var originalBounds = ComputeBounds(bmd, bones);

                // ── render-target ─────────────────────────────────────────────────────
                var rt = new RenderTarget2D(gd, width, height, false,
                                            SurfaceFormat.Color, DepthFormat.Depth24);
                var prevTargets = gd.GetRenderTargets();
                var originalBlendState = gd.BlendState;
                var originalDepthStencilState = gd.DepthStencilState;
                var originalRasterizerState = gd.RasterizerState;

                gd.SetRenderTarget(rt);
                gd.Clear(Color.Transparent);

                // Set appropriate render states for 3D preview
                gd.BlendState = BlendState.AlphaBlend;
                gd.DepthStencilState = DepthStencilState.Default;
                gd.RasterizerState = RasterizerState.CullNone; // Show both sides for inventory items

                // ── basic effect ──────────────────────────────────────────────────────
                var effect = GraphicsManager.Instance.BasicEffect3D;
                Matrix oldV = effect.View;
                Matrix oldP = effect.Projection;
                Matrix oldW = effect.World;

                effect.View = Matrix.CreateLookAt(new Vector3(0, 0, 40f),
                                                  Vector3.Zero, Vector3.Up);
                effect.Projection = Matrix.CreatePerspectiveFieldOfView(
                                        MathHelper.ToRadians(30f),
                                        (float)width / height,
                                        1f, 100f);

                // ── Calculate rotation based on item group ──
                Matrix baseRotation = MuRotationConverter.ConvertToMonoGame(25f, 45f, 0f);

                if (def.Group == 0 || def.Group == 1 || def.Group == 2 || def.Group == 3 || def.Group == 5)
                {
                    baseRotation = MuRotationConverter.ConvertToMonoGame(25f, 45f, 0f);
                }
                else if (def.Group == 6)
                {
                    baseRotation = MuRotationConverter.ConvertToMonoGame(270f, 270f, 0f);
                }
                else if (def.Group == 13) //helpers
                {
                    baseRotation = MuRotationConverter.ConvertToMonoGame(270f, 0f, 0f);
                }
                else if (def.Group == 14) //potions
                {
                    baseRotation = MuRotationConverter.ConvertToMonoGame(270f, 0f, 0f);
                }
                else
                {
                    baseRotation = MuRotationConverter.ConvertToMonoGame(270f, -10f, 0f);
                }

                // Add mouse-over rotation (around Y-axis for horizontal spin)
                Matrix mouseRotation = Matrix.CreateRotationY(MathHelper.ToRadians(rotationAngle));

                // For stable centering, use the base rotation for bounding box calculation
                // but apply mouse rotation separately
                Vector3[] originalCorners = originalBounds.GetCorners();
                Vector3 rotatedMin = new Vector3(float.MaxValue);
                Vector3 rotatedMax = new Vector3(float.MinValue);

                Matrix rotationForBounds;
                if (rotationAngle == 0f)
                {
                    rotationForBounds = baseRotation;
                }
                else
                {
                    rotationForBounds = baseRotation;
                }

                foreach (Vector3 corner in originalCorners)
                {
                    Vector3 rotatedCorner = Vector3.Transform(corner, rotationForBounds);
                    rotatedMin = Vector3.Min(rotatedMin, rotatedCorner);
                    rotatedMax = Vector3.Max(rotatedMax, rotatedCorner);
                }

                // Create new bounding box 
                BoundingBox rotatedBounds = new BoundingBox(rotatedMin, rotatedMax);
                Vector3 rotatedSize = rotatedBounds.Max - rotatedBounds.Min;

                // Calculate scale based on size
                float scale = 15f / Math.Max(rotatedSize.X, Math.Max(rotatedSize.Y, rotatedSize.Z));

                Vector3 originalCenter = (originalBounds.Min + originalBounds.Max) * 0.5f;

                Matrix finalRotation = baseRotation;
                if (rotationAngle != 0f)
                {
                    finalRotation = baseRotation * mouseRotation;
                }

                Matrix worldBase = Matrix.CreateScale(scale) *
                                  finalRotation *
                                  Matrix.CreateTranslation(-originalCenter * scale);

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

                effect.World = worldBase * Matrix.CreateTranslation(xOffset, yOffset, zOffset);

                // ── Render meshes with proper BlendState support ──
                var meshOrder = GetMeshRenderOrder(bmd);

                foreach (int meshIdx in meshOrder)
                {
                    RenderMeshWithBlendState(gd, effect, bmd, meshIdx, bones);
                }

                // Restore effect matrices
                effect.View = oldV;
                effect.Projection = oldP;
                effect.World = oldW;

                // Restore render states
                gd.SetRenderTarget(null);
                gd.SetRenderTargets(prevTargets);
                gd.BlendState = originalBlendState;
                gd.DepthStencilState = originalDepthStencilState;
                gd.RasterizerState = originalRasterizerState;

                return rt;
            }
            catch
            {
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