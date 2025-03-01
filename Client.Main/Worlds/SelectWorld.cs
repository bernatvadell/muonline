using Client.Main.Controls;
using Client.Main.Objects.Player;
using Client.Main.Objects.Worlds.SelectWrold;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System.Linq;

namespace Client.Main.Worlds
{

    public class SelectWorld : WorldControl
    {
        private PlayerObject _player;

        public SelectWorld() : base(worldIndex: 94)
        {
            Camera.Instance.ViewFar = 5500f;
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
            MapTileObjects[14] = typeof(PlayerObject);

            MapTileObjects[71] = typeof(BlendedObjects);
            MapTileObjects[11] = typeof(BlendedObjects);

            MapTileObjects[36] = typeof(LightObject);
            // MapTileObjects[36] = typeof(BlendedObjects);
            MapTileObjects[25] = typeof(BlendedObjects);
            MapTileObjects[33] = typeof(BlendedObjects);
            MapTileObjects[30] = typeof(BlendedObjects);

            MapTileObjects[31] = typeof(FlowersObject2);
            MapTileObjects[34] = typeof(FlowersObject);

            MapTileObjects[26] = typeof(WaterFallObject);
            MapTileObjects[24] = typeof(WaterFallObject);

            MapTileObjects[54] = typeof(WaterSplashObject);
            MapTileObjects[55] = typeof(WaterSplashObject);
            MapTileObjects[56] = typeof(WaterSplashObject);
        }

        public override void AfterLoad()
        {
            base.AfterLoad();

            // water animation parameters
            Terrain.WaterSpeed = 0.05f;             // Example: faster water movement
            Terrain.DistortionAmplitude = 0.2f;      // Example: stronger distortion
            Terrain.DistortionFrequency = 1.0f;      // Example: lower frequency for distortion

            _player = Objects.OfType<PlayerObject>().FirstOrDefault();
            _player.Angle = new Vector3(0, 0, MathHelper.ToRadians(90));
            _player.Interactive = true;
            _player.Click += (s, e) =>
            {
                MuGame.Instance.ChangeScene<GameScene>();
            };

            // TODO: You should check the loading of CameraAnglePosition in World. I am not able to get the correct target.
            Camera.Instance.Target = new Vector3(14229.295898f, 12340.358398f, 380);
            Camera.Instance.FOV = 29;
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
