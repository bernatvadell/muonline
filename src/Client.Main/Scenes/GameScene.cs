using Client.Main.Controls;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        private readonly PlayerObject _hero = new();
        private readonly MainControl _main;
        private WorldControl _nextWorld = null;
        private LoadingScreenControl _loadingScreen;
        private MapListControl _mapListControl;
        private bool _isChangingWorld = false;

        private KeyboardState _previousKeyboardState;

        public PlayerObject Hero => _hero;

        public GameScene()
        {
            Controls.Add(_main = new MainControl());

            _mapListControl = new MapListControl { Visible = false };

            _loadingScreen = new LoadingScreenControl { Visible = true };
            Controls.Add(_loadingScreen);
        }

        public override async Task Load()
        {
            await base.Load();
            await ChangeMap<DeviasWorld>();
            await _hero.Load();
        }

        public async Task ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            _isChangingWorld = true;

            if (_loadingScreen == null)
            {
                _loadingScreen = new LoadingScreenControl();
                Controls.Add(_loadingScreen);
            }
            _loadingScreen.Message = "Loading...";
            _loadingScreen.Visible = true;
            _main.Visible = false;

            // You can additionally force a screen redraw
            await Task.Delay(100);

            _nextWorld = new T() { Walker = _hero };
            _nextWorld.Objects.Add(_hero);
            await _nextWorld.Initialize();

            // Before removing the old world, ensure that no update/draw refers to it
            World?.Dispose();
            World = _nextWorld;
            _nextWorld = null;
            Controls.Insert(0, World);
            _hero.Reset();

            Controls.Remove(_loadingScreen);
            _loadingScreen = null;
            _main.Visible = true;

            _isChangingWorld = false;
        }

        public override void Update(GameTime gameTime)
        {
            if (_isChangingWorld)
                return; // do not update while the world is changing

            KeyboardState currentKeyboardState = Keyboard.GetState();
            if (currentKeyboardState.IsKeyDown(Keys.M) && !_previousKeyboardState.IsKeyDown(Keys.M))
            {
                bool newVisibility = !_mapListControl.Visible;
                _mapListControl.Visible = newVisibility;
                if (newVisibility)
                {
                    if (!Controls.Contains(_mapListControl))
                        Controls.Add(_mapListControl);
                }
                else
                {
                    Controls.Remove(_mapListControl);
                }
            }
            _previousKeyboardState = currentKeyboardState;

            if (World == null || World.Status != GameControlStatus.Ready)
                return;

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (_isChangingWorld)
            {
                _loadingScreen?.Draw(gameTime);
                return;
            }

            if (World == null || World.Status != GameControlStatus.Ready)
            {
                _loadingScreen?.Draw(gameTime);
                return;
            }

            base.Draw(gameTime);
        }
    }
}