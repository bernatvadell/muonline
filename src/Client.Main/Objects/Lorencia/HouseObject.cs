using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class HouseObject : ModelObject
    {
        private float _alpha = 0.6f;
        private float _alphaTarget = 1f;

        public HouseObject()
        {
            LightEnabled = true;
        }

        public override async Task Load()
        {
            var idx = (Type - (ushort)ModelType.House01 + 1).ToString().PadLeft(2, '0');

            if (idx == "03")
            {
                BlendMesh = 4;
                BlendMeshState = BlendState.Additive;
            }
            else if (idx == "04")
            {
                BlendMesh = 8;
                BlendMeshState = BlendState.Additive;
            }

            Model = await BMDLoader.Instance.Prepare($"Object1/House{idx}.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _alphaTarget = (MuGame.Random.Next() % 4 + 6) * 0.1f;
            _alpha = MathHelper.Lerp(_alpha, _alphaTarget, 0.1f);
            BlendMeshLight = _alpha;
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
