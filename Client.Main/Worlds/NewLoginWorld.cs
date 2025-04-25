using Client.Main.Controls;
using Client.Main.Objects.Player;
using Client.Main.Objects.Worlds.Login;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;

namespace Client.Main.Worlds
{
    public class NewLoginWorld : WorldControl
    {
        private PlayerObject _player;

        public NewLoginWorld() : base(worldIndex: 95)
        {
            _player = new PlayerObject();
            Camera.Instance.ViewFar = 50000f;
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
            MapTileObjects[5] = typeof(ShipObject);
            MapTileObjects[12] = typeof(ShipObject);
            MapTileObjects[13] = typeof(ShipObject);

            MapTileObjects[54] = typeof(WaterSplashObject);
            MapTileObjects[1] = typeof(ShipWaterPathObject);

            MapTileObjects[18] = typeof(BlendedObjects);
            MapTileObjects[7] = typeof(BlendedObjects);
            MapTileObjects[10] = typeof(BlendedObjects);
        }

        public override void AfterLoad()
        {
            base.AfterLoad();

            // water animation parameters
            Terrain.WaterSpeed = 0.15f;             // Example: faster water movement
            Terrain.DistortionAmplitude = 0.2f;      // Example: stronger distortion
            Terrain.DistortionFrequency = 1.0f;      // Example: lower frequency for distortion
            Terrain.WaterFlowDirection = Vector2.UnitY;

            // TODO: We need fix CameraAnglePosition load
            Camera.Instance.Target += new Vector3(0, 0, 650);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (!Visible || _player == null) return;

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
