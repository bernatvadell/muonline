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
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Client.Main.Helpers;
using System;
using Client.Main.Controls.UI;

namespace Client.Main.Controls.UI.Game
{
    public class CharacterInfoWindowControl : UIControl, IUiTexturePreloadable
    {
        private const int WINDOW_WIDTH = 280;
        private const int WINDOW_HEIGHT = 520;

        private const int HEIGHT_STRENGTH = 140;
        private const int HEIGHT_DEXTERITY = 200;
        private const int HEIGHT_VITALITY = 270;
        private const int HEIGHT_ENERGY = 340;
        private const int HEIGHT_CHARISMA = 410; // Command/Leadership
        private const int BTN_STAT_COUNT = 5;

        private static readonly string[] s_tableTexturePaths =
        {
            "Interface/newui_item_table01(L).tga",
            "Interface/newui_item_table01(R).tga",
            "Interface/newui_item_table02(L).tga",
            "Interface/newui_item_table02(R).tga",
            "Interface/newui_item_table03(Up).tga",
            "Interface/newui_item_table03(Dw).tga",
            "Interface/newui_item_table03(L).tga",
            "Interface/newui_item_table03(R).tga"
        };

        private static readonly string[] s_additionalPreloadTextures =
        {
            "Interface/newui_msgbox_back.jpg",
            "Interface/newui_item_back04.tga",
            "Interface/newui_item_back02-L.tga",
            "Interface/newui_item_back02-R.tga",
            "Interface/newui_item_back03.tga",
            "Interface/newui_cha_textbox02.tga",
            "Interface/newui_chainfo_btn_level.tga",
            "Interface/newui_exit_00.tga",
            "Interface/newui_chainfo_btn_quest.tga",
            "Interface/newui_chainfo_btn_pet.tga",
            "Interface/newui_chainfo_btn_master.tga"
        };

        private CharacterState _characterState;
        private NetworkManager _networkManager; // Changed to non-readonly to allow re-initialization
        private ILogger<CharacterInfoWindowControl> _logger;

        // UI Elements
        private TextureControl _background;
        private TextureControl _topFrame, _leftFrame, _rightFrame, _bottomFrame;
        private TextureControl[] _statTextBoxes = new TextureControl[BTN_STAT_COUNT];
        private ButtonControl[] _statButtons = new ButtonControl[BTN_STAT_COUNT];
        private ButtonControl _exitButton, _questButton, _petButton, _masterLevelButton;

        private LabelControl _nameLabel, _classLabel;
        private LabelControl _levelLabel, _expLabel, _fruitPointsProbLabel, _fruitPointsStatsLabel, _statPointsLabel;
        private LabelControl[] _statNameLabels = new LabelControl[BTN_STAT_COUNT];
        private LabelControl[] _statValueLabels = new LabelControl[BTN_STAT_COUNT];

        // Labels for detailed stats (Damage, Attack Rate, etc.)
        private LabelControl _strDetail1Label, _strDetail2Label;
        private LabelControl _agiDetail1Label, _agiDetail2Label, _agiDetail3Label;
        private LabelControl _vitDetail1Label, _vitDetail2Label;
        private LabelControl _eneDetail1Label, _eneDetail2Label, _eneDetail3Label;

        // Additional info labels for PvM rates
        private LabelControl _pvmInfoLabel1, _pvmInfoLabel2;

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
            // VisibilityChanged += (s, e) => { if (Visible) OpenningProcess(); }; // Removed: event does not exist

            // Initialize NetworkManager here. Ensure it's globally available in MuGame.
            _networkManager = MuGame.Network;
            if (_networkManager == null)
            { _logger.LogWarning("NetworkManager is null in CharacterInfoWindowControl constructor. Stat increase functionality may not work."); }
        }

        public IEnumerable<string> GetPreloadTexturePaths()
        {
            foreach (var path in s_additionalPreloadTextures)
            {
                yield return path;
            }

            foreach (var path in s_tableTexturePaths)
            {
                yield return path;
            }
        }

