using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Models;

using Client.Main.Objects;
using Client.Main.Objects.Wings;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets; // CharacterClassNumber enum
using System.Threading.Tasks;
using Client.Main.Core.Utilities;
using Client.Main.Networking.Services;
using Client.Main.Networking;
using System;
using Client.Data.ATT;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers;
using Client.Main.Helpers;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Objects.Vehicle;

namespace Client.Main.Objects.Player
{
    /// <summary>High-level movement states.</summary>
    public enum MovementMode
    {
        Walk,
        Fly,
        Swim
    }

    public class PlayerObject : WalkerObject
    {
        protected override bool RequiresPerFrameAnimation => IsMainWalker;
        private CharacterClassNumber _characterClass;
        // Cached gender flag – avoids evaluating gender every frame
        private bool _isFemale;

        public PlayerMaskHelmObject HelmMask { get; private set; }
        public PlayerHelmObject Helm { get; private set; }
        public PlayerArmorObject Armor { get; private set; }
        public PlayerPantObject Pants { get; private set; }
        public PlayerGloveObject Gloves { get; private set; }
        public PlayerBootObject Boots { get; private set; }
        public WeaponObject Weapon1 { get; private set; }
        public WeaponObject Weapon2 { get; private set; }
        public WingObject EquippedWings { get; private set; }

        public VehicleObject Vehicle { get; private set; }

        private const int LeftHandBoneIndex = 33;
        private const int RightHandBoneIndex = 42;
        private const int BackWeaponBoneIndex = 47; // Same anchor used by wings

        // Safe-zone weapon placement (editable XYZ values for clarity)
        private static readonly Vector3[] WeaponHolsterOffsets =
        {
            new(-20f, 8f, 50f), // Weapon 1 (left hand) XYZ offset
            new(20f, 8f, 50f)  // Weapon 2 (right hand) XYZ offset
        };

        private static readonly Vector3[] WeaponHolsterRotationDegrees =
        {
            new(60f, 0f, 90f), // Weapon 1 (left hand) rotation in degrees (X,Y,Z)
            new(60f, 0f, -90f)  // Weapon 2 (right hand) rotation in degrees (X,Y,Z)
        };

        private bool _weaponsHolstered;

        // Vehicle/mount state
        private bool _isRiding;
        private short _currentVehicleIndex = -1;
        private float _currentRiderHeightOffset = 0f;

        private int _lastEquipmentAnimationStride = -1;

        // Timer for footstep sound playback
        private float _footstepTimer;

        // ───────────────────────────────── PROPERTIES ─────────────────────────────────
        public bool IsResting { get; set; }
        public bool IsSitting { get; set; }
        public Vector2? RestPlaceTarget { get; set; }
        public Vector2? SitPlaceTarget { get; set; }

        public string Name { get; set; } = "Character";
        public override string DisplayName => Name;

        private readonly CharacterService _characterService;
        private readonly NetworkManager _networkManager;

        public PlayerEquipment Equipment { get; } = new PlayerEquipment();

        public AppearanceData Appearance { get; private set; }

        public CharacterClassNumber CharacterClass
        {
            get => _characterClass;
            set
            {
                if (_characterClass != value)
                {
                    if (_logger?.IsEnabled(LogLevel.Debug) == true)
                        _logger.LogDebug($"PlayerObject {Name}: class {_characterClass} → {value}");
                    _characterClass = value;
                    _isFemale = PlayerActionMapper.IsCharacterFemale(value);
                }
            }
        }

        /// <summary>True if wings are equipped and visible.</summary>
        public bool HasEquippedWings => EquippedWings is { Hidden: false, Type: > 0 };

        public new PlayerAction CurrentAction
        {
            get => (PlayerAction)base.CurrentAction;
            set => base.CurrentAction = (int)value;
        }

        public PlayerAction SelectedAttackAction { get; set; } = PlayerAction.PlayerAttackDeathstab;

        // Events
        public event EventHandler PlayerMoved;
        public event EventHandler PlayerTookDamage;

        // ───────────────────────────────── CONSTRUCTOR ─────────────────────────────────
        public PlayerObject(AppearanceData appearance = default)
        {
            _logger = AppLoggerFactory?.CreateLogger(GetType());
            HelmMask = new PlayerMaskHelmObject { LinkParentAnimation = true, Hidden = true };
            Helm = new PlayerHelmObject { LinkParentAnimation = true };
            Armor = new PlayerArmorObject { LinkParentAnimation = true };
            Pants = new PlayerPantObject { LinkParentAnimation = true };
            Gloves = new PlayerGloveObject { LinkParentAnimation = true };
            Boots = new PlayerBootObject { LinkParentAnimation = true };
            Weapon1 = new WeaponObject { };
            Weapon2 = new WeaponObject { };
            EquippedWings = new WingObject { LinkParentAnimation = true, Hidden = true };
            Vehicle = new VehicleObject { Hidden = true };

            Children.Add(HelmMask);
            Children.Add(Helm);
            Children.Add(Armor);
            Children.Add(Pants);
            Children.Add(Gloves);
            Children.Add(Boots);
            Children.Add(Weapon1);
            Children.Add(Weapon2);
            Children.Add(EquippedWings);
            Children.Add(Vehicle);

            // Enable mouse hover interactions so the name is shown
            Interactive = true;
            BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 120));

            Scale = 0.85f;
            AnimationSpeed = 10f;
            CurrentAction = PlayerAction.PlayerStopMale;
            _characterClass = CharacterClassNumber.DarkWizard;
            _isFemale = PlayerActionMapper.IsCharacterFemale(_characterClass);

            _networkManager = MuGame.Network;
            _characterService = _networkManager?.GetCharacterService();

