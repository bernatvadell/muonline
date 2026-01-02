using Client.Main.Content;
using Microsoft.Xna.Framework;
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
            BlendMesh = 0;
            LightEnabled = true;
            Light = new Vector3(1f, 0.7f, 0.3f);
            await base.Load();
        }
        protected override void CalculateOutOfView()
        {
            OutOfView = false;
        }
    }
}
