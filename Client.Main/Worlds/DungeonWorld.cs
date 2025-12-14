using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Worlds.Dungeon;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    [WorldInfo(1, "Dungeon")]
    public class DungeonWorld : WalkableWorldControl
    {
        public DungeonWorld() : base(worldIndex: 2)
        {
            BackgroundMusicPath = "Music/Dungeon.mp3";
            AmbientSoundPath = "Sound/aDungeon.wav"; // Dungeon atmosphere
            Name = "Dungeon";
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(232, 126);
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
            MapTileObjects[1] = typeof(SpiderWeb1Object);
            MapTileObjects[17] = typeof(SpiderWeb17Object);
            MapTileObjects[52] = typeof(RestPlaceObject);
            MapTileObjects[60] = typeof(RestPlaceObject);
        }
    }
}
