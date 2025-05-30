using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls.UI.Common;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Networking;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets; // For CharacterClassNumber
using System.Threading.Tasks;
using System.Threading;
using Client.Main.Helpers;
using System;

namespace Client.Main.Controls.UI.Game
{
    public class CharacterInfoWindowControl : UIControl
    {
        private const int WINDOW_WIDTH = 190;
        private const int WINDOW_HEIGHT = 429;

        private const int HEIGHT_STRENGTH = 120;
        private const int HEIGHT_DEXTERITY = 175;
        private const int HEIGHT_VITALITY = 240;
        private const int HEIGHT_ENERGY = 295;
        private const int HEIGHT_CHARISMA = 350; // Command/Leadership
        private const int BTN_STAT_COUNT = 5;

        private CharacterState _characterState;
        private NetworkManager _networkManager; // Changed to non-readonly to allow re-initialization
        private ILogger<CharacterInfoWindowControl> _logger;

        // UI Elements
        private TextureControl _background;
        private TextureControl _topFrame, _leftFrame, _rightFrame, _bottomFrame;
        private TextureControl[] _statTextBoxes = new TextureControl[BTN_STAT_COUNT];
        private ButtonControl[] _statButtons = new ButtonControl[BTN_STAT_COUNT];
        private ButtonControl _exitButton, _questButton, _petButton, _masterLevelButton;

        private LabelControl _nameLabel, _classLabel, _serverLabel;
        private LabelControl _levelLabel, _expLabel, _fruitPointsProbLabel, _fruitPointsStatsLabel, _statPointsLabel;
        private LabelControl[] _statNameLabels = new LabelControl[BTN_STAT_COUNT];
        private LabelControl[] _statValueLabels = new LabelControl[BTN_STAT_COUNT];

        // Labels for detailed stats (Damage, Attack Rate, etc.)
        private LabelControl _strDetail1Label, _strDetail2Label;
        private LabelControl _agiDetail1Label, _agiDetail2Label, _agiDetail3Label;
        private LabelControl _vitDetail1Label, _vitDetail2Label;
        private LabelControl _eneDetail1Label, _eneDetail2Label, _eneDetail3Label;

        // Table drawing textures
        private Texture2D _texTableTopLeft, _texTableTopRight, _texTableBottomLeft, _texTableBottomRight;
        private Texture2D _texTableTopHorizontalLinePixel, _texTableBottomHorizontalLinePixel, _texTableLeftVerticalLinePixel, _texTableRightVerticalLinePixel;

        public CharacterInfoWindowControl()
        {
            _logger = MuGame.AppLoggerFactory.CreateLogger<CharacterInfoWindowControl>();
            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize; // Use fixed size
            AutoViewSize = false;
            Interactive = true;
            Visible = false; // Start hidden
            VisibilityChanged += (s, e) => { if (Visible) OpenningProcess(); };

            // Initialize NetworkManager here. Ensure it's globally available in MuGame.
            _networkManager = MuGame.Network;
            if (_networkManager == null)
            { _logger.LogWarning("NetworkManager is null in CharacterInfoWindowControl constructor. Stat increase functionality may not work."); }
        }

        public override Task Load()
        {
            _characterState = MuGame.Network.GetCharacterState();
            _networkManager = MuGame.Network;

            // Ensure _networkManager is re-initialized in Load, in case it was null earlier.
            _networkManager = MuGame.Network;

            // Load Textures
            var tl = TextureLoader.Instance;
            _background = new TextureControl { TexturePath = "Interface/newui_msgbox_back.jpg", ViewSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT), AutoViewSize = false, BlendState = BlendState.Opaque };
            _topFrame = new TextureControl { TexturePath = "Interface/newui_item_back04.tga", ViewSize = new Point(WINDOW_WIDTH, 64), AutoViewSize = false, BlendState = BlendState.AlphaBlend }; // Should be 190x64
            _leftFrame = new TextureControl { TexturePath = "Interface/newui_item_back02-L.tga", ViewSize = new Point(21, 320), AutoViewSize = false, BlendState = BlendState.AlphaBlend };
            _rightFrame = new TextureControl { TexturePath = "Interface/newui_item_back02-R.tga", ViewSize = new Point(21, 320), AutoViewSize = false, BlendState = BlendState.AlphaBlend };
            _bottomFrame = new TextureControl { TexturePath = "Interface/newui_item_back03.tga", ViewSize = new Point(WINDOW_WIDTH, 45), AutoViewSize = false, BlendState = BlendState.AlphaBlend }; // Should be 190x45

