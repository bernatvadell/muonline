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

            var world = new T() { Walker = _hero };
            await world.AddObjectAsync(_hero);

            World = world;
            Controls.Insert(0, world);
            await World.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready || !Visible)
                return;
        }
    }
}
