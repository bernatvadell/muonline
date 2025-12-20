using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using System;
using System.Threading.Tasks;
using Client.Main.Core.Utilities;
using Client.Main.Core.Client;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Effects;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to character stats, level, status, and skills.
    /// </summary>
    public class CharacterDataHandler : IGamePacketHandler
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly ILogger<CharacterDataHandler> _logger;
        private readonly CharacterState _characterState;
        private readonly NetworkManager _networkManager;
        private readonly TargetProtocolVersion _targetVersion;
        private readonly ElfBuffEffectManager _elfBuffEffectManager;

        // ───────────────────────── Constructors ─────────────────────────
        public CharacterDataHandler(
            ILoggerFactory loggerFactory,
            CharacterState characterState,
            NetworkManager networkManager,
            TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<CharacterDataHandler>();
            _characterState = characterState;
            _networkManager = networkManager;
            _targetVersion = targetVersion;
            _elfBuffEffectManager = new ElfBuffEffectManager();
        }

        // ───────────────────────── Packet Handlers ─────────────────────────

        [PacketHandler(0x07, PacketRouter.NoSubCode)] // MagicEffectStatus
        public Task HandleMagicEffectStatusAsync(Memory<byte> packet)
        {
            try
            {
                var effectPacket = new MagicEffectStatus(packet);
                byte effectId = effectPacket.EffectId;
                ushort playerId = effectPacket.PlayerId;
                bool isActive = effectPacket.IsActive;

                _logger.LogDebug("MagicEffectStatus received: Effect ID: {EffectId}, Player ID: {PlayerId}, Active: {IsActive}",
                    effectId, playerId, isActive);

                // Update character state
                if (isActive)
                {
                    _characterState.ActivateBuff(effectId, playerId);

                    // Special message for Elf Soldier buff (ID 3 - NPC Helper buff) - only for local player
                    if (effectId == 3 && (playerId & 0x7FFF) == (_characterState.Id & 0x7FFF))
                    {
                        MuGame.ScheduleOnMainThread(() =>
                        {
                            var gameScene = MuGame.Instance?.ActiveScene as Scenes.GameScene;
                            if (gameScene != null)
                            {
                                gameScene.ChatLog?.AddMessage("System", "You received a buff from Elf Soldier that enhances attack and defense!", Models.MessageType.Info);
                            }
                        });
                    }
                }
                else
                {
                    _characterState.DeactivateBuff(effectId, playerId);
                }

                HandleElfBuffVisual(effectId, playerId, isActive);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MagicEffectStatus packet.");
                return Task.CompletedTask;
            }
        }

        [PacketHandler(0x1B, PacketRouter.NoSubCode)] // MagicEffectCancelled
        public Task HandleMagicEffectCancelledAsync(Memory<byte> packet)
        {
            try
            {
                var effectPacket = new MagicEffectCancelled(packet);
                ushort skillId = effectPacket.SkillId;
                ushort targetId = effectPacket.TargetId;

                _logger.LogDebug("MagicEffectCancelled received: Skill ID: {SkillId}, Target ID: {TargetId}",
                    skillId, targetId);

                // Map skill ID to effect ID and deactivate
                // For now, we'll use the lower byte of skill ID as effect ID
                // This mapping may need to be adjusted based on actual game data
                byte effectId = (byte)(skillId & 0xFF);
                _characterState.DeactivateBuff(effectId, targetId);
                HandleElfBuffVisual(effectId, targetId, false);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MagicEffectCancelled packet.");
                return Task.CompletedTask;
            }
        }

        [PacketHandler(0xF3, 0x03)] // CharacterInformation
        public Task HandleCharacterInformationAsync(Memory<byte> packet)
        {
            try
            {
                ushort mapId = 0;
                byte x = 0, y = 0;
                ulong currentExp = 0, nextExp = 1;
                ushort level = 1, levelUpPoints = 0;
                uint initialHp = 0, maxHp = 1;
                uint initialSd = 0, maxSd = 0;
                uint initialMana = 0, maxMana = 1;
                uint initialAbility = 0, maxAbility = 0;
                ushort str = 0, agi = 0, vit = 0, ene = 0, cmd = 0;
                ushort attackSpeed = 0, magicSpeed = 0, maxAttackSpeed = 0;
                byte inventoryExpansion = 0;
                CharacterStatus status = CharacterStatus.Normal;
                CharacterHeroState heroState = CharacterHeroState.Normal;
                uint money = 0;
                byte direction = 0; // Default direction

                // Parse according to protocol version
                switch (_targetVersion)
                {
                    case TargetProtocolVersion.Season6:
                        if (packet.Length >= CharacterInformationExtended.Length)
                        {
                            var info = new CharacterInformationExtended(packet);
                            mapId = info.MapId; x = info.X; y = info.Y;
                            currentExp = info.CurrentExperience;
                            nextExp = info.ExperienceForNextLevel;
                            levelUpPoints = info.LevelUpPoints;
                            str = info.Strength; agi = info.Agility;
                            vit = info.Vitality; ene = info.Energy;
                            cmd = info.Leadership;
                            initialHp = info.CurrentHealth;
                            maxHp = info.MaximumHealth;
                            initialSd = info.CurrentShield;
                            maxSd = info.MaximumShield;
                            initialMana = info.CurrentMana;
                            maxMana = info.MaximumMana;
                            initialAbility = info.CurrentAbility;
                            maxAbility = info.MaximumAbility;
                            attackSpeed = info.AttackSpeed;
                            magicSpeed = info.MagicSpeed;
                            maxAttackSpeed = info.MaximumAttackSpeed;
                            money = info.Money;
                            heroState = info.HeroState;
                            status = info.Status;
                            inventoryExpansion = info.InventoryExtensions;
                            // Note: CharacterInformationExtended (S6) does not have a direction field. Client might get it from MapChange or default.
                        }
                        else if (packet.Length >= CharacterInformation.Length)
                        {
                            var info = new CharacterInformation(packet);
                            mapId = info.MapId; x = info.X; y = info.Y;
                            currentExp = info.CurrentExperience;
                            nextExp = info.ExperienceForNextLevel;
                            levelUpPoints = info.LevelUpPoints;
                            str = info.Strength; agi = info.Agility;
                            vit = info.Vitality; ene = info.Energy;
                            cmd = info.Leadership;
                            initialHp = info.CurrentHealth;
                            maxHp = info.MaximumHealth;
                            initialSd = info.CurrentShield;
                            maxSd = info.MaximumShield;
                            initialMana = info.CurrentMana;
                            maxMana = info.MaximumMana;
                            initialAbility = info.CurrentAbility;
                            maxAbility = info.MaximumAbility;
                            money = info.Money;
                            heroState = info.HeroState;
                            status = info.Status;
                            inventoryExpansion = info.InventoryExtensions;
                            // Note: CharacterInformation (S6, non-extended) does not have a direction field.
                        }
                        else goto DefaultCase;
                        break;

                    case TargetProtocolVersion.Version097:
                        if (packet.Length >= CharacterInformation097.Length)
                        {
                            var info = new CharacterInformation097(packet);
                            mapId = info.MapId; x = info.X; y = info.Y;
                            currentExp = info.CurrentExperience;
                            nextExp = info.ExperienceForNextLevel;
                            levelUpPoints = info.LevelUpPoints;
                            str = info.Strength; agi = info.Agility;
                            vit = info.Vitality; ene = info.Energy;
                            cmd = info.Leadership;
                            initialHp = info.CurrentHealth;
                            maxHp = info.MaximumHealth;
                            // Shield not present in 0.97
                            initialMana = info.CurrentMana;
                            maxMana = info.MaximumMana;
                            initialAbility = info.CurrentAbility;
                            maxAbility = info.MaximumAbility;
                            money = info.Money;
                            heroState = info.HeroState;
                            status = info.Status;
                            inventoryExpansion = 0;
                            direction = info.Direction;
                        }
                        else goto DefaultCase;
                        break;

                    case TargetProtocolVersion.Version075:
                        if (packet.Length >= CharacterInformation075.Length)
                        {
                            var info = new CharacterInformation075(packet);
                            mapId = info.MapId; x = info.X; y = info.Y;
                            currentExp = info.CurrentExperience;
                            nextExp = info.ExperienceForNextLevel;
                            levelUpPoints = info.LevelUpPoints;
                            str = info.Strength; agi = info.Agility;
                            vit = info.Vitality; ene = info.Energy;
                            cmd = 0; // Leadership not in 0.75
                            initialHp = info.CurrentHealth;
                            maxHp = info.MaximumHealth;
                            initialMana = info.CurrentMana;
                            maxMana = info.MaximumMana;
                            initialAbility = 0;
                            maxAbility = 0;
                            money = info.Money;
                            heroState = info.HeroState;
                            status = info.Status;
                            inventoryExpansion = 0;
                            // direction = info.Direction;
                        }
                        else goto DefaultCase;
                        break;

                    default:
                    DefaultCase:
                        _logger.LogWarning(
                            "⚠️ Unexpected length ({Length}) or unsupported version ({Version}) for CharacterInformation.",
                            packet.Length, _targetVersion);
                        return Task.CompletedTask;
                }

                _logger.LogInformation("✅ Entered game world (CharacterInformation).");

                // Update CharacterState
                _logger.LogInformation("Character name: {Name}", _characterState.Name ?? "NOT SET");

                _characterState.UpdatePosition(x, y);
                _characterState.UpdateMap(mapId);
                _characterState.UpdateDirection(direction); // Update direction based on packet data or default
                _characterState.UpdateLevelAndExperience(level, currentExp, nextExp, levelUpPoints);
                _characterState.UpdateStats(str, agi, vit, ene, cmd);
                _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                _characterState.UpdateMaximumManaAbility(maxMana, maxAbility);
                _characterState.UpdateCurrentHealthShield(initialHp, initialSd);
                _characterState.UpdateCurrentManaAbility(initialMana, initialAbility);
                _characterState.UpdateInventoryZen(money);
                _characterState.UpdateStatus(status, heroState);
                _characterState.InventoryExpansionState = inventoryExpansion;
                _characterState.UpdateAttackSpeeds(attackSpeed, magicSpeed, maxAttackSpeed);

                _logger.LogInformation(
                    "🗺️ Map: {MapName} ({MapId}) at ({X},{Y}).",
                    MapDatabase.GetMapName(_characterState.MapId),
                    _characterState.MapId,
                    _characterState.PositionX,
                    _characterState.PositionY);

                // Notify NetworkManager that character info is ready
                _networkManager.ProcessCharacterInformation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing CharacterInformation packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x04)] // RespawnAfterDeath
        public Task HandleRespawnAfterDeathAsync(Memory<byte> packet)
        {
            try
            {
                // Defaults
                byte x = 0, y = 0, mapNumber = 0, direction = 0;
                ushort currentHp = 0, currentMana = 0, currentAbility = 0;
                uint experience = 0, money = 0;
                ushort mapId = 0;

                // Parse based on version
                if ((_targetVersion == TargetProtocolVersion.Season6 || _targetVersion == TargetProtocolVersion.Version097)
                    && packet.Length >= RespawnAfterDeath095.Length)
                {
                    var respawn = new RespawnAfterDeath095(packet);
                    x = respawn.PositionX;
                    y = respawn.PositionY;
                    mapNumber = respawn.MapNumber;
                    direction = respawn.Direction;
                    currentHp = respawn.CurrentHealth;
                    currentMana = respawn.CurrentMana;
                    currentAbility = respawn.CurrentAbility;
                    experience = respawn.Experience;
                    money = respawn.Money;
                    _logger.LogInformation(
                        "🔄 Respawn (0.95+): Pos=({X},{Y}), Map={Map}, HP={HP}, MP={MP}, AG={AG}",
                        x, y, mapNumber, currentHp, currentMana, currentAbility);
                }
                else if (_targetVersion == TargetProtocolVersion.Version075
                         && packet.Length >= RespawnAfterDeath075.Length)
                {
                    var respawn = new RespawnAfterDeath075(packet);
                    x = respawn.PositionX;
                    y = respawn.PositionY;
                    mapNumber = respawn.MapNumber;
                    direction = respawn.Direction;
                    currentHp = respawn.CurrentHealth;
                    currentMana = respawn.CurrentMana;
                    experience = respawn.Experience;
                    money = respawn.Money;
                    _logger.LogInformation(
                        "🔄 Respawn (0.75): Pos=({X},{Y}), Map={Map}, HP={HP}, MP={MP}",
                        x, y, mapNumber, currentHp, currentMana);
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) or unsupported version ({Version}) for RespawnAfterDeath.",
                        packet.Length, _targetVersion);
                    return Task.CompletedTask;
                }

                // Adjust map ID (server uses zero-based)
                mapId = (ushort)(mapNumber + 1); // Server map IDs are 0-based, client/world uses 1-based.

                // Update state
                _characterState.UpdatePosition(x, y);
                _characterState.UpdateMap(mapId);
                _characterState.UpdateDirection(direction);
                _characterState.UpdateCurrentHealthShield(currentHp, _characterState.MaximumShield);
                _characterState.UpdateCurrentManaAbility(currentMana, currentAbility);
                _characterState.Experience = experience;
                _characterState.UpdateInventoryZen(money);

                // Trigger UI update
                _networkManager.ProcessCharacterRespawn(mapId, x, y, direction);

                // Heuristic: Some servers don't send CharacterInformation after selection
                if (_networkManager.CurrentState == ClientConnectionState.SelectingCharacter)
                {
                    _logger.LogInformation("Heuristic EnteredGame: SelectingCharacter + RespawnAfterDeath received. Marking InGame.");
                    _networkManager.ProcessCharacterInformation();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing RespawnAfterDeath packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x1C, 0x0F)] // MapChanged (teleport/respawn)
        public Task HandleMapChangedAsync(Memory<byte> packet)
        {
            byte x = 0, y = 0, direction = 0;
            ushort mapNumber = 0;

            // Parse packet versions
            if (_targetVersion == TargetProtocolVersion.Version075)
            {
                var m75 = new MapChanged075(packet);
                x = m75.PositionX;
                y = m75.PositionY;
                mapNumber = m75.MapNumber;
                direction = m75.Rotation;
            }
            else // Season 6 and 0.97 use the same structure for MapChanged
            {
                var m = new MapChanged(packet);
                x = m.PositionX;
                y = m.PositionY;
                mapNumber = (ushort)m.MapNumber; // Ensure it's treated as ushort for mapId logic
                direction = m.Rotation;
            }

            // Adjust and log
            ushort mapId = (ushort)(mapNumber); // Server's MapNumber is already the correct WorldIndex for client worlds.
            _logger.LogInformation("🔄 MapChanged: Map={Map}, Pos=({X},{Y}), Dir={Dir}", mapId, x, y, direction);

            // Update state & notify
            _characterState.UpdatePosition(x, y);
            _characterState.UpdateMap(mapId);
            _characterState.UpdateDirection(direction);
            _networkManager.ProcessCharacterRespawn(mapId, x, y, direction);

            // Heuristic: Some servers go directly to MapChanged without sending CharacterInformation
            if (_networkManager.CurrentState == ClientConnectionState.SelectingCharacter)
            {
                _logger.LogInformation("Heuristic EnteredGame: SelectingCharacter + MapChanged received. Marking InGame.");
                _networkManager.ProcessCharacterInformation();
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x05)] // CharacterLevelUpdate
        public Task HandleCharacterLevelUpdateAsync(Memory<byte> packet)
        {
            ushort oldLevel = _characterState.Level;
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAbility = 0;
                ushort newLevel = _characterState.Level, newPoints = _characterState.LevelUpPoints;

                if (packet.Length >= CharacterLevelUpdateExtended.Length
                    && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var upd = new CharacterLevelUpdateExtended(packet);
                    newLevel = upd.Level;
                    newPoints = upd.LevelUpPoints;
                    maxHp = upd.MaximumHealth;
                    maxSd = upd.MaximumShield;
                    maxMana = upd.MaximumMana;
                    maxAbility = upd.MaximumAbility;
                    _logger.LogInformation("⬆️ LevelUpdate (Extended): Lvl={Lvl}, Pts={Pts}", newLevel, newPoints);
                }
                else if (packet.Length >= CharacterLevelUpdate.Length)
                {
                    var upd = new CharacterLevelUpdate(packet);
                    newLevel = upd.Level;
                    newPoints = upd.LevelUpPoints;
                    maxHp = upd.MaximumHealth;
                    maxSd = upd.MaximumShield;
                    maxMana = upd.MaximumMana;
                    maxAbility = upd.MaximumAbility;
                    _logger.LogInformation("⬆️ LevelUpdate (Standard): Lvl={Lvl}, Pts={Pts}", newLevel, newPoints);
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) for CharacterLevelUpdate packet.",
                        packet.Length);
                    return Task.CompletedTask;
                }

                // Apply updates
                // Calculate new experience requirement for next level
                // Formula: ExpForNextLevel = (newLevel + 9) * newLevel * newLevel * 10
                ulong nextExp = (ulong)((newLevel + 9) * newLevel * newLevel * 10);
                _characterState.UpdateLevelAndExperience(newLevel, _characterState.Experience, nextExp, newPoints);
                _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                _characterState.UpdateMaximumManaAbility(maxMana, maxAbility);

                // Announce level-up
                if (newLevel > oldLevel)
                {
                    _logger.LogInformation("🎉 LEVEL UP! You are now level {Level} with {Points} points.", newLevel, newPoints);
                    Console.WriteLine($"*** LEVEL UP! You are now level {newLevel}! ***");
                    SoundController.Instance.PlayBuffer("Sound/pLevelUp.wav");
                    MuGame.ScheduleOnMainThread(async () =>
                    {
                        if (MuGame.Instance?.ActiveScene is not GameScene gameScene || gameScene.World == null || gameScene.Hero == null)
                        {
                            _logger.LogWarning("Cannot spawn LevelUpEffect: GameScene, World, or Hero is null.");
                            return;
                        }

                        var heroPosition = gameScene.Hero.Position;
                        var levelUpEffect = new LevelUpEffect(heroPosition);
                        gameScene.World.Objects.Add(levelUpEffect);
                        await levelUpEffect.Load(); // Asynchronously load the effect and its children

                        gameScene.ChatLog?.AddMessage("System", $"You have reached Level {newLevel}! You have {newPoints} stat points remaining.", Models.MessageType.System);
                    });
                }
                else
                {
                    _logger.LogInformation("📊 Level/points updated: Lvl={Lvl}, Pts={Pts}", newLevel, newPoints);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing CharacterLevelUpdate packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x26, 0xFF)] // CurrentHealthShield / CurrentStatsExtended
        public Task HandleCurrentHealthShieldAsync(Memory<byte> packet)
        {
            try
            {
                uint currentHp = 0, currentSd = 0, currentMana = 0, currentAbility = 0;
                bool updatedManaAbility = false;

                if (packet.Length >= CurrentStatsExtended.Length
                    && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var stats = new CurrentStatsExtended(packet);
                    currentHp = stats.Health;
                    currentSd = stats.Shield;
                    currentMana = stats.Mana;
                    currentAbility = stats.Ability;
                    _characterState.UpdateAttackSpeeds(stats.AttackSpeed, stats.MagicSpeed);
                    updatedManaAbility = true;
                    _logger.LogDebug("Parsing CurrentStats (Extended).");
                }
                else if (packet.Length >= CurrentHealthAndShield.Length)
                {
                    var stats = new CurrentHealthAndShield(packet);
                    currentHp = stats.Health;
                    currentSd = stats.Shield;
                    _logger.LogDebug("Parsing CurrentHealthAndShield (Standard).");
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) for CurrentHealthShield packet.",
                        packet.Length);
                    return Task.CompletedTask;
                }

                _characterState.UpdateCurrentHealthShield(currentHp, currentSd);
                if (updatedManaAbility)
                    _characterState.UpdateCurrentManaAbility(currentMana, currentAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing CurrentHealthShield packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x26, 0xFE)] // MaximumHealthShield / MaximumStatsExtended
        public Task HandleMaximumHealthShieldAsync(Memory<byte> packet)
        {
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAbility = 0;
                bool updatedManaAbility = false;

                if (packet.Length >= MaximumStatsExtended.Length
                    && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var stats = new MaximumStatsExtended(packet);
                    maxHp = stats.Health;
                    maxSd = stats.Shield;
                    maxMana = stats.Mana;
                    maxAbility = stats.Ability;
                    updatedManaAbility = true;
                    _logger.LogDebug("Parsing MaximumStats (Extended).");
                }
                else if (packet.Length >= MaximumHealthAndShield.Length)
                {
                    var stats = new MaximumHealthAndShield(packet);
                    maxHp = stats.Health;
                    maxSd = stats.Shield;
                    _logger.LogDebug("Parsing MaximumHealthAndShield (Standard).");
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) for MaximumHealthShield packet.",
                        packet.Length);
                    return Task.CompletedTask;
                }

                _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                if (updatedManaAbility)
                    _characterState.UpdateMaximumManaAbility(maxMana, maxAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing MaximumHealthShield packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x27, 0xFF)] // CurrentManaAbility / CurrentStatsExtended
        public Task HandleCurrentManaAbilityAsync(Memory<byte> packet)
        {
            try
            {
                uint currentHp = 0, currentSd = 0, currentMana = 0, currentAbility = 0;
                bool updatedHealthShield = false;

                if (packet.Length >= CurrentStatsExtended.Length
                    && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var stats = new CurrentStatsExtended(packet);
                    currentHp = stats.Health;
                    currentSd = stats.Shield;
                    currentMana = stats.Mana;
                    currentAbility = stats.Ability;
                    _characterState.UpdateAttackSpeeds(stats.AttackSpeed, stats.MagicSpeed);
                    updatedHealthShield = true;
                    _logger.LogDebug("Parsing CurrentStats (Extended).");
                }
                else if (packet.Length >= CurrentManaAndAbility.Length)
                {
                    var stats = new CurrentManaAndAbility(packet);
                    currentMana = stats.Mana;
                    currentAbility = stats.Ability;
                    _logger.LogDebug("Parsing CurrentManaAndAbility (Standard).");
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) for CurrentManaAbility packet.",
                        packet.Length);
                    return Task.CompletedTask;
                }

                _characterState.UpdateCurrentManaAbility(currentMana, currentAbility);
                if (updatedHealthShield)
                    _characterState.UpdateCurrentHealthShield(currentHp, currentSd);

                // If we're selecting a character and already receive in-game stats,
                // consider the character entered into game even if CharacterInformation wasn't sent.
                if (_networkManager.CurrentState == ClientConnectionState.SelectingCharacter)
                {
                    _logger.LogInformation("Heuristic EnteredGame: SelectingCharacter + stats received. Marking InGame.");
                    _networkManager.ProcessCharacterInformation();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing CurrentManaAbility packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x27, 0xFE)] // MaximumManaAbility / MaximumStatsExtended
        public Task HandleMaximumManaAbilityAsync(Memory<byte> packet)
        {
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAbility = 0;
                bool updatedHealthShield = false;

                if (packet.Length >= MaximumStatsExtended.Length
                    && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var stats = new MaximumStatsExtended(packet);
                    maxHp = stats.Health;
                    maxSd = stats.Shield;
                    maxMana = stats.Mana;
                    maxAbility = stats.Ability;
                    updatedHealthShield = true;
                    _logger.LogDebug("Parsing MaximumStats (Extended).");
                }
                else if (packet.Length >= MaximumManaAndAbility.Length)
                {
                    var stats = new MaximumManaAndAbility(packet);
                    maxMana = stats.Mana;
                    maxAbility = stats.Ability;
                    _logger.LogDebug("Parsing MaximumManaAndAbility (Standard).");
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) for MaximumManaAbility packet.",
                        packet.Length);
                    return Task.CompletedTask;
                }

                _characterState.UpdateMaximumManaAbility(maxMana, maxAbility);
                if (updatedHealthShield)
                    _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing MaximumManaAbility packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x16, PacketRouter.NoSubCode)] // ExperienceGained
        private Task HandleExperienceGainedAsync(Memory<byte> packet)
        {
            try
            {
                uint addedExperience = 0;
                ushort killedObjectId = 0;

                if (_targetVersion >= TargetProtocolVersion.Season6
                    && packet.Length >= ExperienceGainedExtended.Length)
                {
                    var exp = new ExperienceGainedExtended(packet);
                    addedExperience = exp.AddedExperience;
                    killedObjectId = (ushort)(exp.KilledObjectId & 0x7FFF);
                    _logger.LogInformation(
                        "✨ ExperienceGained (Extended): {Exp} for killing {KilledId:X4}",
                        addedExperience, killedObjectId);
                }
                else if (packet.Length >= ExperienceGained.Length)
                {
                    var exp = new ExperienceGained(packet);
                    addedExperience = exp.AddedExperience;
                    killedObjectId = (ushort)(exp.KilledObjectId & 0x7FFF);
                    _logger.LogInformation(
                        "✨ ExperienceGained (Standard): {Exp} for killing {KilledId:X4}",
                        addedExperience, killedObjectId);
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) for ExperienceGained packet.",
                        packet.Length);
                    return Task.CompletedTask;
                }

                _characterState.AddExperience(addedExperience);

                MuGame.ScheduleOnMainThread(() =>
                {
                    var gameScene = MuGame.Instance?.ActiveScene as Client.Main.Scenes.GameScene;
                    if (gameScene != null)
                    {
                        gameScene.ChatLog?.AddMessage("System", $"Gained {addedExperience} experience.", Client.Main.Models.MessageType.System);

                        if (addedExperience > 0 &&
                            killedObjectId != 0 &&
                            gameScene.World is WorldControl world &&
                            world.Status == GameControlStatus.Ready &&
                            world.TryGetWalkerById(killedObjectId, out var walker) &&
                            walker is MonsterObject monster &&
                            gameScene.Hero != null)
                        {
                            var hero = gameScene.Hero;
                            Vector3 origin = monster.WorldPosition.Translation + new Vector3(0f, 0f, 25f);

                            Func<Vector3> targetProvider = () =>
                            {
                                if (hero == null || hero.Status == GameControlStatus.Disposed)
                                    return origin;

                                var anchor = hero.WorldPosition.Translation;
                                anchor.Z += 110f;
                                return anchor;
                            };

                            int orbCount = Math.Clamp((int)Math.Round(Math.Log10(addedExperience + 1) + 3), 4, 8);
                            ExperienceOrbEffect.SpawnBurst(world, origin, targetProvider, orbCount);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing ExperienceGained packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x08)] // HeroStateChanged
        private Task HandleHeroStateChangedAsync(Memory<byte> packet)
        {
            try
            {
                var change = new HeroStateChanged(packet);
                ushort raw = change.PlayerId;
                ushort maskedId = (ushort)(raw & 0x7FFF);

                if (maskedId == _characterState.Id)
                {
                    var oldState = _characterState.HeroState;
                    _characterState.UpdateStatus(_characterState.Status, change.NewState);

                    string desc = change.NewState switch
                    {
                        CharacterHeroState.Hero => "a Hero",
                        CharacterHeroState.PlayerKiller1stStage => "a Player Killer (Stage 1)",
                        CharacterHeroState.PlayerKiller2ndStage => "a Player Killer (Stage 2)",
                        CharacterHeroState.PlayerKillWarning => "warned for Player Killing",
                        _ => "Normal"
                    };
                    _logger.LogInformation("⚖️ Your status changed to: {Desc}", desc);
                }
                else
                {
                    _logger.LogInformation(
                        "⚖️ HeroState of {Id:X4} changed to {NewState}.",
                        maskedId, change.NewState);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing HeroStateChanged packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x06)] // CharacterStatIncreaseResponse
        public Task HandleCharacterStatIncreaseResponseAsync(Memory<byte> packet)
        {
            ushort oldPoints = _characterState.LevelUpPoints;
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAbility = 0;
                bool success = false;
                CharacterStatAttribute attr = default;
                ushort addedAmount = 1;

                if (_targetVersion >= TargetProtocolVersion.Season6
                    && packet.Length >= CharacterStatIncreaseResponseExtended.Length)
                {
                    var resp = new CharacterStatIncreaseResponseExtended(packet);
                    success = true;
                    attr = resp.Attribute;
                    addedAmount = resp.AddedAmount;
                    maxHp = resp.UpdatedMaximumHealth;
                    maxSd = resp.UpdatedMaximumShield;
                    maxMana = resp.UpdatedMaximumMana;
                    maxAbility = resp.UpdatedMaximumAbility;
                    _logger.LogInformation("➕ StatIncreaseResponse (Extended): Attr={Attr}", attr);
                }
                else if (packet.Length >= CharacterStatIncreaseResponse.Length)
                {
                    var resp = new CharacterStatIncreaseResponse(packet);
                    success = resp.Success;
                    attr = resp.Attribute;
                    switch (attr)
                    {
                        case CharacterStatAttribute.Vitality:
                            maxHp = resp.UpdatedDependentMaximumStat;
                            break;
                        case CharacterStatAttribute.Energy:
                            maxMana = resp.UpdatedDependentMaximumStat;
                            break;
                    }
                    maxSd = resp.UpdatedMaximumShield;
                    maxAbility = resp.UpdatedMaximumAbility;
                    _logger.LogInformation("➕ StatIncreaseResponse (Standard): Attr={Attr}, Success={Succ}", attr, success);
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) for CharacterStatIncreaseResponse packet.",
                        packet.Length);
                    return Task.CompletedTask;
                }

                if (success)
                {
                    if (maxHp > 0) _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                    if (maxMana > 0) _characterState.UpdateMaximumManaAbility(maxMana, maxAbility);
                    _characterState.IncrementStat(attr, addedAmount);

                    ushort newPoints = (ushort)Math.Max(0, oldPoints - addedAmount);
                    _characterState.LevelUpPoints = newPoints;
                    _logger.LogInformation(
                        "➕ Stat point added to {Attr}. Points left: {Pts}", attr, newPoints);
                }
                else
                {
                    _logger.LogWarning("⚠️ Failed to increase stat {Attr}.", attr);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing CharacterStatIncreaseResponse packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x11)] // SkillListUpdate / Add / Remove
        public Task HandleSkillListUpdateAsync(Memory<byte> packet)
        {
            try
            {
                byte flag = packet.Span[4];
                if (flag == 0xFE)
                    ParseSkillAdded(packet);
                else if (flag == 0xFF)
                    ParseSkillRemoved(packet);
                else
                    ParseFullSkillList(packet, flag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing SkillListUpdate/Add/Remove packet.");
            }
            return Task.CompletedTask;
        }

        private void ParseSkillAdded(Memory<byte> packet)
        {
            ushort skillId = 0;
            byte level = 0;

            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var s6 = new SkillAdded(packet);
                    skillId = s6.SkillNumber;
                    level = s6.SkillLevel;
                    _logger.LogInformation("✨ Added Skill (S6): ID={Id}, Lvl={Lvl}", skillId, level);
                    break;
                case TargetProtocolVersion.Version097:
                    var s97 = new SkillAdded095(packet);
                    skillId = (ushort)(s97.SkillNumberAndLevel >> 8);
                    level = (byte)(s97.SkillNumberAndLevel & 0xFF);
                    _logger.LogInformation("✨ Added Skill (0.97): ID={Id}, Lvl={Lvl}", skillId, level);
                    break;
                case TargetProtocolVersion.Version075:
                    var s75 = new SkillAdded075(packet);
                    skillId = (ushort)(s75.SkillNumberAndLevel >> 8);
                    level = (byte)(s75.SkillNumberAndLevel & 0xFF);
                    _logger.LogInformation("✨ Added Skill (0.75): ID={Id}, Lvl={Lvl}", skillId, level);
                    break;
            }

            if (skillId > 0)
            {
                _characterState.AddOrUpdateSkill(new SkillEntryState { SkillId = skillId, SkillLevel = level });
            }
        }

        private void ParseSkillRemoved(Memory<byte> packet)
        {
            ushort skillId = 0;
            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    skillId = new SkillRemoved(packet).SkillNumber;
                    _logger.LogInformation("🗑️ Removed Skill (S6): ID={Id}", skillId);
                    break;
                case TargetProtocolVersion.Version097:
                    var r97 = new SkillRemoved095(packet);
                    skillId = (ushort)(r97.SkillNumberAndLevel >> 8);
                    _logger.LogInformation("🗑️ Removed Skill (0.97): ID={Id}", skillId);
                    break;
                case TargetProtocolVersion.Version075:
                    var r75 = new SkillRemoved075(packet);
                    skillId = (ushort)(r75.SkillNumberAndLevel >> 8);
                    _logger.LogInformation("🗑️ Removed Skill (0.75): ID={Id}", skillId);
                    break;
            }

            if (skillId > 0)
            {
                _characterState.RemoveSkill(skillId);
            }
        }

        private void ParseFullSkillList(Memory<byte> packet, byte count)
        {
            _logger.LogInformation("✨ SkillListUpdate ({Version}): {Count} skills.", _targetVersion, count);
            _characterState.ClearSkillList();

            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var listS6 = new SkillListUpdate(packet);
                    for (int i = 0; i < count; i++)
                    {
                        var e = listS6[i];
                        _characterState.AddOrUpdateSkill(new SkillEntryState { SkillId = e.SkillNumber, SkillLevel = e.SkillLevel });
                        _logger.LogDebug("  -> Skill {Idx}: ID={Id}, Lvl={Lvl}", e.SkillIndex, e.SkillNumber, e.SkillLevel);
                    }
                    break;

                case TargetProtocolVersion.Version097:
                case TargetProtocolVersion.Version075:
                    var listOld = new SkillListUpdate075(packet);
                    for (int i = 0; i < count; i++)
                    {
                        var e = listOld[i];
                        ushort sid = (ushort)(e.SkillNumberAndLevel >> 8);
                        byte lvl = (byte)(e.SkillNumberAndLevel & 0xFF);
                        _characterState.AddOrUpdateSkill(new SkillEntryState { SkillId = sid, SkillLevel = lvl });
                        _logger.LogDebug("  -> Skill {Idx}: ID={Id}, Lvl={Lvl}", e.SkillIndex, sid, lvl);
                    }
                    break;
            }
        }

        [PacketHandler(0xF3, 0x50)] // MasterStatsUpdate
        public Task HandleMasterStatsUpdateAsync(Memory<byte> packet)
        {
            ushort oldMasterLevel = _characterState.MasterLevel;
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAbility = 0;
                ushort mlvl = 0; ulong mexp = 0, nextMexp = 1; ushort mpts = 0;

                if (packet.Length >= MasterStatsUpdateExtended.Length
                    && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var upd = new MasterStatsUpdateExtended(packet);
                    mlvl = upd.MasterLevel;
                    mexp = upd.MasterExperience;
                    nextMexp = upd.MasterExperienceOfNextLevel;
                    mpts = upd.MasterLevelUpPoints;
                    maxHp = upd.MaximumHealth;
                    maxSd = upd.MaximumShield;
                    maxMana = upd.MaximumMana;
                    maxAbility = upd.MaximumAbility;
                    _logger.LogInformation("Ⓜ️ MasterStatsUpdate (Extended): MLvl={Lvl}, MPts={Pts}", mlvl, mpts);
                }
                else if (packet.Length >= MasterStatsUpdate.Length)
                {
                    var upd = new MasterStatsUpdate(packet);
                    mlvl = upd.MasterLevel;
                    mexp = upd.MasterExperience;
                    nextMexp = upd.MasterExperienceOfNextLevel;
                    mpts = upd.MasterLevelUpPoints;
                    maxHp = upd.MaximumHealth;
                    maxSd = upd.MaximumShield;
                    maxMana = upd.MaximumMana;
                    maxAbility = upd.MaximumAbility;
                    _logger.LogInformation("Ⓜ️ MasterStatsUpdate (Standard): MLvl={Lvl}, MPts={Pts}", mlvl, mpts);
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) for MasterStatsUpdate packet.",
                        packet.Length);
                    return Task.CompletedTask;
                }

                _characterState.UpdateMasterLevelAndExperience(mlvl, mexp, nextMexp, mpts);
                _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                _characterState.UpdateMaximumManaAbility(maxMana, maxAbility);

                if (mlvl > oldMasterLevel)
                {
                    _logger.LogInformation("Ⓜ️ MASTER LEVEL UP! Level {Lvl}, Points {Pts}", mlvl, mpts);
                    Console.WriteLine($"*** MASTER LEVEL UP! You are now master level {mlvl}! ***");
                    // TODO: Play master level up sound
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        var gameScene = MuGame.Instance?.ActiveScene as Client.Main.Scenes.GameScene;
                        if (gameScene != null)
                        {
                            gameScene.ChatLog?.AddMessage("System", $"You have reached Master Level {mlvl}! You have {mpts} master points remaining.", Client.Main.Models.MessageType.System);
                        }
                    });
                }
                else
                {
                    _logger.LogInformation("Ⓜ️ Master stats updated: MLvl={Lvl}, MPts={Pts}", mlvl, mpts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing MasterStatsUpdate packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x52)] // MasterSkillLevelUpdate
        public Task HandleMasterSkillLevelUpdateAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < MasterSkillLevelUpdate.Length)
                {
                    _logger.LogWarning(
                        "⚠️ Unexpected length ({Length}) for MasterSkillLevelUpdate packet.",
                        packet.Length);
                    return Task.CompletedTask;
                }

                var upd = new MasterSkillLevelUpdate(packet);
                _logger.LogInformation(
                    "Ⓜ️ MasterSkillLevelUpdate: Success={Succ}, Skill={Skill}, Lvl={Lvl}, Val={Val}, Next={Next}, Pts={Pts}",
                    upd.Success, upd.MasterSkillNumber, upd.Level,
                    upd.DisplayValue, upd.DisplayValueOfNextLevel, upd.MasterLevelUpPoints);

                if (upd.Success)
                {
                    _characterState.MasterLevelUpPoints = upd.MasterLevelUpPoints;
                    _characterState.AddOrUpdateSkill(new SkillEntryState
                    {
                        SkillId = upd.MasterSkillNumber,
                        SkillLevel = upd.Level,
                        DisplayValue = upd.DisplayValue,
                        NextDisplayValue = upd.DisplayValueOfNextLevel
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error parsing MasterSkillLevelUpdate packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x19, PacketRouter.NoSubCode)] // SkillAnimation (Targeted)
        public Task HandleSkillAnimationAsync(Memory<byte> packet)
        {
            try
            {
                var skillAnim = new SkillAnimation(packet);
                ushort playerId = skillAnim.PlayerId;
                ushort skillId = skillAnim.SkillId;
                ushort targetId = skillAnim.TargetId;

                _logger.LogDebug("SkillAnimation: Player={PlayerId}, Skill={SkillId}, Target={TargetId}",
                    playerId, skillId, targetId);

                MuGame.ScheduleOnMainThread(() =>
                {
                    var activeScene = MuGame.Instance?.ActiveScene as GameScene;
                    if (activeScene?.Hero == null) return;

                    // Check if this is our player
                    if (playerId == _characterState.Id)
                    {
                        int animationId = Core.Utilities.SkillDatabase.GetSkillAnimation(skillId);
                        string soundPath = Client.Data.BMD.SkillDefinitions.GetSkillSound(skillId);

                        // Play skill sound if available
                        if (!string.IsNullOrEmpty(soundPath))
                        {
                            SoundController.Instance.PlayBuffer(soundPath);
                        }

                        // Trigger vehicle skill animation if riding
                        activeScene.Hero.TriggerVehicleSkillAnimation();

                        if (animationId > 0)
                        {
                            activeScene.Hero.PlayAction((ushort)animationId);
                            _logger.LogInformation(
                                "Playing targeted skill animation {AnimationId} for skill {SkillId} ({SkillName}) on target {TargetId}",
                                animationId,
                                skillId,
                                Core.Utilities.SkillDatabase.GetSkillName(skillId),
                                targetId);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Playing targeted skill {SkillId} ({SkillName}) on target {TargetId}",
                                skillId,
                                Core.Utilities.SkillDatabase.GetSkillName(skillId),
                                targetId);
                        }
                    }
                    else
                    {
                        // TODO: Handle other players' skill animations when scope system supports it
                        _logger.LogDebug("Other player {PlayerId} used targeted skill {SkillId} on {TargetId}",
                            playerId, skillId, targetId);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error handling SkillAnimation packet.");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x1E, PacketRouter.NoSubCode)] // AreaSkillAnimation
        public Task HandleAreaSkillAnimationAsync(Memory<byte> packet)
        {
            try
            {
                var areaSkill = new AreaSkillAnimation(packet);
                ushort playerId = areaSkill.PlayerId;
                ushort skillId = areaSkill.SkillId;
                byte targetX = areaSkill.PointX;
                byte targetY = areaSkill.PointY;

                _logger.LogDebug("AreaSkillAnimation: Player={PlayerId}, Skill={SkillId}, Target=({X},{Y})",
                    playerId, skillId, targetX, targetY);

                MuGame.ScheduleOnMainThread(() =>
                {
                    var activeScene = MuGame.Instance?.ActiveScene as GameScene;
                    if (activeScene?.Hero == null) return;

                    // Check if this is our player
                    if (playerId == _characterState.Id)
                    {
                        // Get animation from SkillDatabase
                        int animationId = Core.Utilities.SkillDatabase.GetSkillAnimation(skillId);
                        string soundPath = Client.Data.BMD.SkillDefinitions.GetSkillSound(skillId);

                        // Play skill sound if available
                        if (!string.IsNullOrEmpty(soundPath))
                        {
                            SoundController.Instance.PlayBuffer(soundPath);
                        }

                        // Trigger vehicle skill animation if riding
                        activeScene.Hero.TriggerVehicleSkillAnimation();

                        if (animationId > 0)
                        {
                            activeScene.Hero.PlayAction((ushort)animationId);
                            _logger.LogInformation("Playing skill animation {AnimationId} for skill {SkillId} ({SkillName})",
                                animationId, skillId, Core.Utilities.SkillDatabase.GetSkillName(skillId));
                        }
                        else
                        {
                            // No specific animation - skill uses generic magic/attack animation
                            _logger.LogDebug("Skill {SkillId} ({SkillName}) uses generic animation",
                                skillId, Core.Utilities.SkillDatabase.GetSkillName(skillId));
                        }
                    }
                    else
                    {
                        // TODO: Handle other players' skill animations when scope system supports it
                        _logger.LogDebug("Other player {PlayerId} used skill {SkillId}", playerId, skillId);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error handling AreaSkillAnimation packet.");
            }

            return Task.CompletedTask;
        }

        private void HandleElfBuffVisual(byte effectId, ushort playerId, bool isActive) =>
            _elfBuffEffectManager.HandleBuff(effectId, playerId, isActive);
    }
}
