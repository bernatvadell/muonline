using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Scenes;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Linq;

namespace Client.Main.Controls.UI.Game
{
    public class MainControl : DynamicLayoutControl
    {
        // Resource paths
        protected override string LayoutJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainLayout.json";
        protected override string TextureRectJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainRect.json";
        protected override string DefaultTexturePath => "Interface/GFx/main_IE.ozd";

        // UI components
        private readonly MainHPControl _hp;
        private readonly MainMPControl _mp;
        private int _hoveredHudElements;

        public MainControl(CharacterState state)
        {
            // Semi-transparent hotkeys
            foreach (var key in new[] { "ActiveSkill_1", "ActiveSkill_2", "0", "1", "2", "3", "4", "5" })
            {
                AlphaOverrides[key] = 0.7f;
            }

            // HP control
            _hp = new MainHPControl { Name = "HP_1" };
            _hp.Tag = new LayoutInfo
            {
                ScreenX = 292,
                ScreenY = 604,
                Width = 100,
                Height = 100,
                Z = -5
            };
            if (_hp is ExtendedUIControl extHp && _hp.Tag is LayoutInfo lhp)
            {
                extHp.RenderOrder = lhp.Z;
            }

            // MP control
            _mp = new MainMPControl { Name = "MP_1" };
            _mp.Tag = new LayoutInfo
            {
                ScreenX = 860,
                ScreenY = 604,
                Width = 100,
                Height = 100,
                Z = -5
            };
            if (_mp is ExtendedUIControl extMp && _mp.Tag is LayoutInfo lmp)
            {
                extMp.RenderOrder = lmp.Z;
            }

            // Initial values from CharacterState (if available)
            _hp.SetValues((int)state.CurrentHp, (int)state.MaxHp);
            _mp.SetValues((int)state.CurrentMana, (int)state.MaxMp);

            // Update when server sends changes
            state.HealthChanged += (cur, max) => _hp.SetValues((int)cur, (int)max);
            state.ManaChanged += (cur, max) => _mp.SetValues((int)cur, (int)max);

            Controls.Clear();
            Controls.Add(_mp);
            Controls.Add(_hp);

            ControlFactories["InventoryButton"] = CreateHudButton;
            ControlFactories["SettingsButton"] = CreateHudButton;

            CreateControls();
            UpdateLayout();

            if (Controls.FirstOrDefault(c => c.Name == "InventoryButton" || c.Name == "InventoryButton") is TextureControl inventoryButton)
            {
                inventoryButton.Interactive = true;
                inventoryButton.BringToFront();
                inventoryButton.Click += (_, _) =>
                {
                    Scene?.SetMouseInputConsumed();
                    var inventory = Scene?.Controls?.OfType<InventoryControl>().FirstOrDefault();
                    if (inventory == null)
                        return;

                    if (inventory.Visible)
                        inventory.Hide();
                    else
                        inventory.Show();

                    SoundController.Instance?.PlayBuffer("Sound/iButtonClick.wav");
                };
            }

            if (Controls.FirstOrDefault(c => c.Name == "SettingsButton") is TextureControl settingsButton)
            {
                settingsButton.Interactive = true;
                settingsButton.BringToFront();
                settingsButton.Click += (_, _) =>
                {
                    Scene?.SetMouseInputConsumed();
                    var pauseMenu = Scene?.Controls?.OfType<PauseMenuControl>().FirstOrDefault();
                    if (pauseMenu == null)
                        return;

                    bool show = !pauseMenu.Visible;
                    pauseMenu.Visible = show;
                    if (show)
                    {
                        pauseMenu.BringToFront();
                        if (Scene != null)
                            Scene.FocusControl = pauseMenu;
                    }
                    else if (Scene?.FocusControl == pauseMenu)
                    {
                        Scene.FocusControl = null;
                    }

                    SoundController.Instance?.PlayBuffer("Sound/iButtonClick.wav");
                };
            }
        }

        public override void Update(GameTime gameTime)
        {
            _hoveredHudElements = 0;
            base.Update(gameTime);

            if (_hoveredHudElements > 0)
            {
                IsMouseOver = true;
            }
            else if (IsMouseOver)
            {
                IsMouseOver = false;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            var sb = GraphicsManager.Instance.Sprite;

            foreach (var c in Controls.OrderBy(x => (x as ExtendedUIControl)?.RenderOrder ?? 0))
            {
                if (c is TextureControl)
                {
                    using (new SpriteBatchScope(
                           sb,
                           SpriteSortMode.Deferred,
                           BlendState.NonPremultiplied,
                           SamplerState.PointClamp,
                           transform: UiScaler.SpriteTransform))
                    {
                        c.Draw(gameTime);
                    }
                }
                else
                    c.Draw(gameTime);
            }

            using (new SpriteBatchScope(
                   sb,
                   SpriteSortMode.Deferred,
                   BlendState.AlphaBlend,
                   transform: UiScaler.SpriteTransform))
            {
                _hp.DrawLabel(gameTime);
                _mp.DrawLabel(gameTime);
            }
        }

        private UIControl CreateHudButton(LayoutInfo info)
        {
            var button = new HudTextureButton
            {
                Name = info.Name,
                AutoViewSize = false,
                TexturePath = DefaultTexturePath,
                BlendState = BlendState.NonPremultiplied
            };

            var texRect = TextureRectDatas.FirstOrDefault(t => t.Name == info.Name);
            button.TextureRectangle = texRect != null
                ? new Rectangle(texRect.X, texRect.Y, texRect.Width, texRect.Height)
                : new Rectangle(0, 0, info.Width, info.Height);

            return button;
        }

        private sealed class HudTextureButton : TextureControl
        {
            public HudTextureButton()
            {
                Interactive = true;
            }

            public override void Update(GameTime gameTime)
            {
                base.Update(gameTime);

                if (!Visible || !Interactive || Scene == null)
                    return;

                if (Parent is MainControl main && main.Scene is BaseScene scene)
                {
                    if (IsMouseOver)
                    {
                        main._hoveredHudElements++;

                        if (scene.MouseHoverControl == null || scene.MouseHoverControl == scene.World)
                        {
                            scene.MouseHoverControl = main;
                        }

                        if (scene.MouseControl == null || scene.MouseControl == scene.World)
                        {
                            scene.MouseControl = main;
                        }
                    }
                }

                var mouse = CurrentMouseState;
                var prevMouse = PreviousMouseState;

                if (IsMouseOver && mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                {
                    Scene.SetMouseInputConsumed();
                }
            }
        }
    }
}
