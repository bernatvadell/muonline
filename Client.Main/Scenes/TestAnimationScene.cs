using System.Collections.Immutable;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects.Vehicle;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets;


namespace Client.Main.Scenes;

public class TestAnimationScene : BaseScene
{
    enum TestAnimationUiState
    {
        Loading,
        EditCharacter,
        TestAction,
    }
    // Fields
    TestAnimationUiState _uiState = TestAnimationUiState.Loading;
    private TestAnimationUiState UiState
    {
        get => _uiState;
        set
        {
            if (_uiState == value)
            {
                return;
            }
            _uiState = value;
            RefreshUI();
            // TODO: refresh
        }
    }


    private const int TOTAL_PLAYER_ACTION_COUNT = 285;
    private int wing;
    public int Wing
    {
        get => wing;
        set => wing = value;
    }

    private int leftHand;
    public int LeftHand
    {
        get => leftHand;
        set => leftHand = value;
    }
    private int rightHand;
    public int RightHand
    {
        get => rightHand;
        set => rightHand = value;
    }
    private int pet;
    public int Pet
    {
        get => pet;
        set => pet = value;
    }
    private int armorSet;
    public int ArmorSet
    {
        get => armorSet;
        set => armorSet = value;
    }

    // CONTROLS
    private SelectWorld _selectWorld;

    private readonly ILogger<TestAnimationScene> _logger;
    private LoadingScreenControl _loadingScreen;
    // DIALOGS


    private readonly SelectOptionControl _selectWingOptionControl;

    private readonly SelectOptionControl _selectArmorOptionControl;
    private readonly SelectOptionControl _selectLeftHandOptionControl;
    private readonly SelectOptionControl _selectRightHandOptionControl;
    private readonly SelectOptionControl _selectCharacterClassOptionControl;
    private readonly SelectOptionControl _selectPetOptionControl;
    private readonly SelectOptionControl _selectVehicleOptionControl;
    private readonly OptionPickerControl _selectAnimationOptionControl;
    private readonly LabelButton _testAnimationButton;
    private readonly LabelButton _appearanceConfigButton;


    private bool _initialLoadComplete = false;

    private IEnumerable<KeyValuePair<string, int>> CharacterClasses;
    private IEnumerable<ItemDefinition> Wings;
    private IEnumerable<ItemDefinition> Armors;
    private IEnumerable<ItemDefinition> Pets;
    private IEnumerable<ItemDefinition> Weapons;
    private IEnumerable<VehicleDefinition> Vehicles;
    private List<KeyValuePair<string, int>> Actions; 