        public override async Task Load()
        {
            _characterState = MuGame.Network.GetCharacterState();
            _networkManager = MuGame.Network;

            // Ensure _networkManager is re-initialized in Load, in case it was null earlier.
            _networkManager = MuGame.Network;

            // Load Textures - scale frames with extra margins to fully cover the larger window
            var tl = TextureLoader.Instance;
            _background = new TextureControl { TexturePath = "Interface/newui_msgbox_back.jpg", ViewSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT), AutoViewSize = false, BlendState = BlendState.Opaque };
            _topFrame = new TextureControl { TexturePath = "Interface/newui_item_back04.tga", ViewSize = new Point(WINDOW_WIDTH + 97, 74), AutoViewSize = false, BlendState = BlendState.AlphaBlend };
            _leftFrame = new TextureControl { TexturePath = "Interface/newui_item_back02-L.tga", ViewSize = new Point(26, WINDOW_HEIGHT + 250), AutoViewSize = false, BlendState = BlendState.AlphaBlend };
            _rightFrame = new TextureControl { TexturePath = "Interface/newui_item_back02-R.tga", ViewSize = new Point(26, WINDOW_HEIGHT + 250), AutoViewSize = false, BlendState = BlendState.AlphaBlend };
            _bottomFrame = new TextureControl { TexturePath = "Interface/newui_item_back03.tga", ViewSize = new Point(WINDOW_WIDTH + 97, 55), AutoViewSize = false, BlendState = BlendState.AlphaBlend };

            // Load all textures in parallel to avoid blocking main thread
            var tableTextureTasks = new List<Task<Texture2D>>(s_tableTexturePaths.Length);
            foreach (var texturePath in s_tableTexturePaths)
            {
                tableTextureTasks.Add(tl.PrepareAndGetTexture(texturePath));
            }

            var loadedTextures = await Task.WhenAll(tableTextureTasks);

            _texTableTopLeft = loadedTextures[0];
            _texTableTopRight = loadedTextures[1];
            _texTableBottomLeft = loadedTextures[2];
            _texTableBottomRight = loadedTextures[3];
            _texTableTopHorizontalLinePixel = loadedTextures[4];
            _texTableBottomHorizontalLinePixel = loadedTextures[5];
            _texTableLeftVerticalLinePixel = loadedTextures[6];
            _texTableRightVerticalLinePixel = loadedTextures[7];

            Controls.Add(_background);
            Controls.Add(_topFrame);
            Controls.Add(_leftFrame);
            Controls.Add(_rightFrame);
            Controls.Add(_bottomFrame);

            // Labels for top info - properly centered for larger window
            _nameLabel = new LabelControl { Y = 5, TextAlign = HorizontalAlign.Center, IsBold = true, FontSize = 13f, TextColor = Color.White, ViewSize = new Point(WINDOW_WIDTH, 22), X = 0 };
            _classLabel = new LabelControl { Y = 23, TextAlign = HorizontalAlign.Center, FontSize = 12f, TextColor = Color.LightGray, ViewSize = new Point(WINDOW_WIDTH, 18), X = 0 };
            Controls.Add(_nameLabel);
            Controls.Add(_classLabel);

