using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(1, "Hound")]
    public class Hound : MonsterObject
    {
        public Hound() 
        {
            RenderShadow = false;
        }

    public override async Task Load()
    {
        Model = await BMDLoader.Instance.Prepare($"Monster/Monster02.bmd");
        await base.Load();
    }
}
}
