using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public abstract class S6EventWorldBase : WalkableWorldControl
    {
        protected S6EventWorldBase(short worldIndex, string name)
            : base(worldIndex)
        {
            Name = name;
        }

        protected virtual Vector2 DefaultSpawn => new Vector2(128, 128);

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = DefaultSpawn;

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
