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
        private PlayerClass _playerClass;

        public PlayerClass PlayerClass { get => _playerClass; set { _playerClass = value; OnChangePlayerClass(); } }

        public new PlayerAction CurrentAction { get => (PlayerAction)base.CurrentAction; set => base.CurrentAction = (int)value; }

        public PlayerObject()
        {
            //var color = new Color(255, (byte)(255 * 0.1f), (byte)(255 * 0.1f));
            //color = Color.White;
            BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 120));
            Scale = 0.85f;
            AnimationSpeed = 8f;
            CurrentAction = PlayerAction.StopMale;
            _playerClass = PlayerClass.DarkWizard;

            Children.Add(_armor = new PlayerArmorObject
            {
                LinkParent = true
            });

            Children.Add(_helmMask = new PlayerMaskHelmObject
            {
                LinkParent = true,
                Hidden = true
            });

            Children.Add(_helm = new PlayerHelmObject
            {
                LinkParent = true
            });

            Children.Add(_pant = new PlayerPantObject
            {
                LinkParent = true
            });

            Children.Add(_glove = new PlayerGloveObject
            {
                LinkParent = true
            });

            Children.Add(_boot = new PlayerBootObject
            {
                LinkParent = true
            });

            Children.Add(_wing = new WingObject
            {
                LinkParent = true,
                Hidden = true,
                Position = new Vector3(0, 5, 140)
            });
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");

            OnChangePlayerClass();

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

        private void OnChangePlayerClass()
        {
            _armor.PlayerClass = _playerClass;
            _helmMask.PlayerClass = _playerClass;
            _helm.PlayerClass = _playerClass;
            _pant.PlayerClass = _playerClass;
            _glove.PlayerClass = _playerClass;
            _boot.PlayerClass = _playerClass;
        }
    }
}
