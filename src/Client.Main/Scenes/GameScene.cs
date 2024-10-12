using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        private readonly PlayerObject _hero = new();
        public PlayerObject Hero { get => _hero; }

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
        }
    }
}
