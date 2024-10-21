using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects;
using Client.Main.Objects.Effects;
using Client.Main.Objects.Icarus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class IcarusWorld : WalkableWorldControl
    {
        private CloudLightEffect _cloudLight;
        private JointThunderEffect _jointThunder;

        public IcarusWorld() : base(worldIndex: 11)
        {
            Terrain.TextureMappingFiles[10] = "TileRock04.OZJ";
            ExtraHeight = 90f;
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
        }

        public override async Task Load()
        {
            //await AddObjectAsync(_cloudLight = new CloudLightEffect());
            //await AddObjectAsync(_jointThunder = new JointThunderEffect());
            await AddObjectAsync(new CloudObject());
            await base.Load();
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (MuGame.Instance.ActiveScene.World is WalkableWorldControl walkableWorld)
            {
                //_cloudLight.Position = walkableWorld.Walker.Position;
                //_jointThunder.Position = walkableWorld.Walker.Position;
            }
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Walker.Location = new Vector2(14, 12);
        }
    }
}