            Appearance = appearance;
        }

        // ───────────────────────────────── LOADING ─────────────────────────────────
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");

            if (IsMainWalker)
            {
                // First, load the base body for the current class
                var charState = _networkManager.GetCharacterState();
                CharacterClass = (CharacterClassNumber)charState.Class;
                await UpdateBodyPartClassesAsync();

                // Then, hook events to update equipment based on inventory
                HookInventoryEvents();
                // Perform the initial appearance update and wait for it to complete
                await UpdateAppearanceFromInventoryAsync();
            }
            else
            {
                // Remote players use AppearanceData
                await UpdateBodyPartClassesAsync();
                await UpdateEquipmentAppearanceAsync();
            }

            await base.Load();

            // Idle actions play at half speed so the character breathes naturally
            SetActionSpeed(PlayerAction.PlayerStopMale, 0.5f);
            SetActionSpeed(PlayerAction.PlayerStopFemale, 0.5f);
            SetActionSpeed(PlayerAction.PlayerStopFly, 0.5f);

            // Fenrir riding animations need to be faster (2x speed)
            SetActionSpeed(PlayerAction.PlayerFenrirRun, 2.0f);
            SetActionSpeed(PlayerAction.PlayerFenrirStand, 1.0f);

            UpdateWorldBoundingBox();
        }

        public async Task Load(PlayerClass playerClass)
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");

            if (IsMainWalker)
            {
                // First, load the base body for the current class
                await UpdateBodyPartClassesAsync(playerClass);

                // Then, hook events to update equipment based on inventory
                HookInventoryEvents();
                // Perform the initial appearance update and wait for it to complete
                await UpdateAppearanceFromInventoryAsync();
            }
            else
            {
                // Remote players use AppearanceData
                await UpdateBodyPartClassesAsync(playerClass);
                await UpdateEquipmentAppearanceAsync();
            }

            await base.Load();

            // Idle actions play at half speed so the character breathes naturally
            SetActionSpeed(PlayerAction.PlayerStopMale, 0.5f);
            SetActionSpeed(PlayerAction.PlayerStopFemale, 0.5f);
            SetActionSpeed(PlayerAction.PlayerStopFly, 0.5f);

            // Fenrir riding animations need to be faster (2x speed)
            SetActionSpeed(PlayerAction.PlayerFenrirRun, 2.0f);
            SetActionSpeed(PlayerAction.PlayerFenrirStand, 2.0f);

            UpdateWorldBoundingBox();
        }



        private void HookInventoryEvents()
        {
            if (_networkManager != null)
            {
                // Subscribe to equipment-only changes to avoid reloading on pure inventory grid moves
                _networkManager.GetCharacterState().EquipmentChanged += OnEquipmentChanged;
            }
        }

        private void UnhookInventoryEvents()
        {
            if (_networkManager == null) return;
            try
            {
                _networkManager.GetCharacterState().EquipmentChanged -= OnEquipmentChanged;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to unhook inventory events. This may happen on shutdown.");
            }
        }

        private void OnEquipmentChanged()
        {
            if (!IsMainWalker) return;
            // Fire-and-forget the async update method
            _ = UpdateAppearanceFromInventoryAsync();
        }

        private static class InventoryConstants
        {
            public const byte LeftHandSlot = 0;
            public const byte RightHandSlot = 1;
            public const byte HelmSlot = 2;
            public const byte ArmorSlot = 3;
            public const byte PantsSlot = 4;
            public const byte GlovesSlot = 5;
            public const byte BootsSlot = 6;
            public const byte WingsSlot = 7;
            public const byte PetSlot = 8;
        }

        private async Task UpdateAppearanceFromInventoryAsync()
        {
            if (_networkManager == null) return;

            var charState = _networkManager.GetCharacterState();
            var inventory = charState.GetInventoryItems();

            // Ensure base body model is correct for the character's class
            var newClass = (CharacterClassNumber)charState.Class;
            if (CharacterClass != newClass)
            {
                CharacterClass = newClass;
            }

            // ALWAYS load the default class-specific body parts first.
            // This will clear shader properties and load defaults
            await UpdateBodyPartClassesAsync();

            ItemDefinition GetItemDef(byte slot)
            {
                return inventory.TryGetValue(slot, out var itemData)
                    ? ItemDatabase.GetItemDefinition(itemData)
                    : null;
            }

            // --- Overwrite with equipped items ---

            // Helm
            var helmDef = GetItemDef(InventoryConstants.HelmSlot);
            if (helmDef != null)
            {
                await LoadPartAsync(Helm, helmDef.TexturePath?.Replace("Item/", "Player/"));
                SetItemProperties(Helm, inventory[InventoryConstants.HelmSlot]);
            }

            // Armor
            var armorDef = GetItemDef(InventoryConstants.ArmorSlot);
            if (armorDef != null)
            {
                await LoadPartAsync(Armor, armorDef.TexturePath?.Replace("Item/", "Player/"));
                SetItemProperties(Armor, inventory[InventoryConstants.ArmorSlot]);
            }

            // Pants
            var pantsDef = GetItemDef(InventoryConstants.PantsSlot);
            if (pantsDef != null)
            {
                await LoadPartAsync(Pants, pantsDef.TexturePath?.Replace("Item/", "Player/"));
                SetItemProperties(Pants, inventory[InventoryConstants.PantsSlot]);
            }

            // Gloves
            var glovesDef = GetItemDef(InventoryConstants.GlovesSlot);
            if (glovesDef != null)
            {
                await LoadPartAsync(Gloves, glovesDef.TexturePath?.Replace("Item/", "Player/"));
                SetItemProperties(Gloves, inventory[InventoryConstants.GlovesSlot]);
            }

            // Boots
            var bootsDef = GetItemDef(InventoryConstants.BootsSlot);
            if (bootsDef != null)
            {
                await LoadPartAsync(Boots, bootsDef.TexturePath?.Replace("Item/", "Player/"));
                SetItemProperties(Boots, inventory[InventoryConstants.BootsSlot]);
            }

            // Wings
            var wingsDef = GetItemDef(InventoryConstants.WingsSlot);
            if (wingsDef != null && !string.IsNullOrEmpty(wingsDef.TexturePath))
            {
                EquippedWings.Hidden = false;
                EquippedWings.Type = (short)(wingsDef.Id + 1);
                EquippedWings.LinkParentAnimation = false;
            }
            else
            {
                EquippedWings.Hidden = true;
            }

            // Left Hand
            var leftHandDef = GetItemDef(InventoryConstants.LeftHandSlot);
            if (leftHandDef != null)
            {
                Weapon1.Model = await BMDLoader.Instance.Prepare(leftHandDef.TexturePath);
                Weapon1.LinkParentAnimation = false;
                SetItemProperties(Weapon1, inventory[InventoryConstants.LeftHandSlot]);
                RefreshWeaponAttachment(Weapon1, isLeftHand: true);
            }
            else
            {
                Weapon1.Model = null;
            }

            // Right Hand
            var rightHandDef = GetItemDef(InventoryConstants.RightHandSlot);
            if (rightHandDef != null)
            {
                Weapon2.Model = await BMDLoader.Instance.Prepare(rightHandDef.TexturePath);
                Weapon2.LinkParentAnimation = false;
                SetItemProperties(Weapon2, inventory[InventoryConstants.RightHandSlot]);
                RefreshWeaponAttachment(Weapon2, isLeftHand: false);
            }
            else
            {
                Weapon2.Model = null;
            }
        }

        private async Task UpdateEquipmentAppearanceAsync()
        {
            if (Appearance.RawData.IsEmpty) return; // No appearance data to process

            // Update CharacterClass based on appearance data
            //CharacterClass = Appearance.CharacterClass; // TODO: Wrong character class?

            // Helm
            if (Appearance.HelmItemIndex != 255)
            {
                var helmDef = ItemDatabase.GetItemDefinition(7, Appearance.HelmItemIndex);
                if (helmDef?.TexturePath != null)
                {
                    await LoadPartAsync(Helm, helmDef.TexturePath.Replace("Item/", "Player/"));
                }

                // Apply item properties for shader effects
                Helm.ItemLevel = Appearance.HelmItemLevel;
                Helm.IsExcellentItem = Appearance.HelmExcellent;
                Helm.IsAncientItem = Appearance.HelmAncient;
            }
            // Armor
            if (Appearance.ArmorItemIndex != 255)
            {
                var armorDef = ItemDatabase.GetItemDefinition(8, Appearance.ArmorItemIndex);
                if (armorDef?.TexturePath != null)
                {
                    await LoadPartAsync(Armor, armorDef.TexturePath.Replace("Item/", "Player/"));
                }

                // Apply item properties for shader effects
                Armor.ItemLevel = Appearance.ArmorItemLevel;
                Armor.IsExcellentItem = Appearance.ArmorExcellent;
                Armor.IsAncientItem = Appearance.ArmorAncient;
            }

            // Pants
            if (Appearance.PantsItemIndex != 255)
            {
                var pantsDef = ItemDatabase.GetItemDefinition(9, Appearance.PantsItemIndex);
                if (pantsDef?.TexturePath != null)
                {
                    await LoadPartAsync(Pants, pantsDef.TexturePath.Replace("Item/", "Player/"));
                }

                // Apply item properties for shader effects
                Pants.ItemLevel = Appearance.PantsItemLevel;
                Pants.IsExcellentItem = Appearance.PantsExcellent;
                Pants.IsAncientItem = Appearance.PantsAncient;
            }

            // Gloves
            if (Appearance.GlovesItemIndex != 255)
            {
                var glovesDef = ItemDatabase.GetItemDefinition(10, Appearance.GlovesItemIndex);
                if (glovesDef?.TexturePath != null)
                {
                    var playerTexturePath = glovesDef.TexturePath.Replace("Item/", "Player/");
                    _logger?.LogInformation($"[PlayerObject] Loading gloves: Group=10, ID={Appearance.GlovesItemIndex}, ItemTexturePath={glovesDef.TexturePath}, PlayerTexturePath={playerTexturePath}");
                    await LoadPartAsync(Gloves, playerTexturePath);
                }
                else
                {
                    _logger?.LogWarning($"[PlayerObject] No gloves definition found for Group=10, ID={Appearance.GlovesItemIndex}");
                }

                // Apply item properties for shader effects
                Gloves.ItemLevel = Appearance.GlovesItemLevel;
                Gloves.IsExcellentItem = Appearance.GlovesExcellent;
                Gloves.IsAncientItem = Appearance.GlovesAncient;
            }

            // Boots
            if (Appearance.BootsItemIndex != 255)
            {
                var bootsDef = ItemDatabase.GetItemDefinition(11, Appearance.BootsItemIndex);
                if (bootsDef?.TexturePath != null)
                {
                    var playerTexturePath = bootsDef.TexturePath.Replace("Item/", "Player/");
                    _logger?.LogInformation($"[PlayerObject] Loading boots: Group=11, ID={Appearance.BootsItemIndex}, ItemTexturePath={bootsDef.TexturePath}, PlayerTexturePath={playerTexturePath}");
                    await LoadPartAsync(Boots, playerTexturePath);
                }
                else
                {
                    _logger?.LogWarning($"[PlayerObject] No boots definition found for Group=11, ID={Appearance.BootsItemIndex}");
                }

                // Apply item properties for shader effects
                Boots.ItemLevel = Appearance.BootsItemLevel;
                Boots.IsExcellentItem = Appearance.BootsExcellent;
                Boots.IsAncientItem = Appearance.BootsAncient;
            }

            // Wings
            if (Appearance.WingInfo.HasWings)
            {
                EquippedWings.Type = (short)(Appearance.WingInfo.Type + Appearance.WingInfo.Level + 1);
                EquippedWings.Hidden = false;
                EquippedWings.LinkParentAnimation = false;
            }
            else
            {
                EquippedWings.Hidden = true;
            }
            // Weapons
            // This requires more sophisticated logic to determine the exact weapon model
            // based on item group, index, and potentially other flags.
            // For now, we'll use generic models if an item is equipped.
            if (Appearance.LeftHandItemIndex != 255 && Appearance.LeftHandItemIndex != 0xFF)
            {
                var leftHandDef = ItemDatabase.GetItemDefinition(Appearance.LeftHandItemGroup, Appearance.LeftHandItemIndex);
                if (leftHandDef != null)
                {
                    Weapon1.Model = await BMDLoader.Instance.Prepare(leftHandDef.TexturePath);
                    Weapon1.LinkParentAnimation = false;

                    // Apply item properties for shader effects
                    Weapon1.ItemLevel = Appearance.LeftHandItemLevel;
                    Weapon1.IsExcellentItem = Appearance.LeftHandExcellent;
                    Weapon1.IsAncientItem = Appearance.LeftHandAncient;
                    RefreshWeaponAttachment(Weapon1, isLeftHand: true);
                }
                else
                {
                    Weapon1.Model = null;
                }
            }
            else
            {
                Weapon1.Model = null;
            }

            if (Appearance.RightHandItemIndex != 255 && Appearance.RightHandItemIndex != 0xFF)
            {
                var rightHandDef = ItemDatabase.GetItemDefinition(Appearance.RightHandItemGroup, Appearance.RightHandItemIndex);
                if (rightHandDef != null)
                {
                    Weapon2.Model = await BMDLoader.Instance.Prepare(rightHandDef.TexturePath);
                    Weapon2.LinkParentAnimation = false;

                    // Apply item properties for shader effects
                    Weapon2.ItemLevel = Appearance.RightHandItemLevel;
                    Weapon2.IsExcellentItem = Appearance.RightHandExcellent;
                    Weapon2.IsAncientItem = Appearance.RightHandAncient;
                    RefreshWeaponAttachment(Weapon2, isLeftHand: false);
                }
                else
                {
                    Weapon2.Model = null;
                }
            }
            else
            {
                Weapon2.Model = null;
            }
        }
        public async Task UpdateEquipmentAppearanceFromConfig(AppearanceConfig appearanceConfig)
        {
            if (appearanceConfig == null) return; // No appearance data to process

            // Update CharacterClass based on appearance data
            //CharacterClass = Appearance.CharacterClass; // TODO: Wrong character class?
            // Update player class
            // await UpdateBodyPartClassesAsync(appearanceConfig.PlayerClass);

            // Helm
            if (appearanceConfig.HelmItemIndex != 0XFFFF)
            {
                var helmDef = ItemDatabase.GetItemDefinition(7, (short)appearanceConfig.HelmItemIndex);
                if (helmDef?.TexturePath != null)
                {
                    string helmTexturePath = helmDef.TexturePath.Replace("Item/", "Player/");
                    bool helmTextureExists = await BMDLoader.Instance.AssestExist(helmTexturePath);
                    if (!helmTextureExists)
                    {
                        helmTexturePath = helmDef.TexturePath;
                    }
                    await LoadPartAsync(Helm, helmTexturePath);
                }

                // Apply item properties for shader effects
                Helm.ItemLevel = appearanceConfig.HelmItemLevel;
                Helm.IsExcellentItem = appearanceConfig.HelmExcellent;
                Helm.IsAncientItem = appearanceConfig.HelmAncient;
            }
            // Armor
            if (appearanceConfig.ArmorItemIndex != 0XFFFF)
            {
                var armorDef = ItemDatabase.GetItemDefinition(8, (short)appearanceConfig.ArmorItemIndex);
                if (armorDef?.TexturePath != null)
                {
                    string armorTexturePath = armorDef.TexturePath.Replace("Item/", "Player/");
                    bool armorTextureExists = await BMDLoader.Instance.AssestExist(armorTexturePath);
                    if (!armorTextureExists)
                    {
                        armorTexturePath = armorDef.TexturePath;
                    }
                    await LoadPartAsync(Armor, armorTexturePath);
                }

                // Apply item properties for shader effects
                Armor.ItemLevel = appearanceConfig.ArmorItemLevel;
                Armor.IsExcellentItem = appearanceConfig.ArmorExcellent;
                Armor.IsAncientItem = appearanceConfig.ArmorAncient;
            }

            // Pants
            if (appearanceConfig.PantsItemIndex != 0XFFFF)
            {
                var pantsDef = ItemDatabase.GetItemDefinition(9, (short)appearanceConfig.PantsItemIndex);
                if (pantsDef?.TexturePath != null)
                {
                    string pantsTexturePath = pantsDef.TexturePath.Replace("Item/", "Player/");
                    bool pantsTextureExists = await BMDLoader.Instance.AssestExist(pantsTexturePath);
                    if (!pantsTextureExists)
                    {
                        pantsTexturePath = pantsDef.TexturePath;
                    }
                    await LoadPartAsync(Pants, pantsTexturePath);
                }

                // Apply item properties for shader effects
                Pants.ItemLevel = appearanceConfig.PantsItemLevel;
                Pants.IsExcellentItem = appearanceConfig.PantsExcellent;
                Pants.IsAncientItem = appearanceConfig.PantsAncient;
            }

            // Gloves
            if (appearanceConfig.GlovesItemIndex != 0XFFFF)
            {
                var glovesDef = ItemDatabase.GetItemDefinition(10, (short)appearanceConfig.GlovesItemIndex);
                if (glovesDef?.TexturePath != null)
                {
                    string glovesTexturePath = glovesDef.TexturePath.Replace("Item/", "Player/");
                    bool glovesTextureExists = await BMDLoader.Instance.AssestExist(glovesTexturePath);
                    if (!glovesTextureExists)
                    {
                        glovesTexturePath = glovesDef.TexturePath;
                    }
                    _logger?.LogInformation($"[PlayerObject] Loading gloves: Group=10, ID={appearanceConfig.GlovesItemIndex}, ItemTexturePath={glovesDef.TexturePath}, PlayerTexturePath={glovesTexturePath}");
                    await LoadPartAsync(Gloves, glovesTexturePath);
                }
                else
                {
                    _logger?.LogWarning($"[PlayerObject] No gloves definition found for Group=10, ID={appearanceConfig.GlovesItemIndex}");
                }

                // Apply item properties for shader effects
                Gloves.ItemLevel = appearanceConfig.GlovesItemLevel;
                Gloves.IsExcellentItem = appearanceConfig.GlovesExcellent;
                Gloves.IsAncientItem = appearanceConfig.GlovesAncient;
            }

            // Boots
            if (appearanceConfig.BootsItemIndex != 0XFFFF)
            {
                var bootsDef = ItemDatabase.GetItemDefinition(11, (short)appearanceConfig.BootsItemIndex);
                if (bootsDef?.TexturePath != null)
                {
                    string bootsTexturePath = bootsDef.TexturePath.Replace("Item/", "Player/");
                    bool bootsTextureExists = await BMDLoader.Instance.AssestExist(bootsTexturePath);
                    if (!bootsTextureExists)
                    {
                        bootsTexturePath = bootsDef.TexturePath;
                    }
                    _logger?.LogInformation($"[PlayerObject] Loading boots: Group=11, ID={appearanceConfig.BootsItemIndex}, ItemTexturePath={bootsDef.TexturePath}, PlayerTexturePath={bootsTexturePath}");
                    await LoadPartAsync(Boots, bootsTexturePath);
                }
                else
                {
                    _logger?.LogWarning($"[PlayerObject] No boots definition found for Group=11, ID={appearanceConfig.BootsItemIndex}");
                }

                // Apply item properties for shader effects
                Boots.ItemLevel = appearanceConfig.BootsItemLevel;
                Boots.IsExcellentItem = appearanceConfig.BootsExcellent;
                Boots.IsAncientItem = appearanceConfig.BootsAncient;
            }

            // Wings
            if (appearanceConfig.WingInfo.ItemIndex >= 0)
            {
                EquippedWings.ItemIndex = appearanceConfig.WingInfo.ItemIndex;
                EquippedWings.Hidden = false;
                EquippedWings.LinkParentAnimation = false;
            }
            else
            {
                EquippedWings.Hidden = true;
            }

            if (appearanceConfig.RidingVehicle >= 0)
            {
                Vehicle.ItemIndex = appearanceConfig.RidingVehicle;
                Vehicle.Hidden = false;
                Vehicle.LinkParentAnimation = false;
            }
            else
            {
                Vehicle.Hidden = true;
            }
            // Weapons
            // This requires more sophisticated logic to determine the exact weapon model
            // based on item group, index, and potentially other flags.
            // For now, we'll use generic models if an item is equipped.
            if (appearanceConfig.LeftHandItemIndex != 255 && appearanceConfig.LeftHandItemIndex != 0xFF)
            {
                var leftHandDef = ItemDatabase.GetItemDefinition(appearanceConfig.LeftHandItemGroup, appearanceConfig.LeftHandItemIndex);
                if (leftHandDef != null)
                {
                    Weapon1.Model = await BMDLoader.Instance.Prepare(leftHandDef.TexturePath);
                    Weapon1.LinkParentAnimation = false;

                    // Apply item properties for shader effects
                    Weapon1.ItemLevel = appearanceConfig.LeftHandItemLevel;
                    Weapon1.IsExcellentItem = appearanceConfig.LeftHandExcellent;
                    Weapon1.IsAncientItem = appearanceConfig.LeftHandAncient;
                    RefreshWeaponAttachment(Weapon1, isLeftHand: true);
                }
                else
                {
                    Weapon1.Model = null;
                }
            }
            else
            {
                Weapon1.Model = null;
            }

            if (appearanceConfig.RightHandItemIndex != 255 && appearanceConfig.RightHandItemIndex != 0xFF)
            {
                var rightHandDef = ItemDatabase.GetItemDefinition(appearanceConfig.RightHandItemGroup, appearanceConfig.RightHandItemIndex);
                if (rightHandDef != null)
                {
                    Weapon2.Model = await BMDLoader.Instance.Prepare(rightHandDef.TexturePath);
                    Weapon2.LinkParentAnimation = false;

                    // Apply item properties for shader effects
                    Weapon2.ItemLevel = appearanceConfig.RightHandItemLevel;
                    Weapon2.IsExcellentItem = appearanceConfig.RightHandExcellent;
                    Weapon2.IsAncientItem = appearanceConfig.RightHandAncient;
                    RefreshWeaponAttachment(Weapon2, isLeftHand: false);
                }
                else
                {
                    Weapon2.Model = null;
                }
            }
            else
            {
                Weapon2.Model = null;
            }
        }

        private void SetActionSpeed(PlayerAction action, float speed)
        {
            int idx = (int)action;
            if (Model?.Actions is { Length: > 0 } actions && idx < actions.Length)
                actions[idx].PlaySpeed = speed;
        }

        // ───────────────────────────────── UPDATE LOOP ─────────────────────────────────
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime); // movement, camera for main walker, etc.

            UpdateEquipmentAnimationStride();

            if (World is not WalkableWorldControl world)
                return;

            if (IsMainWalker)
                UpdateLocalPlayer(world, gameTime);
            else
                UpdateRemotePlayer(world, gameTime);
        }

        private void UpdateEquipmentAnimationStride()
        {
            int desiredStride = 1;

            if (!IsMainWalker)
            {
                desiredStride = IsOneShotPlaying ? 1 : (LowQuality ? 4 : 2);
            }

            if (_lastEquipmentAnimationStride == desiredStride)
                return;

            SetAnimationUpdateStride(desiredStride);
            HelmMask?.SetAnimationUpdateStride(desiredStride);
            Helm?.SetAnimationUpdateStride(desiredStride);
            Armor?.SetAnimationUpdateStride(desiredStride);
            Pants?.SetAnimationUpdateStride(desiredStride);
            Gloves?.SetAnimationUpdateStride(desiredStride);
            Boots?.SetAnimationUpdateStride(desiredStride);
            Weapon1?.SetAnimationUpdateStride(desiredStride);
            Weapon2?.SetAnimationUpdateStride(desiredStride);
            EquippedWings?.SetAnimationUpdateStride(desiredStride);

            _lastEquipmentAnimationStride = desiredStride;
        }

        // --------------- Helpers for correct animation selection ----------------
        private MovementMode GetCurrentMovementMode(WalkableWorldControl world, TWFlags? flagsOverride = null)
        {
            var flags = flagsOverride ?? world.Terrain.RequestTerrainFlag((int)Location.X, (int)Location.Y);
            // Atlans (index 8) is the only place where flying is forbidden.
            if (world.WorldIndex == 8)
            {

                return flags.HasFlag(TWFlags.SafeZone) ? MovementMode.Walk : MovementMode.Swim;
            }

            // Icarus (index 11) is a pure-flight map.
            if (world.WorldIndex == 11)
                return MovementMode.Fly;

            // On every other map we may fly whenever wings are equipped.
            if (HasEquippedWings && !flags.HasFlag(TWFlags.SafeZone))
            {
                return MovementMode.Fly;
            }
            return MovementMode.Walk;
        }

        /// <summary>Action that should play while moving (gender already cached).</summary>
        private PlayerAction GetMovementAction(MovementMode mode) =>
            mode switch
            {
                MovementMode.Swim => PlayerAction.PlayerRunSwim,
                MovementMode.Fly => PlayerAction.PlayerFly,
                _ => GetMovementActionForWeapon(GetEquippedWeaponType())
            };

        /// <summary>
        /// Gets the appropriate movement action based on equipped weapon
        /// </summary>
        private PlayerAction GetMovementActionForWeapon(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.Sword => PlayerAction.PlayerWalkSword,
                WeaponType.TwoHandSword => PlayerAction.PlayerWalkTwoHandSword,
                WeaponType.Spear => PlayerAction.PlayerWalkSpear,
                WeaponType.Bow => PlayerAction.PlayerWalkBow,
                WeaponType.Crossbow => PlayerAction.PlayerWalkCrossbow,
                WeaponType.Staff => PlayerAction.PlayerWalkWand,
                WeaponType.Scythe => PlayerAction.PlayerWalkScythe,
                _ => _isFemale ? PlayerAction.PlayerWalkFemale : PlayerAction.PlayerWalkMale
            };
        }

        // Back-compat overload used in older call-sites
        private PlayerAction GetMovementAction(WalkableWorldControl world) =>
            GetMovementAction(GetCurrentMovementMode(world));

        /// <summary>Action that should play while standing (gender already cached).</summary>
        private PlayerAction GetIdleAction(MovementMode mode) =>
            mode switch
            {
                MovementMode.Fly or MovementMode.Swim => PlayerAction.PlayerStopFly,
                _ => GetIdleActionForWeapon(GetEquippedWeaponType())
            };

        /// <summary>
        /// Gets the appropriate idle action based on equipped weapon
        /// </summary>
        private PlayerAction GetIdleActionForWeapon(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.TwoHandSword => PlayerAction.PlayerStopTwoHandSword,
                WeaponType.Spear => PlayerAction.PlayerStopSpear,
                WeaponType.Bow => PlayerAction.PlayerStopBow,
                WeaponType.Crossbow => PlayerAction.PlayerStopCrossbow,
                WeaponType.Staff => PlayerAction.PlayerStopWand,
                WeaponType.Scythe => PlayerAction.PlayerStopScythe,
                _ => _isFemale ? PlayerAction.PlayerStopFemale : PlayerAction.PlayerStopMale
            };
        }

        private PlayerAction GetRelaxedIdleAction() => _isFemale ? PlayerAction.PlayerStopFemale : PlayerAction.PlayerStopMale;
        private PlayerAction GetRelaxedWalkAction() => _isFemale ? PlayerAction.PlayerWalkFemale : PlayerAction.PlayerWalkMale;

        // ───────────────────────────────── VEHICLE/MOUNT SUPPORT ─────────────────────────────────

        /// <summary>
        /// Checks if the player has a rideable pet equipped (Horn of Fenrir, Dark Horse, etc.)
        /// </summary>
        private bool HasRideablePetEquipped(out short vehicleIndex)
        {
            vehicleIndex = -1;

            if (_networkManager == null)
                return false;

            var charState = _networkManager.GetCharacterState();
            var inventory = charState.GetInventoryItems();

            // Check pet slot (slot 8)
            if (!inventory.TryGetValue(InventoryConstants.PetSlot, out var petData))
                return false;

            var itemDef = ItemDatabase.GetItemDefinition(petData);
            if (itemDef == null)
                return false;

            string itemName = itemDef.Name?.ToLowerInvariant() ?? string.Empty;

            // Map pet items to vehicle indices
            vehicleIndex = MapPetToVehicleIndex(itemName, itemDef.Id);
            return vehicleIndex >= 0;
        }

        /// <summary>
        /// Maps pet item name/id to the corresponding VehicleDatabase index.
        /// </summary>
        private static short MapPetToVehicleIndex(string itemNameLower, int itemId)
        {
            // Dark Horse variations
            if (itemNameLower.Contains("dark horse"))
                return 0; // Dark Horse

            // Dinorant
            if (itemNameLower.Contains("uniria"))
                return 7; // Rider 01

            if (itemNameLower.Contains("dinorant"))
                return 8; // Rider 02
                
            // Horn of Fenrir variations - check for different colors
            if (itemNameLower.Contains("horn of"))
            {
                if (itemNameLower.Contains("black") || itemNameLower.Contains("fenrir"))
                    return 11; // Fenrir Black
                if (itemNameLower.Contains("blue"))
                    return 12; // Fenrir Blue
                if (itemNameLower.Contains("gold"))
                    return 13; // Fenrir Gold
                if (itemNameLower.Contains("red"))
                    return 14; // Fenrir Red

                // Default Fenrir
                return 14; // Fenrir Red as default
            }

            return -1; // Not a rideable pet
        }

        /// <summary>
        /// Updates the vehicle visibility and animations based on current zone (for local player).
        /// </summary>
        private void UpdateVehicleState(bool isInSafeZone)
        {
            bool hasRideablePet = HasRideablePetEquipped(out short vehicleIndex);
            bool shouldRide = hasRideablePet && !isInSafeZone;

            if (shouldRide != _isRiding || (shouldRide && vehicleIndex != _currentVehicleIndex))
            {
                _isRiding = shouldRide;
                _currentVehicleIndex = shouldRide ? vehicleIndex : (short)-1;

                if (Vehicle != null)
                {
                    Vehicle.Hidden = !shouldRide;
                    if (shouldRide)
                    {
                        Vehicle.ItemIndex = vehicleIndex;
                    }
                }
            }

            // Apply rider height offset when riding
            ApplyRiderHeightOffset();
        }

        /// <summary>
        /// Updates the vehicle visibility for remote players based on AppearanceData.
        /// </summary>
        private void UpdateVehicleStateFromAppearance(bool isInSafeZone)
        {
            if (Appearance.RawData.IsEmpty)
                return;

            // Check appearance flags for rideable pets
            short vehicleIndex = -1;

            if (Appearance.HasDarkHorse)
            {
                vehicleIndex = 0; // Dark Horse
            }
            else if (Appearance.HasFenrir)
            {
                vehicleIndex = 14; // Default Fenrir (red)
            }

            bool hasRideablePet = vehicleIndex >= 0;
            bool shouldRide = hasRideablePet && !isInSafeZone;

            if (shouldRide != _isRiding || (shouldRide && vehicleIndex != _currentVehicleIndex))
            {
                _isRiding = shouldRide;
                _currentVehicleIndex = shouldRide ? vehicleIndex : (short)-1;

                if (Vehicle != null)
                {
                    Vehicle.Hidden = !shouldRide;
                    if (shouldRide)
                    {
                        Vehicle.ItemIndex = vehicleIndex;
                    }
                }
            }

            // Apply rider height offset when riding
            ApplyRiderHeightOffset();
        }

        /// <summary>
        /// Applies the rider height offset from the current vehicle to the player's position.
        /// This raises or lowers the player model to sit correctly on the mount.
        /// The offset is applied additively, preserving any existing Position offset.
        /// The vehicle gets a compensating negative offset to stay at ground level.
        /// </summary>
        private void ApplyRiderHeightOffset()
        {
            float targetOffset = 0f;

            if (_isRiding && Vehicle != null && !Vehicle.Hidden)
            {
                targetOffset = Vehicle.RiderHeightOffset;
            }

            // Only update if the offset changed
            if (Math.Abs(_currentRiderHeightOffset - targetOffset) > 0.01f)
            {
                // Remove old offset and apply new one
                float deltaOffset = targetOffset - _currentRiderHeightOffset;
                Position = new Vector3(Position.X, Position.Y, Position.Z + deltaOffset);
                _currentRiderHeightOffset = targetOffset;

                // Apply compensating negative offset to the vehicle so it stays at ground level
                // (since vehicle inherits parent transform, we need to counter the player's Z offset)
                if (Vehicle != null)
                {
                    Vehicle.Position = new Vector3(Vehicle.Position.X, Vehicle.Position.Y, -targetOffset);
                }
            }
        }

        /// <summary>
        /// Gets the appropriate movement action when riding a mount.
        /// </summary>
        private PlayerAction GetRidingMovementAction()
        {
            // Fenrir variants have special running animation
            if (_currentVehicleIndex >= 11 && _currentVehicleIndex <= 18) // All Fenrir variants
                return PlayerAction.PlayerFenrirRun;

            // Dark Horse has a specific animation
            if (_currentVehicleIndex == 0) // Dark Horse
                return PlayerAction.PlayerRunRideHorse;

            // Default riding animation for Dinorant, Uniria, etc.
            return PlayerAction.PlayerRunRide;
        }

        /// <summary>
        /// Gets the appropriate idle action when riding a mount.
        /// </summary>
        private PlayerAction GetRidingIdleAction()
        {
            // Fenrir variants have special standing animation
            if (_currentVehicleIndex >= 11 && _currentVehicleIndex <= 18) // All Fenrir variants
                return PlayerAction.PlayerFenrirStand;

            // All other mounts use the same riding idle animation
            return PlayerAction.PlayerStopRide;
        }

        /// <summary>
        /// Triggers the vehicle skill animation when the player uses a skill while riding.
        /// Should be called when a skill animation is played.
        /// </summary>
        public void TriggerVehicleSkillAnimation()
        {
            if (_isRiding && Vehicle != null && !Vehicle.Hidden)
            {
                Vehicle.SetRiderAnimation(isMoving: false, isUsingSkill: true);
            }
        }

        /// <summary>
        /// Resets the vehicle animation after skill animation completes.
        /// </summary>
        public void ResetVehicleAnimation()
        {
            if (_isRiding && Vehicle != null && !Vehicle.Hidden)
            {
                bool isMoving = IsMoving || _currentPath?.Count > 0 || MovementIntent;
                Vehicle.SetRiderAnimation(isMoving: isMoving, isUsingSkill: false);
            }
        }

        private void UpdateWeaponHolsterState(bool shouldHolster)
        {
            if (_weaponsHolstered == shouldHolster)
                return;

            _weaponsHolstered = shouldHolster;
            RefreshWeaponAttachment(Weapon1, isLeftHand: true);
            RefreshWeaponAttachment(Weapon2, isLeftHand: false);
        }

        private void RefreshWeaponAttachment(WeaponObject weapon, bool isLeftHand)
        {
            if (weapon == null)
                return;

            ApplyWeaponAttachment(weapon, isLeftHand, _weaponsHolstered);
        }

        private static Vector3 GetHolsterOffset(bool isLeftHand) => WeaponHolsterOffsets[isLeftHand ? 0 : 1];

        private static Vector3 GetHolsterRotationRadians(bool isLeftHand)
        {
            var degrees = WeaponHolsterRotationDegrees[isLeftHand ? 0 : 1];
            return new Vector3(
                MathHelper.ToRadians(degrees.X),
                MathHelper.ToRadians(degrees.Y),
                MathHelper.ToRadians(degrees.Z));
        }

        private void ApplyWeaponAttachment(WeaponObject weapon, bool isLeftHand, bool holster)
        {
            if (weapon == null)
                return;

            if (holster)
            {
                weapon.ParentBoneLink = BackWeaponBoneIndex;
                weapon.Position = GetHolsterOffset(isLeftHand);
                weapon.Angle = GetHolsterRotationRadians(isLeftHand);
            }
            else
            {
                weapon.ParentBoneLink = isLeftHand ? LeftHandBoneIndex : RightHandBoneIndex;
                weapon.Position = Vector3.Zero;
                weapon.Angle = Vector3.Zero;
            }
        }

        private MovementMode GetModeFromCurrentAction() =>
            CurrentAction switch
            {
                PlayerAction.PlayerFly or PlayerAction.PlayerStopFly or PlayerAction.PlayerPoseMale1
                    => MovementMode.Fly,
                PlayerAction.PlayerRunSwim
                    => MovementMode.Swim,
                _ => MovementMode.Walk
            };

        private PlayerAction GetIdleAction(WalkableWorldControl world)
        {
            var flags = world.Terrain.RequestTerrainFlag((int)Location.X, (int)Location.Y);
            return GetIdleAction(world, flags);
        }

        private PlayerAction GetIdleAction(WalkableWorldControl world, TWFlags flags)
        {
            if (flags.HasFlag(TWFlags.SafeZone))
                return GetRelaxedIdleAction();

            return GetIdleAction(GetCurrentMovementMode(world, flags));
        }

        // --------------- LOCAL PLAYER (the one we control) ----------------
        private void UpdateLocalPlayer(WalkableWorldControl world, GameTime gameTime)
        {
            // Rest / sit handling first
            if (HandleRestTarget(world) || HandleSitTarget())
                return;

            var flags = world.Terrain.RequestTerrainFlag((int)Location.X, (int)Location.Y);
            bool isInSafeZone = flags.HasFlag(TWFlags.SafeZone);
            UpdateWeaponHolsterState(isInSafeZone);
            UpdateVehicleState(isInSafeZone);

            bool pathQueued = _currentPath?.Count > 0;
            bool isAboutToMove = IsMoving || pathQueued || MovementIntent;

            var mode = (!IsMoving && (pathQueued || MovementIntent))
                ? GetModeFromCurrentAction()
                : GetCurrentMovementMode(world, flags);

            if (isAboutToMove)
            {
                ResetRestSitStates();

                PlayerAction desired;
                if (isInSafeZone)
                {
                    desired = GetRelaxedWalkAction();
                }
                else if (_isRiding)
                {
                    // Use riding animation when mounted
                    desired = GetRidingMovementAction();
                    // Sync vehicle animation
                    Vehicle?.SetRiderAnimation(isMoving: true);
                }
                else
                {
                    desired = (HasEquippedWings && mode == MovementMode.Fly)
                        ? PlayerAction.PlayerFly
                        : GetMovementAction(mode);
                }

                if (!IsOneShotPlaying && CurrentAction != desired)
                    PlayAction((ushort)desired);
                PlayFootstepSound(world, gameTime);
            }
            else if (!IsOneShotPlaying)
            {
                PlayerAction idleAction;
                if (isInSafeZone)
                {
                    idleAction = GetRelaxedIdleAction();
                }
                else if (_isRiding)
                {
                    // Use riding idle animation when mounted
                    idleAction = GetRidingIdleAction();
                    // Sync vehicle animation
                    Vehicle?.SetRiderAnimation(isMoving: false);
                }
                else
                {
                    idleAction = GetIdleAction(mode);
                }

                if (CurrentAction != idleAction)
                    PlayAction((ushort)idleAction);
            }
        }

        // --------------- REMOTE PLAYERS ----------------
        private void UpdateRemotePlayer(WalkableWorldControl world, GameTime gameTime)
        {
            bool pathQueued = _currentPath?.Count > 0;
            bool isAboutToMove = IsMoving || pathQueued || MovementIntent;

            var flags = world.Terrain.RequestTerrainFlag((int)Location.X, (int)Location.Y);
            bool isInSafeZone = flags.HasFlag(TWFlags.SafeZone);
            UpdateWeaponHolsterState(isInSafeZone);
            UpdateVehicleStateFromAppearance(isInSafeZone);

            var mode = (!IsMoving && (pathQueued || MovementIntent))
                ? GetModeFromCurrentAction()
                : GetCurrentMovementMode(world, flags);

            if (isAboutToMove)
            {
                ResetRestSitStates();
                PlayerAction desired;
                if (isInSafeZone)
                {
                    desired = GetRelaxedWalkAction();
                }
                else if (_isRiding)
                {
                    // Use riding animation when mounted
                    desired = GetRidingMovementAction();
                    // Sync vehicle animation
                    Vehicle?.SetRiderAnimation(isMoving: true);
                }
                else
                {
                    desired = (HasEquippedWings && mode == MovementMode.Fly)
                        ? PlayerAction.PlayerFly
                        : GetMovementAction(mode);
                }

                if (!IsOneShotPlaying && CurrentAction != desired)
                    PlayAction((ushort)desired);
                PlayFootstepSound(world, gameTime);
            }
            else if (!IsOneShotPlaying)
            {
                PlayerAction idleAction;
                if (isInSafeZone)
                {
                    idleAction = GetRelaxedIdleAction();
                }
                else if (_isRiding)
                {
                    // Use riding idle animation when mounted
                    idleAction = GetRidingIdleAction();
                    // Sync vehicle animation
                    Vehicle?.SetRiderAnimation(isMoving: false);
                }
                else
                {
                    idleAction = GetIdleAction(mode);
                }

                if (CurrentAction != idleAction)
                    PlayAction((ushort)idleAction);
            }
        }

        // --------------- REST / SIT LOGIC ----------------
        private bool HandleRestTarget(WalkableWorldControl world)
        {
            if (!RestPlaceTarget.HasValue) return false;

            float dist = Vector2.Distance(Location, RestPlaceTarget.Value);
            if (dist < 0.1f && !IsMoving && !IsOneShotPlaying)
            {
                var restAction = world.WorldIndex == 4
                    ? PlayerAction.PlayerPoseMale1
                    : PlayerAction.PlayerPoseMale1;

                if (CurrentAction != restAction)
                {
                    PlayAction((ushort)restAction);
                    IsResting = true;
                    if (IsMainWalker)
                        SendActionToServer(PlayerActionMapper.GetServerActionType(restAction, CharacterClass));
                }
                return true;
            }

            if (dist > 1.0f)
            {
                RestPlaceTarget = null;
                IsResting = false;
            }
            return false;
        }

        private bool HandleSitTarget()
        {
            if (!SitPlaceTarget.HasValue) return false;

            float dist = Vector2.Distance(Location, SitPlaceTarget.Value);
            if (dist < 0.1f && !IsOneShotPlaying)
            {
                var sitAction = PlayerActionMapper.IsCharacterFemale(CharacterClass)
                    ? PlayerAction.PlayerSitFemale1
                    : PlayerAction.PlayerSit1;

                if (CurrentAction != sitAction)
                {
                    PlayAction((ushort)sitAction);
                    IsSitting = true;
                    if (IsMainWalker)
                        SendActionToServer(ServerPlayerActionType.Sit);
                }
                return true;
            }

            if (dist > 1.0f)
            {
                SitPlaceTarget = null;
                IsSitting = false;
            }
            return false;
        }

        private void ResetRestSitStates()
        {
            if (IsResting || IsSitting)
            {
                IsResting = false;
                IsSitting = false;
                RestPlaceTarget = null;
                SitPlaceTarget = null;
            }
        }

        public new void Reset()
        {
            // Call base reset first
            base.Reset();

            // Reset player-specific states
            ResetRestSitStates();
        }

        // --------------- UTILITIES ----------------
        public ushort GetCorrectIdleAction()
        {
            if (World is not WalkableWorldControl world)
                return (ushort)(_isFemale ? PlayerAction.PlayerStopFemale : PlayerAction.PlayerStopMale);

            return (ushort)GetIdleAction(world);
        }

        private bool IsMovementAnimation(ushort action)
        {
            var a = (PlayerAction)action;
            return a is PlayerAction.PlayerWalkMale or PlayerAction.PlayerWalkFemale
                       or PlayerAction.PlayerRunSwim or PlayerAction.PlayerFly;
        }

        // ────────────────────────────── ATTACKS (unchanged) ──────────────────────────────
        public PlayerAction GetAttackAnimation()
        {
            // Get actual equipped weapon type
            WeaponType weapon = GetEquippedWeaponType();
            return weapon switch
            {
                WeaponType.Sword => PlayerAction.PlayerAttackSwordRight1,
                WeaponType.TwoHandSword => PlayerAction.PlayerAttackTwoHandSword1,
                WeaponType.Spear => PlayerAction.PlayerAttackSpear1,
                WeaponType.Bow => PlayerAction.PlayerAttackBow,
                WeaponType.Crossbow => PlayerAction.PlayerAttackCrossbow,
                WeaponType.Staff => PlayerAction.PlayerSkillHand1,
                WeaponType.Scepter => PlayerAction.PlayerAttackFist, // TODO: Add proper scepter animation
                WeaponType.Scythe => PlayerAction.PlayerAttackScythe1,
                WeaponType.Book => PlayerAction.PlayerSkillHand1, // TODO: Add proper book animation
                WeaponType.Fist or WeaponType.None => PlayerAction.PlayerAttackFist,
                _ => PlayerAction.PlayerAttackFist
            };
        }

        public void Attack(MonsterObject target)
        {
            if (target == null || World == null) return;

            // Don't attack dead monsters
            if (target.IsDead) return;

            float rangeTiles = GetAttackRangeTiles();
            if (Vector2.Distance(Location, target.Location) > rangeTiles)
            {
                MoveTo(target.Location);
                return;
            }

            _currentPath?.Clear();

            // Rotate to face the target
            int dx = (int)(target.Location.X - Location.X);
            int dy = (int)(target.Location.Y - Location.Y);
            if (dx != 0 || dy != 0)
                Direction = DirectionExtensions.GetDirectionFromMovementDelta(dx, dy);

            PlayAction((ushort)GetAttackAnimation());

            // Play weapon swing sound based on equipped weapon type
            PlayWeaponSwingSound();

            // Map client dir → server dir
            byte clientDir = (byte)Direction;
            byte serverDir = _networkManager?.GetDirectionMap()?.GetValueOrDefault(clientDir, clientDir) ?? clientDir;

            _characterService?.SendHitRequestAsync(
                target.NetworkId,
                (byte)GetAttackAnimation(),
                serverDir);
        }

        public float GetAttackRangeTiles() => GetAttackRangeForAction(GetAttackAnimation());

        /// <summary>
        /// Gets the currently equipped weapon type based on actual equipment
        /// </summary>
        private WeaponType GetEquippedWeaponType()
        {
            if (_networkManager == null)
                return Equipment.GetDefaultWeaponTypeForClass(CharacterClass);

            var charState = _networkManager.GetCharacterState();
            var inventory = charState.GetInventoryItems();

            // Get item definitions for both hands
            var leftHandItem = inventory.TryGetValue(InventoryConstants.LeftHandSlot, out var leftData)
                ? ItemDatabase.GetItemDefinition(leftData)
                : null;
            var rightHandItem = inventory.TryGetValue(InventoryConstants.RightHandSlot, out var rightData)
                ? ItemDatabase.GetItemDefinition(rightData)
                : null;

            // Use equipment system to determine weapon type
            var equippedWeapon = Equipment.GetEquippedWeaponType(leftHandItem, rightHandItem, ItemDatabase.GetItemGroup(leftData), ItemDatabase.GetItemGroup(rightData));

            // Fall back to class default if no weapon equipped
            return equippedWeapon != WeaponType.None
                ? equippedWeapon
                : Equipment.GetDefaultWeaponTypeForClass(CharacterClass);
        }

        /// <summary>
        /// Plays weapon swing sound based on equipped weapon type.
        /// Follows original client logic from ZzzCharacter.cpp SetPlayerAttack.
        /// </summary>
        private void PlayWeaponSwingSound()
        {
            var weaponType = GetEquippedWeaponType();
            string soundPath = weaponType switch
            {
                WeaponType.Bow => "Sound/eBow.wav",
                WeaponType.Crossbow => GetCrossbowSound(),
                WeaponType.Spear => "Sound/eSwingLightSword.wav",
                _ => GetRandomMeleeSwingSound() // Swords, axes, maces, etc.
            };

            Controllers.SoundController.Instance.PlayBuffer(soundPath);
        }

        /// <summary>
        /// Determines crossbow sound (special crossbows use magic sound).
        /// </summary>
        private string GetCrossbowSound()
        {
            if (_networkManager == null)
                return "Sound/eCrossbow.wav";

            var charState = _networkManager.GetCharacterState();
            var inventory = charState.GetInventoryItems();

            if (inventory.TryGetValue(InventoryConstants.RightHandSlot, out var rightData))
            {
                var item = ItemDatabase.GetItemDefinition(rightData);
                // Check for special crossbows like Bluewing Crossbow
                if (item?.Name?.ToLowerInvariant().Contains("bluewing") == true)
                {
                    return "Sound/sMagic.wav";
                }
            }

            return "Sound/eCrossbow.wav";
        }

        /// <summary>
        /// Returns random melee weapon swing sound (eSwingWeapon1.wav or eSwingWeapon2.wav).
        /// </summary>
        private string GetRandomMeleeSwingSound()
        {
            return MuGame.Random.Next(2) == 0
                ? "Sound/eSwingWeapon1.wav"
                : "Sound/eSwingWeapon2.wav";
        }

        private static float GetAttackRangeForAction(PlayerAction a) => a switch
        {
            PlayerAction.PlayerAttackFist => 3f,
            PlayerAction.PlayerAttackBow => 8f,
            PlayerAction.PlayerAttackCrossbow => 8f,
            PlayerAction.PlayerAttackFlyBow => 8f,
            PlayerAction.PlayerAttackFlyCrossbow => 8f,
            PlayerAction.PlayerAttackSpear1 => 3f,
            PlayerAction.PlayerAttackSkillSword1 => 6f,
            PlayerAction.PlayerAttackSkillSpear => 6f,
            _ => 3f
        };

        // ────────────────────────────── CLASS → MODEL MAP ──────────────────────────────
        private PlayerClass MapNetworkClassToModelClass(CharacterClassNumber n) => n switch
        {
            CharacterClassNumber.DarkWizard => PlayerClass.DarkWizard,
            CharacterClassNumber.SoulMaster => PlayerClass.SoulMaster,
            CharacterClassNumber.GrandMaster => PlayerClass.GrandMaster,
            CharacterClassNumber.DarkKnight => PlayerClass.DarkKnight,
            CharacterClassNumber.BladeKnight => PlayerClass.BladeKnight,
            CharacterClassNumber.BladeMaster => PlayerClass.BladeMaster,
            CharacterClassNumber.FairyElf => PlayerClass.FairyElf,
            CharacterClassNumber.MuseElf => PlayerClass.MuseElf,
            CharacterClassNumber.HighElf => PlayerClass.HighElf,
            CharacterClassNumber.MagicGladiator => PlayerClass.MagicGladiator,
            CharacterClassNumber.DuelMaster => PlayerClass.DuelMaster,
            CharacterClassNumber.DarkLord => PlayerClass.DarkLord,
            CharacterClassNumber.LordEmperor => PlayerClass.LordEmperor,
            CharacterClassNumber.Summoner => PlayerClass.Summoner,
            CharacterClassNumber.BloodySummoner => PlayerClass.BloodySummoner,
            CharacterClassNumber.DimensionMaster => PlayerClass.DimensionMaster,
            CharacterClassNumber.RageFighter => PlayerClass.RageFighter,
            CharacterClassNumber.FistMaster => PlayerClass.FistMaster,
            _ => PlayerClass.DarkWizard
        };

        public async Task UpdateBodyPartClassesAsync()
        {
            // Clear shader properties from all body parts before loading defaults
            ClearItemProperties(Helm);
            ClearItemProperties(Armor);
            ClearItemProperties(Pants);
            ClearItemProperties(Gloves);
            ClearItemProperties(Boots);

            PlayerClass mapped = MapNetworkClassToModelClass(_characterClass);
            await SetBodyPartsAsync("Player/",
                "HelmClass", "ArmorClass", "PantClass", "GloveClass", "BootClass",
                (int)mapped);
        }
        public async Task UpdateBodyPartClassesAsync(PlayerClass playerClass)
        {
            // Clear shader properties from all body parts before loading defaults
            ClearItemProperties(Helm);
            ClearItemProperties(Armor);
            ClearItemProperties(Pants);
            ClearItemProperties(Gloves);
            ClearItemProperties(Boots);

            await SetBodyPartsAsync("Player/",
                "HelmClass", "ArmorClass", "PantClass", "GloveClass", "BootClass",
                (int)playerClass);
        }

        private async Task ResetBodyPartToClassDefaultAsync(ModelObject bodyPart, string partPrefix)
        {
            Console.WriteLine($"[PlayerObject] ResetBodyPartToClassDefaultAsync called: partPrefix={partPrefix}");

            // Clear item shader properties first
            ClearItemProperties(bodyPart);

            PlayerClass mapped = MapNetworkClassToModelClass(_characterClass);
            string fileSuffix = ((int)mapped).ToString("D2");
            string modelPath = $"Player/{partPrefix}{fileSuffix}.bmd";

            Console.WriteLine($"[PlayerObject] Resetting body part: CharClass={_characterClass}, MappedClass={mapped}, Path={modelPath}");
            _logger?.LogDebug("Resetting body part to class default: CharClass={CharClass}, MappedClass={Mapped}, Path={Path}",
                _characterClass, mapped, modelPath);

            try
            {
                await LoadPartAsync(bodyPart, modelPath);
                Console.WriteLine($"[PlayerObject] LoadPartAsync completed successfully for {modelPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayerObject] LoadPartAsync failed: {ex.Message}");
                throw;
            }
        }

        private string GetPartPrefix(ModelObject bodyPart)
        {
            if (bodyPart == Helm) return "HelmClass";
            if (bodyPart == Armor) return "ArmorClass";
            if (bodyPart == Pants) return "PantClass";
            if (bodyPart == Gloves) return "GloveClass";
            if (bodyPart == Boots) return "BootClass";
            return "";
        }

        // ────────────────────────────── SERVER COMMUNICATION ──────────────────────────────
        private void SendActionToServer(ServerPlayerActionType serverAction)
        {
            if (_characterService == null || !_networkManager.IsConnected) return;

            float angleDegrees = MathHelper.ToDegrees(Angle.Z);
            byte clientDirEnum = (byte)Direction;
            byte serverDirection = _networkManager.GetDirectionMap()
                                        ?.GetValueOrDefault(clientDirEnum, clientDirEnum) ?? clientDirEnum;

            _ = _characterService.SendAnimationRequestAsync(serverDirection, (byte)serverAction);
        }

        // ────────────────────────────── EVENTS ──────────────────────────────
        public void OnPlayerMoved() => PlayerMoved?.Invoke(this, EventArgs.Empty);
        public void OnPlayerTookDamage() => PlayerTookDamage?.Invoke(this, EventArgs.Empty);

        public override void DrawAfter(GameTime gameTime)
        {
            base.DrawAfter(gameTime);
            DrawPartyHealthBar();
        }

        private void DrawPartyHealthBar()
        {
            var partyManager = MuGame.Network?.GetPartyManager();
            if (partyManager == null || !partyManager.IsPartyActive())
                return;

            if (!partyManager.IsMember(NetworkId))
                return;

            float hpPercent = partyManager.GetHealthPercentage(NetworkId);

            Vector3 anchor = new(
                (BoundingBoxWorld.Min.X + BoundingBoxWorld.Max.X) * 0.5f,
                (BoundingBoxWorld.Min.Y + BoundingBoxWorld.Max.Y) * 0.5f,
                BoundingBoxWorld.Max.Z + 20f);

            Vector3 screen = GraphicsDevice.Viewport.Project(
                anchor,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            // Projected coordinates are already in the correct space

            if (screen.Z < 0f || screen.Z > 1f)
                return;

            const int width = 50;
            const int height = 5;

            Rectangle bgRect = new(
                (int)screen.X - width / 2,
                (int)screen.Y - height - 2,
                width,
                height);

            Rectangle fillRect = new(
                bgRect.X + 1,
                bgRect.Y + 1,
                (int)((width - 2) * Math.Clamp(hpPercent, 0f, 1f)),
                height - 2);

            float segmentWidth = (width - 2) / 8f;

            var sb = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;
            using (new SpriteBatchScope(
                       sb,
                       SpriteSortMode.BackToFront,
                       BlendState.NonPremultiplied,
                       SamplerState.PointClamp,
                       DepthStencilState.DepthRead))
            {
                sb.Draw(pixel, bgRect, Color.Black * 0.6f);
                sb.Draw(pixel, fillRect, Color.Red);
                for (int i = 1; i < 8; i++)
                {
                    int x = bgRect.X + 1 + (int)(segmentWidth * i);
                    sb.Draw(pixel,
                            new Rectangle(x, bgRect.Y + 1, 1, height - 2),
                            Color.Black * 0.4f);
                }
                sb.Draw(pixel,
                        new Rectangle(bgRect.X - 1, bgRect.Y - 1, bgRect.Width + 2, bgRect.Height + 2),
                        Color.White * 0.3f);
            }
        }

        protected override void OnLocationChanged(Vector2 oldLocation, Vector2 newLocation)
        {
            base.OnLocationChanged(oldLocation, newLocation);
            if (IsMainWalker)
            {
                CheckForGateEntry((int)newLocation.X, (int)newLocation.Y);
            }
        }

        private void CheckForGateEntry(int x, int y)
        {
            if (World == null) return;
            var gate = GateDataManager.Instance.GetGate((int)World.MapId, x, y);
            if (gate != null && gate.Flag != 0)
            {
                var charState = MuGame.Network.GetCharacterState();
                if (charState.Level >= gate.Level)
                    _characterService?.SendEnterGateRequestAsync((ushort)gate.Id);
            }
        }

        /// <summary>
        /// Plays a footstep sound based on terrain and movement mode.
        /// </summary>
        private void PlayFootstepSound(WalkableWorldControl world, GameTime gameTime)
        {
            if (!IsMoving)
            {
                _footstepTimer = 0f;
                return;
            }

            _footstepTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            var mode = GetCurrentMovementMode(world);
            // Do not play footstep sounds while flying
            if (mode == MovementMode.Fly)
            {
                _footstepTimer = 0f;
                return;
            }
            float interval = mode == MovementMode.Swim ? 2.0f : 0.4f;
            if (_footstepTimer < interval)
                return;

            _footstepTimer = 0f;

            string soundPath;
            if (mode == MovementMode.Swim)
            {
                soundPath = "Sound/pSwim.wav";
            }
            else
            {
                byte tex = world.Terrain.GetBaseTextureIndexAt((int)Location.X, (int)Location.Y);
                if (world.Name == "Devias")
                {
                    if (tex == 0 || tex == 1)
                        soundPath = "Sound/pWalk(Snow).wav";
                    else if (tex == 4)
                        soundPath = "Sound/pWalk(Soil).wav";
                    else
                        soundPath = "Sound/pWalk(Soil).wav";
                }
                else
                {
                    if (tex == 0 || tex == 1)
                        soundPath = "Sound/pWalk(Grass).wav";
                    else if (tex == 4)
                        soundPath = "Sound/pWalk(Snow).wav";
                    else
                        soundPath = "Sound/pWalk(Soil).wav";
                }

            }

            SoundController.Instance.PlayBufferWithAttenuation(soundPath, Position, world.Walker.Position);
        }

        /// <summary>
        /// Loads the models for all body parts based on a specified path prefix, part prefixes, and a file suffix.
        /// Example: ("Npc/", "FemaleHead", "FemaleUpper", ..., 2) -> "Data/Npc/FemaleHead02.bmd"
        /// </summary>
        protected async Task SetBodyPartsAsync(
            string pathPrefix, string helmPrefix, string armorPrefix, string pantPrefix,
            string glovePrefix, string bootPrefix, int skinIndex)
        {
            // Format skin index to two digits (e.g., 1 -> "01", 10 -> "10")
            string fileSuffix = skinIndex.ToString("D2");

            var tasks = new List<Task>
            {
                LoadPartAsync(Helm, $"{pathPrefix}{helmPrefix}{fileSuffix}.bmd"),
                LoadPartAsync(Armor, $"{pathPrefix}{armorPrefix}{fileSuffix}.bmd"),
                LoadPartAsync(Pants, $"{pathPrefix}{pantPrefix}{fileSuffix}.bmd"),
                LoadPartAsync(Gloves, $"{pathPrefix}{glovePrefix}{fileSuffix}.bmd"),
                LoadPartAsync(Boots, $"{pathPrefix}{bootPrefix}{fileSuffix}.bmd")
            };

            await Task.WhenAll(tasks);
        }

        public Task PreloadAppearanceModelsAsync()
        {
            var paths = CollectAppearanceModelPaths();
            if (paths.Count == 0)
                return Task.CompletedTask;

            var tasks = new Task[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                tasks[i] = BMDLoader.Instance.Prepare(paths[i]);
            }

            return Task.WhenAll(tasks);
        }

        private List<string> CollectAppearanceModelPaths()
        {
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in EnumerateDefaultBodyModelPaths())
            {
                if (!string.IsNullOrWhiteSpace(path))
                    uniquePaths.Add(path);
            }

            foreach (var path in EnumerateEquippedModelPaths())
            {
                if (!string.IsNullOrWhiteSpace(path))
                    uniquePaths.Add(path);
            }

            return new List<string>(uniquePaths);
        }

        private IEnumerable<string> EnumerateDefaultBodyModelPaths()
        {
            PlayerClass mapped = MapNetworkClassToModelClass(_characterClass);
            string suffix = ((int)mapped).ToString("D2");

            yield return $"Player/HelmClass{suffix}.bmd";
            yield return $"Player/ArmorClass{suffix}.bmd";
            yield return $"Player/PantClass{suffix}.bmd";
            yield return $"Player/GloveClass{suffix}.bmd";
            yield return $"Player/BootClass{suffix}.bmd";
        }

        private IEnumerable<string> EnumerateEquippedModelPaths()
        {
            if (Appearance.RawData.IsEmpty)
                yield break;

            string NormalizePlayerPath(string path)
                => string.IsNullOrWhiteSpace(path) ? null : path.Replace("Item/", "Player/");

            string TryGetItemPath(int group, short itemIndex)
            {
                if (itemIndex is 0xFF or 255 || itemIndex < 0)
                    return null;

                var def = ItemDatabase.GetItemDefinition((byte)group, itemIndex);
                return def?.TexturePath;
            }

            string path = NormalizePlayerPath(TryGetItemPath(7, Appearance.HelmItemIndex));
            if (path != null) yield return path;

            path = NormalizePlayerPath(TryGetItemPath(8, Appearance.ArmorItemIndex));
            if (path != null) yield return path;

            path = NormalizePlayerPath(TryGetItemPath(9, Appearance.PantsItemIndex));
            if (path != null) yield return path;

            path = NormalizePlayerPath(TryGetItemPath(10, Appearance.GlovesItemIndex));
            if (path != null) yield return path;

            path = NormalizePlayerPath(TryGetItemPath(11, Appearance.BootsItemIndex));
            if (path != null) yield return path;

            if (Appearance.LeftHandItemIndex != 0xFF && Appearance.LeftHandItemIndex != 255)
            {
                var leftHandDef = ItemDatabase.GetItemDefinition(Appearance.LeftHandItemGroup, Appearance.LeftHandItemIndex);
                if (!string.IsNullOrWhiteSpace(leftHandDef?.TexturePath))
                    yield return leftHandDef.TexturePath;
            }

            if (Appearance.RightHandItemIndex != 0xFF && Appearance.RightHandItemIndex != 255)
            {
                var rightHandDef = ItemDatabase.GetItemDefinition(Appearance.RightHandItemGroup, Appearance.RightHandItemIndex);
                if (!string.IsNullOrWhiteSpace(rightHandDef?.TexturePath))
                    yield return rightHandDef.TexturePath;
            }

            if (Appearance.WingInfo.HasWings)
            {
                int wingModelIndex = Appearance.WingInfo.Type + Appearance.WingInfo.Level + 1;
                yield return $"Item/Wing{wingModelIndex:D2}.bmd";
            }
        }

        private async Task LoadPartAsync(ModelObject part, string modelPath)
        {
            if (part != null && !string.IsNullOrEmpty(modelPath))
            {
                Console.WriteLine($"[PlayerObject] LoadPartAsync: Loading model {modelPath} for {part.GetType().Name}");
                _logger?.LogInformation($"[PlayerObject] LoadPartAsync: Loading model {modelPath} for {part.GetType().Name}");
                part.Model = await BMDLoader.Instance.Prepare(modelPath);
                if (part.Model == null)
                {
                    Console.WriteLine($"[PlayerObject] LoadPartAsync: FAILED to load model {modelPath} for {part.GetType().Name}");
                    _logger?.LogWarning($"[PlayerObject] LoadPartAsync: Failed to load model {modelPath} for {part.GetType().Name}");
                }
                else
                {
                    Console.WriteLine($"[PlayerObject] LoadPartAsync: SUCCESSFULLY loaded model {modelPath} for {part.GetType().Name}");
                    _logger?.LogInformation($"[PlayerObject] LoadPartAsync: Successfully loaded model {modelPath} for {part.GetType().Name}");
                }
            }
            else
            {
                Console.WriteLine($"[PlayerObject] LoadPartAsync: Skipped - part is null or modelPath is empty. Part={part?.GetType().Name}, Path='{modelPath}'");
            }
        }

        private void SetItemProperties(ModelObject part, byte[] itemData)
        {
            if (part == null || itemData == null) return;

            var itemDetails = ItemDatabase.ParseItemDetails(itemData);
            part.ItemLevel = itemDetails.Level;
            part.IsExcellentItem = itemDetails.IsExcellent;
        }

        protected override void UpdateWorldBoundingBox()
        {
            base.UpdateWorldBoundingBox();

            Vector3 min = BoundingBoxWorld.Min;
            Vector3 max = BoundingBoxWorld.Max;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child is ModelObject modelChild && modelChild.Visible && modelChild.Model != null)
                {
                    var childBox = modelChild.BoundingBoxWorld;
                    min = Vector3.Min(min, childBox.Min);
                    max = Vector3.Max(max, childBox.Max);
                }
            }

            BoundingBoxWorld = new BoundingBox(min, max);
        }

        /// <summary>
        /// Updates a specific equipment slot based on AppearanceChanged packet data
        /// </summary>
        public async Task UpdateEquipmentSlotAsync(byte itemSlot, EquipmentSlotData equipmentData)
        {
            Console.WriteLine($"[PlayerObject] UpdateEquipmentSlotAsync called: slot={itemSlot}, equipmentData={(equipmentData == null ? "NULL" : "NOT NULL")}");
            _logger?.LogInformation("UpdateEquipmentSlotAsync called: slot={Slot}, equipmentData={Data}", itemSlot, equipmentData == null ? "NULL" : "NOT NULL");

            if (equipmentData == null)
            {
                // Item is being unequipped
                Console.WriteLine($"[PlayerObject] UNEQUIPPING SLOT {itemSlot} for player {Name}");
                _logger?.LogDebug("UNEQUIPPING SLOT {Slot} for player {Name}", itemSlot, Name);
                await UnequipSlotAsync(itemSlot);
                return;
            }

            Console.WriteLine($"[PlayerObject] UpdateEquipmentSlotAsync: Past null check, continuing with slot={itemSlot}");

            _logger?.LogDebug($"[PlayerObject] UpdateEquipmentSlotAsync: slot={itemSlot}, data group={equipmentData?.ItemGroup}, data number={equipmentData?.ItemNumber}");

            try
            {
                switch (itemSlot)
                {
                    case InventoryConstants.LeftHandSlot: // 0 - Left Hand (Weapon)
                        await UpdateWeaponSlotAsync(Weapon1, equipmentData, 33);
                        break;

                    case InventoryConstants.RightHandSlot: // 1 - Right Hand (Shield/Weapon)
                        await UpdateWeaponSlotAsync(Weapon2, equipmentData, 42);
                        break;

                    case InventoryConstants.HelmSlot: // 2 - Helm
                        await UpdateArmorSlotAsync(Helm, equipmentData, equipmentData.ItemGroup, equipmentData.ItemNumber);
                        break;

                    case InventoryConstants.ArmorSlot: // 3 - Armor
                        await UpdateArmorSlotAsync(Armor, equipmentData, equipmentData.ItemGroup, equipmentData.ItemNumber);
                        break;

                    case InventoryConstants.PantsSlot: // 4 - Pants
                        await UpdateArmorSlotAsync(Pants, equipmentData, equipmentData.ItemGroup, equipmentData.ItemNumber);
                        break;

                    case InventoryConstants.GlovesSlot: // 5 - Gloves
                        Console.WriteLine($"[PlayerObject] Processing gloves slot 5, calling UpdateArmorSlotAsync");
                        _logger?.LogDebug($"[PlayerObject] UpdateEquipmentSlotAsync: Processing gloves slot, calling UpdateArmorSlotAsync");
                        await UpdateArmorSlotAsync(Gloves, equipmentData, equipmentData.ItemGroup, equipmentData.ItemNumber);
                        Console.WriteLine($"[PlayerObject] UpdateArmorSlotAsync completed for gloves slot 5");
                        break;

                    case InventoryConstants.BootsSlot: // 6 - Boots
                        await UpdateArmorSlotAsync(Boots, equipmentData, equipmentData.ItemGroup, equipmentData.ItemNumber);
                        break;

                    case InventoryConstants.WingsSlot: // 7 - Wings
                        await UpdateWingsSlotAsync(equipmentData);
                        break;

                    case InventoryConstants.PetSlot: // 8 - Pet
                        // Pet handling would go here
                        _logger?.LogDebug("Pet slot update not implemented yet for slot {Slot}", itemSlot);
                        break;

                    default:
                        _logger?.LogWarning("Unknown equipment slot {Slot} in appearance change", itemSlot);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating equipment slot {Slot}", itemSlot);
            }
        }

        private async Task UnequipSlotAsync(byte itemSlot)
        {
            Console.WriteLine($"[PlayerObject] UnequipSlotAsync called for slot {itemSlot}");
            _logger?.LogDebug("UnequipSlotAsync called for slot {Slot}", itemSlot);

            switch (itemSlot)
            {
                case InventoryConstants.LeftHandSlot:
                    Weapon1.Model = null;
                    ClearItemProperties(Weapon1);
                    break;

                case InventoryConstants.RightHandSlot:
                    Weapon2.Model = null;
                    ClearItemProperties(Weapon2);
                    break;

                case InventoryConstants.HelmSlot:
                    await ResetBodyPartToClassDefaultAsync(Helm, "HelmClass");
                    ClearItemProperties(Helm);
                    break;

                case InventoryConstants.ArmorSlot:
                    Console.WriteLine($"[PlayerObject] ARMOR CASE: slot={itemSlot}, ArmorSlot={InventoryConstants.ArmorSlot}");
                    _logger?.LogDebug("ARMOR CASE: calling ResetBodyPartToClassDefaultAsync");
                    await ResetBodyPartToClassDefaultAsync(Armor, "ArmorClass");
                    ClearItemProperties(Armor);
                    break;

                case InventoryConstants.PantsSlot:
                    await ResetBodyPartToClassDefaultAsync(Pants, "PantClass");
                    ClearItemProperties(Pants);
                    break;

                case InventoryConstants.GlovesSlot:
                    await ResetBodyPartToClassDefaultAsync(Gloves, "GloveClass");
                    ClearItemProperties(Gloves);
                    break;

                case InventoryConstants.BootsSlot:
                    await ResetBodyPartToClassDefaultAsync(Boots, "BootClass");
                    ClearItemProperties(Boots);
                    break;

                case InventoryConstants.WingsSlot:
                    EquippedWings.Hidden = true;
                    EquippedWings.Type = 0;
                    break;

                default:
                    Console.WriteLine($"[PlayerObject] DEFAULT CASE: Unknown slot {itemSlot}");
                    _logger?.LogWarning("Unknown equipment slot {Slot} in unequip", itemSlot);
                    break;
            }
        }

        private async Task UpdateWeaponSlotAsync(WeaponObject weapon, EquipmentSlotData equipmentData, int boneLink)
        {
            bool isLeftHand = boneLink == LeftHandBoneIndex;
            var itemDef = ItemDatabase.GetItemDefinition(equipmentData.ItemGroup, (short)equipmentData.ItemNumber);
            if (itemDef != null && !string.IsNullOrEmpty(itemDef.TexturePath))
            {
                weapon.Model = await BMDLoader.Instance.Prepare(itemDef.TexturePath);
                weapon.LinkParentAnimation = false;
                SetItemPropertiesFromEquipmentData(weapon, equipmentData);
                RefreshWeaponAttachment(weapon, isLeftHand);
            }
            else
            {
                weapon.Model = null;
                ClearItemProperties(weapon);
            }
        }

        private async Task UpdateArmorSlotAsync(ModelObject armorPart, EquipmentSlotData equipmentData, byte itemGroup, ushort itemNumber)
        {
            Console.WriteLine($"[PlayerObject] UpdateArmorSlotAsync START: Part={armorPart.GetType().Name}, Group={itemGroup}, Number={itemNumber}");
            _logger?.LogDebug($"[PlayerObject] UpdateArmorSlotAsync: Part={armorPart.GetType().Name}, Group={itemGroup}, Number={itemNumber}");

            var itemDef = ItemDatabase.GetItemDefinition(itemGroup, (short)itemNumber);
            Console.WriteLine($"[PlayerObject] UpdateArmorSlotAsync: ItemDef {(itemDef == null ? "NULL" : "FOUND")}");
            if (itemDef != null && !string.IsNullOrEmpty(itemDef.TexturePath))
            {
                string playerTexturePath = itemDef.TexturePath.Replace("Item/", "Player/");
                Console.WriteLine($"[PlayerObject] UpdateArmorSlotAsync: Converting texture path: {itemDef.TexturePath} -> {playerTexturePath}");
                _logger?.LogDebug($"[PlayerObject] UpdateArmorSlotAsync: ItemDef found. Original={itemDef.TexturePath}, Player={playerTexturePath}");

                // Clear old model first
                var oldModel = armorPart.Model;
                armorPart.Model = null;
                Console.WriteLine($"[PlayerObject] UpdateArmorSlotAsync: Cleared old model for {armorPart.GetType().Name}");
                _logger?.LogDebug($"[PlayerObject] UpdateArmorSlotAsync: Cleared old model for {armorPart.GetType().Name}");

                Console.WriteLine($"[PlayerObject] UpdateArmorSlotAsync: About to call LoadPartAsync with: {playerTexturePath}");
                await LoadPartAsync(armorPart, playerTexturePath);
                Console.WriteLine($"[PlayerObject] UpdateArmorSlotAsync: LoadPartAsync completed");
                SetItemPropertiesFromEquipmentData(armorPart, equipmentData);

                Console.WriteLine($"[PlayerObject] UpdateArmorSlotAsync: Model loading completed. New model null? {armorPart.Model == null}");
                _logger?.LogDebug($"[PlayerObject] UpdateArmorSlotAsync: Model loading completed. New model null? {armorPart.Model == null}");
            }
            else
            {
                // If item not found, determine which part to reset based on the armor part
                string partPrefix = GetPartPrefix(armorPart);
                if (!string.IsNullOrEmpty(partPrefix))
                {
                    await ResetBodyPartToClassDefaultAsync(armorPart, partPrefix);
                }
                ClearItemProperties(armorPart);
            }
        }

        private Task UpdateWingsSlotAsync(EquipmentSlotData equipmentData)
        {
            var itemDef = ItemDatabase.GetItemDefinition(equipmentData.ItemGroup, (short)equipmentData.ItemNumber);
            if (itemDef != null)
            {
                EquippedWings.Hidden = false;
                // Wing type calculation may need adjustment based on your wing system
                EquippedWings.Type = (short)(equipmentData.ItemType + equipmentData.ItemLevel + 1);
                EquippedWings.LinkParentAnimation = false;
            }
            else
            {
                EquippedWings.Hidden = true;
                EquippedWings.Type = 0;
            }
            return Task.CompletedTask;
        }

        private void SetItemPropertiesFromEquipmentData(ModelObject part, EquipmentSlotData equipmentData)
        {
            part.ItemLevel = equipmentData.ItemLevel;
            part.IsExcellentItem = equipmentData.ExcellentFlags > 0;
            part.IsAncientItem = equipmentData.AncientDiscriminator > 0;
        }

        private void ClearItemProperties(ModelObject part)
        {
            part.ItemLevel = 0;
            part.IsExcellentItem = false;
            part.IsAncientItem = false;

            // Force shader to update by invalidating buffers
            part.InvalidateBuffers();
        }

        public void PlayEmoteAnimation(PlayerAction emoteAction)
        {
            if (_animationController != null)
            {
                _animationController.PlayAnimation((ushort)emoteAction);
            }
        }
    }
}
