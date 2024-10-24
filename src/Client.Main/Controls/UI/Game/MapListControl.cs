using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Scenes;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
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
            int spacing = 20;
            Controls.Add(new MapButton<LorenciaWorld> { Name = "Lorencia", Y = y });
            Controls.Add(new MapButton<NoriaWorld> { Name = "Noria", Y = y += spacing });
            Controls.Add(new MapButton<ElvelandWorld> { Name = "Elveland", Y = y += spacing });
            Controls.Add(new MapButton<DeviasWorld> { Name = "Devias", Y = y += spacing });
            Controls.Add(new MapButton<DungeonWorld> { Name = "Dungeon", Y = y += spacing });
            Controls.Add(new MapButton<AtlansWorld> { Name = "Atlans", Y = y += spacing });
            Controls.Add(new MapButton<LostTowerWorld> { Name = "Lost Tower", Y = y += spacing });
            Controls.Add(new MapButton<IcarusWorld> { Name = "Icarus", Y = y += spacing });
            Controls.Add(new MapButton<World101World> { Name = "Uruk Mountain", Y = y += spacing });
            Controls.Add(new MapButton<StadiumWorld> { Name = "Arena", Y = y += spacing });
            Controls.Add(new MapButton<World009World> { Name = "Tarkan", Y = y += spacing });
            Controls.Add(new MapButton<World010World> { Name = "Devil Square", Y = y += spacing });
            Controls.Add(new MapButton<World031World> { Name = "Valley Of Loren", Y = y += spacing });
            Controls.Add(new MapButton<World032World> { Name = "Land Of Trials", Y = y += spacing });
            Controls.Add(new MapButton<World034World> { Name = "Aida", Y = y += spacing });
            Controls.Add(new MapButton<World035World> { Name = "Cry Wolf", Y = y += spacing });
            Controls.Add(new MapButton<World038World> { Name = "Kanturu (RUINS)", Y = y += spacing });
            Controls.Add(new MapButton<World039World> { Name = "Kanturu Remain (RELICS)", Y = y += spacing });
            Controls.Add(new MapButton<World040World> { Name = "Refine Tower", Y = y += spacing });
            Controls.Add(new MapButton<World041World> { Name = "Silent Map", Y = y += spacing });
            Controls.Add(new MapButton<World042World> { Name = "Barracks", Y = y += spacing });
            Controls.Add(new MapButton<World043World> { Name = "Refuge", Y = y += spacing });
            Controls.Add(new MapButton<World047World> { Name = "Illusion Temple", Y = y += spacing });
            Controls.Add(new MapButton<World057World> { Name = "Swamp of Peace", Y = y += spacing });
            Controls.Add(new MapButton<World058World> { Name = "Raklion", Y = y += spacing });
            Controls.Add(new MapButton<World059World> { Name = "Raklion Boss", Y = y += spacing });
            Controls.Add(new MapButton<World063World> { Name = "Santa Village", Y = y += spacing });
            Controls.Add(new MapButton<World064World> { Name = "Vulcanus", Y = y += spacing });
            Controls.Add(new MapButton<World065World> { Name = "Duel Arena", Y = y += spacing });
            Controls.Add(new MapButton<World066World> { Name = "Doppelganger Ice Zone", Y = y += spacing });
        }
    }

    public class MapButton<TWorld> : UIControl where TWorld : WalkableWorldControl, new()
    {
        public string Name { get; set; }
        public Color TextColor { get; set; } = Color.White;

        public MapButton()
        {
            Height = 20;
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
            if (Root is GameScene gameScene)
            {
                gameScene.ChangeMap<TWorld>();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            GraphicsManager.Instance.Sprite.Begin();
            GraphicsManager.Instance.Sprite.DrawString(GraphicsManager.Instance.Font, Name, new Vector2() { X = ScreenLocation.X + 5, Y = ScreenLocation.Y + 5 }, TextColor, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0);
            GraphicsManager.Instance.Sprite.End();

            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Scene.MouseControl == this)
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