using Client.Main.Controls;
using Client.Main.Objects.Worlds.Devias;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class DeviasWorld : WalkableWorldControl
    {
        public DeviasWorld() : base(worldIndex: 3)
        {
            BackgroundMusicPath = "Music/Devias.mp3";
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Walker.Location = new Vector2(219, 24);
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();

            MapTileObjects[0] = typeof(TreeObject);

            MapTileObjects[19] = typeof(AuroraObject);
            MapTileObjects[20] = typeof(SteelDoorObject);

            for (var i = 65; i < 67; i++)
                MapTileObjects[i] = typeof(SteelDoorObject);

            for (var i = 75; i < 79; i++)
                MapTileObjects[i] = typeof(HouseWallObject); // Wall

            for (var i = 81; i < 83; i++)
                MapTileObjects[i] = typeof(HouseWallObject); // Roof

            for (var i = 88; i < 89; i++)
                MapTileObjects[i] = typeof(SteelDoorObject);

            MapTileObjects[98] = typeof(HouseWallObject); // Roof

            for (var i = 83; i < 84; i++)
                MapTileObjects[i] = typeof(FlagObject);
        }
    }
}
