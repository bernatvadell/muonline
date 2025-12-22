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
    [WorldInfo(38, "Kanturu Remain")]
    public class World039World : WalkableWorldControl
    {
        public World039World() : base(worldIndex: 39) // KANTURU REMAIN (RELICS)
        {
            // World39 folder is missing TileRock05-07.ozj - map to existing rock textures
            Terrain.TextureMappingFiles[10] = "TileRock04.OZJ";
            Terrain.TextureMappingFiles[11] = "TileRock04.OZJ"; // Fallback for missing TileRock05
            Terrain.TextureMappingFiles[12] = "TileRock03.OZJ"; // Fallback for missing TileRock06
            Terrain.TextureMappingFiles[13] = "TileRock02.OZJ"; // Fallback for missing TileRock07
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(72, 105);
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
