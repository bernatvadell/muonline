using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Simple sprite effect representing blood on the ground.
    /// </summary>
    public class BloodStainEffect : WorldObject
    {
        private const float BaseSize = 40f;
        private const float LifeTotal = 3.5f;
        public const float GroundOffset = 70f;
        private const float DepthBias = -0.00002f;

        private static readonly short[] QuadIndices = { 0, 1, 2, 2, 1, 3 };
        private readonly VertexPositionTexture[] _quadVerts = new VertexPositionTexture[4];

        private readonly string _texturePath;
        private Texture2D _texture;
        private float _life = LifeTotal;

        /// <summary>
        /// Path to the blood texture.
        /// </summary>
        public string TexturePath => _texturePath;

        public BloodStainEffect()
        {
            string[] textures =
            {
                "Effect/blood.tga",
                "Effect/blood01.tga"
            };

            var rand = Random.Shared;
            _texturePath = textures[rand.Next(textures.Length)];

            float scaleX = MathHelper.Lerp(0.75f, 1.35f, (float)rand.NextDouble());
            float scaleZ = MathHelper.Lerp(0.75f, 1.35f, (float)rand.NextDouble());

            float uvScaleX = MathHelper.Lerp(0.7f, 1f, (float)rand.NextDouble());
            float uvScaleY = MathHelper.Lerp(0.7f, 1f, (float)rand.NextDouble());
            float uvOffsetX = MathHelper.Lerp(0f, 1f - uvScaleX, (float)rand.NextDouble());
            float uvOffsetY = MathHelper.Lerp(0f, 1f - uvScaleY, (float)rand.NextDouble());

            bool flipU = rand.Next(2) == 0;
            bool flipV = rand.Next(2) == 0;

            float u0 = uvOffsetX;
            float u1 = uvOffsetX + uvScaleX;
            float v0 = uvOffsetY;
            float v1 = uvOffsetY + uvScaleY;

            if (flipU)
                (u0, u1) = (u1, u0);

            if (flipV)
                (v0, v1) = (v1, v0);

            const float jitter = 0.18f;
            var o0 = new Vector2(RandomRange(rand, -jitter, jitter), RandomRange(rand, -jitter, jitter));
            var o1 = new Vector2(RandomRange(rand, -jitter, jitter), RandomRange(rand, -jitter, jitter));
            var o2 = new Vector2(RandomRange(rand, -jitter, jitter), RandomRange(rand, -jitter, jitter));
            var o3 = new Vector2(RandomRange(rand, -jitter, jitter), RandomRange(rand, -jitter, jitter));

            var c0 = new Vector3(-1f * scaleX + o0.X, 0f, -1f * scaleZ + o0.Y);
            var c1 = new Vector3(1f * scaleX + o1.X, 0f, -1f * scaleZ + o1.Y);
            var c2 = new Vector3(-1f * scaleX + o2.X, 0f, 1f * scaleZ + o2.Y);
            var c3 = new Vector3(1f * scaleX + o3.X, 0f, 1f * scaleZ + o3.Y);

            _quadVerts[0] = new VertexPositionTexture(c0, new Vector2(u0, v0));
            _quadVerts[1] = new VertexPositionTexture(c1, new Vector2(u1, v0));
            _quadVerts[2] = new VertexPositionTexture(c2, new Vector2(u0, v1));
            _quadVerts[3] = new VertexPositionTexture(c3, new Vector2(u1, v1));

            BlendState = BlendState.NonPremultiplied;
            LightEnabled = false;
            IsTransparent = true;
            float scaleMul = MathHelper.Lerp(0.85f, 1.25f, (float)rand.NextDouble());
            Scale = 1f * scaleMul;
            Angle = new Vector3(0f, 0f, MathHelper.Lerp(0f, MathHelper.TwoPi, (float)rand.NextDouble()));
        }

        public override async Task Load()
        {
            await base.Load();

            if (Status != GameControlStatus.Ready)
                return;

            var textureData = await TextureLoader.Instance.Prepare(TexturePath);

            if (textureData != null)
            {
                _texture = TextureLoader.Instance.GetTexture2D(TexturePath);
            }
            else
            {
                Status = GameControlStatus.Error;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready)
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _life -= dt;
            if (_life <= 0f)
            {
                World?.RemoveObject(this);
                Dispose();
                return;
            }

            Alpha = MathHelper.Clamp(_life / LifeTotal, 0f, 2f);

            if (!Visible)
                return;

            var terrain = World?.Terrain;
            if (terrain == null)
                return;

            float groundZ = terrain.RequestTerrainHeight(Position.X, Position.Y) + GroundOffset;
            if (MathF.Abs(Position.Z - groundZ) > 0.01f)
                Position = new Vector3(Position.X, Position.Y, groundZ);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _texture == null)
                return;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var effect = GraphicsManager.Instance.AlphaTestEffect3D;

            var previousBlendState = gd.BlendState;
            var previousSampler = gd.SamplerStates[0];
            var previousDepthState = gd.DepthStencilState;
            var previousRasterizer = gd.RasterizerState;
            var previousDiffuse = effect.DiffuseColor;
            var previousAlpha = effect.Alpha;
            var previousVertexColor = effect.VertexColorEnabled;
            var previousTexture = effect.Texture;

            try
            {
                gd.BlendState = BlendState ?? BlendState.AlphaBlend;
                gd.SamplerStates[0] = GraphicsManager.GetQualitySamplerState();
                gd.DepthStencilState = DepthStencilState.DepthRead;
                gd.RasterizerState = GraphicsManager.GetCachedRasterizerState(DepthBias, CullMode.None, previousRasterizer);

                float size = BaseSize * Scale;

                effect.World = Matrix.CreateScale(size)
                              * Matrix.CreateRotationX(-MathHelper.PiOver2)
                              * Matrix.CreateRotationZ(TotalAngle.Z)
                              * Matrix.CreateTranslation(Position);
                effect.View = Camera.Instance.View;
                effect.Projection = Camera.Instance.Projection;
                effect.Texture = _texture;
                effect.VertexColorEnabled = false;
                effect.DiffuseColor = Color.White.ToVector3();
                effect.Alpha = TotalAlpha;

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _quadVerts, 0, 4,
                        QuadIndices, 0, 2);
                }
            }
            finally
            {
                effect.Texture = previousTexture;
                effect.VertexColorEnabled = previousVertexColor;
                effect.DiffuseColor = previousDiffuse;
                effect.Alpha = previousAlpha;
                gd.BlendState = previousBlendState;
                gd.SamplerStates[0] = previousSampler;
                gd.DepthStencilState = previousDepthState;
                gd.RasterizerState = previousRasterizer;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _texture = null;
        }

        private static float RandomRange(Random rand, float min, float max)
        {
            return min + (max - min) * (float)rand.NextDouble();
        }
    }
}
