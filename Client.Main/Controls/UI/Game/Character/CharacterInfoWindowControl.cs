using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Networking;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Main.Helpers;

namespace Client.Main.Controls.UI.Game.Character
{
    public class CharacterInfoWindowControl : UIControl, IUiTexturePreloadable
    {
        private const int WINDOW_WIDTH = 280;
        private const int WINDOW_HEIGHT = 520;
        private const int STAT_BOX_WIDTH = 170;
        private const int STAT_BOX_HEIGHT = 21;
        private const int BTN_STAT_COUNT = 5;

        private static readonly float[] s_statRowY =
        {
            140f,
            200f,
            270f,
            340f,
            410f
        };

        private static readonly string[] s_statShortNames = { "STR", "AGI", "STA", "ENE", "CMD" };

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

        private enum TextAlignment
        {
            Left,
            Center,
            Right
        }

        private sealed class CharacterInfoTextEntry
        {
            public CharacterInfoTextEntry(Vector2 basePosition, float fontScale, Color color, TextAlignment alignment)
            {
                BasePosition = basePosition;
                FontScale = fontScale;
                Color = color;
                Alignment = alignment;
            }

            public Vector2 BasePosition { get; set; }
            public string Text { get; set; } = string.Empty;
            public Color Color { get; set; }
            public float FontScale { get; set; }
            public TextAlignment Alignment { get; set; }
            public bool Visible { get; set; } = true;
        }

        private sealed class CharacterInfoButton
        {
            public CharacterInfoButton(Rectangle baseBounds, Texture2D texture, Rectangle? normalRect, Rectangle? hoverRect, Rectangle? pressedRect, Action onClick)
            {
                BaseBounds = baseBounds;
                Texture = texture;
                NormalRect = normalRect;
                HoverRect = hoverRect;
                PressedRect = pressedRect;
                FrameSize = normalRect?.Size ?? baseBounds.Size;
                OnClick = onClick;
            }

            public Rectangle BaseBounds { get; set; }
            public Texture2D Texture { get; }
            public Rectangle? NormalRect { get; }
            public Rectangle? HoverRect { get; }
            public Rectangle? PressedRect { get; }
            public Point FrameSize { get; }
            public Action OnClick { get; }
            public bool Visible { get; set; } = true;
            public bool Enabled { get; set; } = true;
        }

        private Texture2D _backgroundTexture;
        private Texture2D _topFrameTexture;
        private Texture2D _leftFrameTexture;
        private Texture2D _rightFrameTexture;
        private Texture2D _bottomFrameTexture;
        private Texture2D _statTextboxTexture;
        private Texture2D _buttonExitTexture;
        private Texture2D _buttonQuestTexture;
        private Texture2D _buttonPetTexture;
        private Texture2D _buttonMasterTexture;
        private Texture2D _statIncreaseButtonTexture;
        private Texture2D _tableTopLeftTexture;
        private Texture2D _tableTopRightTexture;
        private Texture2D _tableBottomLeftTexture;
        private Texture2D _tableBottomRightTexture;
        private Texture2D _tableTopLineTexture;
        private Texture2D _tableBottomLineTexture;
        private Texture2D _tableLeftLineTexture;
        private Texture2D _tableRightLineTexture;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private readonly List<CharacterInfoTextEntry> _texts = new();
        private readonly List<CharacterInfoButton> _buttons = new();

        private CharacterInfoTextEntry _nameText;
        private CharacterInfoTextEntry _classText;
        private CharacterInfoTextEntry _levelText;
        private CharacterInfoTextEntry _expText;
        private CharacterInfoTextEntry _fruitProbText;
        private CharacterInfoTextEntry _fruitStatsText;
        private CharacterInfoTextEntry _statPointsText;
        private readonly CharacterInfoTextEntry[] _statValueTexts = new CharacterInfoTextEntry[BTN_STAT_COUNT];
        private CharacterInfoTextEntry _strDetail1Text;
        private CharacterInfoTextEntry _strDetail2Text;
        private CharacterInfoTextEntry _agiDetail1Text;
        private CharacterInfoTextEntry _agiDetail2Text;
        private CharacterInfoTextEntry _agiDetail3Text;
        private CharacterInfoTextEntry _vitDetail1Text;
        private CharacterInfoTextEntry _vitDetail2Text;
        private CharacterInfoTextEntry _eneDetail1Text;
        private CharacterInfoTextEntry _eneDetail2Text;
        private CharacterInfoTextEntry _eneDetail3Text;
        private CharacterInfoTextEntry _pvmInfo1Text;
        private CharacterInfoTextEntry _pvmInfo2Text;

        private readonly CharacterInfoButton[] _statButtons = new CharacterInfoButton[BTN_STAT_COUNT];
        private CharacterInfoButton _exitButton;
        private CharacterInfoButton _questButton;
        private CharacterInfoButton _petButton;
        private CharacterInfoButton _masterButton;

        private int _hoveredButtonIndex = -1;
        private int _pressedButtonIndex = -1;

        private CharacterState _characterState;
        private NetworkManager _networkManager;
        private readonly ILogger<CharacterInfoWindowControl> _logger;

        private CharacterInfoSnapshot _cachedSnapshot;
        private bool _hasCachedSnapshot;
        private bool _lastIsDarkLordFamily;

        public CharacterInfoWindowControl()
        {
            _logger = MuGame.AppLoggerFactory.CreateLogger<CharacterInfoWindowControl>();
            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;

            _networkManager = MuGame.Network;
            if (_networkManager == null)
            {
                _logger.LogWarning("NetworkManager is null in CharacterInfoWindowControl constructor. Stat increase functionality may not work.");
            }
            else
            {
                // Subscribe to attack speed changes to refresh UI
                var characterState = _networkManager.GetCharacterState();
                if (characterState != null)
                {
                    characterState.AttackSpeedsChanged += OnAttackSpeedsChanged;
                }
            }
        }

        public IEnumerable<string> GetPreloadTexturePaths()
            => s_additionalPreloadTextures.Concat(s_tableTexturePaths);

