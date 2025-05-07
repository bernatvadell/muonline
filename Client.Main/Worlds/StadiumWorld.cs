using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class StadiumWorld : WalkableWorldControl
    {
        public StadiumWorld() : base(worldIndex: 7) // ARENA
        {
            Name = "Arena";
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(56, 85);
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
