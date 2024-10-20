using Client.Data;
using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Arena
{
    public class BugObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object7/Bug01.bmd");
            await base.Load();
        }
    }
}