        public override async Task Load()
        {
            await base.Load();

            var tl = TextureLoader.Instance;

            _backgroundTexture = await tl.PrepareAndGetTexture("Interface/newui_msgbox_back.jpg");
            _topFrameTexture = await tl.PrepareAndGetTexture("Interface/newui_item_back04.tga");
            _leftFrameTexture = await tl.PrepareAndGetTexture("Interface/newui_item_back02-L.tga");
            _rightFrameTexture = await tl.PrepareAndGetTexture("Interface/newui_item_back02-R.tga");
            _bottomFrameTexture = await tl.PrepareAndGetTexture("Interface/newui_item_back03.tga");
            _statTextboxTexture = await tl.PrepareAndGetTexture("Interface/newui_cha_textbox02.tga");

            _buttonExitTexture = await tl.PrepareAndGetTexture("Interface/newui_exit_00.tga");
            _buttonQuestTexture = await tl.PrepareAndGetTexture("Interface/newui_chainfo_btn_quest.tga");
            _buttonPetTexture = await tl.PrepareAndGetTexture("Interface/newui_chainfo_btn_pet.tga");
            _buttonMasterTexture = await tl.PrepareAndGetTexture("Interface/newui_chainfo_btn_master.tga");
            _statIncreaseButtonTexture = await tl.PrepareAndGetTexture("Interface/newui_chainfo_btn_level.tga");

            var tableTextures = await Task.WhenAll(s_tableTexturePaths.Select(path => tl.PrepareAndGetTexture(path)));
            _tableTopLeftTexture = tableTextures.ElementAtOrDefault(0);
            _tableTopRightTexture = tableTextures.ElementAtOrDefault(1);
            _tableBottomLeftTexture = tableTextures.ElementAtOrDefault(2);
            _tableBottomRightTexture = tableTextures.ElementAtOrDefault(3);
            _tableTopLineTexture = tableTextures.ElementAtOrDefault(4);
            _tableBottomLineTexture = tableTextures.ElementAtOrDefault(5);
            _tableLeftLineTexture = tableTextures.ElementAtOrDefault(6);
            _tableRightLineTexture = tableTextures.ElementAtOrDefault(7);

            InitializeLayout();
            InvalidateStaticSurface();
            UpdateDisplayData();
        }

        private void InitializeLayout()
        {
            _texts.Clear();
            _buttons.Clear();

            float statBoxLeft = GetStatBoxLeft();
            float statValueLeft = statBoxLeft + 70f;
            float statValueYoffset = -2f;
            float statDetailYOffset = 15f;

            _nameText = CreateText(new Vector2(WINDOW_WIDTH / 2f, 5f), 13f, Color.White, TextAlignment.Center);
            _classText = CreateText(new Vector2(WINDOW_WIDTH / 2f, 23f), 12f, Color.LightGray, TextAlignment.Center);
            _levelText = CreateText(new Vector2(28f, 60f), 11f, new Color(230, 230, 0));
            _expText = CreateText(new Vector2(28f, 78f), 10f, Color.WhiteSmoke);
            _fruitProbText = CreateText(new Vector2(28f, 96f), 10f, new Color(76, 197, 254));
            _fruitStatsText = CreateText(new Vector2(28f, 114f), 10f, new Color(76, 197, 254));
            _statPointsText = CreateText(new Vector2(155f, 60f), 11f, new Color(255, 138, 0));

            for (int i = 0; i < BTN_STAT_COUNT; i++)
            {
                float rowY = s_statRowY[i];
                _statValueTexts[i] = CreateText(new Vector2(statValueLeft, rowY + statValueYoffset), 11f, new Color(230, 230, 0), TextAlignment.Left);
            }

            _strDetail1Text = CreateText(new Vector2(25f, s_statRowY[0] + statDetailYOffset), 10f, Color.LightGray);
            _strDetail2Text = CreateText(new Vector2(25f, s_statRowY[0] + statDetailYOffset + 16f), 10f, Color.LightGray);

            _agiDetail1Text = CreateText(new Vector2(25f, s_statRowY[1] + statDetailYOffset), 10f, Color.LightGray);
            _agiDetail2Text = CreateText(new Vector2(25f, s_statRowY[1] + statDetailYOffset + 16f), 10f, Color.LightGray);
            _agiDetail3Text = CreateText(new Vector2(25f, s_statRowY[1] + statDetailYOffset + 32f), 10f, Color.LightGray);

            _vitDetail1Text = CreateText(new Vector2(25f, s_statRowY[2] + statDetailYOffset), 10f, Color.LightGray);
            _vitDetail2Text = CreateText(new Vector2(25f, s_statRowY[2] + statDetailYOffset + 16f), 10f, Color.LightGray);

            _eneDetail1Text = CreateText(new Vector2(25f, s_statRowY[3] + statDetailYOffset), 10f, Color.LightGray);
            _eneDetail2Text = CreateText(new Vector2(25f, s_statRowY[3] + statDetailYOffset + 16f), 10f, Color.LightGray);
            _eneDetail3Text = CreateText(new Vector2(25f, s_statRowY[3] + statDetailYOffset + 32f), 10f, Color.LightGray);

            _pvmInfo1Text = CreateText(new Vector2(18f, s_statRowY[4] + 20f), 10f, new Color(255, 200, 100));
            _pvmInfo2Text = CreateText(new Vector2(18f, s_statRowY[4] + 38f), 10f, new Color(255, 200, 100));

            // Stat increase buttons
            for (int i = 0; i < BTN_STAT_COUNT; i++)
            {
                int statIndex = i;
                var bounds = new Rectangle(
                    WINDOW_WIDTH - 70,
                    (int)(s_statRowY[i] - 2f),
                    16,
                    15);

                Rectangle? normal = null;
                Rectangle? hover = null;
                Rectangle? pressed = null;
                if (_statIncreaseButtonTexture != null)
                {
                    int frameCount = DetermineFrameCount(_statIncreaseButtonTexture);
                    int frameHeight = Math.Max(1, _statIncreaseButtonTexture.Height / frameCount);
                    normal = new Rectangle(0, 0, _statIncreaseButtonTexture.Width, frameHeight);
                    if (frameCount > 1)
                    {
                        hover = new Rectangle(0, frameHeight, _statIncreaseButtonTexture.Width, frameHeight);
                    }
                    if (frameCount > 2)
                    {
                        pressed = new Rectangle(0, frameHeight * 2, _statIncreaseButtonTexture.Width, frameHeight);
                    }
                    else
                    {
                        pressed = hover;
                    }
                    bounds.Width = _statIncreaseButtonTexture.Width;
                    bounds.Height = frameHeight;
                }

                _statButtons[i] = CreateButton(bounds, _statIncreaseButtonTexture, normal, hover, pressed, () => OnStatButtonClicked(statIndex));
            }

            // Bottom buttons
            _exitButton = CreateButton(CreateBottomBounds(20, _buttonExitTexture), _buttonExitTexture, CreateButtonFrames(_buttonExitTexture, out var exitHover, out var exitPressed), exitHover, exitPressed, () =>
            {
                Visible = false;
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            });

            _questButton = CreateButton(CreateBottomBounds(70, _buttonQuestTexture), _buttonQuestTexture, CreateButtonFrames(_buttonQuestTexture, out var questHover, out var questPressed), questHover, questPressed, () =>
            {
                _logger.LogInformation("Quest button clicked.");
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            });

            _petButton = CreateButton(CreateBottomBounds(120, _buttonPetTexture), _buttonPetTexture, CreateButtonFrames(_buttonPetTexture, out var petHover, out var petPressed), petHover, petPressed, () =>
            {
                _logger.LogInformation("Pet button clicked.");
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            });

            _masterButton = CreateButton(CreateBottomBounds(170, _buttonMasterTexture), _buttonMasterTexture, CreateButtonFrames(_buttonMasterTexture, out var masterHover, out var masterPressed), masterHover, masterPressed, () =>
            {
                _logger.LogInformation("Master Level button clicked.");
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            });
        }