            // Capture the current synchronization context (main thread)
            var context = SynchronizationContext.Current;
            if (context == null)
            {
                // Fallback to thread pool if there's no context (shouldn't happen in game)
                context = new SynchronizationContext();
            }

            // Start a task to load the textures in the background
            Task.Run(async () =>
            {
                // Load each texture asynchronously
                var texTableTopLeft = await tl.PrepareAndGetTexture("Interface/newui_item_table01(L).tga");
                var texTableTopRight = await tl.PrepareAndGetTexture("Interface/newui_item_table01(R).tga");
                var texTableBottomLeft = await tl.PrepareAndGetTexture("Interface/newui_item_table02(L).tga");
                var texTableBottomRight = await tl.PrepareAndGetTexture("Interface/newui_item_table02(R).tga");
                var texTableTopHorizontalLinePixel = await tl.PrepareAndGetTexture("Interface/newui_item_table03(Up).tga");
                var texTableBottomHorizontalLinePixel = await tl.PrepareAndGetTexture("Interface/newui_item_table03(Dw).tga");
                var texTableLeftVerticalLinePixel = await tl.PrepareAndGetTexture("Interface/newui_item_table03(L).tga");
                var texTableRightVerticalLinePixel = await tl.PrepareAndGetTexture("Interface/newui_item_table03(R).tga");

                // Post the results to the main thread
                context.Post(_ =>
                {
                    _texTableTopLeft = texTableTopLeft;
                    _texTableTopRight = texTableTopRight;
                    _texTableBottomLeft = texTableBottomLeft;
                    _texTableBottomRight = texTableBottomRight;
                    _texTableTopHorizontalLinePixel = texTableTopHorizontalLinePixel;
                    _texTableBottomHorizontalLinePixel = texTableBottomHorizontalLinePixel;
                    _texTableLeftVerticalLinePixel = texTableLeftVerticalLinePixel;
                    _texTableRightVerticalLinePixel = texTableRightVerticalLinePixel;
                }, null);
            });

            Controls.Add(_background);
            Controls.Add(_topFrame);
            Controls.Add(_leftFrame);
            Controls.Add(_rightFrame);
            Controls.Add(_bottomFrame);

            // Labels for top info
            _nameLabel = new LabelControl { Y = 12 - 7, TextAlign = HorizontalAlign.Center, IsBold = true, FontSize = 12f, TextColor = Color.White, ViewSize = new Point(WINDOW_WIDTH, 20), X = 0 };
            _classLabel = new LabelControl { Y = 27 - 7, TextAlign = HorizontalAlign.Center, FontSize = 11f, TextColor = Color.LightGray, ViewSize = new Point(WINDOW_WIDTH, 15), X = 0 };
            //_serverLabel = new LabelControl { Y = 27 - 7, TextAlign = HorizontalAlign.Center, FontSize = 11f, TextColor = Color.LightSkyBlue, ViewSize = new Point(WINDOW_WIDTH, 15), X = 0, Visible = false };
            Controls.Add(_nameLabel);
            Controls.Add(_classLabel);

