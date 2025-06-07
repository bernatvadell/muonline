using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI.Game;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{

    [NpcInfo(242, "Elf Lala")]
    public class ElfLala : NPCObject
    {
        public override async Task Load()
        {
            ExtraHeight = 90f;
            BlendMesh = 1;
            Model = await BMDLoader.Instance.Prepare($"NPC/ElfWizard01.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible)
                return;

            if (FPSCounter.Instance.RandFPSCheck(125))
            {
                Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
                SoundController.Instance.PlayBufferWithAttenuation("Sound/nHarp.wav", Position, listenerPosition);
                CurrentAction = CurrentAction == 0 ? 1 : 0;
            }
        }
        protected override void HandleClick()
        {
            NpcShopControl.Instance.Visible = true;
        }

    }
}
