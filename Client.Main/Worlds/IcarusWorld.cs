using Client.Main.Controls;
using Client.Main.Objects.Worlds.Icarus;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class IcarusWorld : WalkableWorldControl
    {
        private static readonly Color CLEAR_COLOR = new Color(3f / 256f, 25f / 256f, 44f / 256f, 1f);
        // private CloudLightEffect _cloudLight;
        // private JointThunderEffect _jointThunder;

        public IcarusWorld() : base(worldIndex: 11)
        {
            Terrain.TextureMappingFiles.Clear();
            Terrain.TextureMappingFiles[10] = "TileRock04.OZJ";
            ExtraHeight = 90f;
            BackgroundMusicPath = "Music/icarus.mp3";
        }

        public override async Task Load()
        {
            // await AddObjectAsync(_cloudLight = new CloudLightEffect());
            // await AddObjectAsync(_jointThunder = new JointThunderEffect());
            // await AddObjectAsync(new CloudObject());
            await base.Load();
        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(14, 12);
            base.AfterLoad();
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();

            MapTileObjects[5] = typeof(CloudObject);
            MapTileObjects[10] = typeof(WallObject);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (World is WalkableWorldControl walkableWorld)
            {
                // _cloudLight.Position = walkableWorld.Walker.Position;
                // _jointThunder.Position = walkableWorld.Walker.Position;
            }
        }

        public override void Draw(GameTime time)
        {
            GraphicsDevice.Clear(CLEAR_COLOR);
            base.Draw(time);
        }
    }
}
