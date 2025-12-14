using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Worlds.Tarkan;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    [WorldInfo(8, "Tarkan")]
    public class TarkanWorld : WalkableWorldControl
    {
        public TarkanWorld() : base(worldIndex: 9) // TARKAN
        {
            Name = "Tarkan";
            BackgroundMusicPath = "Music/tarkan.mp3";
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(200, 58);
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
            MapTileObjects[82] = typeof(LightBeamObject);
            MapTileObjects[7] = typeof(LavaObject);
            MapTileObjects[8] = typeof(FlagObject);
            MapTileObjects[6] = typeof(GrassObject);
            MapTileObjects[15] = typeof(GrassObject);
            MapTileObjects[16] = typeof(GrassObject);
            MapTileObjects[17] = typeof(GrassObject);
            MapTileObjects[18] = typeof(GrassObject);
            MapTileObjects[19] = typeof(GrassObject);
            MapTileObjects[22] = typeof(GrassObject);
            MapTileObjects[33] = typeof(TreeObject);
            MapTileObjects[35] = typeof(GrassObject);
        }
    }
}
