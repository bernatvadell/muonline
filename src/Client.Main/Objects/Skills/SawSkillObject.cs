using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Skills
{
    public class SawSkillObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Skill/Saw01.bmd");
            await base.Load();
        }
    }
}
