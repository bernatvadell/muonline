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
    [WorldInfo(63, "Vulcanus")]
    public class VulcanusWorld : WalkableWorldControl
    {
        public VulcanusWorld() : base(worldIndex: 64) // VULCANUS
        {

        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(170, 185);
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
