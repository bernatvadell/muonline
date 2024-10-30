using Client.Main.Content;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class Monster439 : MonsterObject
    {
        public Monster439()
        {
            RenderShadow = true;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster439.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
    }
}
