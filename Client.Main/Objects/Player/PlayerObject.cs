using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Wings;
using Client.Main.Objects.Monsters;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets; // For CharacterClassNumber
using System.Threading.Tasks;
using Client.Main.Core.Utilities;
using Client.Main.Networking.Services;
using Client.Main.Networking;
using System.Collections.Generic; // Required for IConnection if CharacterService needs it directly

namespace Client.Main.Objects.Player
{
    public class PlayerObject : HumanoidObject
    {
        private CharacterClassNumber _characterClass;

        public bool IsResting { get; set; } = false;
        public bool IsSitting { get; set; } = false;
        public Vector2? RestPlaceTarget { get; set; }
        public Vector2? SitPlaceTarget { get; set; }

        public string Name { get; set; } = "Character";
        public override string DisplayName => Name;
        private ushort _networkId; // Private backing field

        private CharacterService _characterService;
        private NetworkManager _networkManager;

        public PlayerEquipment Equipment { get; private set; } = new PlayerEquipment();

        public CharacterClassNumber CharacterClass
        {
            get => _characterClass;
            set
            {
                if (_characterClass != value)
                {
                    _logger?.LogDebug($"PlayerObject {Name}: Setting CharacterClass from {_characterClass} to {value}");
                    _characterClass = value;
                }
            }
        }

        public WingObject Wings { get; private set; }

        public new PlayerAction CurrentAction
        {
            get => (PlayerAction)base.CurrentAction;
            set => base.CurrentAction = (int)value;
        }

        public PlayerAction SelectedAttackAction { get; set; } = PlayerAction.BlowSkill;

        public PlayerObject()
        {
            _networkId = 0x0000;

            // Enable mouse hover interactions so the name is shown
            Interactive = true;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-40, -40, 0),
                new Vector3(40, 40, 120));

            Scale = 0.85f;
            AnimationSpeed = 5f;
            CurrentAction = PlayerAction.StopMale;
            _characterClass = CharacterClassNumber.DarkWizard;

            Wings = new Wing403 { LinkParentAnimation = false, Hidden = true };

            Children.Add(Wings);

            _networkManager = MuGame.Network;
            if (_networkManager != null && MuGame.AppLoggerFactory != null)
            {
                _characterService = _networkManager.GetCharacterService();
                if (_characterService == null)
                {
                    _logger?.LogError("PlayerObject: Could not obtain CharacterService from NetworkManager.");
                }
            }
            else
            {
                _logger?.LogWarning("PlayerObject: NetworkManager or AppLoggerFactory is null during construction.");
            }
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await UpdateBodyPartClassesAsync();
            await base.Load();

