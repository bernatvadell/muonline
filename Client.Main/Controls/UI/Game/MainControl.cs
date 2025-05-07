using Client.Main.Core.Client;
using Microsoft.Xna.Framework;
using System.Linq;

namespace Client.Main.Controls.UI.Game
{
    public class MainControl : DynamicLayoutControl
    {
        protected override string LayoutJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainLayout.json";
        protected override string TextureRectJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainRect.json";
        protected override string DefaultTexturePath => "Interface/GFx/main_IE.ozd";

        private readonly MainHPControl _hp;
        private readonly MainMPControl _mp;

        public MainControl(CharacterState state)
        {
            // pół-przezroczyste skróty klawiszowe
            foreach (var key in new[] { "ActiveSkill_1", "ActiveSkill_2", "0", "1", "2", "3", "4", "5" })
                AlphaOverrides[key] = 0.7f;

            /* ------------ HP -------------*/
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
                extHp.RenderOrder = lhp.Z;

            /* ------------ MP -------------*/
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
                extMp.RenderOrder = lmp.Z;

            // startowe wartości z CharacterState (jeśli już dostępne)
            _hp.SetValues((int)state.CurrentHp, (int)state.MaxHp);
            _mp.SetValues((int)state.CurrentMana, (int)state.MaxMp);

            // subskrypcje – reagujemy kiedy serwer coś przyśle
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
            foreach (var c in Controls.OrderBy(c => (c as ExtendedUIControl)?.RenderOrder ?? 0))
                c.Draw(gameTime);

            _hp.DrawLabel(gameTime);
            _mp.DrawLabel(gameTime);
        }
    }
}
