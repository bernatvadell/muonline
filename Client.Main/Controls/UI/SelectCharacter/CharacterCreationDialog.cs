using Client.Main.Controllers;
using Client.Main.Controls.UI.Common;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;

namespace Client.Main.Controls.UI.SelectCharacter
{
    /// <summary>
    /// Dialog for creating a new character with class selection.
    /// </summary>
    public class CharacterCreationDialog : GameControl
    {
        // ═══════════════════════════════════════════════════════════════
        // MODERN DARK THEME - Matching SelectCharacterScene
        // ═══════════════════════════════════════════════════════════════
        private static class Theme
        {
            // Background layers
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BgLight = new(35, 42, 52, 245);
            public static readonly Color BgLighter = new(48, 56, 68, 240);

            // Accent - Warm Gold
            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color AccentBright = new(255, 215, 120);
            public static readonly Color AccentDim = new(140, 115, 55);
            public static readonly Color AccentGlow = new(255, 200, 80, 40);

            // Secondary accent - Cool Blue
            public static readonly Color Secondary = new(90, 140, 200);
            public static readonly Color SecondaryBright = new(130, 180, 240);
            public static readonly Color SecondaryDim = new(50, 80, 120);

            // Borders
            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color BorderHighlight = new(100, 110, 130, 120);

            // Text
            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color TextGold = new(255, 220, 130);
            public static readonly Color TextGray = new(160, 165, 175);
            public static readonly Color TextDark = new(100, 105, 115);

            // Status colors
            public static readonly Color Success = new(80, 200, 120);
            public static readonly Color Warning = new(240, 180, 60);
            public static readonly Color Danger = new(220, 80, 80);
        }

        private readonly List<CharacterClassInfo> _availableClasses;
        private int _selectedClassIndex = 0;
        
        private LabelControl _titleLabel;
        private TextBoxControl _nameInput;
        private LabelControl _classLabel;
        private LabelControl _classDescriptionLabel;
        private ButtonControl _previousClassButton;
        private ButtonControl _nextClassButton;
        private ButtonControl _createButton;
        private ButtonControl _cancelButton;
        
        // Static surface cache for background (following muonline-ui-design pattern)
        private RenderTarget2D _backgroundSurface;
        private bool _surfaceNeedsRedraw = true;
        private double _bringToFrontTimer = 0;
        private const double BRING_TO_FRONT_INTERVAL = 0.1; // Throttle to every 100ms
        
        public event EventHandler<(string Name, CharacterClassNumber Class)> CharacterCreateRequested;
        public event EventHandler CancelRequested;

        private struct CharacterClassInfo
        {
            public CharacterClassNumber Class;
            public string Name;
            public string Description;
        }

        public CharacterCreationDialog()
        {
            _availableClasses = new List<CharacterClassInfo>
            {
                new CharacterClassInfo 
                { 
                    Class = CharacterClassNumber.DarkWizard, 
                    Name = "Dark Wizard",
                    Description = "Masters of magical destruction.\nHigh magic damage, low defense."
                },
                new CharacterClassInfo 
                { 
                    Class = CharacterClassNumber.DarkKnight, 
                    Name = "Dark Knight",
                    Description = "Warriors of strength and honor.\nHigh health and physical damage."
                },
                new CharacterClassInfo 
                { 
                    Class = CharacterClassNumber.FairyElf, 
                    Name = "Fairy Elf",
                    Description = "Agile archers and healers.\nHigh agility, support abilities."
                },
                new CharacterClassInfo 
                { 
                    Class = CharacterClassNumber.MagicGladiator, 
                    Name = "Magic Gladiator",
                    Description = "Hybrid warriors with magic.\nBalanced melee and magic skills."
                },
                new CharacterClassInfo 
                { 
                    Class = CharacterClassNumber.DarkLord, 
                    Name = "Dark Lord",
                    Description = "Commanders with dark powers.\nSummons pets and commands armies."
                },
                new CharacterClassInfo 
                { 
                    Class = CharacterClassNumber.Summoner, 
                    Name = "Summoner",
                    Description = "Mystics with curse powers.\nCurses enemies and summons."
                },
                new CharacterClassInfo 
                { 
                    Class = CharacterClassNumber.RageFighter, 
                    Name = "Rage Fighter",
                    Description = "Hand-to-hand combat masters.\nHighest HP, powerful combos."
                }
            };

            AutoViewSize = false;
            ViewSize = new Point(650, 550);
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;
            Interactive = true;
            
            InitializeControls();
            UpdateClassDisplay();
            
            BringToFront();
        }

