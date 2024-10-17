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

        public PlayerObject Hero { get => _hero; }

        public GameScene()
        {
            Controls.Add(_main = new MainControl());
        }

        public override async Task Load()
        {
            await base.Load();
            await ChangeMapAsync<LorenciaWorld>();
            await World.AddObjectAsync(_hero);
        }

        public void ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            World?.Dispose();
            var world = new T() { Walker = _hero };
            World = world;
            Controls.Add(world);
            Task.Run(() => World.Initialize());
        }

        public async Task ChangeMapAsync<T>() where T : WalkableWorldControl, new()
        {
            World?.Dispose();
            var world = new T() { Walker = _hero };
            World = world;
            Controls.Add(world);
            await World.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready || !Visible)
                return;

            _hero.BringToFront();
            _main.BringToFront();
        }
    }
}
