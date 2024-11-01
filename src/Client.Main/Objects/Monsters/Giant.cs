using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class Giant : MonsterObject
    {
        public Giant()
        {
            RenderShadow = false;
        }

    public override async Task Load()
    {
        Model = await BMDLoader.Instance.Prepare($"Monster/Monster06.bmd");
        await base.Load();
    }
}
}
