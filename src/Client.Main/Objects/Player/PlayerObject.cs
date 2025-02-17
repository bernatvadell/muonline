using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerObject : WalkerObject
    {
        // Indicates whether the player is currently in a resting state.
        public bool IsResting { get; set; } = false;
        // When set, indicates the target tile of a RestPlace.
        public Vector2? RestPlaceTarget { get; set; }

        private PlayerMaskHelmObject _helmMask;
        private PlayerHelmObject _helm;
        private PlayerArmorObject _armor;
        private PlayerPantObject _pant;
        private PlayerGloveObject _glove;
        private PlayerBootObject _boot;
        private WingObject _wing;
        private PlayerClass _playerClass;

        public PlayerClass PlayerClass
        {
            get => _playerClass;
            set { _playerClass = value; OnChangePlayerClass(); }
        }

        // Changed property to use PlayerAction.
        public new PlayerAction CurrentAction
        {
            get => (PlayerAction)base.CurrentAction;
            set => base.CurrentAction = (int)value;
        }

        public PlayerObject()
        {
            BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 120));
            Scale = 0.85f;
            AnimationSpeed = 8f;
            CurrentAction = PlayerAction.StopMale;
            _playerClass = Constants.Character;

            Children.Add(_armor = new PlayerArmorObject
            {
                LinkParentAnimation = true
            });

            Children.Add(_helmMask = new PlayerMaskHelmObject
            {
                LinkParentAnimation = true,
                Hidden = true
            });

            Children.Add(_helm = new PlayerHelmObject
            {
                LinkParentAnimation = true
            });

            Children.Add(_pant = new PlayerPantObject
            {
                LinkParentAnimation = true
            });

            Children.Add(_glove = new PlayerGloveObject
            {
                LinkParentAnimation = true
            });

            Children.Add(_boot = new PlayerBootObject
            {
                LinkParentAnimation = true
            });

            Children.Add(_wing = new WingObject
            {
                LinkParentAnimation = false,
                Hidden = false,
                // Position = new Vector3(0, 5, 140)
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

            if (World is not WalkableWorldControl)
                return;

            // If a rest target has been set, check if the player is at the rest place.
            if (RestPlaceTarget.HasValue)
            {
                float restDistance = Vector2.Distance(Location, RestPlaceTarget.Value);
                // If the player is very close to the designated rest tile, force resting animation.
                if (restDistance < 0.1f)
                {
                    if (World.WorldIndex == 4)
                    {
                        CurrentAction = PlayerAction.PlayerFlyingRest;
                    }
                    else
                    {
                        CurrentAction = PlayerAction.PlayerStandingRest;
                    }
                    // Remain in resting state as long as the player stays at the rest place.
                    return;
                }
                // If the player has started moving away from the rest place beyond a threshold, clear the rest state.
                else if (restDistance > 1.0f) // threshold,  1 tile
                {
                    RestPlaceTarget = null;
                    IsResting = false;
                }
            }

            // Normal update of animations when not in rest state.
            if (IsMoving)
            {
                if (World.WorldIndex == 8)
                    CurrentAction = PlayerAction.RunSwim;
                else if (World.WorldIndex == 11)
                    CurrentAction = PlayerAction.Fly;
                else
                    CurrentAction = PlayerAction.WalkMale;
            }
            else
            {
                if (World.WorldIndex == 8 || World.WorldIndex == 11)
                    CurrentAction = PlayerAction.StopFlying;
                else
                    CurrentAction = PlayerAction.StopMale;
            }
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