            SetActionSpeed(PlayerAction.StopMale, 0.5f);
            SetActionSpeed(PlayerAction.StopFemale, 0.5f);
            SetActionSpeed(PlayerAction.StopFlying, 0.5f);
        }

        private void SetActionSpeed(PlayerAction action, float speed)
        {
            var actionIndex = (int)action;
            if (Model?.Actions != null && actionIndex >= 0 && actionIndex < Model.Actions.Length)
            {
                Model.Actions[actionIndex].PlaySpeed = speed;
                _logger?.LogDebug($"Set PlaySpeed for action '{action}' to {speed}");
            }
            else
            {
                _logger?.LogWarning($"Could not set PlaySpeed for action '{action}' - model or action not loaded.");
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime); // Handles movement, camera for main walker, base object updates

            if (World is not WalkableWorldControl worldControl)
                return;

            if (!IsMainWalker)
            {
                UpdateRemotePlayer(worldControl);
                return;
            }

            UpdateLocalPlayer(worldControl);
        }

        /// <summary>
        /// Gets the appropriate attack animation based on equipped weapon
        /// </summary>
        public PlayerAction GetAttackAnimation()
        {
            // Temporary: use class-based weapon until we have real equipment system
            var weaponType = Equipment.GetWeaponTypeForClass(CharacterClass);

            return weaponType switch
            {
                WeaponType.Sword => PlayerAction.AttackFist, //PlayerAttackSwordRight1
                WeaponType.TwoHandSword => PlayerAction.PlayerAttackTwoHandSword1,
                WeaponType.Spear => PlayerAction.PlayerAttackSpear1,
                WeaponType.Bow => PlayerAction.PlayerAttackBow,
                WeaponType.Crossbow => PlayerAction.PlayerAttackCrossbow,
                WeaponType.Staff => PlayerAction.PlayerSkillHand1, // Magic attack
                WeaponType.Scepter => PlayerAction.PlayerAttackStrike, // Dark Lord
                WeaponType.Scythe => PlayerAction.PlayerAttackScythe1,
                WeaponType.Book => PlayerAction.PlayerSkillSummon, // Summoner
                WeaponType.Fist => PlayerAction.AttackFist,
                WeaponType.None => PlayerAction.AttackFist, // Default unarmed
                _ => PlayerAction.AttackFist
            };
        }
        private void UpdateRemotePlayer(WalkableWorldControl world)
        {
            // Remote players: ensure movement animations are correct
            if (IsMoving && !IsMovementAnimation((ushort)CurrentAction))
            {
                ResetRestSitStates();
                PlayMovementAnimation(world);
            }
            else if (!IsMoving && IsMovementAnimation((ushort)CurrentAction))
            {
                PlayIdleAnimation(world);
            }
        }

        private void UpdateLocalPlayer(WalkableWorldControl world)
        {
            // Handle rest/sit targets first
            if (HandleRestTarget(world) || HandleSitTarget())
                return; // If resting/sitting, don't change animation

            // Handle movement animations
            if (IsMoving)
            {
                ResetRestSitStates();
                if (!IsOneShotPlaying && !IsMovementAnimation((ushort)CurrentAction))
                {
                    PlayMovementAnimation(world);
                }
            }
            else if (!IsOneShotPlaying && IsMovementAnimation((ushort)CurrentAction))
            {
                PlayIdleAnimation(world);
            }
        }

        private bool HandleRestTarget(WalkableWorldControl world)
        {
            if (!RestPlaceTarget.HasValue) return false;

            float distance = Vector2.Distance(Location, RestPlaceTarget.Value);
            if (distance < 0.1f && !IsMoving && !IsOneShotPlaying)
            {
                var restAction = world.WorldIndex == 4
                    ? PlayerAction.PlayerFlyingRest
                    : PlayerAction.PlayerStandingRest;

                if (CurrentAction != restAction)
                {
                    PlayAction((ushort)restAction);
                    IsResting = true;
                    if (IsMainWalker)
                    {
                        SendActionToServer(PlayerActionMapper.GetServerActionType(restAction, CharacterClass));
                    }
                }
                return true;
            }
            else if (distance > 1.0f)
            {
                RestPlaceTarget = null;
                IsResting = false;
            }
            return false;
        }

        private bool HandleSitTarget()
        {
            if (!SitPlaceTarget.HasValue) return false;

            float distance = Vector2.Distance(Location, SitPlaceTarget.Value);
            if (distance < 0.1f && !IsOneShotPlaying)
            {
                var sitAction = PlayerActionMapper.IsCharacterFemale(CharacterClass)
                    ? PlayerAction.PlayerSitFemale1
                    : PlayerAction.PlayerSit1;

                if (CurrentAction != sitAction)
                {
                    PlayAction((ushort)sitAction);
                    IsSitting = true;
                    if (IsMainWalker)
                    {
                        SendActionToServer(ServerPlayerActionType.Sit);
                    }
                }
                return true;
            }
            else if (distance > 1.0f)
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

        private bool IsMovementAnimation(ushort action)
        {
            var playerAction = (PlayerAction)action;
            return playerAction is PlayerAction.WalkMale or PlayerAction.WalkFemale or
                                 PlayerAction.RunSwim or PlayerAction.Fly;
        }

        private void PlayMovementAnimation(WalkableWorldControl world)
        {
            PlayerAction moveAction = world.WorldIndex switch
            {
                8 => PlayerAction.RunSwim,
                11 => PlayerAction.Fly,
                _ => PlayerActionMapper.IsCharacterFemale(CharacterClass)
                    ? PlayerAction.WalkFemale
                    : PlayerAction.WalkMale
            };

            PlayAction((ushort)moveAction);
        }

        private void PlayIdleAnimation(WalkableWorldControl world)
        {
            bool isFlying = world.WorldIndex == 8 || world.WorldIndex == 11;

            PlayerAction idleAction = isFlying
                ? PlayerAction.StopFlying
                : PlayerActionMapper.IsCharacterFemale(CharacterClass)
                    ? PlayerAction.StopFemale
                    : PlayerAction.StopMale;

            PlayAction((ushort)idleAction);
        }

        private void SendActionToServer(ServerPlayerActionType serverAction)
        {
            if (_characterService == null)
            {
                _logger?.LogWarning("CharacterService not initialized. Cannot send action to server.");
                return;
            }
            if (!_networkManager.IsConnected)
            {
                _logger?.LogWarning("Not connected to server. Cannot send action.");
                return;
            }

            float angleDegrees = MathHelper.ToDegrees(this.Angle.Z);
            angleDegrees = (angleDegrees % 360 + 360) % 360;
            byte playerRotationForPacket = (byte)(((MathHelper.ToDegrees(this.Angle.Z) + 22.5f) / 360.0f * 8.0f + 1.0f) % 8);


            byte serverActionIdByte = (byte)serverAction;

            _logger?.LogInformation($"[PlayerObject] Sending action to server: {serverAction} (ID: {serverActionIdByte}), Direction: {playerRotationForPacket} for player {this.Name}");

            _ = _characterService.SendAnimationRequestAsync(playerRotationForPacket, serverActionIdByte);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }

        public override void OnClick()
        {
            base.OnClick();
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

            // Calculate tile-based delta for direction
            int dx = (int)(target.Location.X - this.Location.X);
            int dy = (int)(target.Location.Y - this.Location.Y);

            if (dx != 0 || dy != 0) // Only change direction if target is not on the same tile
            {
                // Use the GetDirectionFromMovementDelta which mirrors OnLocationChanged logic
                this.Direction = DirectionExtensions.GetDirectionFromMovementDelta(dx, dy);
            }
            // If on the same tile, player's current Direction is maintained.
            // OnDirectionChanged() is called implicitly by the Direction setter, updating this.Angle for visuals.

            PlayAction((ushort)GetAttackAnimation());

            // Map the client's visual direction (this.Direction) to the server's expected byte value
            byte clientDirEnumByte = (byte)this.Direction;
            byte serverLookingDirection = clientDirEnumByte; // Default if no map or NetworkManager

            if (_networkManager != null)
            {
                var directionMap = _networkManager.GetDirectionMap();
                if (directionMap != null && directionMap.TryGetValue(clientDirEnumByte, out byte mappedDir))
                {
                    serverLookingDirection = mappedDir;
                }
            }
            else
            {
                _logger?.LogWarning("NetworkManager is null in PlayerObject.Attack. Cannot map direction for server.");
            }

            _logger?.LogDebug($"Attack: PlayerLoc={this.Location}, TargetLoc={target.Location}, dx={dx}, dy={dy}, ClientDirEnum={this.Direction} ({(byte)this.Direction}), ServerDirByte={serverLookingDirection}, TargetId={target.NetworkId}");

            _characterService?.SendHitRequestAsync(
                target.NetworkId,
                (byte)GetAttackAnimation(),
                serverLookingDirection); // Send the server-mapped direction
        }

        public float GetAttackRangeTiles() => GetAttackRangeForAction(GetAttackAnimation());

        private static float GetAttackRangeForAction(PlayerAction action) => action switch
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

        private PlayerClass MapNetworkClassToModelClass(CharacterClassNumber networkClass)
        {
            return networkClass switch
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
        }

        public async Task UpdateBodyPartClassesAsync()
        {
            PlayerClass mappedClass = MapNetworkClassToModelClass(_characterClass);
            // Use the new helper from HumanoidObject with an integer index
            await SetBodyPartsAsync("Player/",
                "HelmClass", "ArmorClass", "PantClass", "GloveClass", "BootClass", (int)mappedClass);
        }
    }
}