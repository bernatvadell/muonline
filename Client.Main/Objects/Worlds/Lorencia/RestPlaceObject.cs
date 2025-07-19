using Client.Data.BMD;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class RestPlaceObject : ModelObject
    {
        private static readonly BMD _dummyBmd = new BMD();

        private bool isWaitingForPlayerArrival = false;
        private Vector2 targetTile;
        private PlayerObject targetPlayer;
        private bool hasTriggeredRestAnimation = false;

        public RestPlaceObject()
        {
            Interactive = true;
            BoundingBoxLocal = new BoundingBox(new Vector3(-25, -25, 0), new Vector3(25, 25, 200));
        }

        public override async Task Load()
        {
            Model = _dummyBmd;
            await base.LoadContent();

            Status = GameControlStatus.Ready;
            targetTile = new Vector2((int)(Position.X / Constants.TERRAIN_SCALE), (int)(Position.Y / Constants.TERRAIN_SCALE));
        }

        public override void OnClick()
        {
            base.OnClick();

            if (World is Controls.WalkableWorldControl control && control.Walker is PlayerObject player)
            {
                player.IsResting = false;
                player.RestPlaceTarget = null;
                hasTriggeredRestAnimation = false;
                isWaitingForPlayerArrival = true;
                targetPlayer = player;
                control.Walker.MoveTo(targetTile);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (isWaitingForPlayerArrival && targetPlayer != null)
            {
                float distance = Vector2.Distance(targetPlayer.Location, targetTile);
                if (distance < 0.1f && !targetPlayer.IsMoving && !hasTriggeredRestAnimation)
                {
                    hasTriggeredRestAnimation = true;
                    isWaitingForPlayerArrival = false;

                    targetPlayer.IsResting = true;
                    targetPlayer.RestPlaceTarget = targetTile;
                    targetPlayer.Direction = DirectionExtensions.GetDirectionFromAngle(this.Angle.Z);

                    targetPlayer = null;
                }
                else if (distance > 5.0f || (!targetPlayer.IsMoving && distance > 1.0f))
                {
                    isWaitingForPlayerArrival = false;
                    hasTriggeredRestAnimation = false;
                    targetPlayer = null;
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            DrawBoundingBox3D();
        }
    }
}