using Client.Data.CWS;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    public abstract class TourWorldControl(short worldIndex) : WorldControl(worldIndex)
    {
        private CameraTourController _tourController;

        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            await base.Load(graphicsDevice);

            var worldFolder = $"World{WorldIndex}";
            var cameraWalkScriptReader = new CWSReader();
            var cameraWalkScript = await cameraWalkScriptReader.Load(Path.Combine(Constants.DataPath, worldFolder, $"CWScript{WorldIndex}.cws"));
            _tourController = new CameraTourController(cameraWalkScript.WayPoints, true, this);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);
            _tourController?.Update(time);
        }
    }
}
