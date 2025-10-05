using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    [WorldInfo(4, "Lost Tower")]
    public class LostTowerWorld : WalkableWorldControl
    {
        public LostTowerWorld() : base(worldIndex: 5)
        {
            BackgroundMusicPath = "Music/lost_tower_b.mp3";
            AmbientSoundPath = "Sound/aTower.wav"; // Tower atmosphere
            Name = "Lost Tower";
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(208, 81);
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
    }
}
