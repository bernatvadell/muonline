using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Objects
{
    public abstract partial class ModelObject
    {
        private static readonly VertexPositionTexture[] BlobShadowVertices =
        [
            new VertexPositionTexture(new Vector3(-1f, 0f, -1f), new Vector2(0f, 1f)),
            new VertexPositionTexture(new Vector3(1f, 0f, -1f), new Vector2(1f, 1f)),
            new VertexPositionTexture(new Vector3(1f, 0f, 1f), new Vector2(1f, 0f)),
            new VertexPositionTexture(new Vector3(-1f, 0f, 1f), new Vector2(0f, 0f))
        ];

        private static readonly short[] BlobShadowIndices = [0, 1, 2, 0, 2, 3];
        private static readonly object BlobShadowTextureLock = new();
        private static Texture2D _blobShadowTexture;

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

        public virtual void DrawBlobShadow(Matrix view, Matrix projection, Matrix shadowWorld, float shadowOpacity)
        {
            if (shadowOpacity <= 0.001f)
                return;

            try
            {
                // Skip shadow rendering if shadows are disabled for this world
                if (MuGame.Instance.ActiveScene?.World is WorldControl world && !world.EnableShadows)
                    return;

                var effect = GraphicsManager.Instance.ShadowEffect;
                var blobTexture = GetOrCreateBlobShadowTexture();
                if (effect == null || blobTexture == null)
                    return;

                var previousBlend = GraphicsDevice.BlendState;
                var previousDepth = GraphicsDevice.DepthStencilState;
                var previousRaster = GraphicsDevice.RasterizerState;

                float constBias = 1f / (1 << 24);
                RasterizerState shadowRasterizer = GraphicsManager.GetCachedRasterizerState(constBias * -20, CullMode.None);

                GraphicsDevice.BlendState = Blendings.ShadowBlend;
                GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                GraphicsDevice.RasterizerState = shadowRasterizer;

                try
                {
                    float localWidth = BoundingBoxLocal.Max.X - BoundingBoxLocal.Min.X;
                    float localDepth = BoundingBoxLocal.Max.Y - BoundingBoxLocal.Min.Y;
                    float scaleX = MathF.Max(45f, localWidth * 0.55f);
                    float scaleZ = MathF.Max(45f, localDepth * 0.55f);

                    Matrix blobWorld = Matrix.CreateScale(scaleX, 1f, scaleZ) * shadowWorld;

                    effect.Parameters["World"]?.SetValue(blobWorld);
                    effect.Parameters["ViewProjection"]?.SetValue(view * projection);
                    effect.Parameters["ShadowTint"]?.SetValue(new Vector4(0f, 0f, 0f, shadowOpacity));
                    effect.Parameters["ShadowTexture"]?.SetValue(blobTexture);

                    foreach (var pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawUserIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            BlobShadowVertices,
                            0,
                            BlobShadowVertices.Length,
                            BlobShadowIndices,
                            0,
                            2);
                    }
                }
                finally
                {
                    GraphicsDevice.BlendState = previousBlend;
                    GraphicsDevice.DepthStencilState = previousDepth;
                    GraphicsDevice.RasterizerState = previousRaster;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Error in DrawBlobShadow: {Message}", ex.Message);
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

        private Texture2D GetOrCreateBlobShadowTexture()
        {
            var texture = _blobShadowTexture;
            if (texture != null && !texture.IsDisposed)
                return texture;

            lock (BlobShadowTextureLock)
            {
                texture = _blobShadowTexture;
                if (texture != null && !texture.IsDisposed)
                    return texture;

                const int size = 64;
                texture = new Texture2D(GraphicsDevice, size, size, false, SurfaceFormat.Color);
                var data = new Color[size * size];
                float center = (size - 1) * 0.5f;
                float invRadius = 1f / center;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - center) * invRadius;
                        float dy = (y - center) * invRadius;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);
                        float alpha = MathF.Max(0f, 1f - dist);
                        alpha *= alpha;
                        data[y * size + x] = new Color((byte)255, (byte)255, (byte)255, (byte)(alpha * 255f));
                    }
                }

                texture.SetData(data);
                _blobShadowTexture = texture;
                return texture;
            }
        }

        public virtual void DrawShadowCaster(Effect shadowEffect, Matrix lightViewProjection)
        {
            if (shadowEffect == null)
                return;

            int shadowSize = GraphicsManager.Instance.ShadowMapRenderer?.ShadowMap?.Width ?? Math.Max(256, Constants.SHADOW_MAP_SIZE);
            Vector2 shadowTexel = new Vector2(1f / shadowSize, 1f / shadowSize);

            // Draw own meshes if available
            if (Model?.Meshes != null && _boneTextures != null)
            {
                try
                {
                    var gd = GraphicsDevice;
                    var prevBlend = gd.BlendState;
                    var prevDepth = gd.DepthStencilState;
                    var prevRaster = gd.RasterizerState;
                    var prevTechnique = shadowEffect?.CurrentTechnique;

                    var shadowCasterTechnique = TryGetTechnique(shadowEffect, "ShadowCaster");
                    var shadowCasterSkinnedTechnique = TryGetTechnique(shadowEffect, "ShadowCaster_Skinned");
                    if (shadowCasterTechnique == null)
                        return;

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
                    EffectTechnique activeTechnique = null;
                    int uploadedSkinnedBoneCount = 0;

                    for (int i = 0; i < meshCount; i++)
                    {
                        if (IsHiddenMesh(i))
                            continue;

                        bool useGpuSkinning = shadowCasterSkinnedTechnique != null &&
                                              _gpuSkinMeshEnabled != null &&
                                              (uint)i < (uint)_gpuSkinMeshEnabled.Length &&
                                              _gpuSkinMeshEnabled[i] &&
                                              _gpuSkinVertexBuffers != null &&
                                              (uint)i < (uint)_gpuSkinVertexBuffers.Length &&
                                              _gpuSkinVertexBuffers[i] != null &&
                                              _gpuSkinIndexBuffers != null &&
                                              (uint)i < (uint)_gpuSkinIndexBuffers.Length &&
                                              _gpuSkinIndexBuffers[i] != null;

                        VertexBuffer vb = useGpuSkinning ? _gpuSkinVertexBuffers[i] : _boneVertexBuffers?[i];
                        IndexBuffer ib = useGpuSkinning ? _gpuSkinIndexBuffers[i] : _boneIndexBuffers?[i];
                        var tex = _boneTextures[i];
                        if (vb == null || ib == null || tex == null)
                            continue;

                        if (useGpuSkinning)
                        {
                            int requiredBoneCount = _gpuSkinBoneCounts != null && (uint)i < (uint)_gpuSkinBoneCounts.Length
                                ? _gpuSkinBoneCounts[i]
                                : 0;

                            if (requiredBoneCount > uploadedSkinnedBoneCount)
                            {
                                if (!TryUploadGpuSkinBoneMatrices(shadowEffect, requiredBoneCount))
                                {
                                    useGpuSkinning = false;
                                    vb = _boneVertexBuffers?[i];
                                    ib = _boneIndexBuffers?[i];
                                    if (vb == null || ib == null)
                                        continue;
                                }
                                else
                                {
                                    uploadedSkinnedBoneCount = requiredBoneCount;
                                }
                            }
                        }

                        var targetTechnique = useGpuSkinning ? shadowCasterSkinnedTechnique : shadowCasterTechnique;
                        if (targetTechnique != activeTechnique)
                        {
                            shadowEffect.CurrentTechnique = targetTechnique;
                            activeTechnique = targetTechnique;
                        }

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
            return modelChild is WeaponObject ||
                   modelChild is PlayerGloveObject ||
                   modelChild is PlayerBootObject ||
                   modelChild is PlayerMaskHelmObject;
        }
    }
}
