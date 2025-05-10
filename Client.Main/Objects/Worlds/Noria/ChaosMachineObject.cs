using Client.Main.Content;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;
using Client.Main.Controllers;

namespace Client.Main.Objects.Worlds.Noria
{

    public class ChaosMachineObject : ModelObject
    {
        public ChaosMachineObject()
        {
            var startX = 0;
            var startY = 0;
            var startZ = 140;
            var position = new Vector3(startX, startY, startZ);

            //Children.Add(new Warp01NPCObject() { Angle = new Vector3(0, 0, 10), Position = position });
            //Children.Add(new Warp02NPCObject() { Angle = new Vector3(0, 0, 10), Position = position });
            //Children.Add(new Warp03NPCObject() { Angle = new Vector3(0, 0, 10), Position = position });

            // Children.Add(
            //    ParticleSystem.Create()
            //       .SetMaxParticles(10)
            //       .SetRegeneration(0.01f, 0.05f)
            //       .Register<Spark03Effect>()
            //           .SetPosition(position, position)
            //           .UseEffect(GravityEffect.Create(new Vector3(0, 0, 0), new Vector3(0, 0, 2), 0))
            //           .UseEffect(DurationEffect.Create(50, 66))
            //           // .UseEffect(BrightEffect.Create())
            //           .EnableRotation()
            //           .SetScale(4f, 5f)
            //           .EnableRotation()
            //       .System
            //);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object4/Object40.bmd");
            BlendMesh = 1;
            Position = new Vector3(Position.X, Position.Y, Position.Z - 40f);
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible)
                return;

            Vector3 listenerPosition = ((Controls.WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/nMix.wav", Position, listenerPosition, maxDistance: 1000f, loop: true);
        }
    }
}
