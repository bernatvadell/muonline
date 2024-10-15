using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{

    public class PlayerObject : WalkerObject
    {
        // private PlayerShadowObject _shadowObject;
        private PlayerMaskHelmObject _helmMask;
        private PlayerHelmObject _helm;
        private PlayerArmorObject _armor;
        private PlayerPantObject _pant;
        private PlayerGloveObject _glove;
        private PlayerBootObject _boot;
        private WingObject _wing;

        public new PlayerAction CurrentAction { get => (PlayerAction)base.CurrentAction; set => base.CurrentAction = (int)value; }

        public PlayerObject()
        {
            var color = new Color(255, (byte)(255 * 0.1f), (byte)(255 * 0.1f));
            color = Color.White;
            BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 120));
            // Children.Add(_shadowObject = new PlayerShadowObject() { LinkParent = true, Hidden = true });
            Children.Add(_armor = new PlayerArmorObject() { LinkParent = true, Color = color });
            Children.Add(_helmMask = new PlayerMaskHelmObject() { LinkParent = true, Color = color, Hidden = true });
            Children.Add(_helm = new PlayerHelmObject() { LinkParent = true, Color = color });
            Children.Add(_pant = new PlayerPantObject() { LinkParent = true, Color = color });
            Children.Add(_glove = new PlayerGloveObject() { LinkParent = true, Color = color });
            Children.Add(_boot = new PlayerBootObject() { LinkParent = true, Color = color });
            Children.Add(_wing = new WingObject() { Position = new Vector3(0, 5, 140), Color = color, Hidden = false });
            Scale = 0.85f;
            AnimationSpeed = 5f;
            CurrentAction = PlayerAction.StopMale;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (IsMoving)
                CurrentAction = PlayerAction.WalkMale;
            else if (!IsMoving)
                CurrentAction = PlayerAction.StopMale;
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
