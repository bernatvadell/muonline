using Client.Main.Controls;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        private readonly PlayerObject _hero = new();
        private readonly MainControl _main;
        private WorldControl _nextWorld = null;

        public PlayerObject Hero { get => _hero; }

        public GameScene()
        {
            Controls.Add(_main = new MainControl());
            Controls.Add(new MapListControl());
        }

        public override async Task Load()
        {
            await base.Load();
            ChangeMap<AtlansWorld>();
            await _hero.Load();
        }

        public void ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            if (_nextWorld != null) return;
            _nextWorld = new T() { Walker = _hero };
            _nextWorld.Objects.Add(_hero);
            Task.Run(() => _nextWorld.Initialize()).ConfigureAwait(true);
        }


        public override void Update(GameTime gameTime)
        {
            if (_nextWorld != null)
            {
                if (_nextWorld.Status == GameControlStatus.Ready)
                {
                    World?.Dispose();
                    World = _nextWorld;
                    _nextWorld = null;

                    Controls.Insert(0, World);
                    _hero.Reset();
                }
            }

            if (World == null)
                return;

            base.Update(gameTime);

            if (Status != GameControlStatus.Ready || !Visible)
                return;
        }

        public override void Draw(GameTime gameTime)
        {
            if (World == null || World.Status != GameControlStatus.Ready)
            {
                // TODO: Show loading screen?
                return;
            }

            base.Draw(gameTime);
        }
    }
}
