using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Expanding golden circle rendered horizontally (flat on ground level).
    /// </summary>
    public class LevelUpMagicCircle : SpriteObject
    {
        private const float _lifeTotal = 3.5f;
        private float _life = _lifeTotal;

        public override string TexturePath => "Effect/Magic_Ground2.jpg";

        public LevelUpMagicCircle(Vector3 startPos)
        {
            Position = startPos;
            IsTransparent = true;
            BlendState = BlendState.NonPremultiplied;
            Scale = 0.1f;
            LightEnabled = false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _life -= dt;
            if (_life <= 0f)
            {
                World?.RemoveObject(this);
                Dispose();
                return;
            }

            float t = 1f - (_life / _lifeTotal);
            Scale = MathHelper.Lerp(0.1f, 5.0f, t);

            const float fadeStartTime = 0.7f;
            if (t < fadeStartTime)
            {
                Alpha = 1.0f;
            }
            else
            {
                float fadeProgress = (t - fadeStartTime) / (1.0f - fadeStartTime);
                Alpha = MathHelper.Lerp(1.0f, 0.0f, fadeProgress);
            }

            Angle = new Vector3(0, 0, (float)(gameTime.TotalGameTime.TotalSeconds * 0.5f));
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || SpriteTexture == null) return;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var effect = GraphicsManager.Instance.AlphaTestEffect3D;

            Vector3 originalDiffuse = effect.DiffuseColor;
            float originalAlpha = effect.Alpha;

            try
            {
                Color tint = new Color(255, 255, 100);

                effect.World = Matrix.CreateScale(Scale * 75)
                                  * Matrix.CreateRotationX(-MathHelper.PiOver2)
                                  * Matrix.CreateRotationZ(Angle.Z)
                                  * Matrix.CreateTranslation(Position);
                effect.View = Camera.Instance.View;
                effect.Projection = Camera.Instance.Projection;
                effect.Texture = SpriteTexture;
                effect.VertexColorEnabled = false;
                effect.DiffuseColor = tint.ToVector3();
                effect.Alpha = this.Alpha;

                var verts = new VertexPositionTexture[4];
                verts[0] = new VertexPositionTexture(new Vector3(-1, 0, -1), new Vector2(0, 0));
                verts[1] = new VertexPositionTexture(new Vector3(1, 0, -1), new Vector2(1, 0));
                verts[2] = new VertexPositionTexture(new Vector3(-1, 0, 1), new Vector2(0, 1));
                verts[3] = new VertexPositionTexture(new Vector3(1, 0, 1), new Vector2(1, 1));

                short[] idx = { 0, 1, 2, 2, 1, 3 };

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        verts, 0, 4,
                        idx, 0, 2);
                }
            }
            finally
            {
                effect.DiffuseColor = originalDiffuse;
                effect.Alpha = originalAlpha;
            }
        }
    }
}