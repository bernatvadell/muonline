using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    [WorldInfo(33, "Aida")]
    public class World034World : WalkableWorldControl
    {
        public World034World() : base(worldIndex: 34) // AIDA
        {
            Name = "Aida";
            BackgroundMusicPath = "Music/Aida.mp3";
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(85, 10);
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
