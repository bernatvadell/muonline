using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Data;
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
using Client.Data.BMD;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Objects.Vehicle;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Scenes;
using GameDirection = Client.Main.Models.Direction;

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

        internal const int LeftHandBoneIndex = 33;
        internal const int RightHandBoneIndex = 42;
        private const int BackWeaponBoneIndex = 47; // Same anchor used by wings
        private const short WingOfStormIndex = 36;
        private const short WingOfRuinIndex = 39;
        private string _helmModelPath;

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
        private float _lastWingAnimationSpeed = -1f;

        // Timer for footstep sound playback
        private float _footstepTimer;

        // Movement speed/run state (mirrors SourceMain 5.2 behavior)
        private float _runFrames;
        private const float RunActivationFrames = 40f;
        private const float FenrirRunDelayFrames = 20f;
        private const float BaseWalkSpeedUnits = 12f;
        private const float BaseRunSpeedUnits = 15f;
        private const float WingFastSpeedUnits = 16f;
        private const float DarkHorseSpeedUnits = 17f;
        private const float FenrirSpeedStage1 = 15f;
        private const float FenrirSpeedStage2 = 16f;
        private const float FenrirSpeedNormal = 17f;
        private const float FenrirSpeedExcellent = 19f;
        private const float CursedTempleQuicknessSpeedUnits = 20f;

        private const byte BuffCursedTempleQuickness = 32;
        private const byte DebuffFreeze = 56;
        private const byte DebuffBlowOfDestruction = 86;

        private const short WingOfDragonIndex = 5;

        // ────────────────────────────── CURSOR LOOK / STAND ROTATION ──────────────────────────────
        private float _headYaw;
        private float _headPitch;
        private float _headTargetYaw;
        private float _headTargetPitch;
        private bool _isLookingAtCursor;
        private int _headBoneIndex = -1;
        private int[] _headBoneSubtreeIndices;

        private int _standFrames;
        private int _attackSequence;
        private ushort _lastAttackSpeedStat = ushort.MaxValue;
        private ushort _lastMagicSpeedStat = ushort.MaxValue;

        private readonly object _inventoryAppearanceUpdateSync = new();
        private bool _inventoryAppearanceUpdateRunning;
        private bool _inventoryAppearanceUpdatePending;

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
        public bool HasEquippedWings =>
            EquippedWings is { Hidden: false } && (EquippedWings.Type > 0 || EquippedWings.ItemIndex >= 0);

        public new PlayerAction CurrentAction
        {
            get => (PlayerAction)base.CurrentAction;
            set => base.CurrentAction = (int)value;
        }

        public PlayerAction SelectedAttackAction { get; set; } = PlayerAction.PlayerAttackDeathstab;
        private bool _helmItemEquipped;

        /// <summary>
        /// Gets a value indicating whether the player is currently in a death animation.
        /// </summary>
        public bool IsDead => CurrentAction == PlayerAction.PlayerDie1 || CurrentAction == PlayerAction.PlayerDie2;

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
            AnimationSpeed = 25f;
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
            CacheHeadBoneHierarchy();
            InitializeActionSpeeds();

            if (IsMainWalker)
            {
                // First, load the base body for the current class
                var charState = _networkManager.GetCharacterState();
                CharacterClass = (CharacterClassNumber)charState.Class;
                await UpdateBodyPartClassesAsync();

                // Then, hook events to update equipment based on inventory
                HookInventoryEvents();
                // Perform the initial appearance update and wait for it to complete
                await RunInventoryAppearanceUpdateAsync();
            }
            else
            {
                // Remote players use AppearanceData
                await UpdateBodyPartClassesAsync();
                await UpdateEquipmentAppearanceAsync();
            }

            await base.Load();

            UpdateWorldBoundingBox();
        }

        public async Task Load(PlayerClass playerClass)
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            CacheHeadBoneHierarchy();
            InitializeActionSpeeds();

            if (IsMainWalker)
            {
                // First, load the base body for the current class
                await UpdateBodyPartClassesAsync(playerClass);

                // Then, hook events to update equipment based on inventory
                HookInventoryEvents();
                // Perform the initial appearance update and wait for it to complete
                await RunInventoryAppearanceUpdateAsync();
            }
            else
            {
                // Remote players use AppearanceData
                await UpdateBodyPartClassesAsync(playerClass);
                await UpdateEquipmentAppearanceAsync();
            }

            await base.Load();

            UpdateWorldBoundingBox();
        }

        protected override void BeforeUpdatePosition(GameTime gameTime)
        {
        }

        private static bool IsMouseOverWorld(BaseScene scene)
        {
            return scene?.World != null && scene.MouseHoverControl == scene.World;
        }

        private bool ShouldLookAtCursor(BaseScene scene)
        {
            if (scene == null) return false;
            if (!IsMouseOverWorld(scene)) return false;

            if (!this.IsAlive()) return false;
            if (IsMoving || MovementIntent) return false;
            if (IsOneShotPlaying) return false;
            if (IsResting || IsSitting) return false;

            return CurrentAction is
                PlayerAction.PlayerStopMale or
                PlayerAction.PlayerStopFemale or
                PlayerAction.PlayerStopFly or
                PlayerAction.PlayerFenrirStand;
        }

        private void UpdateHeadRotationTowardsCursor()
        {
            var scene = MuGame.Instance.ActiveScene as BaseScene;
            bool lookAt = ShouldLookAtCursor(scene);
            _isLookingAtCursor = lookAt;

            if (!lookAt)
            {
                _headTargetYaw = 0f;
                _headTargetPitch = 0f;
            }
            else
            {
                var mouse = MuGame.Instance.UiMouseState.Position;

                float heroX = UiScaler.VirtualSize.X * 0.5f;
                float heroY = UiScaler.VirtualSize.Y * (180f / 480f);

                float bodyDeg = AngleUtils.NormalizeDegrees360(MathHelper.ToDegrees(Angle.Z));
                float cursorDeg = AngleUtils.CreateAngleDegrees(heroX, heroY, mouse.X, mouse.Y);

                float angleDeg = bodyDeg + cursorDeg + 360f - 45f;
                angleDeg = AngleUtils.NormalizeDegrees360(angleDeg);
                angleDeg = Math.Clamp(angleDeg, 120f, 240f);

                float yawDeg = angleDeg - 180f; // [-60..60] degrees
                float clampedMouseY = Math.Min(mouse.Y, UiScaler.VirtualSize.Y);
                float pitchDeg = (heroY - clampedMouseY) * 0.05f;

                _headTargetYaw = MathHelper.ToRadians(-yawDeg);
                _headTargetPitch = MathHelper.ToRadians(pitchDeg);
            }

            const float smooth = 0.2f;
            _headYaw = MathHelper.WrapAngle(_headYaw + MathHelper.WrapAngle(_headTargetYaw - _headYaw) * smooth);
            _headPitch += (_headTargetPitch - _headPitch) * smooth;
        }

        private void UpdateStandingDirectionTowardsCursor(GameTime gameTime)
        {
            var scene = MuGame.Instance.ActiveScene as BaseScene;

            bool canStandRotate =
                scene != null &&
                IsMouseOverWorld(scene) &&
                this.IsAlive() &&
                !IsMoving &&
                !MovementIntent &&
                !IsOneShotPlaying &&
                !IsResting &&
                !IsSitting &&
                _isLookingAtCursor;

            if (!canStandRotate)
            {
                _standFrames = 0;
                return;
            }

            _standFrames++;
            if (_standFrames < 40)
                return;

            _standFrames = 0;

            var mouse = MuGame.Instance.UiMouseState.Position;
            float heroX = UiScaler.VirtualSize.X * 0.5f;
            float heroY = UiScaler.VirtualSize.Y * (180f / 480f);

            float heroAngleDeg = -AngleUtils.CreateAngleDegrees(mouse.X, mouse.Y, heroX, heroY) + 360f + 45f;
            heroAngleDeg = AngleUtils.NormalizeDegrees360(heroAngleDeg);

            float currentDeg = AngleUtils.NormalizeDegrees360(MathHelper.ToDegrees(Angle.Z));
            GameDirection currentDir = QuantizeDirectionFromAngleDegrees(currentDeg);
            GameDirection desiredDir = QuantizeDirectionFromAngleDegrees(heroAngleDeg);

            if (currentDir != desiredDir)
            {
                if (CurrentAction != PlayerAction.PlayerAttackSkillSword2)
                {
                    SetFacingAngleZ(MathHelper.ToRadians(heroAngleDeg), immediate: true);
                }

                SendActionToServer(ServerPlayerActionType.Stand1, desiredDir);
            }
        }

        private static GameDirection QuantizeDirectionFromAngleDegrees(float angleDeg)
        {
            angleDeg = AngleUtils.NormalizeDegrees360(angleDeg);
            int sector = ((int)((angleDeg + 22.5f) / 360f * 8f + 1f)) % 8;
            return (GameDirection)sector;
        }

        private void CacheHeadBoneHierarchy()
        {
            _headBoneIndex = ResolveHeadBoneIndex();
            _headBoneSubtreeIndices = null;

            var bones = Model?.Bones;
            if (bones == null || bones.Length == 0)
                return;

            if (_headBoneIndex < 0 || _headBoneIndex >= bones.Length)
                return;

            var indices = new List<int>(bones.Length);
            for (int i = 0; i < bones.Length; i++)
            {
                int p = i;
                while (p >= 0 && p < bones.Length && p != _headBoneIndex)
                {
                    p = bones[p].Parent;
                }

                if (p == _headBoneIndex)
                {
                    indices.Add(i);
                }
            }

            _headBoneSubtreeIndices = indices.ToArray();
        }

        private int ResolveHeadBoneIndex()
        {
            var bones = Model?.Bones;
            if (bones == null || bones.Length == 0)
                return -1;

            int exact = Array.FindIndex(bones, b => string.Equals(b.Name, "Bip01 Head", StringComparison.OrdinalIgnoreCase));
            if (exact >= 0)
                return exact;

            int contains = Array.FindIndex(bones, b => b.Name?.Contains("Head", StringComparison.OrdinalIgnoreCase) == true);
            if (contains >= 0)
                return contains;

            if (bones.Length > 20)
                return 20;

            if (bones.Length > 7)
                return 7;

            return -1;
        }

        protected override bool PostProcessBoneTransforms(BMDTextureBone[] bones, Matrix[] boneTransforms)
        {
            if (!IsMainWalker)
                return false;

            if (_headBoneSubtreeIndices == null || _headBoneSubtreeIndices.Length == 0)
                return false;

            if (_headYaw == 0f && _headPitch == 0f)
                return false;

            int headIndex = _headBoneIndex;
            if ((uint)headIndex >= (uint)boneTransforms.Length)
                return false;

            Matrix head = boneTransforms[headIndex];
            Vector3 headPos = head.Translation;

            // BMD uses a different axis convention than the world (matches SourceMain5.2 head logic):
            // Head yaw is applied to Euler component [0] and pitch to [2], so we rotate around the head bone's
            // local X axis (row 1) for yaw and local Z axis (row 3) for pitch.
            Vector3 axisYaw = new Vector3(head.M11, head.M12, head.M13);
            Vector3 axisPitchBase = new Vector3(head.M31, head.M32, head.M33);

            const float minAxisLenSq = 1e-6f;
            if (axisYaw.LengthSquared() < minAxisLenSq) axisYaw = Vector3.UnitZ;
            if (axisPitchBase.LengthSquared() < minAxisLenSq) axisPitchBase = Vector3.UnitX;
            axisYaw.Normalize();
            axisPitchBase.Normalize();

            Matrix yaw = Matrix.CreateFromAxisAngle(axisYaw, _headYaw);
            Vector3 pitchAxis = _headYaw == 0f ? axisPitchBase : Vector3.TransformNormal(axisPitchBase, yaw);
            if (pitchAxis.LengthSquared() < minAxisLenSq) pitchAxis = axisPitchBase;
            pitchAxis.Normalize();

            Matrix pitch = Matrix.CreateFromAxisAngle(pitchAxis, _headPitch);
            Matrix rot = yaw * pitch;

            Matrix delta = Matrix.CreateTranslation(-headPos) * rot * Matrix.CreateTranslation(headPos);

            for (int i = 0; i < _headBoneSubtreeIndices.Length; i++)
            {
                int idx = _headBoneSubtreeIndices[i];
                if ((uint)idx >= (uint)boneTransforms.Length)
                    continue;

                boneTransforms[idx] = boneTransforms[idx] * delta;
            }

            return true;
        }

        private void HookInventoryEvents()
        {
            if (_networkManager != null)
            {
                // Subscribe to equipment-only changes to avoid reloading on pure inventory grid moves
                _networkManager.GetCharacterState().EquipmentChanged += OnEquipmentChanged;
                _networkManager.GetCharacterState().AttackSpeedsChanged += OnAttackSpeedsChanged;
            }
        }

        private void UnhookInventoryEvents()
        {
            if (_networkManager == null) return;
            try
            {
                _networkManager.GetCharacterState().EquipmentChanged -= OnEquipmentChanged;
                _networkManager.GetCharacterState().AttackSpeedsChanged -= OnAttackSpeedsChanged;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to unhook inventory events. This may happen on shutdown.");
            }
        }

        private void OnEquipmentChanged()
        {
            if (!IsMainWalker) return;
            // Equipment updates can arrive from networking threads; marshal to main thread.
            MuGame.ScheduleOnMainThread(() => _ = RunInventoryAppearanceUpdateAsync());
        }

        private void OnAttackSpeedsChanged()
        {
            if (!IsMainWalker) return;
            // Attack speed updates can arrive from networking threads; marshal to main thread.
            MuGame.ScheduleOnMainThread(() => UpdateAttackAnimationSpeeds());
        }

        private async Task RunInventoryAppearanceUpdateAsync()
        {
            lock (_inventoryAppearanceUpdateSync)
            {
                if (_inventoryAppearanceUpdateRunning)
                {
                    _inventoryAppearanceUpdatePending = true;
                    return;
                }

                _inventoryAppearanceUpdateRunning = true;
                _inventoryAppearanceUpdatePending = false;
            }

            try
            {
                while (true)
                {
                    await UpdateAppearanceFromInventoryAsync();

                    lock (_inventoryAppearanceUpdateSync)
                    {
                        if (_inventoryAppearanceUpdatePending)
                        {
                            _inventoryAppearanceUpdatePending = false;
                            continue;
                        }

                        _inventoryAppearanceUpdateRunning = false;
                        break;
                    }
                }
            }
            finally
            {
                lock (_inventoryAppearanceUpdateSync)
                {
                    _inventoryAppearanceUpdateRunning = false;
                    _inventoryAppearanceUpdatePending = false;
                }
            }
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
                _helmItemEquipped = true;
            }
            else
            {
                _helmItemEquipped = false;
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
                EquippedWings.Type = 0;
                EquippedWings.ItemIndex = (short)wingsDef.Id;
                EquippedWings.LinkParentAnimation = false;
            }
            else
            {
                // For the main player we rely on the inventory slot as source of truth.
                // Only if an item is present but couldn't be parsed, fall back to appearance mapping.
                if (inventory.ContainsKey(InventoryConstants.WingsSlot) && Appearance.WingInfo.HasWings)
                {
                    var mappedIndex = TryMapWingAppearanceToItemIndex(Appearance.WingInfo, CharacterClass);
                    if (mappedIndex.HasValue)
                    {
                        EquippedWings.Hidden = false;
                        EquippedWings.Type = 0;
                        EquippedWings.ItemIndex = mappedIndex.Value;
                        EquippedWings.LinkParentAnimation = false;
                    }
                    else
                    {
                        EquippedWings.Hidden = true;
                        EquippedWings.Type = 0;
                        EquippedWings.ItemIndex = -1;
                    }
                }
                else
                {
                    EquippedWings.Hidden = true;
                    EquippedWings.Type = 0;
                    EquippedWings.ItemIndex = -1;
                }
            }

            // Left Hand
            var leftHandDef = GetItemDef(InventoryConstants.LeftHandSlot);
            if (leftHandDef != null)
            {
                Weapon1.Model = await BMDLoader.Instance.Prepare(leftHandDef.TexturePath);
                Weapon1.TexturePath = leftHandDef.TexturePath;
                Weapon1.ItemGroup = ItemDatabase.GetItemGroup(inventory[InventoryConstants.LeftHandSlot]);
                Weapon1.LinkParentAnimation = false;
                SetItemProperties(Weapon1, inventory[InventoryConstants.LeftHandSlot]);
                RefreshWeaponAttachment(Weapon1, isLeftHand: true);
            }
            else
            {
                Weapon1.Model = null;
                Weapon1.TexturePath = null;
            }

            // Right Hand
            var rightHandDef = GetItemDef(InventoryConstants.RightHandSlot);
            if (rightHandDef != null)
            {
                Weapon2.Model = await BMDLoader.Instance.Prepare(rightHandDef.TexturePath);
                Weapon2.TexturePath = rightHandDef.TexturePath;
                Weapon2.ItemGroup = ItemDatabase.GetItemGroup(inventory[InventoryConstants.RightHandSlot]);
                Weapon2.LinkParentAnimation = false;
                SetItemProperties(Weapon2, inventory[InventoryConstants.RightHandSlot]);
                RefreshWeaponAttachment(Weapon2, isLeftHand: false);
            }
            else
            {
                Weapon2.Model = null;
                Weapon2.TexturePath = null;
            }

            await EnsureHelmHeadVisibleAsync();
        }

        private async Task UpdateEquipmentAppearanceAsync()
        {
            if (Appearance.RawData.IsEmpty) return; // No appearance data to process

            static bool HasEquippedAppearanceItem(short index)
                => index != 0xFF && index != 0x1FF;

            // Helm
            if (HasEquippedAppearanceItem(Appearance.HelmItemIndex))
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
                _helmItemEquipped = true;
            }
            else
            {
                _helmItemEquipped = false;
            }
            // Armor
            if (HasEquippedAppearanceItem(Appearance.ArmorItemIndex))
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
            if (HasEquippedAppearanceItem(Appearance.PantsItemIndex))
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
            if (HasEquippedAppearanceItem(Appearance.GlovesItemIndex))
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
            if (HasEquippedAppearanceItem(Appearance.BootsItemIndex))
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
                var mappedIndex = TryMapWingAppearanceToItemIndex(Appearance.WingInfo, CharacterClass);
                if (mappedIndex.HasValue)
                {
                    EquippedWings.Hidden = false;
                    EquippedWings.Type = 0;
                    EquippedWings.ItemIndex = mappedIndex.Value;
                    EquippedWings.LinkParentAnimation = false;
                }
                else
                {
                    EquippedWings.Hidden = true;
                    EquippedWings.Type = 0;
                    EquippedWings.ItemIndex = -1;
                }
            }
            else
            {
                EquippedWings.Hidden = true;
                EquippedWings.Type = 0;
                EquippedWings.ItemIndex = -1;
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
                    Weapon1.TexturePath = leftHandDef.TexturePath;
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
                    Weapon1.TexturePath = null;
                }
            }
            else
            {
                Weapon1.Model = null;
                Weapon1.TexturePath = null;
            }

            if (Appearance.RightHandItemIndex != 255 && Appearance.RightHandItemIndex != 0xFF)
            {
                var rightHandDef = ItemDatabase.GetItemDefinition(Appearance.RightHandItemGroup, Appearance.RightHandItemIndex);
                if (rightHandDef != null)
                {
                    Weapon2.Model = await BMDLoader.Instance.Prepare(rightHandDef.TexturePath);
                    Weapon2.TexturePath = rightHandDef.TexturePath;
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
                    Weapon2.TexturePath = null;
                }
            }
            else
            {
                Weapon2.Model = null;
                Weapon2.TexturePath = null;
            }
            await EnsureHelmHeadVisibleAsync();
        }
        public async Task UpdateEquipmentAppearanceFromConfig(AppearanceConfig appearanceConfig)
        {
            if (appearanceConfig == null) return; // No appearance data to process

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
                _helmItemEquipped = true;
            }
            else
            {
                _helmItemEquipped = false;
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
                    Weapon1.TexturePath = leftHandDef.TexturePath;
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
                    Weapon1.TexturePath = null;
                }
            }
            else
            {
                Weapon1.Model = null;
                Weapon1.TexturePath = null;
            }

            if (appearanceConfig.RightHandItemIndex != 255 && appearanceConfig.RightHandItemIndex != 0xFF)
            {
                var rightHandDef = ItemDatabase.GetItemDefinition(appearanceConfig.RightHandItemGroup, appearanceConfig.RightHandItemIndex);
                if (rightHandDef != null)
                {
                    Weapon2.Model = await BMDLoader.Instance.Prepare(rightHandDef.TexturePath);
                    Weapon2.TexturePath = rightHandDef.TexturePath;
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
                    Weapon2.TexturePath = null;
                }
            }
            else
            {
                Weapon2.Model = null;
                Weapon2.TexturePath = null;
            }

            await EnsureHelmHeadVisibleAsync();
        }

        private void SetActionSpeed(PlayerAction action, float speed)
        {
            int idx = (int)action;
            if (Model?.Actions is { Length: > 0 } actions && idx < actions.Length)
                actions[idx].PlaySpeed = speed;
        }

        private void SetActionSpeedRange(PlayerAction start, PlayerAction end, float speed)
        {
            int from = (int)start;
            int to = (int)end;
            if (from > to)
            {
                int tmp = from;
                from = to;
                to = tmp;
            }

            for (int i = from; i <= to; i++)
            {
                SetActionSpeed((PlayerAction)i, speed);
            }
        }

        private void InitializeActionSpeeds()
        {
            // Mirrors SourceMain5.2 ZzzOpenData.cpp player action PlaySpeed table.
            SetActionSpeedRange(PlayerAction.PlayerStopMale, PlayerAction.PlayerStopRideWeapon, 0.28f);
            SetActionSpeed(PlayerAction.PlayerStopSword, 0.26f);
            SetActionSpeed(PlayerAction.PlayerStopTwoHandSword, 0.24f);
            SetActionSpeed(PlayerAction.PlayerStopSpear, 0.24f);
            SetActionSpeed(PlayerAction.PlayerStopBow, 0.22f);
            SetActionSpeed(PlayerAction.PlayerStopCrossbow, 0.22f);
            SetActionSpeed(PlayerAction.PlayerStopSummoner, 0.24f);
            SetActionSpeed(PlayerAction.PlayerStopWand, 0.30f);

            // Walk animations = 0.33f (from ZzzCharacter.cpp:429, NOT ZzzOpenData.cpp)
            SetActionSpeedRange(PlayerAction.PlayerWalkMale, PlayerAction.PlayerWalkCrossbow, 0.38f); // different animation speed?
            SetActionSpeed(PlayerAction.PlayerWalkWand, 0.44f);
            SetActionSpeed(PlayerAction.PlayerWalkSwim, 0.35f);

            // Run animations = 0.34f (faster than walk!)
            SetActionSpeedRange(PlayerAction.PlayerRun, PlayerAction.PlayerRunRideWeapon, 0.34f);
            SetActionSpeed(PlayerAction.PlayerRunWand, 0.76f);
            SetActionSpeed(PlayerAction.PlayerRunSwim, 0.35f);

            SetActionSpeedRange(PlayerAction.PlayerDefense1, PlayerAction.PlayerShock, 0.32f);
            SetActionSpeedRange(PlayerAction.PlayerDie1, PlayerAction.PlayerDie2, 0.45f);
            SetActionSpeedRange(PlayerAction.PlayerSit1, (PlayerAction)((int)PlayerAction.MaxPlayerAction - 1), 0.40f);

            SetActionSpeed(PlayerAction.PlayerShock, 0.40f);

            // Emote animations - set to normal speed (not affected by attack speed)
            SetActionSpeed(PlayerAction.PlayerSee1, 0.28f);
            SetActionSpeed(PlayerAction.PlayerSeeFemale1, 0.28f);
            SetActionSpeed(PlayerAction.PlayerWin1, 0.28f);
            SetActionSpeed(PlayerAction.PlayerWinFemale1, 0.28f);
            SetActionSpeed(PlayerAction.PlayerSmile1, 0.28f);
            SetActionSpeed(PlayerAction.PlayerSmileFemale1, 0.28f);

            SetActionSpeed(PlayerAction.PlayerHealing1, 0.20f);
            SetActionSpeed(PlayerAction.PlayerHealingFemale1, 0.20f);

            SetActionSpeed(PlayerAction.PlayerJack1, 0.38f);
            SetActionSpeed(PlayerAction.PlayerJack2, 0.38f);
            SetActionSpeed(PlayerAction.PlayerSanta1, 0.34f);
            SetActionSpeed(PlayerAction.PlayerSanta2, 0.30f);

            SetActionSpeed(PlayerAction.PlayerSkillRider, 0.20f);
            SetActionSpeed(PlayerAction.PlayerSkillRiderFly, 0.20f);

            SetActionSpeed(PlayerAction.PlayerStopTwoHandSwordTwo, 0.24f);
            SetActionSpeed(PlayerAction.PlayerWalkTwoHandSwordTwo, 0.30f);
            SetActionSpeed(PlayerAction.PlayerRunTwoHandSwordTwo, 0.30f);
            SetActionSpeed(PlayerAction.PlayerAttackTwoHandSwordTwo, 0.24f);

            SetActionSpeed(PlayerAction.PlayerAttackDeathstab, 0.45f);

            SetActionSpeed(PlayerAction.PlayerDarklordStand, 0.30f);
            SetActionSpeed(PlayerAction.PlayerDarklordWalk, 0.30f);
            SetActionSpeed(PlayerAction.PlayerStopRideHorse, 0.30f);
            SetActionSpeed(PlayerAction.PlayerRunRideHorse, 0.30f);
            SetActionSpeed(PlayerAction.PlayerAttackStrike, 0.20f);
            SetActionSpeed(PlayerAction.PlayerAttackTeleport, 0.28f);
            SetActionSpeed(PlayerAction.PlayerAttackRideStrike, 0.30f);
            SetActionSpeed(PlayerAction.PlayerAttackRideTeleport, 0.30f);
            SetActionSpeed(PlayerAction.PlayerAttackRideHorseSword, 0.28f);
            SetActionSpeed(PlayerAction.PlayerAttackRideAttackFlash, 0.30f);
            SetActionSpeed(PlayerAction.PlayerAttackRideAttackMagic, 0.30f);
            SetActionSpeed(PlayerAction.PlayerAttackDarkhorse, 0.20f);

            SetActionSpeed(PlayerAction.PlayerIdle1Darkhorse, 1.00f);
            SetActionSpeed(PlayerAction.PlayerIdle2Darkhorse, 1.00f);

            SetActionSpeedRange(PlayerAction.PlayerFenrirAttack, PlayerAction.PlayerFenrirWalkOneLeft, 0.45f);
            SetActionSpeedRange(PlayerAction.PlayerFenrirRun, PlayerAction.PlayerFenrirRunOneLeftElf, 0.71f);
            SetActionSpeedRange(PlayerAction.PlayerFenrirStand, PlayerAction.PlayerFenrirStandOneLeft, 0.40f);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackMagic, 0.30f);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackDarklordStrike, 0.30f);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackDarklordTeleport, 0.30f);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackDarklordSword, 0.28f);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackDarklordFlash, 0.30f);

            for (int i = (int)PlayerAction.PlayerRageFenrir; i <= (int)PlayerAction.PlayerRageFenrirAttackRight; i++)
            {
                float speed = (i >= (int)PlayerAction.PlayerRageFenrirTwoSword && i <= (int)PlayerAction.PlayerRageFenrirOneLeft)
                    ? 0.225f
                    : 0.45f;
                SetActionSpeed((PlayerAction)i, speed);
            }

            SetActionSpeedRange(PlayerAction.PlayerRageFenrirStandTwoSword, PlayerAction.PlayerRageFenrirStandOneLeft, 0.20f);
            SetActionSpeed(PlayerAction.PlayerRageFenrirStand, 0.21f);

            SetActionSpeedRange(PlayerAction.PlayerRageFenrirRun, PlayerAction.PlayerRageFenrirRunOneLeft, 0.355f);
            SetActionSpeed(PlayerAction.PlayerRageUniRun, 0.30f);
            SetActionSpeed(PlayerAction.PlayerRageUniAttackOneRight, 0.20f);
            SetActionSpeed(PlayerAction.PlayerRageUniStopOneRight, 0.18f);
            SetActionSpeed(PlayerAction.PlayerStopRagefighter, 0.16f);
        }

        private void UpdateAttackAnimationSpeeds()
        {
            if (!IsMainWalker || Model?.Actions == null)
                return;

            var state = _networkManager?.GetCharacterState();
            if (state == null)
                return;

            ushort attackSpeed = state.AttackSpeed;
            ushort magicSpeed = state.MagicSpeed;

            if (attackSpeed == _lastAttackSpeedStat && magicSpeed == _lastMagicSpeedStat)
                return;

            ApplyAttackSpeedToActions(attackSpeed, magicSpeed);
            _lastAttackSpeedStat = attackSpeed;
            _lastMagicSpeedStat = magicSpeed;
        }

        private void ApplyAttackSpeedToActions(ushort attackSpeed, ushort magicSpeed)
        {
            float attackSpeed1 = attackSpeed * 0.004f;
            float magicSpeed1 = magicSpeed * 0.004f;
            float magicSpeed2 = magicSpeed * 0.002f;
            float rageAttackSpeed = attackSpeed * 0.002f;

            SetActionSpeed(PlayerAction.PlayerAttackFist, 0.6f + attackSpeed1);

            for (int i = (int)PlayerAction.PlayerAttackSwordRight1; i <= (int)PlayerAction.PlayerAttackRideCrossbow; i++)
                SetActionSpeed((PlayerAction)i, 0.25f + attackSpeed1);

            SetActionSpeed(PlayerAction.PlayerAttackSkillSword1, 0.30f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackSkillSword2, 0.30f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackSkillSword3, 0.27f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackSkillSword4, 0.30f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackSkillSword5, 0.24f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackSkillWheel, 0.24f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackDeathstab, 0.25f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackSkillSpear, 0.30f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerSkillRider, 0.30f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerSkillRiderFly, 0.30f + attackSpeed1);

            SetActionSpeed(PlayerAction.PlayerAttackTwoHandSwordTwo, 0.25f + attackSpeed1);

            for (int i = (int)PlayerAction.PlayerAttackBow; i <= (int)PlayerAction.PlayerAttackFlyCrossbow; i++)
                SetActionSpeed((PlayerAction)i, 0.30f + attackSpeed1);
            for (int i = (int)PlayerAction.PlayerAttackRideBow; i <= (int)PlayerAction.PlayerAttackRideCrossbow; i++)
                SetActionSpeed((PlayerAction)i, 0.30f + attackSpeed1);

            SetActionSpeed(PlayerAction.PlayerSkillElf1, 0.25f + magicSpeed1);

            for (int i = (int)PlayerAction.PlayerSkillHand1; i <= (int)PlayerAction.PlayerSkillWeapon2; i++)
                SetActionSpeed((PlayerAction)i, 0.29f + magicSpeed2);

            SetActionSpeed(PlayerAction.PlayerSkillTeleport, 0.30f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillFlash, 0.40f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillInferno, 0.60f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillHell, 0.50f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerRideSkill, 0.30f + magicSpeed2);

            SetActionSpeed(PlayerAction.PlayerSkillHellBegin, 0.50f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerAttackStrike, 0.25f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackRideStrike, 0.20f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackRideHorseSword, 0.25f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackRideAttackFlash, 0.40f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerAttackRideAttackMagic, 0.30f + magicSpeed2);

            SetActionSpeed(PlayerAction.PlayerFenrirAttack, 0.25f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackDarklordStrike, 0.20f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackDarklordSword, 0.25f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackDarklordFlash, 0.40f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackTwoSword, 0.25f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackMagic, 0.37f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackCrossbow, 0.30f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackSpear, 0.25f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackOneSword, 0.25f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackBow, 0.30f + attackSpeed1);

            for (int i = (int)PlayerAction.PlayerAttackBowUp; i <= (int)PlayerAction.PlayerAttackRideCrossbowUp; i++)
                SetActionSpeed((PlayerAction)i, 0.30f + attackSpeed1);

            SetActionSpeed(PlayerAction.PlayerAttackOneFlash, 0.40f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackRush, 0.30f + attackSpeed1);
            SetActionSpeed(PlayerAction.PlayerAttackDeathCannon, 0.20f + attackSpeed1);

            SetActionSpeed(PlayerAction.PlayerSkillSleep, 0.30f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillSleepUni, 0.30f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillSleepDino, 0.30f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillSleepFenrir, 0.30f + magicSpeed2);

            SetActionSpeed(PlayerAction.PlayerSkillLightningOrb, 0.40f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillLightningOrbUni, 0.25f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillLightningOrbDino, 0.25f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillLightningOrbFenrir, 0.25f + magicSpeed2);

            SetActionSpeed(PlayerAction.PlayerSkillChainLightning, 0.25f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillChainLightningUni, 0.15f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillChainLightningDino, 0.15f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillChainLightningFenrir, 0.15f + magicSpeed2);

            SetActionSpeed(PlayerAction.PlayerSkillDrainLife, 0.25f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillDrainLifeUni, 0.25f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillDrainLifeDino, 0.25f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillDrainLifeFenrir, 0.25f + magicSpeed2);

            SetActionSpeed(PlayerAction.PlayerSkillGiganticstorm, 0.55f + magicSpeed1);
            SetActionSpeed(PlayerAction.PlayerSkillFlamestrike, 0.69f + magicSpeed2);
            SetActionSpeed(PlayerAction.PlayerSkillLightningShock, 0.35f + magicSpeed2);

            SetActionSpeed(PlayerAction.PlayerSkillSummon, 0.25f);
            SetActionSpeed(PlayerAction.PlayerSkillSummonUni, 0.25f);
            SetActionSpeed(PlayerAction.PlayerSkillSummonDino, 0.25f);
            SetActionSpeed(PlayerAction.PlayerSkillSummonFenrir, 0.25f);

            SetActionSpeed(PlayerAction.PlayerSkillBlowOfDestruction, 0.30f);
            SetActionSpeed(PlayerAction.PlayerSkillRecovery, 0.33f);
            SetActionSpeed(PlayerAction.PlayerSkillSwellOfMp, 0.20f);

            SetActionSpeed(PlayerAction.PlayerAttackSkillFuryStrike, 0.38f);
            SetActionSpeed(PlayerAction.PlayerSkillVitality, 0.34f);
            SetActionSpeed(PlayerAction.PlayerSkillHellStart, 0.30f);
            SetActionSpeed(PlayerAction.PlayerAttackTeleport, 0.28f);
            SetActionSpeed(PlayerAction.PlayerAttackRideTeleport, 0.30f);
            SetActionSpeed(PlayerAction.PlayerAttackDarkhorse, 0.30f);
            SetActionSpeed(PlayerAction.PlayerFenrirAttackDarklordTeleport, 0.30f);
            SetActionSpeed(PlayerAction.PlayerAttackRemoval, 0.28f);

            SetActionSpeed(PlayerAction.PlayerSkillThrust, 0.40f + rageAttackSpeed);
            SetActionSpeed(PlayerAction.PlayerSkillStamp, 0.40f + rageAttackSpeed);
            SetActionSpeed(PlayerAction.PlayerSkillGiantswing, 0.40f + rageAttackSpeed);
            SetActionSpeed(PlayerAction.PlayerSkillDarksideReady, 0.30f + rageAttackSpeed);
            SetActionSpeed(PlayerAction.PlayerSkillDarksideAttack, 0.30f + rageAttackSpeed);
            SetActionSpeed(PlayerAction.PlayerSkillDragonkick, 0.40f + rageAttackSpeed);
            SetActionSpeed(PlayerAction.PlayerSkillDragonlore, 0.30f + rageAttackSpeed);
            SetActionSpeed(PlayerAction.PlayerSkillAttUpOurforces, 0.35f);
            SetActionSpeed(PlayerAction.PlayerSkillHpUpOurforces, 0.35f);
            SetActionSpeed(PlayerAction.PlayerRageFenrirAttackRight, 0.25f + rageAttackSpeed);
        }

        // ───────────────────────────────── UPDATE LOOP ─────────────────────────────────
        public override void Update(GameTime gameTime)
        {
            if (IsMainWalker)
            {
                var scene = MuGame.Instance.ActiveScene as BaseScene;
                _isLookingAtCursor = ShouldLookAtCursor(scene);

                // Rotate body first (if needed), then compute head target from the updated body angle.
                // This removes the 1-frame mismatch that caused a visible "head shake" on stand turns.
                UpdateStandingDirectionTowardsCursor(gameTime);
                UpdateHeadRotationTowardsCursor();
            }

            base.Update(gameTime); // movement, camera for main walker, etc.

            UpdateEquipmentAnimationStride();

            if (World is not WalkableWorldControl world)
                return;

            if (IsMainWalker)
                UpdateLocalPlayer(world, gameTime);
            else
                UpdateRemotePlayer(world, gameTime);

            UpdateWingAnimationSpeed();
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

        private void UpdateWingAnimationSpeed()
        {
            if (EquippedWings == null || EquippedWings.Hidden)
                return;

            short wingIndex = GetEquippedWingIndex();
            if (wingIndex < 0)
                return;

            bool isFlyingAction = CurrentAction == PlayerAction.PlayerFly ||
                                  CurrentAction == PlayerAction.PlayerFlyCrossbow;

            float desiredSpeed = 0.25f;

            if (wingIndex == WingOfRuinIndex)
            {
                desiredSpeed = 0.15f;
            }
            else if (isFlyingAction)
            {
                desiredSpeed = wingIndex == WingOfStormIndex ? 0.5f : 1f;
            }

            if (Math.Abs(desiredSpeed - _lastWingAnimationSpeed) > 0.0001f)
            {
                EquippedWings.AnimationSpeed = desiredSpeed;
                _lastWingAnimationSpeed = desiredSpeed;
            }
        }

        private short GetEquippedWingIndex()
        {
            if (EquippedWings == null)
                return -1;

            if (EquippedWings.ItemIndex >= 0)
                return EquippedWings.ItemIndex;

            return EquippedWings.Type > 0 ? EquippedWings.Type : (short)-1;
        }

        public PlayerAction GetSkillAction(ushort skillId, bool isInSafeZone)
        {
            int animationId = SkillDatabase.GetSkillAnimation(skillId);
            if (animationId > 0 && (Model?.Actions == null || animationId < Model.Actions.Length))
                return (PlayerAction)animationId;

            return GetDefaultSkillAction(isInSafeZone);
        }

        private PlayerAction GetDefaultSkillAction(bool isInSafeZone)
        {
            if (_isRiding && !isInSafeZone)
            {
                if (_currentVehicleIndex == 7 || _currentVehicleIndex == 8)
                    return PlayerAction.PlayerRideSkill;

                if (IsFenrirVehicle(_currentVehicleIndex))
                    return PlayerAction.PlayerFenrirAttackMagic;
            }

            if (_isFemale)
                return PlayerAction.PlayerSkillElf1;

            return MuGame.Random.Next(2) == 0 ? PlayerAction.PlayerSkillHand1 : PlayerAction.PlayerSkillHand2;
        }

        // --------------- Helpers for correct animation selection ----------------
        private enum WeaponKind
        {
            None,
            Sword,
            TwoHandSword,
            TwoHandSwordTwo,
            Spear,
            Scythe,
            Bow,
            Crossbow,
            StaffOneHand,
            StaffTwoHand,
            SummonerStick,
            Book
        }

        private readonly struct WeaponContext
        {
            public WeaponContext(
                ItemDefinition right,
                ItemDefinition left,
                byte rightGroup,
                byte leftGroup,
                WeaponKind rightKind,
                WeaponKind leftKind,
                bool rightIsAmmo,
                bool leftIsAmmo)
            {
                Right = right;
                Left = left;
                RightGroup = rightGroup;
                LeftGroup = leftGroup;
                RightKind = rightKind;
                LeftKind = leftKind;
                RightIsAmmo = rightIsAmmo;
                LeftIsAmmo = leftIsAmmo;
            }

            public ItemDefinition Right { get; }
            public ItemDefinition Left { get; }
            public byte RightGroup { get; }
            public byte LeftGroup { get; }
            public WeaponKind RightKind { get; }
            public WeaponKind LeftKind { get; }
            public bool RightIsAmmo { get; }
            public bool LeftIsAmmo { get; }

            public bool HasRightWeapon => !RightIsAmmo && RightKind != WeaponKind.None;
            public bool HasLeftWeapon => !LeftIsAmmo && LeftKind != WeaponKind.None;
            public bool HasAnyWeapon => HasRightWeapon || HasLeftWeapon;
            public bool HasDualWeapons => HasRightWeapon && HasLeftWeapon;
            public WeaponKind PrimaryKind => RightKind != WeaponKind.None ? RightKind : LeftKind;
        }

        private static bool IsSummonerClass(CharacterClassNumber cls) =>
            cls == CharacterClassNumber.Summoner ||
            cls == CharacterClassNumber.BloodySummoner ||
            cls == CharacterClassNumber.DimensionMaster;

        private static bool IsDarkKnightClass(CharacterClassNumber cls) =>
            cls == CharacterClassNumber.DarkKnight ||
            cls == CharacterClassNumber.BladeKnight ||
            cls == CharacterClassNumber.BladeMaster;

        private static bool IsRageFighterClass(CharacterClassNumber cls) =>
            cls == CharacterClassNumber.RageFighter ||
            cls == CharacterClassNumber.FistMaster;

        private static bool IsElfClass(CharacterClassNumber cls) =>
            cls == CharacterClassNumber.FairyElf ||
            cls == CharacterClassNumber.MuseElf ||
            cls == CharacterClassNumber.HighElf;

        private static bool IsDarkWizardClass(CharacterClassNumber cls) =>
            cls == CharacterClassNumber.DarkWizard ||
            cls == CharacterClassNumber.SoulMaster ||
            cls == CharacterClassNumber.GrandMaster;

        private static bool IsBloodCastleMap(short worldIndex) =>
            (worldIndex >= 11 && worldIndex <= 17) || worldIndex == 52;

        private static bool IsChaosCastleMap(short worldIndex) =>
            (worldIndex >= 18 && worldIndex <= 23) || worldIndex == 53 || worldIndex == 97;

        private static bool IsAmmo(ItemDefinition item)
        {
            if (item?.Name == null)
                return false;

            return string.Equals(item.Name, "Arrow", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(item.Name, "Bolt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSpecialTwoHandSwordTwo(ItemDefinition item)
        {
            if (item?.Name == null)
                return false;

            return item.Name.Contains("Dark Reign", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Rune Blade", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Explosion Blade", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Sword Dancer", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCrossbow(ItemDefinition item)
        {
            if (item?.Name == null)
                return false;

            return item.Name.Contains("Crossbow", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBook(ItemDefinition item)
        {
            if (item?.Name == null)
                return false;

            return item.Name.Contains("Book", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSummonerStick(ItemDefinition item)
        {
            if (item?.Name == null)
                return false;

            return item.Name.Contains("Stick", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsScythe(ItemDefinition item)
        {
            if (item?.Name == null)
                return false;

            return item.Name.Contains("Scythe", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSpear(ItemDefinition item)
        {
            if (item?.Name == null)
                return false;

            return item.Name.Contains("Spear", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Lance", StringComparison.OrdinalIgnoreCase);
        }

        private static WeaponKind GetWeaponKind(ItemDefinition item, byte group)
        {
            if (item == null)
                return WeaponKind.None;

            switch (group)
            {
                case 0:
                case 1:
                case 2:
                    if (IsSpecialTwoHandSwordTwo(item))
                        return WeaponKind.TwoHandSwordTwo;
                    return item.TwoHanded ? WeaponKind.TwoHandSword : WeaponKind.Sword;
                case 3:
                    if (IsScythe(item))
                        return WeaponKind.Scythe;
                    if (IsSpear(item))
                        return WeaponKind.Spear;
                    return item.TwoHanded ? WeaponKind.Scythe : WeaponKind.Spear;
                case 4:
                    return IsCrossbow(item) ? WeaponKind.Crossbow : WeaponKind.Bow;
                case 5:
                    if (IsBook(item))
                        return WeaponKind.Book;
                    if (IsSummonerStick(item))
                        return WeaponKind.SummonerStick;
                    return item.TwoHanded ? WeaponKind.StaffTwoHand : WeaponKind.StaffOneHand;
                default:
                    return WeaponKind.None;
            }
        }

        private WeaponContext GetWeaponContext()
        {
            if (!IsMainWalker || _networkManager == null)
            {
                // Remote players: use class defaults to avoid incorrect local-inventory mapping.
                var defaultWeapon = Equipment.GetDefaultWeaponTypeForClass(CharacterClass);
                WeaponKind kind = defaultWeapon switch
                {
                    WeaponType.Sword => WeaponKind.Sword,
                    WeaponType.TwoHandSword => WeaponKind.TwoHandSword,
                    WeaponType.Spear => WeaponKind.Spear,
                    WeaponType.Bow => WeaponKind.Bow,
                    WeaponType.Crossbow => WeaponKind.Crossbow,
                    WeaponType.Staff => WeaponKind.StaffTwoHand,
                    WeaponType.Scythe => WeaponKind.Scythe,
                    WeaponType.Book => WeaponKind.Book,
                    _ => WeaponKind.None
                };

                return new WeaponContext(null, null, 0, 0, kind, WeaponKind.None, false, false);
            }

            var charState = _networkManager.GetCharacterState();
            var inventory = charState.GetInventoryItems();

            ItemDefinition left = null;
            ItemDefinition right = null;
            byte leftGroup = 0;
            byte rightGroup = 0;

            if (inventory.TryGetValue(InventoryConstants.LeftHandSlot, out var leftData))
            {
                left = ItemDatabase.GetItemDefinition(leftData);
                leftGroup = ItemDatabase.GetItemGroup(leftData);
            }

            if (inventory.TryGetValue(InventoryConstants.RightHandSlot, out var rightData))
            {
                right = ItemDatabase.GetItemDefinition(rightData);
                rightGroup = ItemDatabase.GetItemGroup(rightData);
            }

            bool leftIsAmmo = IsAmmo(left);
            bool rightIsAmmo = IsAmmo(right);

            var leftKind = leftIsAmmo ? WeaponKind.None : GetWeaponKind(left, leftGroup);
            var rightKind = rightIsAmmo ? WeaponKind.None : GetWeaponKind(right, rightGroup);

            return new WeaponContext(right, left, rightGroup, leftGroup, rightKind, leftKind, rightIsAmmo, leftIsAmmo);
        }

        private bool HasActiveBuff(byte effectId)
        {
            var charState = _networkManager?.GetCharacterState();
            if (charState == null)
            {
                return false;
            }

            ushort id = NetworkId != 0 ? NetworkId : charState.Id;
            return charState.HasActiveBuff(effectId, id);
        }

        private void GetEquipmentLevels(out int bootsLevel, out int glovesLevel)
        {
            if (IsMainWalker)
            {
                bootsLevel = Boots?.ItemLevel ?? 0;
                glovesLevel = Gloves?.ItemLevel ?? 0;
                return;
            }

            bootsLevel = Appearance.BootsItemLevel;
            glovesLevel = Appearance.GlovesItemLevel;
        }

        private static bool IsUnderwaterRunMap(short worldIndex) =>
            worldIndex == 8 || worldIndex == 68; // Atlans + Doppelganger Underwater

        private static bool IsFenrirVehicle(short vehicleIndex) =>
            vehicleIndex >= 11 && vehicleIndex <= 18;

        private static bool IsFenrirExcellentVehicle(short vehicleIndex) =>
            vehicleIndex == 11 || vehicleIndex == 12 || vehicleIndex == 13 ||
            vehicleIndex == 15 || vehicleIndex == 16 || vehicleIndex == 17;

        private bool HasFastWingEquipped()
        {
            if (!HasEquippedWings)
                return false;

            short wingIndex = GetEquippedWingIndex();
            return wingIndex == WingOfDragonIndex || wingIndex == WingOfStormIndex;
        }

        private float GetFenrirSpeedUnits()
        {
            if (_runFrames < FenrirRunDelayFrames / 2f)
                return FenrirSpeedStage1;
            if (_runFrames < FenrirRunDelayFrames)
                return FenrirSpeedStage2;
            return IsFenrirExcellentVehicle(_currentVehicleIndex) ? FenrirSpeedExcellent : FenrirSpeedNormal;
        }

        private bool UpdateMovementSpeedAndRunState(WalkableWorldControl world, TWFlags flags, bool isAboutToMove)
        {
            bool isInSafeZone = flags.HasFlag(TWFlags.SafeZone);
            bool hasFenrir = _isRiding && IsFenrirVehicle(_currentVehicleIndex);
            bool hasDarkHorse = _isRiding && _currentVehicleIndex == 0;
            bool hasUniriaOrDino = _isRiding && (_currentVehicleIndex == 7 || _currentVehicleIndex == 8);
            bool hasWings = HasEquippedWings && !isInSafeZone;

            GetEquipmentLevels(out int bootsLevel, out int glovesLevel);
            bool useGlovesForRun = IsUnderwaterRunMap(world.WorldIndex);
            bool hasRunItem = useGlovesForRun ? glovesLevel >= 5 : bootsLevel >= 5;

            bool canRun =
                IsDarkKnightClass(CharacterClass) ||
                IsDarkLordClass(CharacterClass) ||
                IsRageFighterClass(CharacterClass) ||
                hasRunItem ||
                hasFenrir;

            if (!isAboutToMove || isInSafeZone)
            {
                _runFrames = 0f;
            }
            else if (_runFrames < RunActivationFrames && canRun)
            {
                _runFrames += FPSCounter.Instance.FPS_ANIMATION_FACTOR;
                if (_runFrames > RunActivationFrames)
                    _runFrames = RunActivationFrames;
            }

            bool inChaosCastle = IsChaosCastleMap(world.WorldIndex);
            if (!isInSafeZone && (hasDarkHorse || hasWings || hasUniriaOrDino || inChaosCastle))
            {
                _runFrames = RunActivationFrames;
            }

            if (HasActiveBuff(BuffCursedTempleQuickness))
            {
                _runFrames = RunActivationFrames;
            }

            float speedUnits;
            if (isInSafeZone)
            {
                speedUnits = BaseWalkSpeedUnits;
            }
            else if (inChaosCastle)
            {
                speedUnits = BaseRunSpeedUnits;
            }
            else if (hasFenrir)
            {
                speedUnits = GetFenrirSpeedUnits();
            }
            else if (hasDarkHorse)
            {
                speedUnits = DarkHorseSpeedUnits;
            }
            else if (hasWings || hasUniriaOrDino)
            {
                speedUnits = HasFastWingEquipped() ? WingFastSpeedUnits : BaseRunSpeedUnits;
            }
            else
            {
                speedUnits = _runFrames >= RunActivationFrames ? BaseRunSpeedUnits : BaseWalkSpeedUnits;
            }

            if (HasActiveBuff(DebuffFreeze))
            {
                speedUnits *= 0.5f;
            }
            else if (HasActiveBuff(DebuffBlowOfDestruction))
            {
                speedUnits *= 0.33f;
            }

            if (HasActiveBuff(BuffCursedTempleQuickness))
            {
                speedUnits = CursedTempleQuicknessSpeedUnits;
            }

            MoveSpeed = Constants.MOVE_SPEED * (speedUnits / BaseWalkSpeedUnits);
            return _runFrames >= RunActivationFrames;
        }

        private static bool IsTwoHandedWeaponKind(WeaponKind kind) =>
            kind == WeaponKind.TwoHandSword ||
            kind == WeaponKind.TwoHandSwordTwo ||
            kind == WeaponKind.Scythe ||
            kind == WeaponKind.StaffTwoHand;

        private PlayerAction GetAttackAction(WeaponContext weapons, bool isInSafeZone)
        {
            if (_isRiding)
            {
                if (_currentVehicleIndex >= 11 && _currentVehicleIndex <= 18)
                    return GetFenrirAttackAction(weapons);

                if (_currentVehicleIndex == 0)
                    return PlayerAction.PlayerAttackRideHorseSword;

                if (_currentVehicleIndex == 7 || _currentVehicleIndex == 8)
                    return GetRideAttackAction(weapons);
            }

            return GetGroundAttackAction(weapons, isInSafeZone);
        }

        private PlayerAction GetFenrirAttackAction(WeaponContext weapons)
        {
            PlayerAction action;
            var primary = weapons.PrimaryKind;

            if (primary == WeaponKind.Spear || primary == WeaponKind.Scythe)
            {
                action = PlayerAction.PlayerFenrirAttackSpear;
            }
            else if (primary == WeaponKind.Bow)
            {
                action = PlayerAction.PlayerFenrirAttackBow;
            }
            else if (primary == WeaponKind.Crossbow)
            {
                action = PlayerAction.PlayerFenrirAttackCrossbow;
            }
            else if (weapons.HasDualWeapons)
            {
                action = PlayerAction.PlayerFenrirAttackTwoSword;
            }
            else if (weapons.HasRightWeapon)
            {
                action = PlayerAction.PlayerFenrirAttackOneSword;
            }
            else if (!weapons.HasRightWeapon && weapons.HasLeftWeapon && IsRageFighterClass(CharacterClass))
            {
                action = PlayerAction.PlayerRageFenrirAttackRight;
            }
            else if (!weapons.HasRightWeapon && weapons.HasLeftWeapon)
            {
                action = PlayerAction.PlayerFenrirAttackOneSword;
            }
            else
            {
                action = PlayerAction.PlayerFenrirAttack;
            }

            if (IsDarkLordClass(CharacterClass))
                action = PlayerAction.PlayerFenrirAttackDarklordSword;

            return action;
        }

        private PlayerAction GetRideAttackAction(WeaponContext weapons)
        {
            var primary = weapons.PrimaryKind;

            if (primary == WeaponKind.Spear)
                return PlayerAction.PlayerAttackRideSpear;
            if (primary == WeaponKind.Scythe)
                return PlayerAction.PlayerAttackRideScythe;
            if (primary == WeaponKind.Bow)
                return PlayerAction.PlayerAttackRideBow;
            if (primary == WeaponKind.Crossbow)
                return PlayerAction.PlayerAttackRideCrossbow;

            if (!weapons.HasAnyWeapon)
            {
                return IsRageFighterClass(CharacterClass)
                    ? PlayerAction.PlayerRageUniAttack
                    : PlayerAction.PlayerAttackRideSword;
            }

            bool rightTwoHand = IsTwoHandedWeaponKind(primary);

            if (IsRageFighterClass(CharacterClass))
            {
                return rightTwoHand
                    ? PlayerAction.PlayerRageUniAttackOneRight
                    : PlayerAction.PlayerRageUniAttack;
            }

            return rightTwoHand
                ? PlayerAction.PlayerAttackRideTwoHandSword
                : PlayerAction.PlayerAttackRideSword;
        }

        private PlayerAction GetGroundAttackAction(WeaponContext weapons, bool isInSafeZone)
        {
            if (!weapons.HasAnyWeapon)
                return PlayerAction.PlayerAttackFist;

            var primary = weapons.PrimaryKind;

            if (weapons.HasDualWeapons && weapons.RightKind == WeaponKind.Sword && weapons.LeftKind == WeaponKind.Sword)
            {
                return (_attackSequence % 4) switch
                {
                    0 => PlayerAction.PlayerAttackSwordRight1,
                    1 => PlayerAction.PlayerAttackSwordLeft1,
                    2 => PlayerAction.PlayerAttackSwordRight2,
                    _ => PlayerAction.PlayerAttackSwordLeft2
                };
            }

            if (primary == WeaponKind.Sword)
            {
                return (_attackSequence % 2) == 0
                    ? PlayerAction.PlayerAttackSwordRight1
                    : PlayerAction.PlayerAttackSwordRight2;
            }

            if (primary == WeaponKind.TwoHandSwordTwo)
                return PlayerAction.PlayerAttackTwoHandSwordTwo;

            if (primary == WeaponKind.TwoHandSword)
                return (PlayerAction)((int)PlayerAction.PlayerAttackTwoHandSword1 + (_attackSequence % 3));

            if (primary == WeaponKind.StaffOneHand)
            {
                return (_attackSequence % 2) == 0
                    ? PlayerAction.PlayerAttackSwordRight1
                    : PlayerAction.PlayerAttackSwordRight2;
            }

            if (primary == WeaponKind.StaffTwoHand)
            {
                return (_attackSequence % 2) == 0
                    ? PlayerAction.PlayerSkillWeapon1
                    : PlayerAction.PlayerSkillWeapon2;
            }

            if (primary == WeaponKind.Spear)
                return PlayerAction.PlayerAttackSpear1;

            if (primary == WeaponKind.Scythe)
                return (PlayerAction)((int)PlayerAction.PlayerAttackScythe1 + (_attackSequence % 3));

            if (primary == WeaponKind.Bow)
            {
                if (HasEquippedWings && !isInSafeZone)
                    return PlayerAction.PlayerAttackFlyBow;
                return PlayerAction.PlayerAttackBow;
            }

            if (primary == WeaponKind.Crossbow)
            {
                if (HasEquippedWings && !isInSafeZone)
                    return PlayerAction.PlayerAttackFlyCrossbow;
                return PlayerAction.PlayerAttackCrossbow;
            }

            return PlayerAction.PlayerAttackFist;
        }

        private MovementMode GetCurrentMovementMode(WalkableWorldControl world, TWFlags? flagsOverride = null)
        {
            var flags = flagsOverride ?? world.Terrain.RequestTerrainFlag((int)Location.X, (int)Location.Y);
            // Atlans (index 8) uses swimming by default, but winged players still use fly animations.
            if (world.WorldIndex == 8)
            {

                if (!flags.HasFlag(TWFlags.SafeZone) && HasEquippedWings)
                {
                    return MovementMode.Fly;
                }

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
        private PlayerAction GetMovementAction(MovementMode mode, WeaponContext weapons, bool isInSafeZone, bool isRunning)
        {
            return mode switch
            {
                MovementMode.Swim => isRunning ? PlayerAction.PlayerRunSwim : PlayerAction.PlayerWalkSwim,
                MovementMode.Fly => weapons.PrimaryKind == WeaponKind.Crossbow && !isInSafeZone
                    ? PlayerAction.PlayerFlyCrossbow
                    : PlayerAction.PlayerFly,
                _ => isRunning ? GetRunActionForWeapon(weapons) : GetMovementActionForWeapon(weapons)
            };
        }

        /// <summary>
        /// Gets the appropriate movement action based on equipped weapon
        /// </summary>
        private PlayerAction GetMovementActionForWeapon(WeaponContext weapons)
        {
            if (!weapons.HasAnyWeapon)
                return GetClassWalkAction(isInChaosCastle: World is WalkableWorldControl w && IsChaosCastleMap(w.WorldIndex));

            return weapons.PrimaryKind switch
            {
                WeaponKind.Sword => PlayerAction.PlayerWalkSword,
                WeaponKind.TwoHandSword => PlayerAction.PlayerWalkTwoHandSword,
                WeaponKind.TwoHandSwordTwo => PlayerAction.PlayerWalkTwoHandSwordTwo,
                WeaponKind.Spear => PlayerAction.PlayerWalkSpear,
                WeaponKind.Scythe => PlayerAction.PlayerWalkScythe,
                WeaponKind.Bow => PlayerAction.PlayerWalkBow,
                WeaponKind.Crossbow => PlayerAction.PlayerWalkCrossbow,
                WeaponKind.SummonerStick => PlayerAction.PlayerWalkWand,
                WeaponKind.StaffOneHand => PlayerAction.PlayerWalkSword,
                WeaponKind.StaffTwoHand => PlayerAction.PlayerWalkScythe,
                WeaponKind.Book => PlayerAction.PlayerWalkWand,
                _ => GetClassWalkAction(isInChaosCastle: World is WalkableWorldControl w && IsChaosCastleMap(w.WorldIndex))
            };
        }

        private PlayerAction GetRunActionForWeapon(WeaponContext weapons)
        {
            if (!weapons.HasAnyWeapon)
                return PlayerAction.PlayerRun;

            if (weapons.HasDualWeapons && weapons.RightKind == WeaponKind.Sword && weapons.LeftKind == WeaponKind.Sword)
            {
                if (IsRageFighterClass(CharacterClass))
                    return PlayerAction.PlayerRun;
                return PlayerAction.PlayerRunTwoSword;
            }

            return weapons.PrimaryKind switch
            {
                WeaponKind.Sword => PlayerAction.PlayerRunSword,
                WeaponKind.TwoHandSword => PlayerAction.PlayerRunTwoHandSword,
                WeaponKind.TwoHandSwordTwo => PlayerAction.PlayerRunTwoHandSwordTwo,
                WeaponKind.Spear => PlayerAction.PlayerRunSpear,
                WeaponKind.Scythe => PlayerAction.PlayerRunSpear,
                WeaponKind.Bow => PlayerAction.PlayerRunBow,
                WeaponKind.Crossbow => PlayerAction.PlayerRunCrossbow,
                WeaponKind.SummonerStick => PlayerAction.PlayerRunWand,
                WeaponKind.StaffOneHand => PlayerAction.PlayerRunSword,
                WeaponKind.StaffTwoHand => PlayerAction.PlayerRunSpear,
                WeaponKind.Book => PlayerAction.PlayerRunWand,
                _ => PlayerAction.PlayerRun
            };
        }

        /// <summary>Action that should play while standing (gender already cached).</summary>
        private PlayerAction GetIdleAction(MovementMode mode, WeaponContext weapons, bool isInSafeZone, bool isInChaosCastle)
        {
            if (mode == MovementMode.Fly)
            {
                return weapons.PrimaryKind == WeaponKind.Crossbow && !isInSafeZone
                    ? PlayerAction.PlayerStopFlyCrossbow
                    : PlayerAction.PlayerStopFly;
            }

            return GetIdleActionForWeapon(weapons, isInChaosCastle);
        }

        /// <summary>
        /// Gets the appropriate idle action based on equipped weapon
        /// </summary>
        private PlayerAction GetIdleActionForWeapon(WeaponContext weapons, bool isInChaosCastle)
        {
            if (!weapons.HasAnyWeapon)
                return GetClassIdleAction(isInChaosCastle);

            return weapons.PrimaryKind switch
            {
                WeaponKind.TwoHandSword => PlayerAction.PlayerStopTwoHandSword,
                WeaponKind.TwoHandSwordTwo => PlayerAction.PlayerStopTwoHandSwordTwo,
                WeaponKind.Spear => PlayerAction.PlayerStopSpear,
                WeaponKind.Scythe => PlayerAction.PlayerStopScythe,
                WeaponKind.Bow => PlayerAction.PlayerStopBow,
                WeaponKind.Crossbow => PlayerAction.PlayerStopCrossbow,
                WeaponKind.SummonerStick => PlayerAction.PlayerStopWand,
                WeaponKind.StaffOneHand => PlayerAction.PlayerStopSword,
                WeaponKind.StaffTwoHand => PlayerAction.PlayerStopScythe,
                WeaponKind.Sword => PlayerAction.PlayerStopSword,
                _ => GetClassIdleAction(isInChaosCastle)
            };
        }

        private PlayerAction GetClassIdleAction(bool isInChaosCastle)
        {
            if (_isFemale)
                return PlayerAction.PlayerStopFemale;

            if (IsSummonerClass(CharacterClass) && !isInChaosCastle)
                return PlayerAction.PlayerStopSummoner;

            if (IsRageFighterClass(CharacterClass))
                return PlayerAction.PlayerStopRagefighter;

            return PlayerAction.PlayerStopMale;
        }

        private PlayerAction GetClassWalkAction(bool isInChaosCastle)
        {
            if (!_isFemale)
                return PlayerAction.PlayerWalkMale;

            if (IsSummonerClass(CharacterClass) && isInChaosCastle)
                return PlayerAction.PlayerWalkMale;

            return PlayerAction.PlayerWalkFemale;
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
                if (Appearance.HasBlackFenrir)
                    vehicleIndex = 11; // Fenrir Black
                else if (Appearance.HasBlueFenrir)
                    vehicleIndex = 12; // Fenrir Blue
                else if (Appearance.HasGoldFenrir)
                    vehicleIndex = 13; // Fenrir Gold
                else
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
        private PlayerAction GetRidingMovementAction(WeaponContext weapons)
        {
            // Fenrir variants have special running animation
            if (IsFenrirVehicle(_currentVehicleIndex)) // All Fenrir variants
            {
                if (_runFrames < FenrirRunDelayFrames)
                    return GetFenrirWalkAction(weapons);
                return GetFenrirRunAction(weapons);
            }

            // Dark Horse has a specific animation
            if (_currentVehicleIndex == 0) // Dark Horse
                return PlayerAction.PlayerRunRideHorse;

            if (_currentVehicleIndex == 7 || _currentVehicleIndex == 8) // Uniria/Dinorant
            {
                if (IsRageFighterClass(CharacterClass))
                {
                    return weapons.HasAnyWeapon ? PlayerAction.PlayerRageUniRunOneRight : PlayerAction.PlayerRageUniRun;
                }

                return weapons.HasAnyWeapon ? PlayerAction.PlayerRunRideWeapon : PlayerAction.PlayerRunRide;
            }

            // Default riding animation for Dinorant, Uniria, etc.
            return PlayerAction.PlayerRunRide;
        }

        private PlayerAction GetFenrirWalkAction(WeaponContext weapons)
        {
            if (IsRageFighterClass(CharacterClass))
            {
                if (weapons.HasDualWeapons)
                    return PlayerAction.PlayerRageFenrirWalkTwoSword;
                if (weapons.HasRightWeapon)
                    return PlayerAction.PlayerRageFenrirWalkOneRight;
                if (weapons.HasLeftWeapon)
                    return PlayerAction.PlayerRageFenrirWalkOneLeft;
                return PlayerAction.PlayerRageFenrirWalk;
            }

            if (weapons.HasDualWeapons)
                return PlayerAction.PlayerFenrirWalkTwoSword;
            if (weapons.HasRightWeapon)
                return PlayerAction.PlayerFenrirWalkOneRight;
            if (weapons.HasLeftWeapon)
                return PlayerAction.PlayerFenrirWalkOneLeft;
            return PlayerAction.PlayerFenrirWalk;
        }

        /// <summary>
        /// Gets the appropriate idle action when riding a mount.
        /// </summary>
        private PlayerAction GetRidingIdleAction(WeaponContext weapons)
        {
            // Fenrir variants have special standing animation
            if (_currentVehicleIndex >= 11 && _currentVehicleIndex <= 18) // All Fenrir variants
                return GetFenrirStandAction(weapons);

            if (_currentVehicleIndex == 0) // Dark Horse
                return PlayerAction.PlayerStopRideHorse;

            if (_currentVehicleIndex == 7 || _currentVehicleIndex == 8) // Uniria/Dinorant
            {
                if (!weapons.HasAnyWeapon)
                    return PlayerAction.PlayerStopRide;

                return IsRageFighterClass(CharacterClass)
                    ? PlayerAction.PlayerRageUniStopOneRight
                    : PlayerAction.PlayerStopRideWeapon;
            }

            return PlayerAction.PlayerStopRide;
        }

        private PlayerAction GetFenrirStandAction(WeaponContext weapons)
        {
            if (IsRageFighterClass(CharacterClass))
            {
                if (weapons.HasDualWeapons)
                    return PlayerAction.PlayerRageFenrirStandTwoSword;
                if (weapons.HasRightWeapon)
                    return PlayerAction.PlayerRageFenrirStandOneRight;
                if (weapons.HasLeftWeapon)
                    return PlayerAction.PlayerRageFenrirStandOneLeft;
                return PlayerAction.PlayerRageFenrirStand;
            }

            if (weapons.HasDualWeapons)
                return PlayerAction.PlayerFenrirStandTwoSword;
            if (weapons.HasRightWeapon)
                return PlayerAction.PlayerFenrirStandOneRight;
            if (weapons.HasLeftWeapon)
                return PlayerAction.PlayerFenrirStandOneLeft;
            return PlayerAction.PlayerFenrirStand;
        }

        private PlayerAction GetFenrirRunAction(WeaponContext weapons)
        {
            bool isElf = IsElfClass(CharacterClass);
            bool isDarkWizard = IsDarkWizardClass(CharacterClass);
            bool isRageFighter = IsRageFighterClass(CharacterClass);

            if (weapons.HasDualWeapons)
            {
                if (isElf)
                    return PlayerAction.PlayerFenrirRunTwoSwordElf;
                if (isDarkWizard)
                    return PlayerAction.PlayerFenrirRunTwoSwordMagom;
                if (isRageFighter)
                    return PlayerAction.PlayerRageFenrirRunTwoSword;
                return PlayerAction.PlayerFenrirRunTwoSword;
            }

            if (weapons.HasRightWeapon)
            {
                if (isElf)
                    return PlayerAction.PlayerFenrirRunOneRightElf;
                if (isDarkWizard)
                    return PlayerAction.PlayerFenrirRunOneRightMagom;
                if (isRageFighter)
                    return PlayerAction.PlayerRageFenrirRunOneRight;
                return PlayerAction.PlayerFenrirRunOneRight;
            }

            if (weapons.HasLeftWeapon)
            {
                if (isElf)
                    return PlayerAction.PlayerFenrirRunOneLeftElf;
                if (isDarkWizard)
                    return PlayerAction.PlayerFenrirRunOneLeftMagom;
                if (isRageFighter)
                    return PlayerAction.PlayerRageFenrirRunOneLeft;
                return PlayerAction.PlayerFenrirRunOneLeft;
            }

            if (isElf)
                return PlayerAction.PlayerFenrirRunElf;
            if (isDarkWizard)
                return PlayerAction.PlayerFenrirRunMagom;
            if (isRageFighter)
                return PlayerAction.PlayerRageFenrirRun;

            return PlayerAction.PlayerFenrirRun;
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

                // Apply specific offsets for shields, bows, and crossbows when holstered
                if (weapon.ItemGroup == 6) // Shields
                {
                    weapon.Position = new Vector3(weapon.Position.X - 15f, weapon.Position.Y - 5f, weapon.Position.Z - 80f);
                    weapon.Angle = new Vector3(weapon.Angle.X + MathHelper.ToRadians(100f), weapon.Angle.Y + MathHelper.ToRadians(180f), weapon.Angle.Z); // Rotate 90 degrees around Y
                }
                else if (weapon.ItemGroup == 4) // Bows and Crossbows
                {
                    weapon.Position = new Vector3(weapon.Position.X, weapon.Position.Y, weapon.Position.Z - 30f); // Slightly lower
                }
            }
            else
            {
                weapon.ParentBoneLink = isLeftHand ? LeftHandBoneIndex : RightHandBoneIndex;
                weapon.Position = Vector3.Zero;
                weapon.Angle = Vector3.Zero;
            }
        }

        public bool TryGetBoneWorldMatrix(int boneIndex, out Matrix worldMatrix)
        {
            var bones = GetBoneTransforms();
            if (bones != null && boneIndex >= 0 && boneIndex < bones.Length)
            {
                worldMatrix = bones[boneIndex] * WorldPosition;
                return true;
            }

            worldMatrix = Matrix.Identity;
            return false;
        }

        public bool TryGetHandWorldMatrix(bool isLeftHand, out Matrix worldMatrix) =>
            TryGetBoneWorldMatrix(isLeftHand ? LeftHandBoneIndex : RightHandBoneIndex, out worldMatrix);

        private MovementMode GetModeFromCurrentAction() =>
            CurrentAction switch
            {
                PlayerAction.PlayerFly or PlayerAction.PlayerFlyCrossbow or
                PlayerAction.PlayerStopFly or PlayerAction.PlayerStopFlyCrossbow or
                PlayerAction.PlayerPoseMale1
                    => MovementMode.Fly,
                PlayerAction.PlayerRunSwim or PlayerAction.PlayerWalkSwim
                    => MovementMode.Swim,
                _ => MovementMode.Walk
            };

        private bool IsRelaxedSafeZone(WalkableWorldControl world, TWFlags flags) =>
            flags.HasFlag(TWFlags.SafeZone) && !IsBloodCastleMap(world.WorldIndex);

        private PlayerAction GetIdleAction(WalkableWorldControl world)
        {
            var flags = world.Terrain.RequestTerrainFlag((int)Location.X, (int)Location.Y);
            return GetIdleAction(world, flags);
        }

        private PlayerAction GetIdleAction(WalkableWorldControl world, TWFlags flags)
        {
            bool relaxedSafeZone = IsRelaxedSafeZone(world, flags);
            if (relaxedSafeZone)
                return GetRelaxedIdleAction();

            if (world.WorldIndex == 8 && !flags.HasFlag(TWFlags.SafeZone))
            {
                var weaponContext = GetWeaponContext();
                return weaponContext.PrimaryKind == WeaponKind.Crossbow
                    ? PlayerAction.PlayerStopFlyCrossbow
                    : PlayerAction.PlayerStopFly;
            }

            var weapons = GetWeaponContext();
            bool isInChaosCastle = IsChaosCastleMap(world.WorldIndex);
            return GetIdleAction(GetCurrentMovementMode(world, flags), weapons, flags.HasFlag(TWFlags.SafeZone), isInChaosCastle);
        }

        // --------------- LOCAL PLAYER (the one we control) ----------------
        private void UpdateLocalPlayer(WalkableWorldControl world, GameTime gameTime)
        {
            // Rest / sit handling first
            if (HandleRestTarget(world) || HandleSitTarget())
                return;

            UpdateAttackAnimationSpeeds();

            var flags = world.Terrain.RequestTerrainFlag((int)Location.X, (int)Location.Y);
            bool isInSafeZone = flags.HasFlag(TWFlags.SafeZone);
            bool relaxedSafeZone = IsRelaxedSafeZone(world, flags);
            var weapons = GetWeaponContext();
            bool isInChaosCastle = IsChaosCastleMap(world.WorldIndex);
            UpdateWeaponHolsterState(isInSafeZone);
            UpdateVehicleState(isInSafeZone);

            bool pathQueued = _currentPath?.Count > 0;
            bool isAboutToMove = IsMoving || pathQueued || MovementIntent;

            var mode = (!IsMoving && (pathQueued || MovementIntent))
                ? GetModeFromCurrentAction()
                : GetCurrentMovementMode(world, flags);

            if (world.WorldIndex == 8 && !isInSafeZone && !HasEquippedWings)
            {
                mode = MovementMode.Swim;
            }

            bool isRunning = UpdateMovementSpeedAndRunState(world, flags, isAboutToMove);

            if (isAboutToMove)
            {
                ResetRestSitStates();

                PlayerAction desired;
                if (relaxedSafeZone)
                {
                    desired = GetRelaxedWalkAction();
                }
                else if (_isRiding)
                {
                    // Use riding animation when mounted
                    desired = GetRidingMovementAction(weapons);
                    // Sync vehicle animation
                    Vehicle?.SetRiderAnimation(isMoving: true);
                }
                else
                {
                    desired = GetMovementAction(mode, weapons, isInSafeZone, isRunning);
                }

                if (!IsOneShotPlaying && CurrentAction != desired)
                    PlayAction((ushort)desired);
                PlayFootstepSound(world, gameTime);
            }
            else if (!IsOneShotPlaying)
            {
                PlayerAction idleAction;
                if (relaxedSafeZone)
                {
                    idleAction = GetRelaxedIdleAction();
                }
                else if (_isRiding)
                {
                    // Use riding idle animation when mounted
                    idleAction = GetRidingIdleAction(weapons);
                    // Sync vehicle animation
                    Vehicle?.SetRiderAnimation(isMoving: false);
                }
                else if (world.WorldIndex == 8 && !isInSafeZone)
                {
                    idleAction = weapons.PrimaryKind == WeaponKind.Crossbow
                        ? PlayerAction.PlayerStopFlyCrossbow
                        : PlayerAction.PlayerStopFly;
                }
                else
                {
                    idleAction = GetIdleAction(mode, weapons, isInSafeZone, isInChaosCastle);
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
            bool relaxedSafeZone = IsRelaxedSafeZone(world, flags);
            var weapons = GetWeaponContext();
            bool isInChaosCastle = IsChaosCastleMap(world.WorldIndex);
            UpdateWeaponHolsterState(isInSafeZone);
            UpdateVehicleStateFromAppearance(isInSafeZone);

            var mode = (!IsMoving && (pathQueued || MovementIntent))
                ? GetModeFromCurrentAction()
                : GetCurrentMovementMode(world, flags);

            if (world.WorldIndex == 8 && !isInSafeZone && !HasEquippedWings)
            {
                mode = MovementMode.Swim;
            }

            bool isRunning = UpdateMovementSpeedAndRunState(world, flags, isAboutToMove);

            if (isAboutToMove)
            {
                ResetRestSitStates();
                PlayerAction desired;
                if (relaxedSafeZone)
                {
                    desired = GetRelaxedWalkAction();
                }
                else if (_isRiding)
                {
                    // Use riding animation when mounted
                    desired = GetRidingMovementAction(weapons);
                    // Sync vehicle animation
                    Vehicle?.SetRiderAnimation(isMoving: true);
                }
                else
                {
                    desired = GetMovementAction(mode, weapons, isInSafeZone, isRunning);
                }

                if (!IsOneShotPlaying && CurrentAction != desired)
                    PlayAction((ushort)desired);
                PlayFootstepSound(world, gameTime);
            }
            else if (!IsOneShotPlaying)
            {
                PlayerAction idleAction;
                if (relaxedSafeZone)
                {
                    idleAction = GetRelaxedIdleAction();
                }
                else if (_isRiding)
                {
                    // Use riding idle animation when mounted
                    idleAction = GetRidingIdleAction(weapons);
                    // Sync vehicle animation
                    Vehicle?.SetRiderAnimation(isMoving: false);
                }
                else if (world.WorldIndex == 8 && !isInSafeZone)
                {
                    idleAction = weapons.PrimaryKind == WeaponKind.Crossbow
                        ? PlayerAction.PlayerStopFlyCrossbow
                        : PlayerAction.PlayerStopFly;
                }
                else
                {
                    idleAction = GetIdleAction(mode, weapons, isInSafeZone, isInChaosCastle);
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

        // ────────────────────────────── ATTACKS (unchanged) ──────────────────────────────
        public PlayerAction GetAttackAnimation()
        {
            return GetAttackAnimation(true);
        }

        private PlayerAction GetAttackAnimation(bool advanceSequence)
        {
            var weapons = GetWeaponContext();
            bool isInSafeZone = false;

            if (World is WalkableWorldControl world)
            {
                var flags = world.Terrain.RequestTerrainFlag((int)Location.X, (int)Location.Y);
                isInSafeZone = flags.HasFlag(TWFlags.SafeZone);
            }

            PlayerAction action = GetAttackAction(weapons, isInSafeZone);
            if (advanceSequence)
                _attackSequence++;
            return action;
        }

        public void Attack(MonsterObject target)
        {
            if (target == null || World == null) return;

            // Don't attack if player is dead
            if (IsDead) return;

            // Don't attack dead monsters
            if (target.IsDead) return;

            float rangeTiles = GetAttackRangeTiles();
            if (Vector2.Distance(Location, target.Location) > rangeTiles)
            {
                MoveTo(target.Location);
                return;
            }

            if (IsAttackOrSkillAnimationPlaying())
                return;

            _currentPath?.Clear();

            // Rotate to face the target
            int dx = (int)(target.Location.X - Location.X);
            int dy = (int)(target.Location.Y - Location.Y);
            if (dx != 0 || dy != 0)
                Direction = DirectionExtensions.GetDirectionFromMovementDelta(dx, dy);

            var attackAction = GetAttackAnimation();
            PlayAction((ushort)attackAction);

            // Play weapon swing sound based on equipped weapon type
            PlayWeaponSwingSound();

            // Map client dir → server dir
            byte clientDir = (byte)Direction;
            byte serverDir = _networkManager?.GetDirectionMap()?.GetValueOrDefault(clientDir, clientDir) ?? clientDir;

            _characterService?.SendHitRequestAsync(
                target.NetworkId,
                (byte)attackAction,
                serverDir);
        }

        public void Attack(PlayerObject target)
        {
            if (target == null || World == null) return;

            if (IsDead) return;
            if (target.IsDead) return;

            float rangeTiles = GetAttackRangeTiles();
            if (Vector2.Distance(Location, target.Location) > rangeTiles)
            {
                MoveTo(target.Location);
                return;
            }

            if (IsAttackOrSkillAnimationPlaying())
                return;

            _currentPath?.Clear();

            int dx = (int)(target.Location.X - Location.X);
            int dy = (int)(target.Location.Y - Location.Y);
            if (dx != 0 || dy != 0)
                Direction = DirectionExtensions.GetDirectionFromMovementDelta(dx, dy);

            var attackAction = GetAttackAnimation();
            PlayAction((ushort)attackAction);
            PlayWeaponSwingSound();

            byte clientDir = (byte)Direction;
            byte serverDir = _networkManager?.GetDirectionMap()?.GetValueOrDefault(clientDir, clientDir) ?? clientDir;

            _characterService?.SendHitRequestAsync(
                target.NetworkId,
                (byte)attackAction,
                serverDir);
        }

        public float GetAttackRangeTiles() => GetAttackRangeForAction(GetAttackAnimation(false));

        /// <summary>
        /// Gets the currently equipped weapon type based on actual equipment
        /// </summary>
        private WeaponType GetEquippedWeaponType()
        {
            var weapons = GetWeaponContext();
            var kind = weapons.RightKind != WeaponKind.None ? weapons.RightKind : weapons.LeftKind;

            return kind switch
            {
                WeaponKind.Sword => WeaponType.Sword,
                WeaponKind.TwoHandSword or WeaponKind.TwoHandSwordTwo => WeaponType.TwoHandSword,
                WeaponKind.Spear => WeaponType.Spear,
                WeaponKind.Scythe => WeaponType.Scythe,
                WeaponKind.Bow => WeaponType.Bow,
                WeaponKind.Crossbow => WeaponType.Crossbow,
                WeaponKind.SummonerStick => WeaponType.Staff,
                WeaponKind.StaffOneHand or WeaponKind.StaffTwoHand => WeaponType.Staff,
                WeaponKind.Book => WeaponType.Book,
                _ => Equipment.GetDefaultWeaponTypeForClass(CharacterClass)
            };
        }

        /// <summary>
        /// Plays weapon swing sound based on equipped weapon type.
        /// Follows original client logic from ZzzCharacter.cpp SetPlayerAttack.
        /// </summary>
        private void PlayWeaponSwingSound()
        {
            var weapons = GetWeaponContext();
            var kind = weapons.RightKind != WeaponKind.None ? weapons.RightKind : weapons.LeftKind;
            string soundPath = kind switch
            {
                WeaponKind.Bow => "Sound/eBow.wav",
                WeaponKind.Crossbow => GetCrossbowSound(),
                WeaponKind.Spear => "Sound/eSwingLightSword.wav",
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
            PlayerAction.PlayerAttackRideBow => 8f,
            PlayerAction.PlayerAttackRideCrossbow => 8f,
            PlayerAction.PlayerFenrirAttackBow => 8f,
            PlayerAction.PlayerFenrirAttackCrossbow => 8f,
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
            HideHelmMask();
            _helmItemEquipped = false;

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
            HideHelmMask();
            _helmItemEquipped = false;

            await SetBodyPartsAsync("Player/",
                "HelmClass", "ArmorClass", "PantClass", "GloveClass", "BootClass",
                (int)playerClass);
        }

        private async Task ResetBodyPartToClassDefaultAsync(ModelObject bodyPart, string partPrefix)
        {
            // Clear item shader properties first
            ClearItemProperties(bodyPart);

            PlayerClass mapped = MapNetworkClassToModelClass(_characterClass);
            string fileSuffix = ((int)mapped).ToString("D2");
            string modelPath = $"Player/{partPrefix}{fileSuffix}.bmd";

            _logger?.LogDebug("Resetting body part to class default: CharClass={CharClass}, MappedClass={Mapped}, Path={Path}",
                _characterClass, mapped, modelPath);

            await LoadPartAsync(bodyPart, modelPath);
        }

        private async Task EnsureHelmHeadVisibleAsync()
        {
            // Some helm models don't include the face mesh; keep the class head visible underneath.
            if (!_helmItemEquipped)
            {
                HideHelmMask();
                return;
            }

            bool helmNeedsBaseHead = HelmModelRules.RequiresBaseHead(_helmModelPath, Helm?.Model) || !HelmetModelHasFace(Helm);

            if (helmNeedsBaseHead)
            {
                await ResetBodyPartToClassDefaultAsync(HelmMask, "HelmClass");
                HelmMask.Hidden = false;
            }
            else
            {
                HideHelmMask();
            }
        }

        private static readonly HashSet<string> HelmModelsRequiringBaseHead = new(StringComparer.OrdinalIgnoreCase)
        {
            "HelmMale01",
            "HelmMale03",
            "HelmElf01",
            "HelmElf02",
            "HelmElf03",
            "HelmElf04"
        };

        private static bool HelmetModelHasFace(ModelObject helmObject)
        {
            var model = helmObject?.Model;
            if (model?.Meshes == null)
                return false;

            if (model.Meshes.Length < 2)
                return false;

            for (int i = 1; i < model.Meshes.Length; i++)
            {
                var mesh = model.Meshes[i];
                if ((mesh?.Vertices?.Length ?? 0) > 0 && (mesh?.Triangles?.Length ?? 0) > 0)
                    return true;
            }

            return false;
        }

        private void HideHelmMask()
        {
            if (HelmMask == null)
                return;

            HelmMask.Hidden = true;
            HelmMask.Model = null;
            ClearItemProperties(HelmMask);
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
        private void SendActionToServer(ServerPlayerActionType serverAction, GameDirection? directionOverride = null)
        {
            if (_characterService == null || !_networkManager.IsConnected) return;

            byte clientDirEnum = (byte)(directionOverride ?? Direction);
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

            const float baseWidth = 50f;
            const float baseHeight = 5f;
            const float baseOffsetY = 2f;
            const float basePadding = 1f;

            // Keep the on-screen size stable when render scale changes (supersampling/undersampling)
            float pixelScale = MathF.Max(Constants.RENDER_SCALE, 0.1f);
            int width = Math.Max(1, (int)MathF.Round(baseWidth * pixelScale));
            int height = Math.Max(1, (int)MathF.Round(baseHeight * pixelScale));
            int verticalOffset = Math.Max(1, (int)MathF.Round(baseOffsetY * pixelScale));
            int padding = Math.Max(1, (int)MathF.Round(basePadding * pixelScale));
            int outline = padding;

            Rectangle bgRect = new(
                (int)MathF.Round(screen.X) - width / 2,
                (int)MathF.Round(screen.Y) - height - verticalOffset,
                width,
                height);

            int innerWidth = Math.Max(0, width - padding * 2);
            int innerHeight = Math.Max(1, height - padding * 2);
            float hpFill = Math.Clamp(hpPercent, 0f, 1f);

            Rectangle fillRect = new(
                bgRect.X + padding,
                bgRect.Y + padding,
                Math.Max(0, (int)MathF.Round(innerWidth * hpFill)),
                innerHeight);

            float segmentWidth = innerWidth / 8f;

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
                    int x = bgRect.X + padding + (int)MathF.Round(segmentWidth * i);
                    sb.Draw(pixel,
                            new Rectangle(x, bgRect.Y + padding, outline, innerHeight),
                            Color.Black * 0.4f);
                }
                sb.Draw(pixel,
                        new Rectangle(
                            bgRect.X - outline,
                            bgRect.Y - outline,
                            bgRect.Width + outline * 2,
                            bgRect.Height + outline * 2),
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
                if (itemIndex is 0xFF or 0x1FF || itemIndex < 0)
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
                var wingTexturePath = TryGetWingTexturePath(Appearance.WingInfo, CharacterClass);
                if (!string.IsNullOrWhiteSpace(wingTexturePath))
                {
                    yield return wingTexturePath;
                }
                else
                {
                    int wingModelIndex = Appearance.WingInfo.Type + Appearance.WingInfo.Level + 1;
                    yield return $"Item/Wing{wingModelIndex:D2}.bmd";
                }
            }
        }

        private const short CapeOfLordItemIndex = 30;

        private static short? TryMapWingAppearanceToItemIndex(WingAppearance wingAppearance, CharacterClassNumber characterClass)
        {
            if (!wingAppearance.HasWings)
            {
                return null;
            }

            // Dark Lord / Lord Emperor use cape models; Cape of Lord is encoded specially in S6 appearance.
            if (IsDarkLordClass(characterClass))
            {
                // Servers often encode Cape of Lord as Level=2, Type=5.
                if (wingAppearance.Level == 2 && wingAppearance.Type == 5)
                {
                    return CapeOfLordItemIndex;
                }

                // Other capes (e.g., Emperor) follow the generic tier/type mapping.
            }

            // Season 6 appearance bits encode wing tier (Level) and type within tier.
            // We map this to actual item ids (group 12) for common wings/capes.
            // Tier 1: 0-6 (Elf..Darkness)
            // Tier 2: Spirits, Soul, Dragon, Darkness, Despair
            // Tier 3: Storm, Eternal, Illusion, Ruin, Emperor, Curse, Dimension
            short[] tierIds = wingAppearance.Level switch
            {
                1 => new short[] { 0, 1, 2, 3, 4, 5, 6 },
                2 => new short[] { 3, 4, 5, 6, 42 },
                3 => new short[] { 36, 37, 38, 39, 40, 41, 43 },
                _ => Array.Empty<short>()
            };

            int index = wingAppearance.Type - 1;
            if (index < 0 || index >= tierIds.Length)
            {
                return null;
            }

            return tierIds[index];
        }

        private static string TryGetWingTexturePath(WingAppearance wingAppearance, CharacterClassNumber characterClass)
        {
            if (wingAppearance.ItemIndex >= 0)
            {
                return ItemDatabase.GetItemDefinition(12, wingAppearance.ItemIndex)?.TexturePath;
            }

            var mappedIndex = TryMapWingAppearanceToItemIndex(wingAppearance, characterClass);
            if (mappedIndex.HasValue)
            {
                return ItemDatabase.GetItemDefinition(12, mappedIndex.Value)?.TexturePath;
            }

            return null;
        }

        private static bool IsDarkLordClass(CharacterClassNumber characterClass) =>
            characterClass == CharacterClassNumber.DarkLord
            || characterClass == CharacterClassNumber.LordEmperor;

        private async Task LoadPartAsync(ModelObject part, string modelPath)
        {
            if (part != null && !string.IsNullOrEmpty(modelPath))
            {
                if (part == Helm)
                {
                    _helmModelPath = modelPath;
                }
                part.Model = await BMDLoader.Instance.Prepare(modelPath);
                if (part.Model == null)
                {
                    _logger?.LogWarning("[PlayerObject] Failed to load model {Path} for {Part}", modelPath, part.GetType().Name);
                }
            }
        }

        private void SetItemProperties(ModelObject part, byte[] itemData)
        {
            if (part == null || itemData == null) return;

            var itemDetails = ItemDatabase.ParseItemDetails(itemData);
            part.ItemLevel = itemDetails.Level;
            part.IsExcellentItem = itemDetails.IsExcellent;
            part.IsAncientItem = itemDetails.IsAncient;
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
            if (equipmentData == null)
            {
                // Item is being unequipped
                await UnequipSlotAsync(itemSlot);
                return;
            }

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
                        _helmItemEquipped = true;
                        await UpdateArmorSlotAsync(Helm, equipmentData, equipmentData.ItemGroup, equipmentData.ItemNumber);
                        await EnsureHelmHeadVisibleAsync();
                        break;

                    case InventoryConstants.ArmorSlot: // 3 - Armor
                        await UpdateArmorSlotAsync(Armor, equipmentData, equipmentData.ItemGroup, equipmentData.ItemNumber);
                        break;

                    case InventoryConstants.PantsSlot: // 4 - Pants
                        await UpdateArmorSlotAsync(Pants, equipmentData, equipmentData.ItemGroup, equipmentData.ItemNumber);
                        break;

                    case InventoryConstants.GlovesSlot: // 5 - Gloves
                        _logger?.LogDebug($"[PlayerObject] UpdateEquipmentSlotAsync: Processing gloves slot, calling UpdateArmorSlotAsync");
                        await UpdateArmorSlotAsync(Gloves, equipmentData, equipmentData.ItemGroup, equipmentData.ItemNumber);
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
            _logger?.LogDebug("UnequipSlotAsync called for slot {Slot}", itemSlot);

            switch (itemSlot)
            {
                case InventoryConstants.LeftHandSlot:
                    Weapon1.Model = null;
                    Weapon1.TexturePath = null;
                    ClearItemProperties(Weapon1);
                    break;

                case InventoryConstants.RightHandSlot:
                    Weapon2.Model = null;
                    Weapon2.TexturePath = null;
                    ClearItemProperties(Weapon2);
                    break;

                case InventoryConstants.HelmSlot:
                    await ResetBodyPartToClassDefaultAsync(Helm, "HelmClass");
                    ClearItemProperties(Helm);
                    HideHelmMask();
                    _helmItemEquipped = false;
                    break;

                case InventoryConstants.ArmorSlot:
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
                    EquippedWings.ItemIndex = -1;
                    break;

                default:
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
            var itemDef = ItemDatabase.GetItemDefinition(itemGroup, (short)itemNumber);
            if (itemDef != null && !string.IsNullOrEmpty(itemDef.TexturePath))
            {
                string playerTexturePath = itemDef.TexturePath.Replace("Item/", "Player/");

                // Clear old model first
                armorPart.Model = null;
                await LoadPartAsync(armorPart, playerTexturePath);
                SetItemPropertiesFromEquipmentData(armorPart, equipmentData);
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
                if (armorPart == Helm)
                {
                    HideHelmMask();
                    _helmItemEquipped = false;
                }
            }
        }

        private Task UpdateWingsSlotAsync(EquipmentSlotData equipmentData)
        {
            if (equipmentData.ItemGroup == 12)
            {
                EquippedWings.Hidden = false;
                EquippedWings.Type = 0;
                EquippedWings.ItemIndex = (short)equipmentData.ItemNumber;
                EquippedWings.LinkParentAnimation = false;
            }
            else
            {
                EquippedWings.Hidden = true;
                EquippedWings.Type = 0;
                EquippedWings.ItemIndex = -1;
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
