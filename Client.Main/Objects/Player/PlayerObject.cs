using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Wings;
using Client.Main.Objects.Monsters;
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

namespace Client.Main.Objects.Player
{
    /// <summary>High-level movement states.</summary>
    public enum MovementMode
    {
        Walk,
        Fly,
        Swim
    }

    public class PlayerObject : HumanoidObject
    {
        private CharacterClassNumber _characterClass;
        // Cached gender flag – avoids evaluating gender every frame
        private bool _isFemale;

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

        public new WingObject Wings { get; }

        /// <summary>True if wings are equipped and visible.</summary>
        public bool HasEquippedWings => Wings is { Hidden: false, Type: > 0 };

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
        public PlayerObject()
        {
            // Enable mouse hover interactions so the name is shown
            Interactive = true;
            BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 120));

            Scale = 0.85f;
            AnimationSpeed = 5f;
            CurrentAction = PlayerAction.StopMale;
            _characterClass = CharacterClassNumber.DarkWizard;
            _isFemale = PlayerActionMapper.IsCharacterFemale(_characterClass);

            // Wings = new Wing403 { LinkParentAnimation = false, Hidden = false };
            // Children.Add(Wings);

            _networkManager = MuGame.Network;
            _characterService = _networkManager?.GetCharacterService();
        }

        // ───────────────────────────────── LOADING ─────────────────────────────────
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await UpdateBodyPartClassesAsync();
            await base.Load();

            // Idle actions play at half speed so the character breathes naturally
            SetActionSpeed(PlayerAction.StopMale, 0.5f);
            SetActionSpeed(PlayerAction.StopFemale, 0.5f);
            SetActionSpeed(PlayerAction.StopFlying, 0.5f);
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
                UpdateLocalPlayer(world);
            else
                UpdateRemotePlayer(world);
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
                MovementMode.Fly  => PlayerAction.Fly,
                _                 => _isFemale ? PlayerAction.WalkFemale : PlayerAction.WalkMale
            };

        // Back-compat overload used in older call-sites
        private PlayerAction GetMovementAction(WalkableWorldControl world) =>
            GetMovementAction(GetCurrentMovementMode(world));

        /// <summary>Action that should play while standing (gender already cached).</summary>
        private PlayerAction GetIdleAction(MovementMode mode) =>
            mode switch
            {
                MovementMode.Fly or MovementMode.Swim => PlayerAction.StopFlying,
                _                                     => _isFemale ? PlayerAction.StopFemale : PlayerAction.StopMale
            };

        private PlayerAction GetIdleAction(WalkableWorldControl world) =>
            GetIdleAction(GetCurrentMovementMode(world));

        // --------------- LOCAL PLAYER (the one we control) ----------------
        private void UpdateLocalPlayer(WalkableWorldControl world)
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
            }
            else if (!IsOneShotPlaying && CurrentAction != GetIdleAction(mode))
            {
                PlayAction((ushort)GetIdleAction(mode));
            }
        }

        // --------------- REMOTE PLAYERS ----------------
        private void UpdateRemotePlayer(WalkableWorldControl world)
        {
            var mode = GetCurrentMovementMode(world);
            if (IsMoving)
            {
                ResetRestSitStates();
                var desired = GetMovementAction(mode);
                if (!IsOneShotPlaying && CurrentAction != desired)
                    PlayAction((ushort)desired);
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
    }
}