        private CharacterInfoTextEntry CreateText(Vector2 basePosition, float fontSize, Color color, TextAlignment alignment = TextAlignment.Left)
        {
            float fontScale = fontSize / Constants.BASE_FONT_SIZE;
            var entry = new CharacterInfoTextEntry(basePosition, fontScale, color, alignment);
            _texts.Add(entry);
            return entry;
        }

        private static Rectangle CreateBottomBounds(int xOffset, Texture2D texture)
        {
            if (texture == null)
            {
                return new Rectangle(xOffset, WINDOW_HEIGHT - 40, 36, 29);
            }

            int frameHeight = texture.Height / 2;
            if (frameHeight <= 0)
            {
                frameHeight = texture.Height;
            }

            return new Rectangle(xOffset, WINDOW_HEIGHT - frameHeight - 11, texture.Width, frameHeight);
        }

        private static Rectangle? CreateButtonFrames(Texture2D texture, out Rectangle? hover, out Rectangle? pressed)
        {
            hover = null;
            pressed = null;

            if (texture == null)
            {
                return null;
            }

            int frameCount = DetermineFrameCount(texture);
            int frameHeight = Math.Max(1, texture.Height / frameCount);

            if (frameHeight <= 0)
            {
                frameHeight = texture.Height;
            }

            var normal = new Rectangle(0, 0, texture.Width, frameHeight);

            if (texture.Height >= frameHeight * 2)
            {
                hover = new Rectangle(0, frameHeight, texture.Width, frameHeight);
            }

            if (texture.Height >= frameHeight * 3)
            {
                pressed = new Rectangle(0, frameHeight * 2, texture.Width, frameHeight);
            }
            else
            {
                pressed = hover;
            }

            return normal;
        }

        private static int DetermineFrameCount(Texture2D texture)
        {
            if (texture == null)
            {
                return 1;
            }

            if (texture.Height % 3 == 0)
            {
                return 3;
            }

            if (texture.Height % 2 == 0)
            {
                return 2;
            }

            return 1;
        }

        private CharacterInfoButton CreateButton(Rectangle baseBounds, Texture2D texture, Rectangle? normalRect, Rectangle? hoverRect, Rectangle? pressedRect, Action onClick)
        {
            if (normalRect.HasValue)
            {
                baseBounds.Width = normalRect.Value.Width;
                baseBounds.Height = normalRect.Value.Height;
            }

            var button = new CharacterInfoButton(baseBounds, texture, normalRect, hoverRect, pressedRect, onClick);
            _buttons.Add(button);
            return button;
        }

        private static float GetStatBoxLeft()
        {
            float value = (WINDOW_WIDTH - STAT_BOX_WIDTH) / 2f - 40f;
            return value < 10f ? 15f : value;
        }

        private void InvalidateStaticSurface()
        {
            _staticSurfaceDirty = true;
        }

        private void EnsureStaticSurface()
        {
            if (!_staticSurfaceDirty && _staticSurface != null && !_staticSurface.IsDisposed)
            {
                return;
            }

            var graphicsDevice = GraphicsManager.Instance.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            _staticSurface?.Dispose();
            _staticSurface = new RenderTarget2D(graphicsDevice, WINDOW_WIDTH, WINDOW_HEIGHT, false, SurfaceFormat.Color, DepthFormat.None);

            var previousTargets = graphicsDevice.GetRenderTargets();
            graphicsDevice.SetRenderTarget(_staticSurface);
            graphicsDevice.Clear(Color.Transparent);

            var spriteBatch = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {
                DrawStaticElements(spriteBatch);
            }

            graphicsDevice.SetRenderTargets(previousTargets);
            _staticSurfaceDirty = false;
        }

        private void DrawStaticElements(SpriteBatch spriteBatch)
        {
            if (_backgroundTexture != null)
            {
                spriteBatch.Draw(_backgroundTexture, new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT), Color.White);
            }

