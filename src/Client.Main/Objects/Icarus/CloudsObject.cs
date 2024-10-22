using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Client.Main.Scenes;
using Client.Main.Controls;

namespace Client.Main.Objects.Icarus
{
    public class CloudObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object11/cloud.bmd");
            BlendMesh = 0;
            BlendMeshState = BlendState.AlphaBlend;
            Scale = 10f;
            LightEnabled = false;
            Light = new Vector3(4, 4, 4);
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != Models.GameControlStatus.Ready) return;

            if (World is WalkableWorldControl walkableWorld)
            {
                Position = new Vector3(
                    walkableWorld.Walker.Position.X + 200 * FPSCounter.Instance.FPS_ANIMATION_FACTOR,
                    walkableWorld.Walker.Position.Y - 190 * FPSCounter.Instance.FPS_ANIMATION_FACTOR,
                    walkableWorld.Walker.Position.Z
                );
            }

            //var luminosity = (MuGame.Random.Next() % 4 + 4) * 0.05f;
            //Light = new Vector3(luminosity * 0.3f, luminosity * 0.3f, luminosity * 0.3f);
        }
    }
}
