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
        private PlayerObject _player;

        public GameScene()
        {
            
        }

        public override async Task Load()
        {
            await base.Load();
            await ChangeWorldAsync<LorenciaWorld>();
            await World.AddObjectAsync(new CursorObject());
            await World.AddObjectAsync(_player = new PlayerObject());
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready || !Visible)
                return;

            _player.BringToFront();
            _player.CurrentAction = ((WalkableWorldControl)World).IsMoving ? 25 : 3;
            _player.Position = ((WalkableWorldControl)World).MoveTargetPosition + new Vector3(0, 0, World.Terrain.RequestTerrainHeight(World.TargetPosition.X, World.TargetPosition.Y) - 40);
        }
    }
}
