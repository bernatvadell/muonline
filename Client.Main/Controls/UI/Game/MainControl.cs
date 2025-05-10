using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace Client.Main.Controls.UI.Game
{
    public class MainControl : DynamicLayoutControl
    {
        // Resource paths
        protected override string LayoutJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainLayout.json";
        protected override string TextureRectJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainRect.json";
        protected override string DefaultTexturePath => "Interface/GFx/main_IE.ozd";

        // UI components
        private readonly MainHPControl _hp;
        private readonly MainMPControl _mp;

        public MainControl(CharacterState state)
        {
            // Semi-transparent hotkeys
            foreach (var key in new[] { "ActiveSkill_1", "ActiveSkill_2", "0", "1", "2", "3", "4", "5" })
            {
                AlphaOverrides[key] = 0.7f;
            }

            // HP control
            _hp = new MainHPControl { Name = "HP_1" };
            _hp.Tag = new LayoutInfo
            {
                ScreenX = 292,
                ScreenY = 604,
                Width = 100,
                Height = 100,
                Z = -5
            };
            if (_hp is ExtendedUIControl extHp && _hp.Tag is LayoutInfo lhp)
            {
                extHp.RenderOrder = lhp.Z;
            }

            // MP control
            _mp = new MainMPControl { Name = "MP_1" };
            _mp.Tag = new LayoutInfo
            {
                ScreenX = 860,
                ScreenY = 604,
                Width = 100,
                Height = 100,
                Z = -5
            };
            if (_mp is ExtendedUIControl extMp && _mp.Tag is LayoutInfo lmp)
            {
                extMp.RenderOrder = lmp.Z;
            }

            // Initial values from CharacterState (if available)
            _hp.SetValues((int)state.CurrentHp, (int)state.MaxHp);
            _mp.SetValues((int)state.CurrentMana, (int)state.MaxMp);

            // Update when server sends changes
            state.HealthChanged += (cur, max) => _hp.SetValues((int)cur, (int)max);
            state.ManaChanged += (cur, max) => _mp.SetValues((int)cur, (int)max);

            Controls.Clear();
            Controls.Add(_mp);
            Controls.Add(_hp);

            CreateControls();
            UpdateLayout();
        }

        public override void Draw(GameTime gameTime)
        {
            var sb = GraphicsManager.Instance.Sprite;

            foreach (var c in Controls.OrderBy(x => (x as ExtendedUIControl)?.RenderOrder ?? 0))
            {
                if (c is TextureControl)
                {
                    using (new SpriteBatchScope(
                           sb,
                           SpriteSortMode.Deferred,
                           BlendState.NonPremultiplied,
                           SamplerState.PointClamp))
                    {
                        c.Draw(gameTime);
                    }
                }
                else
                    c.Draw(gameTime);
            }

            _hp.DrawLabel(gameTime);
            _mp.DrawLabel(gameTime);
        }
    }
}