        private void InitializeControls()
        {
            // Title
            _titleLabel = new LabelControl
            {
                Text = "CREATE CHARACTER",
                FontSize = 20f,
                TextColor = Theme.TextGold,
                Align = ControlAlign.Top | ControlAlign.HorizontalCenter,
                Margin = new Margin { Top = 10 }
            };
            Controls.Add(_titleLabel);

            // Character name input label
            var nameLabel = new LabelControl
            {
                Text = "Character Name:",
                FontSize = 13f,
                TextColor = Theme.TextWhite,
                X = 50,
                Y = 75
            };
            Controls.Add(nameLabel);

            // Character name input
            _nameInput = new TextBoxControl
            {
                X = 50,
                Y = 100,
                ViewSize = new Point(550, 36),
                MaxLength = 10,
                PlaceholderText = "Enter name (3-10 chars)...",
                FontSize = 8f,
                BackgroundColor = Theme.BgDark,
                TextColor = Theme.TextWhite,
                BorderColor = Theme.BorderInner,
                BorderThickness = 1
            };
            Controls.Add(_nameInput);

            // Class selection section
            var classSectionLabel = new LabelControl
            {
                Text = "Select Class:",
                FontSize = 13f,
                TextColor = Theme.TextWhite,
                X = 50,
                Y = 165
            };
            Controls.Add(classSectionLabel);

            // Class navigation buttons
            _previousClassButton = CreateModernNavigationButton("<");
            _previousClassButton.X = 50;
            _previousClassButton.Y = 200;
            _previousClassButton.Click += (s, e) => ChangeClass(-1);
            Controls.Add(_previousClassButton);

            _nextClassButton = CreateModernNavigationButton(">");
            _nextClassButton.X = ViewSize.X - 50 - _nextClassButton.ViewSize.X;
            _nextClassButton.Y = 200;
            _nextClassButton.Click += (s, e) => ChangeClass(1);
            Controls.Add(_nextClassButton);

            // Class name label
            _classLabel = new LabelControl
            {
                Text = "Dark Wizard",
                FontSize = 18f,
                TextColor = Theme.TextGold,
                Align = ControlAlign.HorizontalCenter,
                Y = 200
            };
            Controls.Add(_classLabel);

            // Class description
            _classDescriptionLabel = new LabelControl
            {
                Text = "",
                FontSize = 12f,
                TextColor = Theme.TextGray,
                Align = ControlAlign.HorizontalCenter,
                Y = 240,
                ViewSize = new Point(550, 100),
                X = 50
            };
            Controls.Add(_classDescriptionLabel);

            // Create button
            _createButton = CreateModernButton("CREATE CHARACTER", Theme.Success);
            _createButton.X = 50;
            _createButton.Y = 380;
            _createButton.ViewSize = new Point(260, 40);
            _createButton.Click += OnCreateButtonClick;
            Controls.Add(_createButton);

            // Cancel button
            _cancelButton = CreateModernButton("CANCEL", Theme.BgLight);
            _cancelButton.X = 340;
            _cancelButton.Y = 380;
            _cancelButton.ViewSize = new Point(260, 40);
            _cancelButton.Click += (s, e) => CancelRequested?.Invoke(this, EventArgs.Empty);
            Controls.Add(_cancelButton);
        }

        private ButtonControl CreateModernNavigationButton(string arrow)
        {
            return new ButtonControl
            {
                Text = arrow,
                FontSize = 32f,
                AutoViewSize = false,
                ViewSize = new Point(60, 60),
                BackgroundColor = Theme.BgMid,
                HoverBackgroundColor = Theme.BgLight,
                PressedBackgroundColor = Theme.BgDark,
                TextColor = Theme.Accent,
                HoverTextColor = Theme.AccentBright,
                DisabledTextColor = Theme.TextDark,
                Interactive = true,
                BorderThickness = 2,
                BorderColor = Theme.BorderInner
            };
        }

        private ButtonControl CreateModernButton(string text, Color baseColor)
        {
            return new ButtonControl
            {
                Text = text,
                FontSize = 13f,
                AutoViewSize = false,
                BackgroundColor = baseColor,
                HoverBackgroundColor = Color.Lerp(baseColor, Color.White, 0.2f),
                PressedBackgroundColor = Color.Lerp(baseColor, Color.Black, 0.2f),
                TextColor = Theme.TextWhite,
                HoverTextColor = Theme.TextWhite,
                DisabledTextColor = Theme.TextDark,
                Interactive = true,
                BorderThickness = 1,
                BorderColor = Theme.BorderInner
            };
        }

