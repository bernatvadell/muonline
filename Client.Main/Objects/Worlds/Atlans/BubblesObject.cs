using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Atlans
{
    public class BubblesObject : ModelObject
    {
        public bool IsActive;
        public Vector3 OriginalPosition;
        public float OriginalTexCoordV;
        public float CurrentTexCoordV;

        public override async Task Load()
        {
            // Load model if needed
            //Model = await BMDLoader.Instance.Prepare($"Object8/Object{Type + 1}.bmd");
            OriginalPosition = Position;
            await base.Load();

            // Create the bubble particle system instance
            var bubbleSystem = new BubbleParticleSystem();
            // Set its position (for example, to match the position of BubblesObject)
            bubbleSystem.Position = new Vector3(Position.X, Position.Y, Position.Z);

            // Add the bubble system to the world's object list directly
            World.Objects.Add(bubbleSystem);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
