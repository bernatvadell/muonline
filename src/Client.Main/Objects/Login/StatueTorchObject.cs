using Client.Main.Content;
using Client.Main.Controls;
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

namespace Client.Main.Objects.Login
{
    public class StatueTorchObject : ModelObject
    {
        public StatueTorchObject()
        {
            Alpha = 1f;

            Children.Add(new LightEffect()
            {
                Scale = 2f
            });

            Children.Add(
                ParticleIssuerObject
                    .Create()
                    .SetDuration(1f, 2f)
                    .SetGravity(new Vector3(0, -9.8f, 0))
                    .EnableRotation()
                    .Use<FireHik01Effect>()
                    .Use<FireHik02Effect>()
                    .Use<FireHik03Effect>()
            );
        }

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            var modelPath = Path.Join($"Object74", $"Object80.bmd");

            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model == null)
                Debug.WriteLine($"Can load MapObject for model: {modelPath}");

            await base.Load(graphicsDevice);
        }
    }
}