            // Labels for Level/Exp/Points table
            _levelLabel = new LabelControl { X = 22, Y = 58 - 7, FontSize = 10f, IsBold = true, TextColor = new Color(230, 230, 0) }; // Adjusted X for padding
            _expLabel = new LabelControl { X = 22, Y = 75 - 7, FontSize = 9f, TextColor = Color.WhiteSmoke };
            _fruitPointsProbLabel = new LabelControl { X = 22, Y = 88 - 7, FontSize = 9f, TextColor = new Color(76, 197, 254) };
            _fruitPointsStatsLabel = new LabelControl { X = 22, Y = 101 - 7, FontSize = 9f, TextColor = new Color(76, 197, 254) };
            _statPointsLabel = new LabelControl { X = 110, Y = 58 - 7, FontSize = 10f, IsBold = true, TextColor = new Color(255, 138, 0) }; // Adjusted X
            Controls.Add(_levelLabel);
            Controls.Add(_expLabel);
            Controls.Add(_fruitPointsProbLabel);
            Controls.Add(_fruitPointsStatsLabel);
            Controls.Add(_statPointsLabel);

            // Stat TextBoxes and Labels
            string[] statNames = { "Strength", "Agility", "Vitality", "Energy", "Command" };
            int[] statHeights = { HEIGHT_STRENGTH, HEIGHT_DEXTERITY, HEIGHT_VITALITY, HEIGHT_ENERGY, HEIGHT_CHARISMA };
            string[] statShortNames = { "STR", "AGI", "STA", "ENE", "CMD" };

            for (int i = 0; i < BTN_STAT_COUNT; i++)
            {
                _statTextBoxes[i] = new TextureControl { TexturePath = "Interface/newui_cha_textbox02.tga", ViewSize = new Point(170, 21), AutoViewSize = false, BlendState = BlendState.AlphaBlend };
                Controls.Add(_statTextBoxes[i]);

                _statNameLabels[i] = new LabelControl { X = 0, Y = statHeights[i], FontSize = 10f, IsBold = true, TextColor = new Color(230, 230, 0) }; // Adjusted Y for C++ like padding
                _statNameLabels[i].Text = statShortNames[i];
                Controls.Add(_statNameLabels[i]);

                _statButtons[i] = new ButtonControl
                {
                    TexturePath = "Interface/newui_chainfo_btn_level.tga",
                    TileWidth = 16,
                    TileHeight = 15,
                    BlendState = BlendState.AlphaBlend
                };

                _statValueLabels[i] = new LabelControl { X = 0, Y = statHeights[i], FontSize = 10f, IsBold = true, TextColor = new Color(230, 230, 0) }; // Adjusted Y
                Controls.Add(_statValueLabels[i]);

                int statIndex = i;
                _statButtons[i].Click += (s, e) => OnStatButtonClicked(statIndex);
                Controls.Add(_statButtons[i]);
            }

