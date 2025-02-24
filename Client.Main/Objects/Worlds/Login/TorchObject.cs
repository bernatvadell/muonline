using Client.Main.Content;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Login
{
    public class TorchObject : ModelObject
    {
        public TorchObject()
        {
            BoundingBoxColor = Color.Blue;
            ParentBoneLink = 1;
            Children.Add(new FlareEffect()
            {
                Scale = 4f
            });
        }

        public override async Task Load()
        {
            var modelPath = Path.Join($"Object74", $"Object38.bmd");

            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model == null)
                Debug.WriteLine($"Can't load MapObject for model: {modelPath}");

            await base.Load();
        }
    }
}
