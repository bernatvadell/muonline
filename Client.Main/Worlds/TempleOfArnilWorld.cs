using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    [WorldInfo(133, "Temple of Arnil")]
    public class TempleOfArnilWorld : WalkableWorldControl
    {
        public TempleOfArnilWorld() : base(worldIndex: 134) // TEMPLE OF ARNIL (ARENIL TEMPLE)
        {

        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(99, 38);

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
