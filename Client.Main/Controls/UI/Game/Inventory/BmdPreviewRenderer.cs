using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Content;
using Client.Main.Controllers;

namespace Client.Main.Controls.UI.Game.Inventory
{
    /// <summary>
    /// Utility for generating simple 3D previews of BMD models.
    /// </summary>
    public static class BmdPreviewRenderer
    {
        private static readonly Dictionary<string, Texture2D> _cache = new();
        private static readonly HashSet<string> _failedRenders = new();

        public static Texture2D GetPreview(ItemDefinition definition, int width, int height)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.TexturePath))
                return null;

            string key = $"{definition.TexturePath}:{width}x{height}";

            // Check cache first
            if (_cache.TryGetValue(key, out var tex))
                return tex;

            // Skip if we know this render failed before
            if (_failedRenders.Contains(key))
                return null;

            try
            {
                tex = Render(definition, width, height);
                if (tex != null)
                {
                    _cache[key] = tex;
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
        private static Texture2D Render(ItemDefinition def, int width, int height)
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
                var bounds = ComputeBounds(bmd, bones);

                // ── render-target ─────────────────────────────────────────────────────
                var rt = new RenderTarget2D(gd, width, height, false,
                                            SurfaceFormat.Color, DepthFormat.Depth24);
                var prevTargets = gd.GetRenderTargets();
                gd.SetRenderTarget(rt);
                gd.Clear(Color.Transparent);

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

                Vector3 size = bounds.Max - bounds.Min;
                float scale = 15f / Math.Max(size.X, Math.Max(size.Y, size.Z));
                Vector3 center = (bounds.Min + bounds.Max) * 0.5f;

                Matrix rot = Matrix.CreateRotationY(0) *
                             Matrix.CreateRotationX(MathHelper.ToRadians(-80f));

                Matrix worldBase = Matrix.CreateScale(scale) *
                                   rot *
                                   Matrix.CreateTranslation(-center * scale);

                Vector3[] corners = bounds.GetCorners();
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < corners.Length; ++i)
                {
                    Vector3 v = Vector3.Transform(corners[i], worldBase);
                    if (v.Y < minY) minY = v.Y;
                    if (v.Y > maxY) maxY = v.Y;
                }
                float yOffset = -(minY + maxY) * 0.5f;

                effect.World = worldBase * Matrix.CreateTranslation(0f, yOffset, 0f);

                for (int meshIdx = 0; meshIdx < bmd.Meshes.Length; ++meshIdx)
                {
                    DynamicVertexBuffer vb = null;
                    DynamicIndexBuffer ib = null;

                    BMDLoader.Instance.GetModelBuffers(bmd, meshIdx, Color.White,
                                                       bones, ref vb, ref ib, false);

                    effect.Texture = TextureLoader.Instance.GetTexture2D(
                                         BMDLoader.Instance.GetTexturePath(
                                             bmd, bmd.Meshes[meshIdx].TexturePath));

                    foreach (var pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        gd.SetVertexBuffer(vb);
                        gd.Indices = ib;
                        gd.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                                                 0, 0, ib.IndexCount / 3);
                    }
                }

                effect.View = oldV;
                effect.Projection = oldP;
                effect.World = oldW;

                gd.SetRenderTarget(null);
                gd.SetRenderTargets(prevTargets);
                return rt;
            }
            catch
            {
                return null;
            }
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
}