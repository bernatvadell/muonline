using Client.Main.Models;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game
{
    public class MapListControl : UIControl
    {
        private const int InnerPadding = 10;
        private const int ButtonSpacing = 0;
        private const float ButtonFontSize = 10f;

        public MapListControl()
        {
            // Set the control alignment.
            Align = ControlAlign.Top | ControlAlign.Left;
            Margin = new Margin { Top = 10, Left = 10 };

            // Set a semi-transparent background and border.
            BackgroundColor = Color.Black * 0.6f;
            BorderColor = Color.Gray;
            BorderThickness = 1;

            // Set a fixed size for the container.
            ControlSize = new Point(220, 650);
            // Explicitly set ViewSize so the background is drawn correctly.
            ViewSize = new Point(220, 650);
            AutoViewSize = false;

            // Add map buttons.
            AddButtons();
        }

        /// <summary>
        /// Adds map buttons to the control.
        /// </summary>
        private void AddButtons()
        {
            int buttonWidth = ControlSize.X - 2 * InnerPadding;
            int buttonHeight = 20;

            Controls.Add(new MapButton<LorenciaWorld>
            {
                Name = "Lorencia",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<NoriaWorld>
            {
                Name = "Noria",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<ElvelandWorld>
            {
                Name = "Elveland",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<DeviasWorld>
            {
                Name = "Devias",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<DungeonWorld>
            {
                Name = "Dungeon",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<AtlansWorld>
            {
                Name = "Atlans",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<LostTowerWorld>
            {
                Name = "Lost Tower",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<IcarusWorld>
            {
                Name = "Icarus",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World101World>
            {
                Name = "Uruk Mountain",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<StadiumWorld>
            {
                Name = "Arena",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<TarkanWorld>
            {
                Name = "Tarkan",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<DevilSquareWorld>
            {
                Name = "Devil Square",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World031World>
            {
                Name = "Valley Of Loren",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World032World>
            {
                Name = "Land Of Trials",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World034World>
            {
                Name = "Aida",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World035World>
            {
                Name = "Cry Wolf",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World038World>
            {
                Name = "Kanturu (RUINS)",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World039World>
            {
                Name = "Kanturu Remain (RELICS)",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World040World>
            {
                Name = "Refine Tower",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World041World>
            {
                Name = "Silent Map",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World042World>
            {
                Name = "Barracks",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World043World>
            {
                Name = "Refuge",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World047World>
            {
                Name = "Illusion Temple",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World057World>
            {
                Name = "Swamp of Peace",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World058World>
            {
                Name = "Raklion",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World059World>
            {
                Name = "Raklion Boss",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World063World>
            {
                Name = "Santa Village",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World064World>
            {
                Name = "Vulcanus",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World065World>
            {
                Name = "Duel Arena",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World066World>
            {
                Name = "Doppelganger Ice Zone",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
            Controls.Add(new MapButton<World143World>
            {
                Name = "Doppelganger Ice Zone new",
                ControlSize = new Point(buttonWidth, buttonHeight),
                FontSize = ButtonFontSize
            });
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            ArrangeMapButtons();
        }

        /// <summary>
        /// Arranges the map buttons in a vertical column with the specified inner padding.
        /// </summary>
        private void ArrangeMapButtons()
        {
            int currentY = InnerPadding;

            foreach (var child in Controls)
            {
                child.X = InnerPadding;
                child.Y = currentY;
                currentY += child.ControlSize.Y + ButtonSpacing;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}