        private void ChangeClass(int direction)
        {
            _selectedClassIndex = (_selectedClassIndex + direction + _availableClasses.Count) % _availableClasses.Count;
            UpdateClassDisplay();
        }

        private void UpdateClassDisplay()
        {
            var selectedClass = _availableClasses[_selectedClassIndex];
            _classLabel.Text = selectedClass.Name;
            _classDescriptionLabel.Text = selectedClass.Description;
        }

        private void OnCreateButtonClick(object sender, EventArgs e)
        {
            string characterName = _nameInput?.Text?.Trim() ?? "";
            
            if (string.IsNullOrWhiteSpace(characterName))
            {
                MessageWindow.Show("Please enter a character name.");
                return;
            }

            if (characterName.Length < 3)
            {
                MessageWindow.Show("Character name must be at least 3 characters long.");
                return;
            }

            if (characterName.Length > 10)
            {
                MessageWindow.Show("Character name must be 10 characters or less.");
                return;
            }

            var selectedClass = _availableClasses[_selectedClassIndex];
            CharacterCreateRequested?.Invoke(this, (characterName, selectedClass.Class));
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            
            _bringToFrontTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_bringToFrontTimer >= BRING_TO_FRONT_INTERVAL && Parent != null)
            {
                _bringToFrontTimer = 0;
                BringToFront();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            DrawCachedBackground();
            
            base.Draw(gameTime);
        }

        private void DrawCachedBackground()
        {
            var device = MuGame.Instance.GraphicsDevice;
            
            if (_backgroundSurface == null || _surfaceNeedsRedraw || 
                _backgroundSurface.Width != ViewSize.X || _backgroundSurface.Height != ViewSize.Y)
            {
                _backgroundSurface?.Dispose();
                _backgroundSurface = new RenderTarget2D(
                    device,
                    ViewSize.X,
                    ViewSize.Y,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None
                );
                
                // Render background to surface
                var oldTargets = device.GetRenderTargets();
                device.SetRenderTarget(_backgroundSurface);
                device.Clear(Color.Transparent);
                
                using var batch = new SpriteBatch(device);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                
                var pixel = UiDrawHelper.GetPixelTexture(device);
                var dialogRect = new Rectangle(0, 0, ViewSize.X, ViewSize.Y);
                
                // Main panel background with gradient
                UiDrawHelper.DrawVerticalGradient(batch, dialogRect, Theme.BgMid, Theme.BgDark);
                
                // Outer border
                batch.Draw(pixel, new Rectangle(0, 0, dialogRect.Width, 1), Theme.BorderOuter);
                batch.Draw(pixel, new Rectangle(0, dialogRect.Height - 1, dialogRect.Width, 1), Theme.BorderOuter);
                batch.Draw(pixel, new Rectangle(0, 0, 1, dialogRect.Height), Theme.BorderOuter);
                batch.Draw(pixel, new Rectangle(dialogRect.Width - 1, 0, 1, dialogRect.Height), Theme.BorderOuter);
                
                // Header section
                var headerRect = new Rectangle(0, 0, dialogRect.Width, 50);
                UiDrawHelper.DrawHorizontalGradient(batch, headerRect, Theme.BgLighter, Theme.BgMid);
                UiDrawHelper.DrawCornerAccents(batch, headerRect, Theme.Accent, 12, 2);
                
                // Header separator
                batch.Draw(pixel, new Rectangle(0, headerRect.Bottom - 1, headerRect.Width, 1), Theme.BorderInner);
                batch.Draw(pixel, new Rectangle(0, headerRect.Bottom - 2, headerRect.Width, 1), Theme.Accent * 0.3f);
                
                batch.End();
                
                device.SetRenderTargets(oldTargets);
                _surfaceNeedsRedraw = false;
            }
            
            // Draw cached surface
            using var scope = new SpriteBatchScope(
                GraphicsManager.Instance.Sprite,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                UiScaler.SpriteTransform
            );
            
            GraphicsManager.Instance.Sprite.Draw(
                _backgroundSurface,
                new Rectangle(DisplayPosition.X, DisplayPosition.Y, ViewSize.X, ViewSize.Y),
                Color.White
            );
        }

        public override void Dispose()
        {
            _backgroundSurface?.Dispose();
            _backgroundSurface = null;
            base.Dispose();
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            _surfaceNeedsRedraw = true;
        }
    }
}