            if (_topFrameTexture != null)
            {
                spriteBatch.Draw(_topFrameTexture, new Rectangle(0, 0, WINDOW_WIDTH + 97, 74), Color.White);
            }

            if (_leftFrameTexture != null)
            {
                spriteBatch.Draw(_leftFrameTexture, new Rectangle(0, 0, 26, WINDOW_HEIGHT + 250), Color.White);
            }

            if (_rightFrameTexture != null)
            {
                spriteBatch.Draw(_rightFrameTexture, new Rectangle(WINDOW_WIDTH - 17, 0, 26, WINDOW_HEIGHT + 250), Color.White);
            }

            if (_bottomFrameTexture != null)
            {
                spriteBatch.Draw(_bottomFrameTexture, new Rectangle(0, WINDOW_HEIGHT - 40, WINDOW_WIDTH + 97, 55), Color.White);
            }

            if (_statTextboxTexture != null)
            {
                float statBoxLeft = GetStatBoxLeft();

                // Draw stat boxes - only show Leadership (5th stat) for Dark Lord family
                int statBoxCount = _lastIsDarkLordFamily ? BTN_STAT_COUNT : BTN_STAT_COUNT - 1;
                for (int i = 0; i < statBoxCount; i++)
                {
                    spriteBatch.Draw(_statTextboxTexture,
                        new Rectangle((int)statBoxLeft, (int)s_statRowY[i], STAT_BOX_WIDTH, STAT_BOX_HEIGHT),
                        Color.White);
                }

                // Draw stat short names
                var font = GraphicsManager.Instance.Font;
                if (font != null)
                {
                    float labelScale = 10f / Constants.BASE_FONT_SIZE;
                    float labelX = statBoxLeft + 8f;
                    for (int i = 0; i < statBoxCount; i++)
                    {
                        spriteBatch.DrawString(font,
                            s_statShortNames[i],
                            new Vector2(labelX, s_statRowY[i] - 1f),
                            new Color(230, 230, 0),
                            0f,
                            Vector2.Zero,
                            labelScale,
                            SpriteEffects.None,
                            0f);
                    }
                }
            }

            // Draw info table background
            DrawInfoTableSurface(spriteBatch, new Vector2(18f, 55f));
        }

        private void DrawInfoTableSurface(SpriteBatch spriteBatch, Vector2 origin)
        {
            const int tableWidth = WINDOW_WIDTH - 36;
            const int tableHeight = 80;

            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel != null)
            {
                spriteBatch.Draw(pixel, new Rectangle((int)origin.X, (int)origin.Y, tableWidth, tableHeight), Color.Black * 0.3f);
            }

            int cornerSizeTopLeftWidth = _tableTopLeftTexture?.Width ?? 0;
            int cornerSizeTopLeftHeight = _tableTopLeftTexture?.Height ?? 0;
            int cornerSizeTopRightWidth = _tableTopRightTexture?.Width ?? 0;
            int cornerSizeTopRightHeight = _tableTopRightTexture?.Height ?? 0;
            int cornerSizeBottomLeftWidth = _tableBottomLeftTexture?.Width ?? 0;
            int cornerSizeBottomLeftHeight = _tableBottomLeftTexture?.Height ?? 0;
            int cornerSizeBottomRightWidth = _tableBottomRightTexture?.Width ?? 0;
            int cornerSizeBottomRightHeight = _tableBottomRightTexture?.Height ?? 0;

            if (_tableTopLeftTexture != null)
            {
                spriteBatch.Draw(_tableTopLeftTexture,
                    new Rectangle((int)origin.X, (int)origin.Y, cornerSizeTopLeftWidth, cornerSizeTopLeftHeight),
                    Color.White);
            }

            if (_tableTopRightTexture != null)
            {
                spriteBatch.Draw(_tableTopRightTexture,
                    new Rectangle((int)(origin.X + tableWidth - cornerSizeTopRightWidth), (int)origin.Y, cornerSizeTopRightWidth, cornerSizeTopRightHeight),
                    Color.White);
            }

            if (_tableBottomLeftTexture != null)
            {
                spriteBatch.Draw(_tableBottomLeftTexture,
                    new Rectangle((int)origin.X, (int)(origin.Y + tableHeight - cornerSizeBottomLeftHeight), cornerSizeBottomLeftWidth, cornerSizeBottomLeftHeight),
                    Color.White);
            }

            if (_tableBottomRightTexture != null)
            {
                spriteBatch.Draw(_tableBottomRightTexture,
                    new Rectangle((int)(origin.X + tableWidth - cornerSizeBottomRightWidth), (int)(origin.Y + tableHeight - cornerSizeBottomRightHeight), cornerSizeBottomRightWidth, cornerSizeBottomRightHeight),
                    Color.White);
            }

            int horizontalStartX = cornerSizeTopLeftWidth;
            int horizontalEndX = tableWidth - cornerSizeTopRightWidth;
            int horizontalWidth = Math.Max(0, horizontalEndX - horizontalStartX);

            if (_tableTopLineTexture != null && horizontalWidth > 0)
            {
                spriteBatch.Draw(_tableTopLineTexture,
                    new Rectangle((int)(origin.X + horizontalStartX), (int)origin.Y, horizontalWidth, _tableTopLineTexture.Height),
                    Color.White);
            }

            if (_tableBottomLineTexture != null && horizontalWidth > 0)
            {
                spriteBatch.Draw(_tableBottomLineTexture,
                    new Rectangle((int)(origin.X + horizontalStartX), (int)(origin.Y + tableHeight - _tableBottomLineTexture.Height), horizontalWidth, _tableBottomLineTexture.Height),
                    Color.White);
            }

            int verticalStartY = cornerSizeTopLeftHeight;
            int verticalEndY = tableHeight - cornerSizeBottomLeftHeight;
            int verticalHeight = Math.Max(0, verticalEndY - verticalStartY);

