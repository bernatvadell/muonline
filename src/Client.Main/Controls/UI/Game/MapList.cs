using Client.Main.Models;
using Client.Main.Scenes;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game
{
    public class MapList : UIControl
    {
        public MapList()
        {
            Align = ControlAlign.Top | ControlAlign.Left;
            Margin = new Margin { Top = 10, Left = 10 };
            BackgroundColor = Color.Black * 0.6f;

            AddButtons();
        }

        public void AddButtons()
        {
            Controls.Add(new MapButton { Name = "Lorencia" }.Initialize<LorenciaWorld>());
            Controls.Add(new MapButton { Name = "Noria" }.Initialize<NoriaWorld>());
            Controls.Add(new MapButton { Name = "Elveland" }.Initialize<ElvelandWorld>());
            Controls.Add(new MapButton { Name = "Devias" }.Initialize<DeviasWorld>());
            Controls.Add(new MapButton { Name = "Dungeon" }.Initialize<DungeonWorld>());
            Controls.Add(new MapButton { Name = "Atlans" }.Initialize<AtlansWorld>());
            Controls.Add(new MapButton { Name = "Lost Tower" }.Initialize<LostTowerWorld>());
            Controls.Add(new MapButton { Name = "Icarus" }.Initialize<IcarusWorld>());
            Controls.Add(new MapButton { Name = "World 101" }.Initialize<World101World>());

            int index = 0;
            foreach (var control in Controls)
            {
                if (control is MapButton)
                {
                    control.Y = index * 25;
                    index++;
                }
            }
        }
    }

    public class MapButton : UIControl
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

        public MapButton Initialize<T>() where T : WalkableWorldControl, new()
        {
            Click += async (sender, e) =>
            {
                if (MuGame.Instance.ActiveScene is GameScene)
                {
                    await (MuGame.Instance.ActiveScene as GameScene).ChangeMapAsync<T>();
                }
            };

            return this;
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