    private (string Name, PlayerClass Class, ushort Level, AppearanceConfig Appearance)? character
    {
        get
        {
            if (_selectCharacterClassOptionControl.Value == null)
            {
                return null;
            }
            int armorSetIndex = _selectArmorOptionControl.Value.HasValue ? Armors.ElementAt(_selectArmorOptionControl.Value.Value.Value).Id : 0xFFFF;
            ItemDefinition leftHand = _selectLeftHandOptionControl.Value.HasValue ? Weapons.ElementAt(_selectLeftHandOptionControl.Value.Value.Value) : null;
            int leftHandItemIndex = leftHand?.Id ?? 0xff;
            int leftHandItemGroupIndex = leftHand?.Group ?? 0xff;
            ItemDefinition rightHand = _selectRightHandOptionControl.Value.HasValue ? Weapons.ElementAt(_selectRightHandOptionControl.Value.Value.Value) : null;
            int rightHandItemIndex = rightHand?.Id ?? 0xff;
            int rightHandItemGroupIndex = rightHand?.Group ?? 0xff;
            ItemDefinition wing = _selectWingOptionControl.Value.HasValue ? Wings.ElementAt(_selectWingOptionControl.Value.Value.Value) : null;
            short wingIndex = -1;
            if (wing != null)
            {
                wingIndex = (short)wing.Id;
            }
            short vehicleIndex = -1;
            VehicleDefinition vehicle = _selectVehicleOptionControl.Value.HasValue ? Vehicles.ElementAt(_selectVehicleOptionControl.Value.Value.Value) : null;
            if (vehicle != null)
            {
                vehicleIndex = (short)vehicle.Id;
            }
            return (
                _selectCharacterClassOptionControl.Value.Value.Key ?? "",
                (PlayerClass)_selectCharacterClassOptionControl.Value.Value.Value,
                1,
                new AppearanceConfig()
                {
                    PlayerClass = (PlayerClass)_selectCharacterClassOptionControl.Value.Value.Value,
                    HelmItemIndex = armorSetIndex,
                    HelmItemLevel = 13,
                    HelmExcellent = true,
                    HelmAncient = true,
                    ArmorItemIndex = armorSetIndex,
                    ArmorItemLevel = 13,
                    ArmorExcellent = true,
                    ArmorAncient = true,
                    PantsItemIndex = armorSetIndex,
                    PantsItemLevel = 13,
                    PantsExcellent = true,
                    PantsAncient = true,
                    GlovesItemIndex = armorSetIndex,
                    GlovesItemLevel = 13,
                    GlovesExcellent = true,
                    GlovesAncient = true,
                    BootsItemIndex = armorSetIndex,
                    BootsItemLevel = 13,
                    BootsExcellent = true,
                    BootsAncient = true,
                    LeftHandItemIndex = (byte)leftHandItemIndex,
                    LeftHandItemGroup = (byte)leftHandItemGroupIndex,
                    LeftHandItemLevel = 13,
                    LeftHandExcellent = true,
                    LeftHandAncient = true,
                    RightHandItemIndex = (byte)rightHandItemIndex,
                    RightHandItemGroup = (byte)rightHandItemGroupIndex,
                    RightHandItemLevel = 13,
                    RightHandExcellent = true,
                    RightHandAncient = true,
                    WingInfo = new WingAppearance(13, 0, wingIndex),
                    RidingVehicle = vehicleIndex,
                }
            );
        }
    }

