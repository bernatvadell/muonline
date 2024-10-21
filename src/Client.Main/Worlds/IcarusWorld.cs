using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Objects.CloudObject;
using Client.Main.Objects.WallObject;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class IcarusWorld : WalkableWorldControl
    {
        public IcarusWorld() : base(worldIndex: 11)
        {

        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Walker.Location = new Vector2(14, 12);
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();

            MapTileObjects[5] = typeof(CloudObject);
            MapTileObjects[10] = typeof(WallObject);
        }

        public override async Task Load()
        {
            await base.Load();
            SoundController.Instance.PlayBackgroundMusic("Music/icarus.mp3");
        }
    }
}
