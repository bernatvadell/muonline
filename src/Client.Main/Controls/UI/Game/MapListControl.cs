using Client.Main.Models;
using Client.Main.Scenes;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Game
{
    public class MapListControl : UIControl
    {
        public MapListControl()
        {
            Align = ControlAlign.Top | ControlAlign.Left;
            Margin = new Margin { Top = 10, Left = 10 };
            BackgroundColor = Color.Black * 0.6f;

            AddButtons();
        }

        public void AddButtons()
        {
            int y = 0;
            Controls.Add(new MapButton<LorenciaWorld> { Name = "Lorencia", Y = y });
            Controls.Add(new MapButton<NoriaWorld> { Name = "Noria", Y = y += 25 });
            Controls.Add(new MapButton<ElvelandWorld> { Name = "Elveland", Y = y += 25 });
            Controls.Add(new MapButton<DeviasWorld> { Name = "Devias", Y = y += 25 });
            Controls.Add(new MapButton<DungeonWorld> { Name = "Dungeon", Y = y += 25 });
            Controls.Add(new MapButton<AtlansWorld> { Name = "Atlans", Y = y += 25 });
            Controls.Add(new MapButton<LostTowerWorld> { Name = "Lost Tower", Y = y += 25 });
            Controls.Add(new MapButton<IcarusWorld> { Name = "Icarus", Y = y += 25 });
            Controls.Add(new MapButton<World101World> { Name = "World 101", Y = y += 25 });
        }
    }

    public class MapButton<TWorld> : UIControl where TWorld : WalkableWorldControl, new()
    {
        public string Name { get; set; }

        public Color TextColor { get; set; } = Color.White;

        public MapButton()
        {
            Height = 25;
            Width = 190;
            AutoSize = false;
            Interactive = true;
        }

        public override async Task Load()
        {
            await base.Load();

            Click += MapButton_Click;
        }

        private void MapButton_Click(object sender, System.EventArgs e)
        {
            if (MuGame.Instance.ActiveScene is GameScene gameScene)
            {
                gameScene.ChangeMap<TWorld>();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            MuGame.Instance.SpriteBatch.Begin();
            MuGame.Instance.SpriteBatch.DrawString(MuGame.Instance.Font, Name, new Vector2() { X = ScreenLocation.X + 5, Y = ScreenLocation.Y + 5 }, TextColor);
            MuGame.Instance.SpriteBatch.End();

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (MuGame.Instance.ActiveScene.MouseControl == this)
            {
                BackgroundColor = Color.Yellow;
                TextColor = Color.Black;
            }
            else
            {
                BackgroundColor = Color.Transparent;
                TextColor = Color.White;
            }
        }
    }
}