            // Labels for Level/Exp/Points table - repositioned for larger window
            _levelLabel = new LabelControl { X = 28, Y = 60, FontSize = 11f, IsBold = true, TextColor = new Color(230, 230, 0) };
            _expLabel = new LabelControl { X = 28, Y = 78, FontSize = 10f, TextColor = Color.WhiteSmoke };
            _fruitPointsProbLabel = new LabelControl { X = 28, Y = 96, FontSize = 10f, TextColor = new Color(76, 197, 254) };
            _fruitPointsStatsLabel = new LabelControl { X = 28, Y = 114, FontSize = 10f, TextColor = new Color(76, 197, 254) };
            _statPointsLabel = new LabelControl { X = 155, Y = 60, FontSize = 11f, IsBold = true, TextColor = new Color(255, 138, 0) };
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
                    BlendState = BlendState.AlphaBlend,
                    AutoViewSize = false,
                    ViewSize = new Point(16, 15) // Match TileWidth and TileHeight exactly
                };

                _statValueLabels[i] = new LabelControl { X = 0, Y = statHeights[i], FontSize = 10f, IsBold = true, TextColor = new Color(230, 230, 0) }; // Adjusted Y
                Controls.Add(_statValueLabels[i]);

                int statIndex = i;
                _statButtons[i].Click += (s, e) => OnStatButtonClicked(statIndex);
                Controls.Add(_statButtons[i]);
            }

            // Initialize Detailed Stat Labels (set to Visible = false by default) - with better spacing for larger window
            float detailFontSize = 10f; Color detailColor = Color.LightGray;
            _strDetail1Label = new LabelControl { X = 25, Y = HEIGHT_STRENGTH + 30 / 2, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _strDetail2Label = new LabelControl { X = 25, Y = HEIGHT_STRENGTH + 30 / 2 + 16, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            Controls.Add(_strDetail1Label); Controls.Add(_strDetail2Label);

            _agiDetail1Label = new LabelControl { X = 25, Y = HEIGHT_DEXTERITY + 30 / 2, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _agiDetail2Label = new LabelControl { X = 25, Y = HEIGHT_DEXTERITY + 30 / 2 + 16, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _agiDetail3Label = new LabelControl { X = 25, Y = HEIGHT_DEXTERITY + 30 / 2 + 16 + 16, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            Controls.Add(_agiDetail1Label); Controls.Add(_agiDetail2Label); Controls.Add(_agiDetail3Label);

            _vitDetail1Label = new LabelControl { X = 25, Y = HEIGHT_VITALITY + 30 / 2, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _vitDetail2Label = new LabelControl { X = 25, Y = HEIGHT_VITALITY + 30 / 2 + 16, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            Controls.Add(_vitDetail1Label); Controls.Add(_vitDetail2Label);

            _eneDetail1Label = new LabelControl { X = 25, Y = HEIGHT_ENERGY + 30 / 2, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _eneDetail2Label = new LabelControl { X = 25, Y = HEIGHT_ENERGY + 30 / 2 + 16, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            _eneDetail3Label = new LabelControl { X = 25, Y = HEIGHT_ENERGY + 30 / 2 + 16 + 16, FontSize = detailFontSize, TextColor = detailColor, Visible = false };
            Controls.Add(_eneDetail1Label); Controls.Add(_eneDetail2Label); Controls.Add(_eneDetail3Label);

            // PvM Info Labels (below stats) - with better spacing
            _pvmInfoLabel1 = new LabelControl { X = 18, Y = HEIGHT_CHARISMA + 40 / 2, FontSize = detailFontSize, TextColor = new Color(255, 200, 100), Visible = false };
            _pvmInfoLabel2 = new LabelControl { X = 18, Y = HEIGHT_CHARISMA + 40 / 2 + 18, FontSize = detailFontSize, TextColor = new Color(255, 200, 100), Visible = false };
            Controls.Add(_pvmInfoLabel1); Controls.Add(_pvmInfoLabel2);

            _exitButton = CreateBottomButton(20, "Interface/newui_exit_00.tga", "Close (C)");
            _exitButton.Click += (s, e) => { Visible = false; SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); };

            _questButton = CreateBottomButton(70, "Interface/newui_chainfo_btn_quest.tga", "Quest (T)");
            _questButton.Click += (s, e) => { _logger.LogInformation("Quest button clicked."); SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); };

            _petButton = CreateBottomButton(120, "Interface/newui_chainfo_btn_pet.tga", "Pet");
            _petButton.Click += (s, e) => { _logger.LogInformation("Pet button clicked."); SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); };

            _masterLevelButton = CreateBottomButton(170, "Interface/newui_chainfo_btn_master.tga", "Master Level");
            _masterLevelButton.Click += (s, e) => { _logger.LogInformation("Master Level button clicked."); SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav"); };

            Controls.Add(_exitButton);
            Controls.Add(_questButton);
            Controls.Add(_petButton);
            Controls.Add(_masterLevelButton);

            SetupLayout();

            await base.Load();
        }

        private ButtonControl CreateBottomButton(int xOffset, string texturePath, string tooltip)
        {
            var button = new ButtonControl
            {
                X = xOffset,
                Y = WINDOW_HEIGHT - 40, // Position near bottom of new window
                TexturePath = texturePath,
                TileWidth = 36,
                TileHeight = 29,
                BlendState = BlendState.AlphaBlend,
                AutoViewSize = false,
                ViewSize = new Point(36, 29) // Match TileWidth and TileHeight exactly
            };
            return button;
        }

        private void SetupLayout()
        {
            // Background and frames positioning - avoid negative positions
            _background.X = 0; _background.Y = 0;

            // Top frame: start from left edge of window, full width coverage
            _topFrame.X = 0; _topFrame.Y = 0;

            // Left frame: start from top edge of window
            _leftFrame.X = 0; _leftFrame.Y = 0;

            // Right frame: position at right edge
            _rightFrame.X = WINDOW_WIDTH - 17; _rightFrame.Y = 0;

            // Bottom frame: start from left edge, position at bottom
            _bottomFrame.X = 0; _bottomFrame.Y = WINDOW_HEIGHT - 40;

            // Top info labels - ensure full width for centering
            _nameLabel.X = 90;
            _nameLabel.ViewSize = new Point(WINDOW_WIDTH, 22);
            // _nameLabel.TextAlign = HorizontalAlign.Center; // Ensure centering is maintained

            _classLabel.X = 90;
            _classLabel.ViewSize = new Point(WINDOW_WIDTH, 18);
            // _classLabel.TextAlign = HorizontalAlign.Center; // Ensure centering is maintained

            // Stat layout with better spacing for larger window
            int[] statHeights = { HEIGHT_STRENGTH, HEIGHT_DEXTERITY, HEIGHT_VITALITY, HEIGHT_ENERGY, HEIGHT_CHARISMA };
            for (int i = 0; i < BTN_STAT_COUNT; i++)
            {
                // Center the 170px wide textboxes in the new window
                _statTextBoxes[i].X = (WINDOW_WIDTH - 170) / 2 - 40;
                _statTextBoxes[i].Y = statHeights[i];

                _statNameLabels[i].X = 25; // More space from left edge
                _statNameLabels[i].ViewSize = new Point(85, 18); // Wider area for stat names
                _statNameLabels[i].TextAlign = HorizontalAlign.Center;

                _statValueLabels[i].X = 90; // Position for values
                _statValueLabels[i].ViewSize = new Point(120, 18); // Wider area for values including bonuses
                _statValueLabels[i].TextAlign = HorizontalAlign.Right;

                _statButtons[i].X = WINDOW_WIDTH - 70; // Position near right edge with original size
                _statButtons[i].Y = statHeights[i] + 3;
                _statButtons[i].ViewSize = new Point(16, 15); // Match texture size exactly
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

            // Centering logic for name and class labels - ensure proper centering
            _nameLabel.X = 90;
            _nameLabel.ViewSize = new Point(WINDOW_WIDTH, _nameLabel.ControlSize.Y > 0 ? _nameLabel.ControlSize.Y : 22);
            _nameLabel.TextAlign = HorizontalAlign.Center;

            _classLabel.X = 90;
            _classLabel.ViewSize = new Point(WINDOW_WIDTH, _classLabel.ControlSize.Y > 0 ? _classLabel.ControlSize.Y : 18);
            _classLabel.TextAlign = HorizontalAlign.Center;

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

            // Show total stats vs base stats when there are item bonuses
            var totalStr = _characterState.TotalStrength;
            var baseStr = _characterState.Strength;
            _statValueLabels[0].Text = totalStr > baseStr ? $"{baseStr}+{totalStr - baseStr}" : baseStr.ToString();
            SetStatLabelColor(_statValueLabels[0], baseStr, (ushort)(totalStr - baseStr));

            // Physical damage for Strength-based classes
            var physDmg = GetPhysicalDamage();
            if (physDmg.min > 0 || physDmg.max > 0)
            {
                _strDetail1Label.Text = $"Attack Damage: {physDmg.min}~{physDmg.max}";
                _strDetail1Label.Visible = true;
                _strDetail2Label.Text = $"Attack Speed: +{GetAttackSpeed()}";
                _strDetail2Label.Visible = true;
            }
            else
            {
                _strDetail1Label.Text = $"Strength: {_characterState.TotalStrength}";
                _strDetail1Label.Visible = true;
                _strDetail2Label.Text = string.Empty;
                _strDetail2Label.Visible = false;
            }

            var totalAgi = _characterState.TotalAgility;
            var baseAgi = _characterState.Agility;
            _statValueLabels[1].Text = totalAgi > baseAgi ? $"{baseAgi}+{totalAgi - baseAgi}" : baseAgi.ToString();
            SetStatLabelColor(_statValueLabels[1], baseAgi, (ushort)(totalAgi - baseAgi));

            var defense = GetDefense();
            var pvpAttackRate = GetPvPAttackRate();
            var pvpDefenseRate = GetPvPDefenseRate();

            _agiDetail1Label.Text = $"Defense: +{defense}";
            _agiDetail1Label.Visible = true;
            _agiDetail2Label.Text = $"Attack Rate: {pvpAttackRate}%";
            _agiDetail2Label.Visible = true;
            _agiDetail3Label.Text = $"Defense Rate: {pvpDefenseRate}%";
            _agiDetail3Label.Visible = true;

            var totalVit = _characterState.TotalVitality;
            var baseVit = _characterState.Vitality;
            _statValueLabels[2].Text = totalVit > baseVit ? $"{baseVit}+{totalVit - baseVit}" : baseVit.ToString();
            SetStatLabelColor(_statValueLabels[2], baseVit, (ushort)(totalVit - baseVit));
            _vitDetail1Label.Text = $"HP: {_characterState.CurrentHealth}/{_characterState.MaximumHealth}";
            _vitDetail1Label.Visible = true;
            _vitDetail2Label.Text = $"SD: {_characterState.CurrentShield}/{_characterState.MaximumShield}";
            _vitDetail2Label.Visible = true;

            var totalEne = _characterState.TotalEnergy;
            var baseEne = _characterState.Energy;
            _statValueLabels[3].Text = totalEne > baseEne ? $"{baseEne}+{totalEne - baseEne}" : baseEne.ToString();
            SetStatLabelColor(_statValueLabels[3], baseEne, (ushort)(totalEne - baseEne));

            var magDmg = GetMagicalDamage();
            if (magDmg.min > 0 || magDmg.max > 0)
            {
                _eneDetail1Label.Text = $"Wizardry Damage: {magDmg.min}~{magDmg.max}";
                _eneDetail1Label.Visible = true;
                _eneDetail2Label.Text = $"Mana: {_characterState.CurrentMana}/{_characterState.MaximumMana}";
                _eneDetail2Label.Visible = true;
                _eneDetail3Label.Text = $"AG: {_characterState.CurrentAbility}/{_characterState.MaximumAbility}";
                _eneDetail3Label.Visible = true;
            }
            else
            {
                _eneDetail1Label.Text = $"Mana: {_characterState.CurrentMana}/{_characterState.MaximumMana}";
                _eneDetail1Label.Visible = true;
                _eneDetail2Label.Text = $"AG: {_characterState.CurrentAbility}/{_characterState.MaximumAbility}";
                _eneDetail2Label.Visible = true;
                _eneDetail3Label.Text = string.Empty;
                _eneDetail3Label.Visible = false;
            }

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

            // Set Leadership stat value if Dark Lord family
            if (isDarkLordFamily)
            {
                var totalCmd = _characterState.TotalLeadership;
                var baseCmd = _characterState.Leadership;
                _statValueLabels[4].Text = totalCmd > baseCmd ? $"{baseCmd}+{totalCmd - baseCmd}" : baseCmd.ToString();
                SetStatLabelColor(_statValueLabels[4], baseCmd, (ushort)(totalCmd - baseCmd));
            }

            for (int i = 0; i < 4; i++)
            {
                _statButtons[i].Visible = _characterState.LevelUpPoints > 0;
            }

            bool canBeMaster = _characterState.Class != CharacterClassNumber.DarkWizard;
            _masterLevelButton.Visible = canBeMaster && _characterState.MasterLevel > 0;
            _masterLevelButton.Enabled = canBeMaster && _characterState.MasterLevel > 0;

            // Show PvM attack and defense rates
            var pvmAttackRate = GetPvMAttackRate();
            var pvmDefenseRate = GetPvMDefenseRate();

            _pvmInfoLabel1.Text = $"PvM Attack Success Rate: {pvmAttackRate}%";
            _pvmInfoLabel1.Visible = true;
            _pvmInfoLabel2.Text = $"PvM Defense Success Rate: {pvmDefenseRate}%";
            _pvmInfoLabel2.Visible = true;
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

        // MU Online formula calculations based on character class
        private (int min, int max) GetPhysicalDamage()
        {
            if (_characterState == null) return (0, 0);

            var str = _characterState.TotalStrength;
            var agi = _characterState.TotalAgility;
            var ene = _characterState.TotalEnergy;

            return _characterState.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    (str / 6, str / 4), // Advanced: str/5, str/3
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    ((str + agi * 2) / 14, (str + agi * 2) / 8),
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    ((str * 2 + ene) / 12, (str * 2 + ene) / 8),
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    ((str * 2 + ene) / 14, (str * 2 + ene) / 10),
                CharacterClassNumber.RageFighter or CharacterClassNumber.FistMaster =>
                    (str / 6 + _characterState.TotalVitality / 10, str / 4 + _characterState.TotalVitality / 8),
                _ => (0, 0)
            };
        }

        private (int min, int max) GetMagicalDamage()
        {
            if (_characterState == null) return (0, 0);

            var ene = _characterState.TotalEnergy;

            return _characterState.Class switch
            {
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    (ene / 9, ene / 4), // Advanced: ene/5, ene/2
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    (ene / 9, ene / 4),
                CharacterClassNumber.Summoner or CharacterClassNumber.BloodySummoner or CharacterClassNumber.DimensionMaster =>
                    (ene / 5, ene / 2),
                _ => (0, 0)
            };
        }

        private int GetAttackSpeed()
        {
            if (_characterState == null) return 0;

            var agi = _characterState.TotalAgility;

            return _characterState.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    agi / 15,
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    agi / 10,
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    agi / 50,
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    agi / 15,
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    agi / 10,
                CharacterClassNumber.Summoner or CharacterClassNumber.BloodySummoner or CharacterClassNumber.DimensionMaster =>
                    agi / 20,
                CharacterClassNumber.RageFighter or CharacterClassNumber.FistMaster =>
                    agi / 9,
                _ => 0
            };
        }

        private int GetDefense()
        {
            if (_characterState == null) return 0;

            var agi = _characterState.TotalAgility;

            return _characterState.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    agi / 3,
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    agi / 4,
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    agi / 10,
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    agi / 5,
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    agi / 7,
                CharacterClassNumber.Summoner or CharacterClassNumber.BloodySummoner or CharacterClassNumber.DimensionMaster =>
                    agi / 4,
                CharacterClassNumber.RageFighter or CharacterClassNumber.FistMaster =>
                    agi / 7,
                _ => 0
            };
        }

        private int GetPvPAttackRate()
        {
            if (_characterState == null) return 0;

            var lvl = _characterState.Level;
            var agi = _characterState.TotalAgility;

            return _characterState.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    lvl * 3 + (int)(agi * 4.5f),
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    lvl * 3 + agi * 4,
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    (int)(lvl * 3 + agi * 0.6f),
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    (int)(lvl * 3 + agi * 3.5f),
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    lvl * 3 + agi * 4,
                _ => 0
            };
        }

        private int GetPvPDefenseRate()
        {
            if (_characterState == null) return 0;

            var lvl = _characterState.Level;
            var agi = _characterState.TotalAgility;

            return _characterState.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    lvl * 2 + agi / 2,
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    lvl * 2 + agi / 4,
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    lvl * 2 + agi / 10,
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    lvl * 2 + agi / 4,
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    lvl * 2 + agi / 2,
                _ => 0
            };
        }

        private int GetPvMAttackRate()
        {
            if (_characterState == null) return 0;

            var lvl = _characterState.Level;
            var agi = _characterState.TotalAgility;
            var str = _characterState.TotalStrength;
            var cmd = _characterState.TotalLeadership;

            return _characterState.Class switch
            {
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    (int)((lvl * 2 + agi) * 2.5f + str / 6 + cmd / 10),
                _ => (int)(lvl * 5 + agi * 1.5f + str / 4)
            };
        }

        private int GetPvMDefenseRate()
        {
            if (_characterState == null) return 0;

            var agi = _characterState.TotalAgility;

            return _characterState.Class switch
            {
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    agi / 4,
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    agi / 7,
                _ => agi / 3
            };
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Status != GameControlStatus.Ready) return;

            base.Draw(gameTime); // Base draw will handle all children including frames
            // Manually draw the info table on top of the background, but under text labels
            using (new SpriteBatchScope(GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform))
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

            int tableX = this.DisplayRectangle.X + 18; // Use CharacterInfoWindowControl's display position
            int tableY = this.DisplayRectangle.Y + 55;
            int tableWidth = WINDOW_WIDTH - 36; // Use most of the window width
            int tableHeight = 80; // Slightly taller table

            Color tableBgColor = Color.Black * 0.3f * Alpha;
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(tableX, tableY, tableWidth, tableHeight), tableBgColor);

            int cornerSize = 16; // Slightly larger corners for bigger table

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
