using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public class CursorObject : WorldObject
    {
        public float _visibleTime = 0f;

        public override async Task Load()
        {
            Scale = 0.7f;
            Children.Add(new MoveTargetPostEffectObject());
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            if (_visibleTime > 0)
            {
                _visibleTime -= gameTime.ElapsedGameTime.Milliseconds;
                Alpha = _visibleTime / 1500f;
            }
            else if (!Hidden)
            {
                Hidden = true;
            }

            base.Update(gameTime);
        }

        protected override void OnPositionChanged()
        {
            base.OnPositionChanged();
            Hidden = false;
            _visibleTime = 1500f;
            Alpha = 1f;
        }
    }
}
