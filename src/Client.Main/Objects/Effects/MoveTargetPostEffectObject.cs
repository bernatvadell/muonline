using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Effects
{
    public class MoveTargetPostEffectObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Effect/MoveTargetPosEffect.bmd");
            await base.Load();
        }
    }
}
