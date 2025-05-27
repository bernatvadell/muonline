using Client.Data;
using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class FurnitureObject : ModelObject
    {
        private bool isActivated = false;
        private Vector2 targetTile;
        public FurnitureObject()
        {
            LightEnabled = true;
            Interactive = true;
        }

        public override async Task Load()
        {
            var idx = (Type - (ushort)ModelType.Furniture01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Furniture{idx}.bmd");
            await base.Load();
            targetTile = new Vector2((int)(Position.X / Constants.TERRAIN_SCALE), (int)(Position.Y / Constants.TERRAIN_SCALE));
        }

        public override void OnClick()
        {
            base.OnClick();

            if (World is Controls.WalkableWorldControl control && control.Walker != null && (Type == 145 || Type == 146))
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

            if (isActivated && World is Controls.WalkableWorldControl control && control.Walker != null && (Type == 145 || Type == 146))
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
                        if (Type != 146)
                            player.Direction = DirectionExtensions.GetDirectionFromAngle(this.Angle.Z);
                    }
                }

            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