    // Constructors
    public TestAnimationScene()
    {

        _logger = MuGame.AppLoggerFactory.CreateLogger<TestAnimationScene>();

        CharacterClasses = new List<KeyValuePair<string, int>>([
            new(PlayerClass.DarkWizard.ToString(), (int)PlayerClass.DarkWizard),
            new(PlayerClass.SoulMaster.ToString(), (int)PlayerClass.SoulMaster),
            new(PlayerClass.GrandMaster.ToString(), (int)PlayerClass.GrandMaster),
            new(PlayerClass.SoulWizard.ToString(), (int)PlayerClass.SoulWizard),
            new(PlayerClass.DarkKnight.ToString(), (int)PlayerClass.DarkKnight),
            new(PlayerClass.BladeKnight.ToString(), (int)PlayerClass.BladeKnight),
            new(PlayerClass.BladeMaster.ToString(), (int)PlayerClass.BladeMaster),
            new(PlayerClass.DragonKnight.ToString(), (int)PlayerClass.DragonKnight),
            new(PlayerClass.FairyElf.ToString(), (int)PlayerClass.FairyElf),
            new(PlayerClass.MuseElf.ToString(), (int)PlayerClass.MuseElf),
            new(PlayerClass.HighElf.ToString(), (int)PlayerClass.HighElf),
            new(PlayerClass.NobleElf.ToString(), (int)PlayerClass.NobleElf),
            new(PlayerClass.MagicGladiator.ToString(), (int)PlayerClass.MagicGladiator),
            new(PlayerClass.DuelMaster.ToString(), (int)PlayerClass.DuelMaster),
            new(PlayerClass.MagicKnight.ToString(), (int)PlayerClass.MagicKnight),
            new(PlayerClass.DarkLord.ToString(), (int)PlayerClass.DarkLord),
            new(PlayerClass.LordEmperor.ToString(), (int)PlayerClass.LordEmperor),
            new(PlayerClass.EmpireLord.ToString(), (int)PlayerClass.EmpireLord),
            new(PlayerClass.Summoner.ToString(), (int)PlayerClass.Summoner),
            new(PlayerClass.BloodySummoner.ToString(), (int)PlayerClass.BloodySummoner),
            new(PlayerClass.DimensionMaster.ToString(), (int)PlayerClass.DimensionMaster),
            new(PlayerClass.DimensionSummoner.ToString(), (int)PlayerClass.DimensionSummoner),
            new(PlayerClass.RageFighter.ToString(), (int)PlayerClass.RageFighter),
            new(PlayerClass.FistMaster.ToString(), (int)PlayerClass.FistMaster),
            new(PlayerClass.FistBlazer.ToString(), (int)PlayerClass.FistBlazer),
            new(PlayerClass.GlowLancer.ToString(), (int)PlayerClass.GlowLancer),
            new(PlayerClass.MirageLancer.ToString(), (int)PlayerClass.MirageLancer),
            new(PlayerClass.ShiningLancer.ToString(), (int)PlayerClass.ShiningLancer),
            new(PlayerClass.RuneMage.ToString(), (int)PlayerClass.RuneMage),
            new(PlayerClass.RuneSpellMaster.ToString(), (int)PlayerClass.RuneSpellMaster),
            new(PlayerClass.GradRuneMaster.ToString(), (int)PlayerClass.GradRuneMaster),
            new(PlayerClass.MajesticRuneWizard.ToString(), (int)PlayerClass.MajesticRuneWizard),
            new(PlayerClass.Slayer.ToString(), (int)PlayerClass.Slayer),
            new(PlayerClass.RoyalSlayer.ToString(), (int)PlayerClass.RoyalSlayer),
            new(PlayerClass.MasterSlayer.ToString(), (int)PlayerClass.MasterSlayer),
            new(PlayerClass.Slaughterer.ToString(), (int)PlayerClass.Slaughterer),
            new(PlayerClass.GunCrusher.ToString(), (int)PlayerClass.GunCrusher),
            new(PlayerClass.GunBreaker.ToString(), (int)PlayerClass.GunBreaker),
            new(PlayerClass.MasterGunBreaker.ToString(), (int)PlayerClass.MasterGunBreaker),
            new(PlayerClass.HeistGunCrasher.ToString(), (int)PlayerClass.HeistGunCrasher),
            new(PlayerClass.WhiteWizard.ToString(), (int)PlayerClass.WhiteWizard),
            new(PlayerClass.LightMaster.ToString(), (int)PlayerClass.LightMaster),
            new(PlayerClass.ShineWizard.ToString(), (int)PlayerClass.ShineWizard),
            new(PlayerClass.ShineMaster.ToString(), (int)PlayerClass.ShineMaster),
            new(PlayerClass.Mage.ToString(), (int)PlayerClass.Mage),
            new(PlayerClass.WoMage.ToString(), (int)PlayerClass.WoMage),
            new(PlayerClass.ArchMage.ToString(), (int)PlayerClass.ArchMage),
            new(PlayerClass.MysticMage.ToString(), (int)PlayerClass.MysticMage),
            new(PlayerClass.IllusionKnight.ToString(), (int)PlayerClass.IllusionKnight),
            new(PlayerClass.MirageKnight.ToString(), (int)PlayerClass.MirageKnight),
            new(PlayerClass.IllusionMaster.ToString(), (int)PlayerClass.IllusionMaster),
            new(PlayerClass.MysticKnight.ToString(), (int)PlayerClass.MysticKnight),
            new(PlayerClass.Alchemist.ToString(), (int)PlayerClass.Alchemist),
            new(PlayerClass.AlchemicMaster.ToString(), (int)PlayerClass.AlchemicMaster),
            new(PlayerClass.AlchemicForce.ToString(), (int)PlayerClass.AlchemicForce),
            new(PlayerClass.Creator.ToString(), (int)PlayerClass.Creator),
        ]);
        Armors = ItemDatabase.GetArmors();
        Weapons = ItemDatabase.GetWeapons();
        Wings = ItemDatabase.GetWings();
        Vehicles = [
            new VehicleDefinition {
                Id = -1,
                Name = "Unset",
            },
            ..VehicleDatabase.VehicleList.Values.ToList()
        ];

        _loadingScreen = new LoadingScreenControl { Visible = true, Message = "Loading Scene" };
        Controls.Add(_loadingScreen);

        Controls.Add(_selectCharacterClassOptionControl = new SelectOptionControl()
        {
            Text = "Select Class",
            Placeholder = "Select Class",
            X = 10 + 5,
            Y = 10,
        });
        _selectCharacterClassOptionControl.ValueChanged += HandleChangeCharacterClass;
        _selectCharacterClassOptionControl.OptionPickerVisibleChanged += HandleChangeCharacterClassOptionPickerVisible;

        Controls.Add(_selectArmorOptionControl = new SelectOptionControl()
        {
            Text = "Select Armor",
            Placeholder = "Select Armor",
            X = 180 + 30 + 10 + 5,
            Y = 10,
        });
        _selectArmorOptionControl.ValueChanged += HandleChangeArmorSet;

        Controls.Add(_selectLeftHandOptionControl = new SelectOptionControl()
        {
            Text = "Select Weapon Left",
            Placeholder = "Select Weapon Left",
            X = (180 + 30) * 2 + 10 + 5,
            Y = 10,
        });
        _selectLeftHandOptionControl.ValueChanged += HandleChangeLeftHand;

        Controls.Add(_selectRightHandOptionControl = new SelectOptionControl()
        {
            Text = "Select Weapon Right",
            Placeholder = "Select Weapon Right",
            X = (180 + 30) * 3 + 10 + 5,
            Y = 10,
        });
        _selectRightHandOptionControl.ValueChanged += HandleChangeRightHand;

        Controls.Add(_selectWingOptionControl = new SelectOptionControl()
        {
            Text = "Select Wing",
            Placeholder = "Select Wing",
            X = (180 + 30) * 4 + 10 + 5,
            Y = 10,
        });
        _selectWingOptionControl.ValueChanged += HandleChangeWing;

        Controls.Add(_selectPetOptionControl = new SelectOptionControl()
        {
            Text = "Select Pet",
            Placeholder = "Select Pet",
            X = (180 + 30) * 5 + 10 + 5,
            Y = 10,
        });
        _selectPetOptionControl.ValueChanged += HandleChangePet;

        Controls.Add(_selectVehicleOptionControl = new SelectOptionControl()
        {
            Text = "Select Vehicle",
            Placeholder = "Select Vehicle",
            ButtonAlign = ControlAlign.Top,
            Y = 10 + 29 + 10,
            X = 15,
        });
        _selectVehicleOptionControl.ValueChanged += HandleChangeVehicle;


        Controls.Add(_appearanceConfigButton = new LabelButton
        {
            Label = new LabelControl
            {
                Text = "Appearance Config",
                X = 8,
                Align = ControlAlign.VerticalCenter,
            },
            Align = ControlAlign.Bottom,
            Margin = new Margin { Bottom = 10 },
            X = 25,
        });
        _appearanceConfigButton.Click += HandleGoToAppearanceConfigButtonClick;

        Controls.Add(_testAnimationButton = new LabelButton
        {
            Label = new LabelControl
            {
                Text = "Test Actions",
                X = 8,
                Align = ControlAlign.VerticalCenter,
            },
            Align = ControlAlign.Bottom,
            Margin = new Margin { Bottom = 10 },
            X = 180 + 25 + 30,
        });
        _testAnimationButton.Click += HandleGoToTestAnimationButtonClick;
        Controls.Add(_selectAnimationOptionControl = new OptionPickerControl
        {
            Align = ControlAlign.Right,
            Options = new(),
            ItemsVisible = 21,
        });
        _selectAnimationOptionControl.ListItemWidth = 320;
        _selectAnimationOptionControl.ValueChanged += HandleChangeAnimation;
        
        Actions = new List<KeyValuePair<string, int>>(TOTAL_PLAYER_ACTION_COUNT);
        Actions.AddRange(
            Enumerable.Range(0, TOTAL_PLAYER_ACTION_COUNT)
                .Select(i => new KeyValuePair<string, int>($"{i}_{(PlayerAction)i}", i)
            )
        );

        _loadingScreen.BringToFront();
    }


