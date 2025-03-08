using Microsoft.Xna.Framework;
using System.Linq;

namespace Client.Main.Controls.UI.Game
{
    public class MainControl : DynamicLayoutControl
    {
        protected override string LayoutJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainLayout.json";
        protected override string TextureRectJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainRect.json";
        protected override string DefaultTexturePath => "Interface/GFx/main_IE.ozd";

        public MainControl() : base()
        {
            AlphaOverrides["ActiveSkill_1"] = 0.7f;
            AlphaOverrides["ActiveSkill_2"] = 0.7f;
            AlphaOverrides["0"] = 0.7f;
            AlphaOverrides["1"] = 0.7f;
            AlphaOverrides["2"] = 0.7f;
            AlphaOverrides["3"] = 0.7f;
            AlphaOverrides["4"] = 0.7f;
            AlphaOverrides["5"] = 0.7f;

            var hpControl = new MainHPControl
            {
                CurrentHP = 400,
                MaxHP = 854,
                Name = "HP_1"
            };

            hpControl.Tag = new LayoutInfo
            {
                ScreenX = 292,
                ScreenY = 604,
                Width = 100,   // e.g., control width
                Height = 100,
                Z = -5     // render order
            };

            if (hpControl is ExtendedUIControl extCtrlHP && hpControl.Tag is LayoutInfo layoutHP)
            {
                extCtrlHP.RenderOrder = layoutHP.Z;
            }

            var mpControl = new MainMPControl
            {
                CurrentMP = 2500,
                MaxMP = 4500,
                Name = "MP_1",
            };

            mpControl.Tag = new LayoutInfo
            {
                ScreenX = 860,
                ScreenY = 604,
                Width = 100,   // e.g., control width
                Height = 100,
                Z = -5     // render order
            };

            if (mpControl is ExtendedUIControl extCtrlMP && mpControl.Tag is LayoutInfo layoutMP)
            {
                extCtrlMP.RenderOrder = layoutMP.Z;
            }

            Controls.Clear();
            Controls.Add(mpControl);
            Controls.Add(hpControl);
            CreateControls();
            UpdateLayout();
        }

        public override void Draw(GameTime gameTime)
        {
            // First, we draw all controls, sorting by RenderOrder.
            foreach (var ctrl in Controls.OrderBy(c => (c as ExtendedUIControl)?.RenderOrder ?? 0))
            {
                ctrl.Draw(gameTime);
            }

            // Then, we draw labels for custom controls.
            foreach (var ctrl in Controls)
            {
                if (ctrl is MainHPControl hpControl)
                {
                    hpControl.DrawLabel(gameTime);
                }
                else if (ctrl is MainMPControl mpControl)
                {
                    mpControl.DrawLabel(gameTime);
                }
            }
        }
    }
}