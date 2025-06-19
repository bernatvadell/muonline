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
    [WorldInfo(142, "Doppelganger Ice Zone (New)")]
    public class World143World : WalkableWorldControl
    {
        public World143World() : base(worldIndex: 143) //
        {

        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(190, 230);
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