    private void RefreshUI()
    {
        switch (UiState)
        {
            case TestAnimationUiState.Loading:
                {
                    break;
                }
            case TestAnimationUiState.EditCharacter:
                {
                    if (_loadingScreen != null)
                    _loadingScreen.Visible = false;
                    _selectCharacterClassOptionControl.Options = CharacterClasses.ToList();
                    _selectCharacterClassOptionControl.Visible = true;
                    _selectArmorOptionControl.Options = Armors.Select((p, i) => new KeyValuePair<string, int>(p.Name, i)).ToList();
                    _selectArmorOptionControl.Visible = true;
                    _selectLeftHandOptionControl.Options = Weapons.Select((p, i) => new KeyValuePair<string, int>(p.Name, i)).ToList();
                    _selectLeftHandOptionControl.Visible = true;
                    _selectRightHandOptionControl.Options = Weapons.Select((p, i) => new KeyValuePair<string, int>(p.Name, i)).ToList();
                    _selectRightHandOptionControl.Visible = true;
                    _selectWingOptionControl.Options = Wings.Select((p, i) => new KeyValuePair<string, int>(p.Name, i)).ToList();
                    _selectWingOptionControl.Visible = true;
                    _selectPetOptionControl.Visible = true;
                    _selectVehicleOptionControl.Options = Vehicles.Select((p, i) => new KeyValuePair<string, int>(p.Name, i)).ToList();
                    _selectVehicleOptionControl.Visible = true;
                    _selectAnimationOptionControl.Visible = false;
                    break;
                }
            case TestAnimationUiState.TestAction:
                {
                    if (_loadingScreen != null)
                    _loadingScreen.Visible = false;
                    _selectCharacterClassOptionControl.Visible = false;
                    _selectCharacterClassOptionControl.HideOptionPicker();
                    _selectArmorOptionControl.Visible = false;
                    _selectArmorOptionControl.HideOptionPicker();
                    _selectLeftHandOptionControl.Visible = false;
                    _selectLeftHandOptionControl.HideOptionPicker();
                    _selectRightHandOptionControl.Visible = false;
                    _selectRightHandOptionControl.HideOptionPicker();
                    _selectWingOptionControl.Visible = false;
                    _selectWingOptionControl.HideOptionPicker();
                    _selectPetOptionControl.Visible = false;
                    _selectPetOptionControl.HideOptionPicker();
                    _selectVehicleOptionControl.Visible = false;
                    _selectVehicleOptionControl.HideOptionPicker();
                    _selectAnimationOptionControl.Options = Actions.Select((p, i) => new KeyValuePair<string, int>(p.Key, p.Value)).ToList();
                    _selectAnimationOptionControl.Visible = true;
                    break;
                }
        }
    }

