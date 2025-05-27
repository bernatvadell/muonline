using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class RestPlaceObject : ModelObject
    {
        private bool isWaitingForPlayerArrival = false;
        private Vector2 targetTile;
        private PlayerObject targetPlayer;
        private bool hasTriggeredRestAnimation = false;

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

            if (World is Controls.WalkableWorldControl control && control.Walker is PlayerObject player)
            {
                // Clear previous resting state
                player.IsResting = false;
                player.RestPlaceTarget = null;

                // Reset animation flag
                hasTriggeredRestAnimation = false;

                // Set waiting flag for player arrival
                isWaitingForPlayerArrival = true;
                targetPlayer = player;

                // Send player to the resting place
                control.Walker.MoveTo(targetTile);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Check if we are waiting for the player to arrive
            if (isWaitingForPlayerArrival && targetPlayer != null)
            {
                // Calculate the distance from the player to the target
                float distance = Vector2.Distance(targetPlayer.Location, targetTile);

                // If the player has reached the resting place and stopped, and the animation has not been triggered yet
                if (distance < 0.1f && !targetPlayer.IsMoving && !hasTriggeredRestAnimation)
                {
                    // Mark that the animation has been triggered
                    hasTriggeredRestAnimation = true;

                    // Reset the waiting flag
                    isWaitingForPlayerArrival = false;

                    // Now set the resting state and animation
                    targetPlayer.IsResting = true;
                    targetPlayer.RestPlaceTarget = targetTile;
                    targetPlayer.Direction = DirectionExtensions.GetDirectionFromAngle(this.Angle.Z);

                    // Clear the reference
                    targetPlayer = null;
                }
                // If the player has moved away or stopped moving towards the target
                else if (distance > 5.0f || (!targetPlayer.IsMoving && distance > 1.0f))
                {
                    // Cancel the waiting if the player has changed direction or stopped far from the target
                    isWaitingForPlayerArrival = false;
                    hasTriggeredRestAnimation = false;
                    targetPlayer = null;
                }
            }
        }
    }
}