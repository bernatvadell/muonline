using Client.Main.Controls;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        private readonly PlayerObject _hero = new(Constants.Character);
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
            await ChangeMapAsync<IcarusWorld>();
            await _hero.Load();
        }

        public void ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            Task.Run(async () =>
            {
                try
                {
                    await ChangeMapAsync<T>();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }).ConfigureAwait(false);
        }

        public async Task ChangeMapAsync<T>() where T : WalkableWorldControl, new()
        {
            World?.Dispose();
            World = null;

            var world = new T() { Walker = _hero };
            world.AddObject(_hero);

            _nextWorld = world;
            await _nextWorld.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            if (_nextWorld != null)
            {
                if (_nextWorld.Status == GameControlStatus.Ready)
                {
                    World?.Dispose();
                    World = _nextWorld;
                    Controls.Insert(0, World);
                    _nextWorld = null;
                }
                else
                {
                    return;
                }
            }

            base.Update(gameTime);

            if (Status != GameControlStatus.Ready || !Visible)
                return;
        }

        public override void Draw(GameTime gameTime)
        {
            if (_nextWorld != null)
            {
                // TODO: Show loading screen?
                return;
            }

            base.Draw(gameTime);
        }
    }
}
