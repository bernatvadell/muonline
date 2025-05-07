using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class DevilSquareWorld : WalkableWorldControl
    {
        public DevilSquareWorld() : base(worldIndex: 10) // DEVIL SQUARE
        {
            Name = "Devil Square";
            BackgroundMusicPath = "Music/devil_square_intro.mp3";
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
    }
}
