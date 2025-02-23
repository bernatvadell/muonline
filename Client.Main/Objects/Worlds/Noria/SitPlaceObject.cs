using Client.Data;
using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Noria
{
    public class SitPlaceObject : ModelObject
    {
        private bool isActivated = false;
        private Vector2 targetTile;

        public SitPlaceObject()
        {
            Interactive = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object4/Object0{Type + 1}.bmd");
            await base.Load();
            targetTile = new Vector2((int)(Position.X / Constants.TERRAIN_SCALE), (int)(Position.Y / Constants.TERRAIN_SCALE));

        }

        public override void OnClick()
        {
            base.OnClick();

            if (World is Controls.WalkableWorldControl control && control.Walker != null)
            {
                isActivated = true;
                // Disable sitting state before moving
                if (control.Walker is PlayerObject player)
                {
                    player.IsSitting = false;
                }
                control.Walker.MoveTo(targetTile);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (isActivated && World is Controls.WalkableWorldControl control && control.Walker != null)
            {
                // Calculate the distance from the walker's current location to the target tile
                float distance = Vector2.Distance(control.Walker.Location, targetTile);
                if (distance < 0.1f)
                {
                    isActivated = false;
                    if (control.Walker is PlayerObject player)
                    {
                        player.IsSitting = true;
                        player.SitPlaceTarget = targetTile;
                        player.Direction = GetDirectionFromAngle(this.Angle.Z);
                    }
                }

            }
        }

        /// <summary>
        /// Maps a given angle (in radians) to the nearest Direction value.
        /// </summary>
        /// <param name="angle">Rotation angle in radians.</param>
        /// <returns>Nearest Direction enum value.</returns>
        private Direction GetDirectionFromAngle(float angle)
        {
            // Convert angle from radians to degrees and normalize to [0,360)
            float degrees = MathHelper.ToDegrees(angle);
            if (degrees < 0)
                degrees += 360;

            // Add half step (22.5) and divide by 45 to determine the nearest index (0-7)
            int index = (int)Math.Floor((degrees + 22.5f) / 45f) % 8;

            // Mapping order: index 0: SouthWest (0°), 1: South (45°), 2: SouthEast (90°),
            // 3: East (135°), 4: NorthEast (180°), 5: North (225°), 6: NorthWest (270°), 7: West (315°)
            Direction[] mapping =
            [
                Direction.SouthWest,
                Direction.South,
                Direction.SouthEast,
                Direction.East,
                Direction.NorthEast,
                Direction.North,
                Direction.NorthWest,
                Direction.West
            ];

            return mapping[index];
        }
    }
}
