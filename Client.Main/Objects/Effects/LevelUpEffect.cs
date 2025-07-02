using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Top-level container that spawns magic circle + flares
    /// and destroys itself after _lifetime_ seconds.
    /// </summary>
    public class LevelUpEffect : WorldObject
    {
        private const float _lifetimeTotal = 3.5f;
        private float _lifetime = _lifetimeTotal;

        public LevelUpEffect(Vector3 position)
        {
            Position = position;
        }

        public override async Task Load()
        {
            await base.Load();

            // magic circle - positioned slightly above ground
            var circle = new LevelUpMagicCircle(Position + new Vector3(0, 0, 10f));
            World.Objects.Add(circle);
            await circle.Load();

            // 20 flares - starting from slightly elevated position
            Vector3 flareStartPos = Position + new Vector3(0, 0, 25f);
            for (int i = 0; i < 30; i++)
            {
                var flare = new LevelUpFlare(flareStartPos);
                World.Objects.Add(flare);
                await flare.Load();
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _lifetime -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_lifetime <= 0f)
            {
                World?.RemoveObject(this);
                Dispose();
            }
        }
    }
}