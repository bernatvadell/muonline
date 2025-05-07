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
            Name = "Devias";
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(219, 24);

            Walker.Reset();

            bool shouldUseDefaultSpawn = false;
            if (MuGame.Network == null ||
                MuGame.Network.CurrentState == Core.Client.ClientConnectionState.Initial ||
                MuGame.Network.CurrentState == Core.Client.ClientConnectionState.Disconnected)
            {
                shouldUseDefaultSpawn = true;
            }
            else if (Walker.Location == Vector2.Zero)
            {
                shouldUseDefaultSpawn = true;
            }

            if (shouldUseDefaultSpawn)
            {
                Walker.Location = defaultSpawn;
            }

            Walker.MoveTargetPosition = Walker.TargetPosition;
            Walker.Position = Walker.TargetPosition;

            base.AfterLoad();
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();

            MapTileObjects[0] = typeof(TreeObject);
            MapTileObjects[75] = typeof(TreeObject);

            MapTileObjects[19] = typeof(AuroraObject);
            MapTileObjects[20] = typeof(SteelDoorObject);

            MapTileObjects[65] = typeof(SteelDoorObject);
            MapTileObjects[67] = typeof(SteelDoorObject);

            MapTileObjects[86] = typeof(SteelDoorObject); // Sliding doors

            for (var i = 76; i < 79; i++)
                MapTileObjects[i] = typeof(HouseWallObject); // Wall

            for (var i = 81; i < 83; i++)
                MapTileObjects[i] = typeof(HouseWallObject); // Roof

            for (var i = 83; i < 86; i++)
                MapTileObjects[i] = typeof(FlagObject);

            for (var i = 88; i < 89; i++)
                MapTileObjects[i] = typeof(SteelDoorObject);

            MapTileObjects[91] = typeof(RestPlaceObject);

            MapTileObjects[98] = typeof(HouseWallObject); // Roof
            MapTileObjects[99] = typeof(HouseWallObject); // Roof
        }
    }
}
