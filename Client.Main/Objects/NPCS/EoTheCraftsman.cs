using Client.Main.Content;
using Client.Main.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    public class EoTheCraftsman : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/ElfMerchant01.bmd");
            await base.Load();
        }
    }
}
