using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(570, "Bone Scorpion")]
    public class BoneScorpion : MonsterObject
    {
        public BoneScorpion()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster211.bmd");
            await base.Load();
        }
    }
}