            // Initialize Detailed Stat Labels (set to Visible = false by default)
            float detailFontSize = 9f; Color detailColor = Color.LightGray;
            _strDetail1Label = new LabelControl { X = 20, Y = HEIGHT_STRENGTH + 25, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _strDetail2Label = new LabelControl { X = 20, Y = HEIGHT_STRENGTH + 25 + 13, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            Controls.Add(_strDetail1Label); Controls.Add(_strDetail2Label);

            _agiDetail1Label = new LabelControl { X = 20, Y = HEIGHT_DEXTERITY + 24, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _agiDetail2Label = new LabelControl { X = 20, Y = HEIGHT_DEXTERITY + 24 + 13, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _agiDetail3Label = new LabelControl { X = 20, Y = HEIGHT_DEXTERITY + 24 + 13 + 13, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            Controls.Add(_agiDetail1Label); Controls.Add(_agiDetail2Label); Controls.Add(_agiDetail3Label);

            _vitDetail1Label = new LabelControl { X = 20, Y = HEIGHT_VITALITY + 24, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _vitDetail2Label = new LabelControl { X = 20, Y = HEIGHT_VITALITY + 24 + 13, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            Controls.Add(_vitDetail1Label); Controls.Add(_vitDetail2Label);

            _eneDetail1Label = new LabelControl { X = 20, Y = HEIGHT_ENERGY + 24, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _eneDetail2Label = new LabelControl { X = 20, Y = HEIGHT_ENERGY + 24 + 13, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _eneDetail3Label = new LabelControl { X = 20, Y = HEIGHT_ENERGY + 24 + 13 + 13, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            Controls.Add(_eneDetail1Label); Controls.Add(_eneDetail2Label); Controls.Add(_eneDetail3Label);

            _exitButton = CreateBottomButton(13, "Interface/newui_exit_00.tga", "Close (C)");
            _exitButton.Click += (s, e) => { Visible = false; SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); };

            _questButton = CreateBottomButton(50, "Interface/newui_chainfo_btn_quest.tga", "Quest (T)");
            _questButton.Click += (s, e) => { _logger.LogInformation("Quest button clicked."); SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); };

            _petButton = CreateBottomButton(87, "Interface/newui_chainfo_btn_pet.tga", "Pet");
            _petButton.Click += (s, e) => { _logger.LogInformation("Pet button clicked."); SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); };

            _masterLevelButton = CreateBottomButton(124, "Interface/newui_chainfo_btn_master.tga", "Master Level");
            _masterLevelButton.Click += (s, e) => { _logger.LogInformation("Master Level button clicked."); SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); };

            Controls.Add(_exitButton);
            Controls.Add(_questButton);
            Controls.Add(_petButton);
            Controls.Add(_masterLevelButton);

            SetupLayout();
            return base.Load();
        }

        private ButtonControl CreateBottomButton(int xOffset, string texturePath, string tooltip)
        {
            var button = new ButtonControl
            {
                X = xOffset,
                Y = 392,
                TexturePath = texturePath,
                TileWidth = 36,
                TileHeight = 29,
                BlendState = BlendState.AlphaBlend,
                AutoViewSize = false,
                ViewSize = new Point(36, 29)
            };
            return button;
        }

        private void SetupLayout()
        {
            _background.X = 0; _background.Y = 0;
            _topFrame.X = 0; _topFrame.Y = 0;
            _topFrame.ViewSize = new Point(WINDOW_WIDTH + 70, _topFrame.ViewSize.Y); // Ensure full width
            _leftFrame.X = 0; _leftFrame.Y = 64;
            _leftFrame.ViewSize = new Point(_leftFrame.ViewSize.X, WINDOW_HEIGHT + 110);
            _rightFrame.X = WINDOW_WIDTH - 11; _rightFrame.Y = 64;
            _rightFrame.ViewSize = new Point(_rightFrame.ViewSize.X, WINDOW_HEIGHT + 110);
            _bottomFrame.X = 0; _bottomFrame.Y = WINDOW_HEIGHT - (_bottomFrame.ViewSize.Y + 20) / 2;
            _bottomFrame.ViewSize = new Point(WINDOW_WIDTH + 70, _bottomFrame.ViewSize.Y); // Ensure full width

            //_leftFrame.ViewSize = new Point(_leftFrame.ViewSize.X, WINDOW_HEIGHT - _topFrame.ViewSize.Y - _bottomFrame.ViewSize.Y);
            //_rightFrame.ViewSize = new Point(_rightFrame.ViewSize.X, WINDOW_HEIGHT - _topFrame.ViewSize.Y - _bottomFrame.ViewSize.Y);

            _nameLabel.X = 0; _nameLabel.ViewSize = new Point(WINDOW_WIDTH, 20);
            _classLabel.X = 0; _classLabel.ViewSize = new Point(WINDOW_WIDTH, 15);
            // _serverLabel.X = 0; _serverLabel.ViewSize = new Point(WINDOW_WIDTH, 15);

            int[] statHeights = { HEIGHT_STRENGTH, HEIGHT_DEXTERITY, HEIGHT_VITALITY, HEIGHT_ENERGY, HEIGHT_CHARISMA };
            for (int i = 0; i < BTN_STAT_COUNT; i++)
            {
                _statTextBoxes[i].X = (WINDOW_WIDTH - _statTextBoxes[i].ViewSize.X) / 2; // Centered textbox
                _statTextBoxes[i].Y = statHeights[i];

                _statNameLabels[i].X = 12; // Relative to window's left edge
                _statNameLabels[i].ViewSize = new Point(74, 15); // Width for centering text
                _statNameLabels[i].TextAlign = HorizontalAlign.Center; // Center text within this 74px box

                _statValueLabels[i].X = 86; // Relative to window's left edge
                _statValueLabels[i].ViewSize = new Point((160 - 3) - 86, 15); // Width for value text area, ending before button
                _statValueLabels[i].TextAlign = HorizontalAlign.Right; // Right-align value text in its box
                _statButtons[i].X = 160; // Absolute X for button
                _statButtons[i].Y = statHeights[i] + 2;
                _statButtons[i].ViewSize = new Point(16, 15);
            }
        }

        private void OnStatButtonClicked(int statIndex)
        {
            _logger.LogInformation($"Stat button {statIndex} clicked.");
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); // Play sound on click.

            // Check network connection and game state
            if (_networkManager == null || !_networkManager.IsConnected || _networkManager.CurrentState < ClientConnectionState.InGame)
            {
                _logger.LogWarning("Cannot increase stat: Not connected to game server or invalid state.");
                MessageWindow.Show("Cannot add stat points: Not connected to server or not in game.");
                return;
            }

            // Map stat index to CharacterStatAttribute enum
            CharacterStatAttribute attributeToSend = statIndex switch
            {
                0 => CharacterStatAttribute.Strength,
                1 => CharacterStatAttribute.Agility,
                2 => CharacterStatAttribute.Vitality,
                3 => CharacterStatAttribute.Energy,
                4 => CharacterStatAttribute.Leadership,
                _ => throw new ArgumentOutOfRangeException(nameof(statIndex), $"Invalid stat index: {statIndex}")
            };

            // Client-side validation: Check for available points
            if (_characterState.LevelUpPoints <= 0)
            {
                _logger.LogInformation("No available points to distribute for {Attribute}.", attributeToSend);
                MessageWindow.Show("No available stat points.");
                return;
            }

            // Specific validation for Leadership (only for Dark Lord)
            if (attributeToSend == CharacterStatAttribute.Leadership && !(_characterState.Class == CharacterClassNumber.DarkLord || _characterState.Class == CharacterClassNumber.LordEmperor))
            {
                _logger.LogInformation("Cannot add Leadership points for non-Dark Lord character.");
                MessageWindow.Show("Only Dark Lords can add Leadership points.");
                return;
            }

            // Send request to server
            var characterService = _networkManager.GetCharacterService();
            if (characterService != null)
            {
                _ = characterService.SendIncreaseCharacterStatPointRequestAsync(attributeToSend);
                _logger.LogInformation("Sent request to add point to {Attribute}.", attributeToSend);
            }
            else
            {
                _logger.LogError("CharacterService is null. Cannot send stat increase request.");
                MessageWindow.Show("Internal error: Could not add points.");
            }
        }

        private void OpenningProcess()
        {
            if (_characterState == null) _characterState = MuGame.Network.GetCharacterState();
            UpdateDisplayData();
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;
            base.Update(gameTime);

            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) && !MuGame.Instance.PrevKeyboard.IsKeyDown(Keys.Escape))
            {
                Visible = false;
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                return;
            }

            UpdateDisplayData();
        }

        private void UpdateDisplayData()
        {
            if (_characterState == null) return;

            // Centering logic for name and class labels
            _nameLabel.ViewSize = new Point(WINDOW_WIDTH, _nameLabel.ControlSize.Y > 0 ? _nameLabel.ControlSize.Y : 20); // Update ViewSize based on current text height
            _classLabel.ViewSize = new Point(WINDOW_WIDTH, _classLabel.ControlSize.Y > 0 ? _classLabel.ControlSize.Y : 15);
            _nameLabel.Text = _characterState.Name;
            _classLabel.Text = $"({CharacterClassDatabase.GetClassName(_characterState.Class)})";

            if (_characterState.MasterLevel > 0)
            {
                _levelLabel.Text = $"Master Lv. {_characterState.MasterLevel}";
                _expLabel.Text = "----------";
                _statPointsLabel.Text = _characterState.MasterLevelUpPoints > 0 ? $"Points: {_characterState.MasterLevelUpPoints}" : "";
            }
            else
            {
                _levelLabel.Text = $"Level: {_characterState.Level}";
                _expLabel.Text = $"Exp: {_characterState.Experience} / {_characterState.ExperienceForNextLevel}";
                _statPointsLabel.Text = $"Points: {_characterState.LevelUpPoints}";
                _statPointsLabel.Visible = true;
            }
            _fruitPointsProbLabel.Text = "[+]100%|[-]100%";
            _fruitPointsStatsLabel.Text = "Create 0/0 | Decrease 0/0";

            _statValueLabels[0].Text = _characterState.Strength.ToString();
            SetStatLabelColor(_statValueLabels[0], _characterState.Strength, 0);
            _strDetail1Label.Text = $"Strength: {_characterState.Strength}";
            _strDetail1Label.Visible = true; // Make visible
            _strDetail2Label.Text = string.Empty;
            _strDetail2Label.Visible = false; // Hide if empty

            _statValueLabels[1].Text = _characterState.Agility.ToString();
            SetStatLabelColor(_statValueLabels[1], _characterState.Agility, 0);
            _agiDetail1Label.Text = $"Agility: {_characterState.Agility}";
            _agiDetail1Label.Visible = true;
            _agiDetail2Label.Text = string.Empty;
            _agiDetail2Label.Visible = false;
            _agiDetail3Label.Text = string.Empty;
            _agiDetail3Label.Visible = false;

            _statValueLabels[2].Text = _characterState.Vitality.ToString();
            SetStatLabelColor(_statValueLabels[2], _characterState.Vitality, 0);
            _vitDetail1Label.Text = $"HP: {_characterState.CurrentHealth}/{_characterState.MaximumHealth}";
            _vitDetail1Label.Visible = true;
            _vitDetail2Label.Text = $"SD: {_characterState.CurrentShield}/{_characterState.MaximumShield}";
            _vitDetail2Label.Visible = true;

            _statValueLabels[3].Text = _characterState.Energy.ToString();
            SetStatLabelColor(_statValueLabels[3], _characterState.Energy, 0);
            _eneDetail1Label.Text = $"Mana: {_characterState.CurrentMana}/{_characterState.MaximumMana}";
            _eneDetail1Label.Visible = true;
            _eneDetail2Label.Text = $"AG: {_characterState.CurrentAbility}/{_characterState.MaximumAbility}";
            _eneDetail2Label.Visible = true;
            _eneDetail3Label.Text = string.Empty;
            _eneDetail3Label.Visible = false;

            bool isDarkLordFamily = false;
            if (_characterState.Class == CharacterClassNumber.DarkLord ||
                _characterState.Class == CharacterClassNumber.LordEmperor)
            {
                isDarkLordFamily = true;
            }

            bool showCharisma = isDarkLordFamily;

            _statTextBoxes[4].Visible = isDarkLordFamily;
            _statNameLabels[4].Visible = isDarkLordFamily;
            _statValueLabels[4].Visible = isDarkLordFamily;
            _statButtons[4].Visible = isDarkLordFamily && _characterState.LevelUpPoints > 0;

            for (int i = 0; i < 4; i++)
            {
                _statButtons[i].Visible = _characterState.LevelUpPoints > 0;
            }

            bool canBeMaster = _characterState.Class != CharacterClassNumber.DarkWizard;
            _masterLevelButton.Visible = canBeMaster && _characterState.MasterLevel > 0;
            _masterLevelButton.Enabled = canBeMaster && _characterState.MasterLevel > 0;
        }

        private void SetStatLabelColor(LabelControl label, ushort statValue, ushort addedStatValue)
        {
            if (addedStatValue > 0)
            {
                label.TextColor = new Color(100, 150, 255);
            }
            else
            {
                label.TextColor = new Color(230, 230, 0);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Status != GameControlStatus.Ready) return;

            base.Draw(gameTime); // Base draw will handle all children including frames
            // Manually draw the info table on top of the background, but under text labels
            using (new SpriteBatchScope(GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {
                DrawInfoTable(GraphicsManager.Instance.Sprite);
            }

            foreach (var child in Controls)
            {
                if (child != _background && child != _topFrame && child != _leftFrame && child != _rightFrame && child != _bottomFrame)
                {
                    child.Draw(gameTime);
                }
            }
        }

        private void DrawInfoTable(SpriteBatch spriteBatch)
        {
            var sb = spriteBatch;

            int tableX = this.DisplayRectangle.X + 12; // Use CharacterInfoWindowControl's display position
            int tableY = this.DisplayRectangle.Y + 48;
            int tableWidth = 165;
            int tableHeight = 66;

            Color tableBgColor = Color.Black * 0.3f * Alpha;
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(tableX, tableY, tableWidth, tableHeight), tableBgColor);

            int cornerSize = 14;

            if (_texTableTopLeft != null) sb.Draw(_texTableTopLeft, new Rectangle(tableX, tableY, cornerSize, cornerSize), Color.White);
            if (_texTableTopRight != null) sb.Draw(_texTableTopRight, new Rectangle(tableX + tableWidth - cornerSize, tableY, cornerSize, cornerSize), Color.White);
            if (_texTableBottomLeft != null) sb.Draw(_texTableBottomLeft, new Rectangle(tableX, tableY + tableHeight - cornerSize, cornerSize, cornerSize), Color.White);
            if (_texTableBottomRight != null) sb.Draw(_texTableBottomRight, new Rectangle(tableX + tableWidth - cornerSize, tableY + tableHeight - cornerSize, cornerSize, cornerSize), Color.White);

            // Horizontal lines (Top and Bottom Edges of the content area)
            if (_texTableTopHorizontalLinePixel != null && _texTableTopHorizontalLinePixel.Height > 0)
            {
                for (int x = tableX + cornerSize; x < tableX + tableWidth - cornerSize; x++)
                { // Top border line
                    sb.Draw(_texTableTopHorizontalLinePixel, new Rectangle(x, tableY, 1, _texTableTopHorizontalLinePixel.Height), Color.White);
                }
            }
            if (_texTableBottomHorizontalLinePixel != null && _texTableBottomHorizontalLinePixel.Height > 0) // Bottom border line
            {
                for (int x = tableX + cornerSize; x < tableX + tableWidth - cornerSize; x++)
                {
                    sb.Draw(_texTableBottomHorizontalLinePixel, new Rectangle(x, tableY + tableHeight - _texTableBottomHorizontalLinePixel.Height, 1, _texTableBottomHorizontalLinePixel.Height), Color.White);
                }
            }

            if (_texTableLeftVerticalLinePixel != null && _texTableLeftVerticalLinePixel.Width > 0) // Left border line
            {
                for (int y = tableY + cornerSize; y < tableY + tableHeight - cornerSize; y++)
                {
                    sb.Draw(_texTableLeftVerticalLinePixel, new Rectangle(tableX, y, _texTableLeftVerticalLinePixel.Width, 1), Color.White);
                }
            }
            if (_texTableRightVerticalLinePixel != null && _texTableRightVerticalLinePixel.Width > 0) // Right border line
            {
                for (int y = tableY + cornerSize; y < tableY + tableHeight - cornerSize; y++)
                {
                    sb.Draw(_texTableRightVerticalLinePixel, new Rectangle(tableX + tableWidth - _texTableRightVerticalLinePixel.Width, y, _texTableRightVerticalLinePixel.Width, 1), Color.White);
                }
            }
        }

        public void ShowWindow()
        {
            Visible = true;
            OpenningProcess();
            BringToFront();
            if (this.Scene != null)
            {
                this.Scene.FocusControl = this;
            }
        }

        public void HideWindow()
        {
            Visible = false;
            if (this.Scene != null && this.Scene.FocusControl == this)
            {
                this.Scene.FocusControl = null;
            }
        }
    }
}
