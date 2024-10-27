﻿using Client.Main.Controls;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System.Linq;

namespace Client.Main.Worlds
{
    public class NewLoginWorld : WorldControl
    {
        private PlayerObject _player;

        public NewLoginWorld() : base(95)
        {
            Camera.Instance.ViewFar = 50000f;
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
        }

        public override void AfterLoad()
        {
            base.AfterLoad();

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