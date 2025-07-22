using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI.Game;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;
using System.Reflection;

namespace Client.Main.Objects.NPCS
{

    [NpcInfo(242, "Elf Lala")]
    public class ElfLala : NPCObject
    {
        public override async Task Load()
        {
            ExtraHeight = 90f;
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

        public override void DrawMesh(int mesh)
        {
            // For wings (mesh 1)
            if (mesh == 1)
            {
                var gd = GraphicsDevice;
                var originalDepth = gd.DepthStencilState;
                var noWriteDepth = new DepthStencilState
                {
                    DepthBufferEnable = true,
                    DepthBufferWriteEnable = false
                };
                gd.DepthStencilState = noWriteDepth;

                // Call base implementation
                base.DrawMesh(mesh);

                // Restore original state
                gd.DepthStencilState = originalDepth;
            }
            else
            {
                // Normal rendering for other meshes
                base.DrawMesh(mesh);
            }
        }

    }
}
