using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Atlans
{
    public class BubblesObject : ModelObject
    {
        private const float RISE_SPEED = 50f;
        public bool IsActive;
        public Vector3 OriginalPosition;
        public float OriginalTexCoordV;
        public float CurrentTexCoordV;


        public override async Task Load()
        {
            Scale = 1f;
            BlendState = BlendState.NonPremultiplied;
            BlendMesh = 0;
            BlendMeshState = BlendState.Additive;
            LightEnabled = true;
            IsTransparent = true;
            Model = await BMDLoader.Instance.Prepare($"Object8/Object{Type + 1}.bmd");
            OriginalPosition = Position;

            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            Angle = new Vector3(0f, 90f, 135f);
            Position = new Vector3(Position.X, Position.Y, Position.Z + RISE_SPEED * deltaTime);

            if (Position.Z >= OriginalPosition.Z + 500f)
            {
                Position = new Vector3(Position.X, Position.Y, OriginalPosition.Z);
            }


            InvalidateBuffers();
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}