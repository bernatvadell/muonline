using Client.Main.Controls;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        private readonly PlayerObject _hero = new();
        private readonly MainControl _main;
        private bool _changingWorld = false;

        public PlayerObject Hero { get => _hero; }

        public GameScene()
        {
            Controls.Add(_main = new MainControl());
            Controls.Add(new MapListControl());
        }

        public override async Task Load()
        {
            await base.Load();
            await ChangeMapAsync<NoriaWorld>();
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
            if (_changingWorld) return;

            _changingWorld = true;
            var sw = new Stopwatch();

            if (World != null)
            {
                sw.Start();
                World?.Dispose();
                sw.Stop();
                Debug.WriteLine($"Time elapsed disposing old world: {sw.ElapsedMilliseconds}ms");
            }

            sw.Restart();
            World = new T() { Walker = _hero };
            _hero.World = World;
            World.Objects.Add(_hero);
            Controls.Insert(0, World);

            await World.Initialize();

            _changingWorld = false;
            _hero.Reset();

            sw.Stop();
            Debug.WriteLine($"Time elapsed initializing map: {sw.ElapsedMilliseconds}ms");
        }

        public override void Update(GameTime gameTime)
        {
            if (World == null || _changingWorld)
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
