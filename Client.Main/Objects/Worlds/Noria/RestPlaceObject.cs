using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Noria
{
    public class RestPlaceObject : ModelObject
    {
        private bool isActivated = false;
        private Vector2 targetTile;

        public RestPlaceObject()
        {
            Interactive = true;
        }

        public override async Task Load()
        {
            await base.Load();
            targetTile = new Vector2((int)(Position.X / Constants.TERRAIN_SCALE), (int)(Position.Y / Constants.TERRAIN_SCALE));
        }

        public override void OnClick()
        {
            base.OnClick();

            if (World is Controls.WalkableWorldControl control && control.Walker != null)
            {
                isActivated = true;
                // Disable resting state before moving
                if (control.Walker is PlayerObject player)
                {
                    player.IsResting = false;
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
                        player.IsResting = true;
                        player.RestPlaceTarget = targetTile;
                        player.Direction = DirectionExtensions.GetDirectionFromAngle(this.Angle.Z);
                    }
                }

            }
        }
    }
}
