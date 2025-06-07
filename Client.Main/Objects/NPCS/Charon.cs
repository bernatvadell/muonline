using Client.Main.Content;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(237, "Charon")]
    public class Charon : NPCObject
    {
        private readonly Lightning2Effect _ligh1;
        private readonly Lightning2Effect _ligh2;

        public Charon()
        {
            Children.Add(_ligh1 = new Lightning2Effect { Scale = 1.2f });
            Children.Add(_ligh2 = new Lightning2Effect { Scale = 1.2f });
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/DevilNpc01.bmd");
            RenderShadow = true;
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible) return;

            var luminosity = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 0.002f) * 0.35f + 0.65f;
            var light = new Vector3(luminosity, luminosity, luminosity);

            _ligh1.Position = _ligh2.Position = BoneTransform[20].Translation + new Vector3(-5f, -10f, 10f);
            _ligh1.Light = _ligh2.Light = light;

            var rotation1 = (float)(FPSCounter.Instance.WorldTime / 50.0f);
            var rotation2 = (float)(-FPSCounter.Instance.WorldTime / 50.0f);

            _ligh1.Angle = new Vector3(rotation1, rotation1, rotation1);
            _ligh2.Angle = new Vector3(rotation2, rotation2, rotation2);
        }
        protected override void HandleClick()
        {
            // Handle the click event specific to this NPC
            Console.WriteLine("Specific NPC clicked!");
        }
    }
}