    private void UpdateLoadProgress(string message, float progress)
    {
        MuGame.ScheduleOnMainThread(() =>
        {
            if (_loadingScreen != null && _loadingScreen.Visible)
            {
                _loadingScreen.Message = message;
                _loadingScreen.Progress = progress;
            }
        });
    }




    protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
    {
        UpdateLoadProgress("Initializing Character Selection...", 0.0f);
        _logger.LogInformation(">>> TestAnimationScene LoadSceneContentWithProgress starting...");

        try
        {
            UpdateLoadProgress("Creating Select World...", 0.05f);
            _selectWorld = new SelectWorld();
            Controls.Add(_selectWorld);

            UpdateLoadProgress("Initializing Select World (Graphics)...", 0.1f);
            await _selectWorld.Initialize();
            World = _selectWorld;
            UpdateLoadProgress("Select World Initialized.", 0.35f); // Zwiększony postęp po inicjalizacji świata
            _logger.LogInformation("--- TestAnimationScene: SelectWorld initialized and set.");

            if (_selectWorld.Terrain != null)
            {
                _selectWorld.Terrain.AmbientLight = 0.6f;
            }
            UiState = TestAnimationUiState.EditCharacter;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "!!! TestAnimationScene: Error during world initialization or character creation.");
            UpdateLoadProgress("Error loading character selection.", 1.0f);
        }
        finally
        {
            _initialLoadComplete = true;
            UpdateLoadProgress("Character Selection Ready.", 1.0f);
            _logger.LogInformation("<<< TestAnimationScene LoadSceneContentWithProgress finished.");
        }
    }

    public override void AfterLoad()
    {
        base.AfterLoad();
        _logger.LogInformation("TestAnimationScene.AfterLoad() called.");
        if (_loadingScreen != null)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_loadingScreen != null)
                {
                    Controls.Remove(_loadingScreen);
                    _loadingScreen.Dispose();
                    _loadingScreen = null;
                    Cursor?.BringToFront();
                    DebugPanel?.BringToFront();
                }
            });
        }
    }

    protected override void OnScreenSizeChanged()
    {
        base.OnScreenSizeChanged();
    }

    public override async Task Load()
    {
        if (Status == GameControlStatus.Initializing)
        {
            await LoadSceneContentWithProgress(UpdateLoadProgress);
        }
        else
        {
            _logger.LogDebug("TestAnimationScene.Load() called outside of InitializeWithProgressReporting flow. Re-routing to progressive load.");
            await LoadSceneContentWithProgress(UpdateLoadProgress);
        }
    }


    public override void Dispose()
    {
        _logger.LogDebug("Disposing TestAnimationScene.");
        if (_loadingScreen != null)
        {
            Controls.Remove(_loadingScreen);
            _loadingScreen.Dispose();
            _loadingScreen = null;
        }
        base.Dispose();
    }




    public override void Update(GameTime gameTime)
    {
        if (_loadingScreen != null && _loadingScreen.Visible)
        {
            _loadingScreen.Update(gameTime);
            Cursor?.Update(gameTime);
            DebugPanel?.Update(gameTime);
            return;
        }
        if (!_initialLoadComplete && Status == GameControlStatus.Initializing)
        {
            Cursor?.Update(gameTime);
            DebugPanel?.Update(gameTime);
            return;
        }
        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
    }

    private void RefreshCharacter()
    {
        if (!character.HasValue)
        {
            _ = _selectWorld.CreateCharacterObjects(new List<(string Name, PlayerClass Class, ushort Level, AppearanceConfig Appearance)>());
            return;
        }
        _ = _selectWorld.CreateCharacterObjects([character.Value]);
    }

    public void HandleChangeCharacterClass(object sender, KeyValuePair<string, int> newCharacter)
    {
        _selectArmorOptionControl.Value = null;

        // _selectArmorOptionControl.Options = 
        // Rebuild the class
        RefreshCharacter();

    }
    public void HandleChangeCharacterClassOptionPickerVisible(object sender, bool isShowPicker)
    {
        _selectVehicleOptionControl.Visible = !isShowPicker;
        _selectVehicleOptionControl.HideOptionPicker();
    }
    public void HandleChangeArmorSet(object sender, KeyValuePair<string, int> armor)
    {
        RefreshCharacter();
    }
    public void HandleChangeWing(object sender, KeyValuePair<string, int> wing)
    {
        RefreshCharacter();
    }
    public void HandleChangeLeftHand(object sender, KeyValuePair<string, int> weaponL)
    {
        RefreshCharacter();
    }
    public void HandleChangeRightHand(object sender, KeyValuePair<string, int> weaponR)
    {
        RefreshCharacter();
    }
    public void HandleChangePet(object sender, KeyValuePair<string, int> newPet)
    {
        Pet = newPet.Value;
    }
    public void HandleChangeVehicle(object sender, KeyValuePair<string, int> newVehicle)
    {
        VehicleDefinition vehicle = Vehicles.ElementAt(newVehicle.Value);
        if (vehicle.Id < 0)
        {
            _selectVehicleOptionControl.ClearValue();
        }
        RefreshCharacter();
    }

    private void HandleGoToTestAnimationButtonClick(object sender, EventArgs e)
    {
        UiState = TestAnimationUiState.TestAction;
    }

    private void HandleGoToAppearanceConfigButtonClick(object sender, EventArgs e)
    {
        UiState = TestAnimationUiState.EditCharacter;
    }

    private void HandleChangeAnimation(object sender, KeyValuePair<string, int>? newAnimation)
    {
        if (!newAnimation.HasValue) return; 
        _selectWorld.PlayEmoteAnimation((PlayerAction)newAnimation.Value.Value);
    }
    
}
