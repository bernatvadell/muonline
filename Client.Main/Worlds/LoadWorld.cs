using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;

namespace Client.Main.Worlds
{
    public class LoadWorld : WorldControl
    {
        public LoadWorld() : base(-1)
        {
            Camera.Instance.ViewFar = 50000f;
        }

        protected override void CreateMapTileObjects()
        {
            // base.CreateMapTileObjects();
        }

        public override void AfterLoad()
        {
            base.AfterLoad();

            // water animation parameters
            // Terrain.WaterSpeed = 0.05f;             // Example: faster water movement
            // Terrain.DistortionAmplitude = 0.2f;      // Example: stronger distortion
            // Terrain.DistortionFrequency = 1.0f;      // Example: lower frequency for distortion

            // TODO: We need fix CameraAnglePosition load
            Camera.Instance.Target += new Vector3(0, 0, 650);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);


            if (MuGame.Instance.PrevKeyboard.IsKeyDown(Keys.Delete) && MuGame.Instance.Keyboard.IsKeyUp(Keys.Delete))
            {
                if (Objects.Count > 0)
                {
                    var obj = Objects[0];
                    Debug.WriteLine($"Removing obj: {obj.Type} -> {obj.ObjectName}");
                    Objects.RemoveAt(0);
                }
            }
            else if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Add))
            {
                Camera.Instance.ViewFar += 10;
            }
            else if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Subtract))
            {
                Camera.Instance.ViewFar -= 10;
            }
        }
    }
}
