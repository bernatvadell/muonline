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
        public Client.Main.Objects.Wings.WingObject EquippedWings { get; private set; }

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

        public PlayerAction SelectedAttackAction { get; set; } = PlayerAction.BlowSkill;

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

            Children.Add(HelmMask);
            Children.Add(Helm);
            Children.Add(Armor);
            Children.Add(Pants);
            Children.Add(Gloves);
            Children.Add(Boots);
            Children.Add(Weapon1);
            Children.Add(Weapon2);
            Children.Add(EquippedWings);

            // Enable mouse hover interactions so the name is shown
            Interactive = true;
            BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 120));

            Scale = 0.85f;
            AnimationSpeed = 5f;
            CurrentAction = PlayerAction.StopMale;
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
            await UpdateBodyPartClassesAsync();
            await UpdateEquipmentAppearanceAsync();
            await base.Load();

            // Idle actions play at half speed so the character breathes naturally
            SetActionSpeed(PlayerAction.StopMale, 0.5f);
            SetActionSpeed(PlayerAction.StopFemale, 0.5f);
            SetActionSpeed(PlayerAction.StopFlying, 0.5f);

            UpdateWorldBoundingBox();
        }

        private async Task UpdateEquipmentAppearanceAsync()
        {
            if (Appearance.RawData.IsEmpty) return; // No appearance data to process

            // Update CharacterClass based on appearance data
            CharacterClass = Appearance.CharacterClass;

            // Helm
            if (Appearance.HelmItemIndex != 255)
            {
                var helmDef = ItemDatabase.GetItemDefinition(7, Appearance.HelmItemIndex);
                await LoadPartAsync(Helm, helmDef?.TexturePath.Replace("Item/", "Player/"));
            }
            // Armor
            if (Appearance.ArmorItemIndex != 255)
            {
                var armorDef = ItemDatabase.GetItemDefinition(8, Appearance.ArmorItemIndex);
                await LoadPartAsync(Armor, armorDef?.TexturePath.Replace("Item/", "Player/"));
            }

            // Pants
            if (Appearance.PantsItemIndex != 255)
            {
                var pantsDef = ItemDatabase.GetItemDefinition(9, Appearance.PantsItemIndex);
                await LoadPartAsync(Pants, pantsDef?.TexturePath.Replace("Item/", "Player/"));
            }

            // Gloves
            if (Appearance.GlovesItemIndex != 255)
            {
                var glovesDef = ItemDatabase.GetItemDefinition(10, Appearance.GlovesItemIndex);
                await LoadPartAsync(Gloves, glovesDef?.TexturePath.Replace("Item/", "Player/"));
            }

            // Boots
            if (Appearance.BootsItemIndex != 255)
            {
                var bootsDef = ItemDatabase.GetItemDefinition(11, Appearance.BootsItemIndex);
                await LoadPartAsync(Boots, bootsDef?.TexturePath.Replace("Item/", "Player/"));
            }

            // Wings
            // The Appearance.md specifies wing item index in byte 9, bits 0-2.
            // This needs to be mapped to actual wing models. For now, a simplified approach.
            // Assuming wing models are named like "Wing01.bmd", "Wing02.bmd" etc.
            if (Appearance.WingItemIndex != 255)
            {
                if (Appearance.WingItemIndex > 0)
                {
                    EquippedWings.Type = Appearance.WingItemIndex;
                    EquippedWings.Hidden = false;
                    await EquippedWings.Load();
                }
                else
                {
                    EquippedWings.Hidden = true;
                }
            }
            // Weapons
            // This requires more sophisticated logic to determine the exact weapon model
            // based on item group, index, and potentially other flags.
            // For now, we'll use generic models if an item is equipped.
            if (Appearance.LeftHandItemIndex != 255)
            {
                if (Appearance.LeftHandItemIndex != 0xFF)
                {
                    var leftHandDef = ItemDatabase.GetItemDefinition(Appearance.LeftHandItemGroup, Appearance.LeftHandItemIndex);
                    if (leftHandDef != null)
                    {
                        Weapon1.Model = await BMDLoader.Instance.Prepare(leftHandDef.TexturePath);
                        Weapon1.ParentBoneLink = 33;
                        Weapon1.LinkParentAnimation = false;
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
            }

            if (Appearance.RightHandItemIndex != 255)
            {
                if (Appearance.RightHandItemIndex != 0xFF)
                {
                    var rightHandDef = ItemDatabase.GetItemDefinition(Appearance.RightHandItemGroup, Appearance.RightHandItemIndex);
                    if (rightHandDef != null)
                    {
                        Weapon2.Model = await BMDLoader.Instance.Prepare(rightHandDef.TexturePath);
                        Weapon2.ParentBoneLink = 42;
                        Weapon2.LinkParentAnimation = false;
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

            if (World is not WalkableWorldControl world)
                return;

            if (IsMainWalker)
                UpdateLocalPlayer(world, gameTime);
            else
                UpdateRemotePlayer(world, gameTime);
        }

        // --------------- Helpers for correct animation selection ----------------
        private MovementMode GetCurrentMovementMode(WalkableWorldControl world)
        {
            var flags = world.Terrain.RequestTerrainFlag((int)Location.X, (int)Location.Y);
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
                MovementMode.Swim => PlayerAction.RunSwim,
                MovementMode.Fly => PlayerAction.Fly,
                _ => _isFemale ? PlayerAction.WalkFemale : PlayerAction.WalkMale
            };

        // Back-compat overload used in older call-sites
        private PlayerAction GetMovementAction(WalkableWorldControl world) =>
            GetMovementAction(GetCurrentMovementMode(world));

        /// <summary>Action that should play while standing (gender already cached).</summary>
        private PlayerAction GetIdleAction(MovementMode mode) =>
            mode switch
            {
                MovementMode.Fly or MovementMode.Swim => PlayerAction.StopFlying,
                _ => _isFemale ? PlayerAction.StopFemale : PlayerAction.StopMale
            };

        private PlayerAction GetIdleAction(WalkableWorldControl world) =>
            GetIdleAction(GetCurrentMovementMode(world));

        // --------------- LOCAL PLAYER (the one we control) ----------------
        private void UpdateLocalPlayer(WalkableWorldControl world, GameTime gameTime)
        {
            // Rest / sit handling first
            if (HandleRestTarget(world) || HandleSitTarget())
                return;

            // Movement / idle logic
            var mode = GetCurrentMovementMode(world);
            if (IsMoving)
            {
                ResetRestSitStates();
                var desired = GetMovementAction(mode);
                if (!IsOneShotPlaying && CurrentAction != desired)
                    PlayAction((ushort)desired);
                PlayFootstepSound(world, gameTime);
            }
            else if (!IsOneShotPlaying && CurrentAction != GetIdleAction(mode))
            {
                PlayAction((ushort)GetIdleAction(mode));
            }
        }

        // --------------- REMOTE PLAYERS ----------------
        private void UpdateRemotePlayer(WalkableWorldControl world, GameTime gameTime)
        {
            var mode = GetCurrentMovementMode(world);
            if (IsMoving)
            {
                ResetRestSitStates();
                var desired = GetMovementAction(mode);
                if (!IsOneShotPlaying && CurrentAction != desired)
                    PlayAction((ushort)desired);
                PlayFootstepSound(world, gameTime);
            }
            else if (!IsOneShotPlaying && CurrentAction != GetIdleAction(mode))
            {
                PlayAction((ushort)GetIdleAction(mode));
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
                    ? PlayerAction.PlayerFlyingRest
                    : PlayerAction.PlayerStandingRest;

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

        // --------------- UTILITIES ----------------
        public ushort GetCorrectIdleAction()
        {
            if (World is not WalkableWorldControl world)
                return (ushort)(_isFemale ? PlayerAction.StopFemale : PlayerAction.StopMale);

            return (ushort)GetIdleAction(world);
        }

        private bool IsMovementAnimation(ushort action)
        {
            var a = (PlayerAction)action;
            return a is PlayerAction.WalkMale or PlayerAction.WalkFemale
                       or PlayerAction.RunSwim or PlayerAction.Fly;
        }

        // ────────────────────────────── ATTACKS (unchanged) ──────────────────────────────
        public PlayerAction GetAttackAnimation()
        {
            // For now choose by class; replace with real equipment logic later
            WeaponType weapon = Equipment.GetWeaponTypeForClass(CharacterClass);
            return weapon switch
            {
                WeaponType.Sword => PlayerAction.AttackFist,
                WeaponType.TwoHandSword => PlayerAction.PlayerAttackTwoHandSword1,
                WeaponType.Spear => PlayerAction.PlayerAttackSpear1,
                WeaponType.Bow => PlayerAction.PlayerAttackBow,
                WeaponType.Crossbow => PlayerAction.PlayerAttackCrossbow,
                WeaponType.Staff => PlayerAction.PlayerSkillHand1,
                WeaponType.Scepter => PlayerAction.PlayerAttackStrike,
                WeaponType.Scythe => PlayerAction.PlayerAttackScythe1,
                WeaponType.Book => PlayerAction.PlayerSkillSummon,
                WeaponType.Fist or WeaponType.None => PlayerAction.AttackFist,
                _ => PlayerAction.AttackFist
            };
        }

        public void Attack(MonsterObject target)
        {
            if (target == null || World == null) return;

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

            // Map client dir → server dir
            byte clientDir = (byte)Direction;
            byte serverDir = _networkManager?.GetDirectionMap()?.GetValueOrDefault(clientDir, clientDir) ?? clientDir;

            _characterService?.SendHitRequestAsync(
                target.NetworkId,
                (byte)GetAttackAnimation(),
                serverDir);
        }

        public float GetAttackRangeTiles() => GetAttackRangeForAction(GetAttackAnimation());

        private static float GetAttackRangeForAction(PlayerAction a) => a switch
        {
            PlayerAction.AttackFist => 3f,
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
            PlayerClass mapped = MapNetworkClassToModelClass(_characterClass);
            await SetBodyPartsAsync("Player/",
                "HelmClass", "ArmorClass", "PantClass", "GloveClass", "BootClass",
                (int)mapped);
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

        private async Task LoadPartAsync(ModelObject part, string modelPath)
        {
            if (part != null)
            {
                part.Model = await BMDLoader.Instance.Prepare(modelPath);
                if (part.Model == null)
                {
                    _logger?.LogDebug("Model part not found (this is often normal for NPCs): {Path}", modelPath);
                }
            }
        }

        protected override void UpdateWorldBoundingBox()
        {
            base.UpdateWorldBoundingBox();

            var allCorners = new List<Vector3>(BoundingBoxWorld.GetCorners());

            foreach (var child in Children)
            {
                if (child is ModelObject modelChild && modelChild.Visible && modelChild.Model != null)
                {
                    allCorners.AddRange(modelChild.BoundingBoxWorld.GetCorners());
                }
            }

            if (allCorners.Count > 0)
            {
                BoundingBoxWorld = BoundingBox.CreateFromPoints(allCorners);
            }
        }
    }
}
