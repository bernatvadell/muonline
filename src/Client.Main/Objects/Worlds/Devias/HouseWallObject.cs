using Client.Data;
using Client.Main.Content;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Devias
{
    public class HouseWallObject : ModelObject
    {
        private float _alpha = 1f;
        private const float TARGET_ALPHA = 0.25f;
        private const float FADE_SPEED = 0.1f;
        private const float Y_PROXIMITY_THRESHOLD = 100f;
        private const float Scale = 100f; // Conversion factor for player position

        public override async Task Load()
        {
            BlendState = BlendState.AlphaBlend;
            LightEnabled = true;

            // Special settings for type 78
            if (Type == 78)
            {
                BlendMesh = 3;
                BlendMeshState = BlendState.Additive;
            }

            Model = await BMDLoader.Instance.Prepare($"Object3/Object{Type + 1}.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // If World is not a WalkableWorldControl, exit
            if (!(World is WalkableWorldControl walkableWorld))
                return;

            // Convert the player's position to the same scale as the object's position
            Vector2 playerPos = walkableWorld.Walker.Location * Scale;

            // Handling building wall objects (types 75, 76, 77, 78)
            if (Type == 75 || Type == 76 || Type == 77 || Type == 78)
            {
                // Determine if the player is "behind" the wall:
                // - the player must be in front of the wall (along the X axis)
                // - the distance along the X axis must be less than 300
                bool isBehind = (playerPos.X < Position.X) && (Math.Abs(playerPos.X - Position.X) < 300f);
                bool isWithinY = Math.Abs(playerPos.Y - Position.Y) <= Y_PROXIMITY_THRESHOLD;
                float targetAlpha = (isBehind && isWithinY) ? TARGET_ALPHA : 1f;

                _alpha = MathHelper.Lerp(_alpha, targetAlpha, FADE_SPEED);
                Alpha = _alpha;
            }
            // Handling fence objects (Fence01, Fence02) and type 98
            else if (Type == (ushort)ModelType.Fence01 || Type == (ushort)ModelType.Fence02 || Type == 98)
            {
                bool playerInside = IsPlayerUnderRoof(walkableWorld);
                float targetAlpha = playerInside ? 0f : 1f;
                Alpha = MathHelper.Lerp(Alpha, targetAlpha, FADE_SPEED);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }

        /// <summary>
        /// Checks if the player is under the roof (inside the building)
        /// </summary>
        /// <param name="walkableWorld">The world in which the player is present</param>
        /// <returns>true if the player is within the defined bounds of the building; otherwise false</returns>
        private bool IsPlayerUnderRoof(WalkableWorldControl walkableWorld)
        {
            // Player's position converted to world units
            Vector2 playerPos = walkableWorld.Walker.Location * Scale;
            Vector3 playerPos3D = new Vector3(playerPos.X, playerPos.Y, Position.Z);

            // Define the building's bounds using offsets – adjust these values as needed
            Vector3 minOffset = new Vector3(500f, 400f, 1000f);
            Vector3 maxOffset = new Vector3(400f, 500f, 1000f);
            BoundingBox buildingBounds = new BoundingBox(Position - minOffset, Position + maxOffset);

            return buildingBounds.Contains(playerPos3D) == ContainmentType.Contains;
        }

        public override void DrawMesh(int mesh)
        {
            base.DrawMesh(mesh);
        }
    }
}