            if (_tableLeftLineTexture != null && verticalHeight > 0)
            {
                spriteBatch.Draw(_tableLeftLineTexture,
                    new Rectangle((int)origin.X, (int)(origin.Y + verticalStartY), _tableLeftLineTexture.Width, verticalHeight),
                    Color.White);
            }

            if (_tableRightLineTexture != null && verticalHeight > 0)
            {
                spriteBatch.Draw(_tableRightLineTexture,
                    new Rectangle((int)(origin.X + tableWidth - _tableRightLineTexture.Width), (int)(origin.Y + verticalStartY), _tableRightLineTexture.Width, verticalHeight),
                    Color.White);
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible)
            {
                return;
            }

            base.Update(gameTime);

            HandleButtonInput();
            UpdateDisplayData();
        }

        private void HandleButtonInput()
        {
            var mouseState = MuGame.Instance.UiMouseState;
            var prevMouseState = MuGame.Instance.PrevUiMouseState;
            var mousePosition = mouseState.Position;

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && prevMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = mouseState.LeftButton == ButtonState.Released && prevMouseState.LeftButton == ButtonState.Pressed;

            _hoveredButtonIndex = -1;

            float controlScale = Scale;

            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                if (!button.Visible)
                {
                    continue;
                }

                Rectangle destRect = GetButtonRectangle(button, controlScale);
                bool hovered = destRect.Contains(mousePosition);

                if (hovered)
                {
                    _hoveredButtonIndex = i;
                    if (leftJustPressed)
                    {
                        _pressedButtonIndex = i;
                    }

                    if (leftJustReleased && _pressedButtonIndex == i && button.Enabled)
                    {
                        button.OnClick?.Invoke();
                    }
                }
            }

            if (!leftPressed)
            {
                _pressedButtonIndex = -1;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible)
            {
                return;
            }

            var font = GraphicsManager.Instance.Font;
            if (font == null)
            {
                return;
            }

            EnsureStaticSurface();

            var spriteBatch = GraphicsManager.Instance.Sprite;
            SpriteBatchScope scope = null;
            if (!SpriteBatchScope.BatchIsBegun)
            {
                scope = new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform);
            }

            try
            {
                if (_staticSurface != null && !_staticSurface.IsDisposed)
                {
                    spriteBatch.Draw(_staticSurface, DisplayRectangle, Color.White * Alpha);
                }

                DrawTexts(spriteBatch, font);
                DrawButtons(spriteBatch);
            }
            finally
            {
                scope?.Dispose();
            }
        }

        private void DrawTexts(SpriteBatch spriteBatch, SpriteFont font)
        {
            float controlScale = Scale;
            Vector2 basePosition = DisplayRectangle.Location.ToVector2();

            foreach (var entry in _texts)
            {
                if (!entry.Visible || string.IsNullOrEmpty(entry.Text))
                {
                    continue;
                }

                float textScale = entry.FontScale * controlScale;
                Vector2 pos = basePosition + entry.BasePosition * controlScale;
                Vector2 size = font.MeasureString(entry.Text) * textScale;

                switch (entry.Alignment)
                {
                    case TextAlignment.Center:
                        pos.X -= size.X * 0.5f;
                        break;
                    case TextAlignment.Right:
                        pos.X -= size.X;
                        break;
                }

                spriteBatch.DrawString(font, entry.Text, pos, entry.Color * Alpha, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawButtons(SpriteBatch spriteBatch)
        {
            float controlScale = Scale;
            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                if (!button.Visible)
                {
                    continue;
                }

                Rectangle destRect = GetButtonRectangle(button, controlScale);
                Rectangle? sourceRect = button.NormalRect;
                if (_pressedButtonIndex == i && button.Enabled && button.PressedRect.HasValue)
                {
                    sourceRect = button.PressedRect;
                }
                else if (_hoveredButtonIndex == i && button.Enabled && button.HoverRect.HasValue)
                {
                    sourceRect = button.HoverRect;
                }

                Color baseColor = button.Enabled ? Color.White : new Color(120, 120, 120);
                if (_pressedButtonIndex == i && button.Enabled)
                {
                    baseColor = new Color(200, 200, 180);
                }
                else if (_hoveredButtonIndex == i && button.Enabled)
                {
                    baseColor = new Color(240, 240, 200);
                }

                baseColor *= Alpha;

                if (button.Texture != null)
                {
                    spriteBatch.Draw(button.Texture, destRect, sourceRect, baseColor);
                }
                else if (_hoveredButtonIndex == i || _pressedButtonIndex == i)
                {
                    var overlayColor = _pressedButtonIndex == i
                        ? new Color(200, 200, 180, 200)
                        : new Color(240, 240, 200, 160);
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, destRect, overlayColor * Alpha);
                }
            }
        }

        private Rectangle GetButtonRectangle(CharacterInfoButton button, float controlScale)
        {
            var baseRect = button.BaseBounds;
            int x = DisplayRectangle.X + (int)MathF.Round(baseRect.X * controlScale);
            int y = DisplayRectangle.Y + (int)MathF.Round(baseRect.Y * controlScale);
            int width = (int)MathF.Round(button.FrameSize.X * controlScale);
            int height = (int)MathF.Round(button.FrameSize.Y * controlScale);
            return new Rectangle(x, y, width, height);
        }

        private void OnAttackSpeedsChanged()
        {
            // Attack speeds changed - invalidate cached snapshot to force UI refresh
            _hasCachedSnapshot = false;
        }

        private void UpdateDisplayData()
        {
            if (_characterState == null)
            {
                _characterState = _networkManager?.GetCharacterState();
            }

            if (_characterState == null)
            {
                return;
            }

            var snapshot = CharacterInfoSnapshot.Create(_characterState);
            if (_hasCachedSnapshot && snapshot.Equals(_cachedSnapshot))
            {
                return;
            }

            ApplySnapshot(snapshot);
            _cachedSnapshot = snapshot;
            _hasCachedSnapshot = true;
        }

        private void ApplySnapshot(CharacterInfoSnapshot snapshot)
        {
            // Check if class family changed - need to redraw static surface
            bool isDarkLordFamily = snapshot.IsDarkLordFamily;
            if (isDarkLordFamily != _lastIsDarkLordFamily)
            {
                _lastIsDarkLordFamily = isDarkLordFamily;
                InvalidateStaticSurface();
            }

            _nameText.Text = snapshot.Name;
            _classText.Text = $"({CharacterClassDatabase.GetClassName(snapshot.Class)})";

            if (snapshot.MasterLevel > 0)
            {
                _levelText.Text = $"Master Lv. {snapshot.MasterLevel}";
                _expText.Text = "----------";
                if (snapshot.MasterLevelUpPoints > 0)
                {
                    _statPointsText.Visible = true;
                    _statPointsText.Text = $"Points: {snapshot.MasterLevelUpPoints}";
                }
                else
                {
                    _statPointsText.Visible = false;
                    _statPointsText.Text = string.Empty;
                }
            }
            else
            {
                _levelText.Text = $"Level: {snapshot.Level}";
                _expText.Text = $"Exp: {snapshot.Experience} / {snapshot.ExperienceForNextLevel}";
                _statPointsText.Visible = snapshot.LevelUpPoints > 0;
                _statPointsText.Text = snapshot.LevelUpPoints > 0 ? $"Points: {snapshot.LevelUpPoints}" : string.Empty;
            }

            _fruitProbText.Text = "[+]100%|[-]100%";
            _fruitStatsText.Text = "Create 0/0 | Decrease 0/0";

            for (int i = 0; i < BTN_STAT_COUNT; i++)
            {
                ushort baseValue = snapshot.GetBaseStat(i);
                ushort addedValue = snapshot.GetAddedStat(i);
                ushort total = (ushort)(baseValue + addedValue);
                _statValueTexts[i].Text = addedValue > 0 ? $"{baseValue}+{addedValue}" : total.ToString();
                _statValueTexts[i].Color = addedValue > 0 ? new Color(100, 150, 255) : new Color(230, 230, 0);
            }

            if (snapshot.HasPhysicalDamage)
            {
                _strDetail1Text.Visible = true;
                _strDetail1Text.Text = $"Attack Damage: {snapshot.PhysicalMin}~{snapshot.PhysicalMax}";
                _strDetail2Text.Visible = true;
                _strDetail2Text.Text = $"Attack Speed: +{snapshot.AttackSpeed}";
            }
            else
            {
                ushort totalStr = (ushort)(snapshot.BaseStrength + snapshot.AddedStrength);
                _strDetail1Text.Visible = true;
                _strDetail1Text.Text = $"Strength: {totalStr}";
                _strDetail2Text.Visible = false;
                _strDetail2Text.Text = string.Empty;
            }

            _agiDetail1Text.Visible = true;
            _agiDetail1Text.Text = $"Defense: +{snapshot.Defense}";
            _agiDetail2Text.Visible = true;
            _agiDetail2Text.Text = $"Attack Rate: {snapshot.PvPAttackRate}%";
            _agiDetail3Text.Visible = true;
            _agiDetail3Text.Text = $"Defense Rate: {snapshot.PvPDefenseRate}%";

            _vitDetail1Text.Visible = true;
            _vitDetail1Text.Text = $"HP: {snapshot.CurrentHealth}/{snapshot.MaxHealth}";
            _vitDetail2Text.Visible = true;
            _vitDetail2Text.Text = $"SD: {snapshot.CurrentShield}/{snapshot.MaxShield}";

            if (snapshot.HasMagicalDamage)
            {
                _eneDetail1Text.Visible = true;
                _eneDetail1Text.Text = $"Wizardry Damage: {snapshot.MagicalMin}~{snapshot.MagicalMax}";
                _eneDetail2Text.Visible = true;
                _eneDetail2Text.Text = $"Mana: {snapshot.CurrentMana}/{snapshot.MaxMana}";
                _eneDetail3Text.Visible = true;
                _eneDetail3Text.Text = $"AG: {snapshot.CurrentAbility}/{snapshot.MaxAbility}";
            }
            else
            {
                _eneDetail1Text.Visible = true;
                _eneDetail1Text.Text = $"Mana: {snapshot.CurrentMana}/{snapshot.MaxMana}";
                _eneDetail2Text.Visible = true;
                _eneDetail2Text.Text = $"AG: {snapshot.CurrentAbility}/{snapshot.MaxAbility}";
                _eneDetail3Text.Visible = false;
                _eneDetail3Text.Text = string.Empty;
            }

            _pvmInfo1Text.Visible = true;
            _pvmInfo1Text.Text = $"PvM Attack Success Rate: {snapshot.PvMAttackRate}%";
            _pvmInfo2Text.Visible = true;
            _pvmInfo2Text.Text = $"PvM Defense Success Rate: {snapshot.PvMDefenseRate}%";

            // Show/hide Leadership (CMD) stat value text based on character class
            _statValueTexts[4].Visible = isDarkLordFamily;

            bool hasLevelUpPoints = snapshot.LevelUpPoints > 0;
            for (int i = 0; i < BTN_STAT_COUNT - 1; i++)
            {
                if (_statButtons[i] != null)
                {
                    _statButtons[i].Visible = hasLevelUpPoints;
                    _statButtons[i].Enabled = hasLevelUpPoints;
                }
            }

            // Leadership button visible only for Dark Lord family with available points
            if (_statButtons[4] != null)
            {
                _statButtons[4].Visible = isDarkLordFamily && hasLevelUpPoints;
                _statButtons[4].Enabled = isDarkLordFamily && hasLevelUpPoints;
            }

            if (_masterButton != null)
            {
                bool masterActive = snapshot.MasterLevel > 0 && snapshot.CanBeMaster;
                _masterButton.Visible = masterActive;
                _masterButton.Enabled = masterActive;
            }

            _exitButton.Visible = true;
            _exitButton.Enabled = true;
            _questButton.Visible = true;
            _questButton.Enabled = true;
            _petButton.Visible = true;
            _petButton.Enabled = true;
        }

        private void OnStatButtonClicked(int statIndex)
        {
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");

            if (_networkManager == null || !_networkManager.IsConnected || _networkManager.CurrentState < ClientConnectionState.InGame)
            {
                _logger.LogWarning("Cannot increase stat: Not connected to game server or invalid state.");
                MessageWindow.Show("Cannot add stat points: Not connected to server or not in game.");
                return;
            }

            CharacterStatAttribute attributeToSend = statIndex switch
            {
                0 => CharacterStatAttribute.Strength,
                1 => CharacterStatAttribute.Agility,
                2 => CharacterStatAttribute.Vitality,
                3 => CharacterStatAttribute.Energy,
                4 => CharacterStatAttribute.Leadership,
                _ => CharacterStatAttribute.Strength
            };

            if (_characterState == null)
            {
                return;
            }

            if (attributeToSend == CharacterStatAttribute.Leadership &&
                !(_characterState.Class == CharacterClassNumber.DarkLord || _characterState.Class == CharacterClassNumber.LordEmperor))
            {
                _logger.LogInformation("Cannot add Leadership points for non-Dark Lord character.");
                MessageWindow.Show("Only Dark Lords can add Leadership points.");
                return;
            }

            var service = _networkManager.GetCharacterService();
            if (service != null)
            {
                _ = service.SendIncreaseCharacterStatPointRequestAsync(attributeToSend);
                _logger.LogInformation("Sent request to add point to {Attribute}.", attributeToSend);
            }
            else
            {
                _logger.LogError("CharacterService is null. Cannot send stat increase request.");
                MessageWindow.Show("Internal error: Could not add points.");
            }
        }

        private CharacterInfoTextEntry CreateText(Vector2 basePosition, float fontSize, Color color)
            => CreateText(basePosition, fontSize, color, TextAlignment.Left);

        public void ShowWindow()
        {
            Visible = true;
            _pressedButtonIndex = -1;
            _hoveredButtonIndex = -1;
            _hasCachedSnapshot = false;
            UpdateDisplayData();
            InvalidateStaticSurface();
            BringToFront();
            SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
            if (Scene != null)
            {
                Scene.FocusControl = this;
            }
        }

        public void HideWindow()
        {
            Visible = false;
            if (Scene != null && Scene.FocusControl == this)
            {
                Scene.FocusControl = null;
            }
        }

        public override void Dispose()
        {
            // Unsubscribe from attack speed changes
            if (_networkManager != null)
            {
                var characterState = _networkManager.GetCharacterState();
                if (characterState != null)
                {
                    characterState.AttackSpeedsChanged -= OnAttackSpeedsChanged;
                }
            }

            base.Dispose();
            _staticSurface?.Dispose();
            _staticSurface = null;
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            InvalidateStaticSurface();
        }

        private readonly struct CharacterInfoSnapshot : IEquatable<CharacterInfoSnapshot>
        {
            public CharacterInfoSnapshot(CharacterState state)
            {
                Name = state.Name ?? string.Empty;
                Class = state.Class;
                Level = state.Level;
                MasterLevel = state.MasterLevel;
                Experience = state.Experience;
                ExperienceForNextLevel = state.ExperienceForNextLevel;
                LevelUpPoints = state.LevelUpPoints;
                MasterLevelUpPoints = state.MasterLevelUpPoints;
                BaseStrength = state.Strength;
                AddedStrength = state.AddedStrength;
                BaseAgility = state.Agility;
                AddedAgility = state.AddedAgility;
                BaseVitality = state.Vitality;
                AddedVitality = state.AddedVitality;
                BaseEnergy = state.Energy;
                AddedEnergy = state.AddedEnergy;
                BaseLeadership = state.Leadership;
                AddedLeadership = state.AddedLeadership;
                CurrentHealth = state.CurrentHealth;
                MaxHealth = state.MaximumHealth;
                CurrentShield = state.CurrentShield;
                MaxShield = state.MaximumShield;
                CurrentMana = state.CurrentMana;
                MaxMana = state.MaximumMana;
                CurrentAbility = state.CurrentAbility;
                MaxAbility = state.MaximumAbility;

                var phys = GetPhysicalDamage(state);
                PhysicalMin = phys.min;
                PhysicalMax = phys.max;

                var magic = GetMagicalDamage(state);
                MagicalMin = magic.min;
                MagicalMax = magic.max;

                // Use AttackSpeed from CharacterState which includes equipment bonuses
                // (weapon speed, excellent options, wizard ring, etc.)
                AttackSpeed = state.AttackSpeed;

                // Use calculated values for other stats (server may not provide these)
                Defense = GetDefense(state);
                PvPAttackRate = GetPvPAttackRate(state);
                PvPDefenseRate = GetPvPDefenseRate(state);
                PvMAttackRate = GetPvMAttackRate(state);
                PvMDefenseRate = GetPvMDefenseRate(state);

                IsDarkLordFamily = state.Class == CharacterClassNumber.DarkLord || state.Class == CharacterClassNumber.LordEmperor;
                CanBeMaster = state.Class != CharacterClassNumber.DarkWizard;
            }

            public static CharacterInfoSnapshot Create(CharacterState state) => new(state);

            public readonly string Name;
            public readonly CharacterClassNumber Class;
            public readonly ushort Level;
            public readonly ushort MasterLevel;
            public readonly ulong Experience;
            public readonly ulong ExperienceForNextLevel;
            public readonly ushort LevelUpPoints;
            public readonly ushort MasterLevelUpPoints;
            public readonly ushort BaseStrength;
            public readonly ushort AddedStrength;
            public readonly ushort BaseAgility;
            public readonly ushort AddedAgility;
            public readonly ushort BaseVitality;
            public readonly ushort AddedVitality;
            public readonly ushort BaseEnergy;
            public readonly ushort AddedEnergy;
            public readonly ushort BaseLeadership;
            public readonly ushort AddedLeadership;
            public readonly uint CurrentHealth;
            public readonly uint MaxHealth;
            public readonly uint CurrentShield;
            public readonly uint MaxShield;
            public readonly uint CurrentMana;
            public readonly uint MaxMana;
            public readonly uint CurrentAbility;
            public readonly uint MaxAbility;
            public readonly int PhysicalMin;
            public readonly int PhysicalMax;
            public readonly int MagicalMin;
            public readonly int MagicalMax;
            public readonly int AttackSpeed;
            public readonly int Defense;
            public readonly int PvPAttackRate;
            public readonly int PvPDefenseRate;
            public readonly int PvMAttackRate;
            public readonly int PvMDefenseRate;
            public readonly bool IsDarkLordFamily;
            public readonly bool CanBeMaster;

            public bool HasPhysicalDamage => PhysicalMin > 0 || PhysicalMax > 0;
            public bool HasMagicalDamage => MagicalMin > 0 || MagicalMax > 0;

            public ushort GetBaseStat(int index) => index switch
            {
                0 => BaseStrength,
                1 => BaseAgility,
                2 => BaseVitality,
                3 => BaseEnergy,
                4 => BaseLeadership,
                _ => 0
            };

            public ushort GetAddedStat(int index) => index switch
            {
                0 => AddedStrength,
                1 => AddedAgility,
                2 => AddedVitality,
                3 => AddedEnergy,
                4 => AddedLeadership,
                _ => 0
            };

            public bool Equals(CharacterInfoSnapshot other)
            {
                return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                       Class == other.Class &&
                       Level == other.Level &&
                       MasterLevel == other.MasterLevel &&
                       Experience == other.Experience &&
                       ExperienceForNextLevel == other.ExperienceForNextLevel &&
                       LevelUpPoints == other.LevelUpPoints &&
                       MasterLevelUpPoints == other.MasterLevelUpPoints &&
                       BaseStrength == other.BaseStrength &&
                       AddedStrength == other.AddedStrength &&
                       BaseAgility == other.BaseAgility &&
                       AddedAgility == other.AddedAgility &&
                       BaseVitality == other.BaseVitality &&
                       AddedVitality == other.AddedVitality &&
                       BaseEnergy == other.BaseEnergy &&
                       AddedEnergy == other.AddedEnergy &&
                       BaseLeadership == other.BaseLeadership &&
                       AddedLeadership == other.AddedLeadership &&
                       CurrentHealth == other.CurrentHealth &&
                       MaxHealth == other.MaxHealth &&
                       CurrentShield == other.CurrentShield &&
                       MaxShield == other.MaxShield &&
                       CurrentMana == other.CurrentMana &&
                       MaxMana == other.MaxMana &&
                       CurrentAbility == other.CurrentAbility &&
                       MaxAbility == other.MaxAbility &&
                       PhysicalMin == other.PhysicalMin &&
                       PhysicalMax == other.PhysicalMax &&
                       MagicalMin == other.MagicalMin &&
                       MagicalMax == other.MagicalMax &&
                       AttackSpeed == other.AttackSpeed &&
                       Defense == other.Defense &&
                       PvPAttackRate == other.PvPAttackRate &&
                       PvPDefenseRate == other.PvPDefenseRate &&
                       PvMAttackRate == other.PvMAttackRate &&
                       PvMDefenseRate == other.PvMDefenseRate &&
                       IsDarkLordFamily == other.IsDarkLordFamily &&
                       CanBeMaster == other.CanBeMaster;
            }
        }

        private static (int min, int max) GetPhysicalDamage(CharacterState state)
        {
            if (state == null) return (0, 0);

            var str = state.TotalStrength;
            var agi = state.TotalAgility;
            var ene = state.TotalEnergy;

            return state.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    (str / 6, str / 4),
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    ((str + agi * 2) / 14, (str + agi * 2) / 8),
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    ((str * 2 + ene) / 12, (str * 2 + ene) / 8),
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    ((str * 2 + ene) / 14, (str * 2 + ene) / 10),
                CharacterClassNumber.RageFighter or CharacterClassNumber.FistMaster =>
                    (str / 6 + state.TotalVitality / 10, str / 4 + state.TotalVitality / 8),
                _ => (0, 0)
            };
        }

        private static (int min, int max) GetMagicalDamage(CharacterState state)
        {
            if (state == null) return (0, 0);

            var ene = state.TotalEnergy;

            return state.Class switch
            {
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    (ene / 9, ene / 4),
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    (ene / 9, ene / 4),
                CharacterClassNumber.Summoner or CharacterClassNumber.BloodySummoner or CharacterClassNumber.DimensionMaster =>
                    (ene / 5, ene / 2),
                _ => (0, 0)
            };
        }

        private static int GetDefense(CharacterState state)
        {
            if (state == null) return 0;

            var agi = state.TotalAgility;

            return state.Class switch
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

        private static int GetPvPAttackRate(CharacterState state)
        {
            if (state == null) return 0;

            var lvl = state.Level;
            var agi = state.TotalAgility;

            return state.Class switch
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

        private static int GetPvPDefenseRate(CharacterState state)
        {
            if (state == null) return 0;

            var lvl = state.Level;
            var agi = state.TotalAgility;

            return state.Class switch
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

        private static int GetPvMAttackRate(CharacterState state)
        {
            if (state == null) return 0;

            var lvl = state.Level;
            var agi = state.TotalAgility;
            var str = state.TotalStrength;
            var cmd = state.TotalLeadership;

            return state.Class switch
            {
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    (int)((lvl * 2 + agi) * 2.5f + str / 6 + cmd / 10),
                _ => (int)(lvl * 5 + agi * 1.5f + str / 4)
            };
        }

        private static int GetPvMDefenseRate(CharacterState state)
        {
            if (state == null) return 0;

            var agi = state.TotalAgility;

            return state.Class switch
            {
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    agi / 4,
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    agi / 7,
                _ => agi / 3
            };
        }
    }
}
