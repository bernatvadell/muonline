using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(6, "Lich")]
    public class Lich : MonsterObject
    {
        public Lich()
        {
            RenderShadow = false;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster05.bmd");
            await base.Load();
        }
    }
}
