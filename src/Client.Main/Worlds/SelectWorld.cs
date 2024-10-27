using Client.Main.Controls;
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Client.Main.Objects.Worlds.Noria;
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

        public SelectWorld() : base(94)
        {
            Camera.Instance.ViewFar = 5500f;
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
            MapTileObjects[14] = typeof(PlayerObject);
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            _player = Objects.OfType<PlayerObject>().FirstOrDefault();
            _player.Angle = new Vector3(0, 0, MathHelper.ToRadians(90));
            _player.Interactive = true;
            _player.Click += (s, e) =>
            {
                MuGame.Instance.ChangeScene<GameScene>();
            };

            // TODO: You should check the loading of CameraAnglePosition in World. I am not able to get the correct target.
            Camera.Instance.Target = new Vector3(14229.295898f, 12360.358398f, 300);
            Camera.Instance.FOV = 45;
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
