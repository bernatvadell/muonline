using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Client; // For CharacterState, SimpleLoginClient, TargetProtocolVersion
using Client.Main.Core.Utilities;
using static MUnique.OpenMU.Network.Packets.ServerToClient.SkillListUpdate;
using System;
using System.Threading.Tasks; // For PacketHandlerAttribute

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles packets related to character stats, level, and status updates.
    /// </summary>
    public class CharacterDataHandler : IGamePacketHandler
    {
        private readonly ILogger<CharacterDataHandler> _logger;
        private readonly CharacterState _characterState;
        private readonly NetworkManager _networkManager; // Needed for UpdateConsoleTitle
        private readonly TargetProtocolVersion _targetVersion;

        public CharacterDataHandler(ILoggerFactory loggerFactory, CharacterState characterState, NetworkManager networkManager, TargetProtocolVersion targetVersion)
        {
            _logger = loggerFactory.CreateLogger<CharacterDataHandler>();
            _characterState = characterState;
            _networkManager = networkManager;
            _targetVersion = targetVersion;
        }

        [PacketHandler(0xF3, 0x03)] // CharacterInformation
        public Task HandleCharacterInformationAsync(Memory<byte> packet)
        {
            try
            {
                string version = "Unknown";
                ushort mapId = 0; byte x = 0, y = 0;
                ulong currentExp = 0, nextExp = 1; ushort level = 1, levelUpPoints = 0;
                uint initialHp = 0, maxHp = 1, initialSd = 0, maxSd = 0;
                uint initialMana = 0, maxMana = 1, initialAg = 0, maxAg = 0;
                ushort str = 0, agi = 0, vit = 0, ene = 0, cmd = 0;
                byte inventoryExpansion = 0;
                CharacterStatus status = CharacterStatus.Normal;
                CharacterHeroState heroState = CharacterHeroState.Normal;
                uint money = 0;

                // Determine level based on experience if not directly in packet (needed for some versions?)
                // (Assuming level is part of the packet structure for simplicity here, adjust if needed)

                switch (_targetVersion)
                {
                    // (Parsing logic remains the same, just without assigning to local charClass)
                    case TargetProtocolVersion.Season6:
                        if (packet.Length >= CharacterInformationExtended.Length) { var info = new CharacterInformationExtended(packet); version = "S6 Extended"; mapId = info.MapId; x = info.X; y = info.Y; currentExp = info.CurrentExperience; nextExp = info.ExperienceForNextLevel; levelUpPoints = info.LevelUpPoints; str = info.Strength; agi = info.Agility; vit = info.Vitality; ene = info.Energy; cmd = info.Leadership; initialHp = info.CurrentHealth; maxHp = info.MaximumHealth; initialSd = info.CurrentShield; maxSd = info.MaximumShield; initialMana = info.CurrentMana; maxMana = info.MaximumMana; initialAg = info.CurrentAbility; maxAg = info.MaximumAbility; money = info.Money; heroState = info.HeroState; status = info.Status; inventoryExpansion = info.InventoryExtensions; /* Level? */ }
                        else if (packet.Length >= CharacterInformation.Length) { var info = new CharacterInformation(packet); version = "S6 Standard"; mapId = info.MapId; x = info.X; y = info.Y; currentExp = info.CurrentExperience; nextExp = info.ExperienceForNextLevel; levelUpPoints = info.LevelUpPoints; str = info.Strength; agi = info.Agility; vit = info.Vitality; ene = info.Energy; cmd = info.Leadership; initialHp = info.CurrentHealth; maxHp = info.MaximumHealth; initialSd = info.CurrentShield; maxSd = info.MaximumShield; initialMana = info.CurrentMana; maxMana = info.MaximumMana; initialAg = info.CurrentAbility; maxAg = info.MaximumAbility; money = info.Money; heroState = info.HeroState; status = info.Status; inventoryExpansion = info.InventoryExtensions; /* Level? */ }
                        else goto default;
                        break;
                    case TargetProtocolVersion.Version097:
                        if (packet.Length >= CharacterInformation097.Length) { var info = new CharacterInformation097(packet); version = "0.97"; mapId = info.MapId; x = info.X; y = info.Y; currentExp = info.CurrentExperience; nextExp = info.ExperienceForNextLevel; levelUpPoints = info.LevelUpPoints; str = info.Strength; agi = info.Agility; vit = info.Vitality; ene = info.Energy; cmd = info.Leadership; initialHp = info.CurrentHealth; maxHp = info.MaximumHealth; initialSd = 0; maxSd = 0; initialMana = info.CurrentMana; maxMana = info.MaximumMana; initialAg = info.CurrentAbility; maxAg = info.MaximumAbility; money = info.Money; heroState = info.HeroState; status = info.Status; inventoryExpansion = 0; /* Level? */}
                        else goto default;
                        break;
                    case TargetProtocolVersion.Version075:
                        if (packet.Length >= CharacterInformation075.Length) { var info = new CharacterInformation075(packet); version = "0.75"; mapId = info.MapId; x = info.X; y = info.Y; currentExp = info.CurrentExperience; nextExp = info.ExperienceForNextLevel; levelUpPoints = info.LevelUpPoints; str = info.Strength; agi = info.Agility; vit = info.Vitality; ene = info.Energy; cmd = 0; initialHp = info.CurrentHealth; maxHp = info.MaximumHealth; initialSd = 0; maxSd = 0; initialMana = info.CurrentMana; maxMana = info.MaximumMana; initialAg = 0; maxAg = 0; money = info.Money; heroState = info.HeroState; status = info.Status; inventoryExpansion = 0; /* Level? */}
                        else goto default;
                        break;
                    default: _logger.LogWarning("⚠️ Unexpected length ({Length}) or unsupported version ({Version}) for CharacterInformation.", packet.Length, _targetVersion); return Task.CompletedTask;
                }

                // Determine level based on experience for older versions if needed
                // Example placeholder: if (_targetVersion <= TargetProtocolVersion.Version097) { level = ExperienceCalculator.GetLevelFromExperience(currentExp); }

                _logger.LogInformation("✅ Successfully entered the game world.");

                // Update CharacterState with data FROM THIS PACKET
                _characterState.UpdatePosition(x, y);
                _characterState.UpdateMap(mapId); // Update state first
                _characterState.UpdateLevelAndExperience(level, currentExp, nextExp, levelUpPoints);
                _characterState.UpdateStats(str, agi, vit, ene, cmd);
                _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                _characterState.UpdateMaximumManaAbility(maxMana, maxAg);
                _characterState.UpdateCurrentHealthShield(initialHp, initialSd);
                _characterState.UpdateCurrentManaAbility(initialMana, initialAg);
                _characterState.UpdateInventoryZen(money);
                _characterState.UpdateStatus(status, heroState);
                _characterState.InventoryExpansionState = inventoryExpansion;

                // Log map change using the updated state and MapDatabase
                _logger.LogInformation("🗺️ Entered map: {MapName} ({MapId}) at ({X},{Y}).",
                                        MapDatabase.GetMapName(_characterState.MapId), _characterState.MapId, _characterState.PositionX, _characterState.PositionY);
                // _networkManager.UpdateStatsDisplay(); // Powiadom ViewModel
                // _networkManager.SetInGameStatus(true); // This will log "Entered game world..."
                _networkManager.ProcessCharacterInformation();
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing CharacterInformation packet."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x05)] // CharacterLevelUpdate
        public Task HandleCharacterLevelUpdateAsync(Memory<byte> packet)
        {
            ushort oldLevel = _characterState.Level; // Store old level before update
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAg = 0;
                ushort newLevel = _characterState.Level; ushort levelUpPoints = _characterState.LevelUpPoints;

                if (packet.Length >= CharacterLevelUpdateExtended.Length && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var update = new CharacterLevelUpdateExtended(packet);
                    newLevel = update.Level; levelUpPoints = update.LevelUpPoints;
                    maxHp = update.MaximumHealth; maxSd = update.MaximumShield; maxMana = update.MaximumMana; maxAg = update.MaximumAbility;
                    _logger.LogInformation("⬆️ Received CharacterLevelUpdate (Extended): Lvl={Lvl}, Pts={Pts}", newLevel, levelUpPoints);
                }
                else if (packet.Length >= CharacterLevelUpdate.Length)
                {
                    var update = new CharacterLevelUpdate(packet);
                    newLevel = update.Level; levelUpPoints = update.LevelUpPoints;
                    maxHp = update.MaximumHealth; maxSd = update.MaximumShield; maxMana = update.MaximumMana; maxAg = update.MaximumAbility;
                    _logger.LogInformation("⬆️ Received CharacterLevelUpdate (Standard): Lvl={Lvl}, Pts={Pts}", newLevel, levelUpPoints);
                }
                else { _logger.LogWarning("⚠️ Unexpected length ({Length}) for CharacterLevelUpdate packet (F3, 05).", packet.Length); return Task.CompletedTask; }

                _characterState.UpdateLevelAndExperience(newLevel, _characterState.Experience, _characterState.ExperienceForNextLevel, levelUpPoints);
                _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                _characterState.UpdateMaximumManaAbility(maxMana, maxAg);

                // Log level up specifically if level increased
                if (newLevel > oldLevel)
                {
                    _logger.LogInformation("🎉 LEVEL UP! Reached level {Level}. You have {Points} points to distribute.", newLevel, levelUpPoints);
                    Console.WriteLine($"*** LEVEL UP! You are now level {newLevel}! ***");
                }
                else // Log even if level didn't change but points/max stats did (e.g., quest reward)
                {
                    _logger.LogInformation("📊 Character stats/points updated: Lvl={Lvl}, Pts={Pts}", newLevel, levelUpPoints);
                }

                // _networkManager.UpdateConsoleTitle();
                // _networkManager.UpdateStatsDisplay(); // Powiadom ViewModel

            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing CharacterLevelUpdate (F3, 05)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0x26, 0xFF)] // CurrentHealthShield / CurrentStatsExtended
        public Task HandleCurrentHealthShieldAsync(Memory<byte> packet)
        {
            try
            {
                uint currentHp = 0, currentSd = 0, currentMana = 0, currentAbility = 0; bool updatedManaAbility = false;
                if (packet.Length >= CurrentStatsExtended.Length && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var stats = new CurrentStatsExtended(packet);
                    currentHp = stats.Health; currentSd = stats.Shield; currentMana = stats.Mana; currentAbility = stats.Ability;
                    updatedManaAbility = true; _logger.LogDebug("❤️🛡️💧✨ Parsing CurrentStats (Extended)");
                }
                else if (packet.Length >= CurrentHealthAndShield.Length)
                {
                    var stats = new CurrentHealthAndShield(packet);
                    currentHp = stats.Health; currentSd = stats.Shield; _logger.LogDebug("❤️🛡️ Parsing CurrentHealthAndShield (Standard)");
                }
                else { _logger.LogWarning("⚠️ Unexpected length ({Length}) for CurrentHealthShield packet (26, FF).", packet.Length); return Task.CompletedTask; }

                _characterState.UpdateCurrentHealthShield(currentHp, currentSd);
                if (updatedManaAbility) _characterState.UpdateCurrentManaAbility(currentMana, currentAbility);
                // _networkManager.UpdateConsoleTitle();
                // _networkManager.UpdateStatsDisplay(); // Użyj dedykowanej metody
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing CurrentHealthShield (26, FF)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0x26, 0xFE)] // MaximumHealthShield / MaximumStatsExtended
        public Task HandleMaximumHealthShieldAsync(Memory<byte> packet)
        {
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAbility = 0; bool updatedManaAbility = false;
                if (packet.Length >= MaximumStatsExtended.Length && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var stats = new MaximumStatsExtended(packet);
                    maxHp = stats.Health; maxSd = stats.Shield; maxMana = stats.Mana; maxAbility = stats.Ability;
                    updatedManaAbility = true; _logger.LogDebug("❤️🛡️💧✨ Parsing MaximumStats (Extended)");
                }
                else if (packet.Length >= MaximumHealthAndShield.Length)
                {
                    var stats = new MaximumHealthAndShield(packet);
                    maxHp = stats.Health; maxSd = stats.Shield; _logger.LogDebug("❤️🛡️ Parsing MaximumHealthAndShield (Standard)");
                }
                else { _logger.LogWarning("⚠️ Unexpected length ({Length}) for MaximumHealthShield packet (26, FE).", packet.Length); return Task.CompletedTask; }

                _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                if (updatedManaAbility) _characterState.UpdateMaximumManaAbility(maxMana, maxAbility);
                // _networkManager.UpdateConsoleTitle();
                // _networkManager.UpdateStatsDisplay(); // Powiadom ViewModel

            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing MaximumHealthShield (26, FE)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0x27, 0xFF)] // CurrentManaAbility / CurrentStatsExtended
        public Task HandleCurrentManaAbilityAsync(Memory<byte> packet)
        {
            try
            {
                uint currentHp = 0, currentSd = 0, currentMana = 0, currentAbility = 0; bool updatedHealthShield = false;
                if (packet.Length >= CurrentStatsExtended.Length && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var stats = new CurrentStatsExtended(packet);
                    currentHp = stats.Health; currentSd = stats.Shield; currentMana = stats.Mana; currentAbility = stats.Ability;
                    updatedHealthShield = true; _logger.LogDebug("❤️🛡️💧✨ Parsing CurrentStats (Extended)");
                }
                else if (packet.Length >= CurrentManaAndAbility.Length)
                {
                    var stats = new CurrentManaAndAbility(packet);
                    currentMana = stats.Mana; currentAbility = stats.Ability; _logger.LogDebug("💧✨ Parsing CurrentManaAndAbility (Standard)");
                }
                else { _logger.LogWarning("⚠️ Unexpected length ({Length}) for CurrentManaAndAbility packet (27, FF).", packet.Length); return Task.CompletedTask; }

                _characterState.UpdateCurrentManaAbility(currentMana, currentAbility);
                if (updatedHealthShield) _characterState.UpdateCurrentHealthShield(currentHp, currentSd);
                // _networkManager.UpdateConsoleTitle();
                // _networkManager.UpdateStatsDisplay();
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing CurrentManaAndAbility (27, FF)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0x27, 0xFE)] // MaximumManaAbility / MaximumStatsExtended
        public Task HandleMaximumManaAbilityAsync(Memory<byte> packet)
        {
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAbility = 0; bool updatedHealthShield = false;
                if (packet.Length >= MaximumStatsExtended.Length && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var stats = new MaximumStatsExtended(packet);
                    maxHp = stats.Health; maxSd = stats.Shield; maxMana = stats.Mana; maxAbility = stats.Ability;
                    updatedHealthShield = true; _logger.LogDebug("❤️🛡️💧✨ Parsing MaximumStats (Extended)");
                }
                else if (packet.Length >= MaximumManaAndAbility.Length)
                {
                    var stats = new MaximumManaAndAbility(packet);
                    maxMana = stats.Mana; maxAbility = stats.Ability; _logger.LogDebug("💧✨ Parsing MaximumManaAndAbility (Standard)");
                }
                else { _logger.LogWarning("⚠️ Unexpected length ({Length}) for MaximumManaAndAbility packet (27, FE).", packet.Length); return Task.CompletedTask; }

                _characterState.UpdateMaximumManaAbility(maxMana, maxAbility);
                if (updatedHealthShield) _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                // _networkManager.UpdateConsoleTitle();
                // _networkManager.UpdateStatsDisplay();
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing MaximumManaAndAbility (27, FE)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0x16, PacketRouter.NoSubCode)] // ExperienceGained
        private Task HandleExperienceGainedAsync(Memory<byte> packet)
        {
            try
            {
                uint addedExperience = 0;
                if (_targetVersion >= TargetProtocolVersion.Season6 && packet.Length >= ExperienceGainedExtended.Length)
                {
                    var exp = new ExperienceGainedExtended(packet);
                    addedExperience = exp.AddedExperience;
                    _logger.LogInformation("✨ Gained Experience (Extended): {Exp} for killing {KilledId:X4}", addedExperience, exp.KilledObjectId);
                }
                else if (packet.Length >= ExperienceGained.Length)
                {
                    var exp = new ExperienceGained(packet);
                    addedExperience = exp.AddedExperience;
                    _logger.LogInformation("✨ Gained Experience (Standard): {Exp} for killing {KilledId:X4}", addedExperience, exp.KilledObjectId);
                }
                else { _logger.LogWarning("⚠️ Unexpected length ({Length}) for ExperienceGained packet (16).", packet.Length); return Task.CompletedTask; }

                _characterState.AddExperience(addedExperience);
                // _networkManager.UpdateConsoleTitle();
                // _networkManager.UpdateStatsDisplay();
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing ExperienceGained (16)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x08)] // HeroStateChanged
        private Task HandleHeroStateChangedAsync(Memory<byte> packet)
        {
            try
            {
                var stateChange = new HeroStateChanged(packet);
                ushort playerIdRaw = stateChange.PlayerId; ushort playerIdMasked = (ushort)(playerIdRaw & 0x7FFF);

                if (playerIdMasked == _characterState.Id)
                {
                    CharacterHeroState oldState = _characterState.HeroState; // Get old state before update
                    _characterState.UpdateStatus(_characterState.Status, stateChange.NewState); // Update state

                    // Describe the change more meaningfully
                    string stateDesc = stateChange.NewState switch
                    {
                        CharacterHeroState.Hero => "a Hero",
                        CharacterHeroState.PlayerKiller1stStage => "a Player Killer (Stage 1)",
                        CharacterHeroState.PlayerKiller2ndStage => "a Player Killer (Stage 2)",
                        CharacterHeroState.PlayerKillWarning => "warned for Player Killing",
                        _ => "Normal"
                    };
                    _logger.LogInformation("⚖️ Your status changed to: {StateDescription}", stateDesc);

                    // _networkManager.UpdateConsoleTitle();
                    // _networkManager.UpdateStatsDisplay();
                }
                else
                {
                    _logger.LogInformation("⚖️ Hero State of {PlayerId:X4} (Raw: {RawId:X4}) changed to {NewState}.", playerIdMasked, playerIdRaw, stateChange.NewState);
                    // TODO: Update hero state in ScopeManager if needed
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing HeroStateChanged (F3, 08)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x06)] // CharacterStatIncreaseResponse
        public Task HandleCharacterStatIncreaseResponseAsync(Memory<byte> packet)
        {
            ushort oldPoints = _characterState.LevelUpPoints; // Get points before potential change
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAg = 0;
                CharacterStatAttribute attribute = default; bool success = false;
                ushort addedAmount = 1; // Default for standard response

                if (packet.Length >= CharacterStatIncreaseResponseExtended.Length && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var response = new CharacterStatIncreaseResponseExtended(packet);
                    success = true; attribute = response.Attribute; addedAmount = response.AddedAmount;
                    maxHp = response.UpdatedMaximumHealth; maxSd = response.UpdatedMaximumShield;
                    maxMana = response.UpdatedMaximumMana; maxAg = response.UpdatedMaximumAbility;
                    _logger.LogInformation("➕ Received StatIncreaseResponse (Extended): Attribute={Attr}", attribute);
                }
                else if (packet.Length >= CharacterStatIncreaseResponse.Length)
                {
                    var response = new CharacterStatIncreaseResponse(packet);
                    success = response.Success; attribute = response.Attribute;
                    switch (attribute) { case CharacterStatAttribute.Vitality: maxHp = response.UpdatedDependentMaximumStat; break; case CharacterStatAttribute.Energy: maxMana = response.UpdatedDependentMaximumStat; break; }
                    maxSd = response.UpdatedMaximumShield; maxAg = response.UpdatedMaximumAbility;
                    _logger.LogInformation("➕ Received StatIncreaseResponse (Standard): Attribute={Attr}, Success={Success}", attribute, success);
                }
                else { _logger.LogWarning("⚠️ Unexpected length ({Length}) for CharacterStatIncreaseResponse packet (F3, 06).", packet.Length); return Task.CompletedTask; }

                if (success)
                {
                    if (maxHp > 0) _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                    if (maxMana > 0) _characterState.UpdateMaximumManaAbility(maxMana, maxAg);
                    _characterState.IncrementStat(attribute, addedAmount); // Update the base stat

                    // Assuming LevelUpPoints were reduced by 'addedAmount' on the server
                    // We don't get the new point count directly in standard packets, so deduce it.
                    ushort newPoints = (ushort)Math.Max(0, oldPoints - addedAmount);
                    _characterState.LevelUpPoints = newPoints; // Update local state

                    _logger.LogInformation("➕ Stat point successfully added to {Attribute}. Points left: {Points}", attribute, newPoints);
                    // _networkManager.UpdateConsoleTitle();
                    // _networkManager.UpdateStatsDisplay(); // Powiadom ViewModel
                }
                else { _logger.LogWarning("⚠️ Stat point update failed for {Attribute}.", attribute); }
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing CharacterStatIncreaseResponse (F3, 06)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x11)] // SkillListUpdate / SkillAdded / SkillRemoved
        public Task HandleSkillListUpdateAsync(Memory<byte> packet)
        {
            try
            {
                byte countOrFlag = packet.Span[4];
                switch (countOrFlag)
                {
                    case 0xFE: // Skill Added
                        ParseSkillAdded(packet);
                        break;
                    case 0xFF: // Skill Removed
                        ParseSkillRemoved(packet);
                        break;
                    default: // Full Skill List
                        ParseFullSkillList(packet, countOrFlag);
                        break;
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing SkillListUpdate/Add/Remove (F3, 11)."); }
            return Task.CompletedTask;
        }

        private void ParseSkillAdded(Memory<byte> packet)
        {
            ushort skillId = 0;
            byte skillLevel = 0;

            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var addedS6 = new SkillAdded(packet);
                    skillId = addedS6.SkillNumber; skillLevel = addedS6.SkillLevel;
                    _logger.LogInformation("✨ Added Skill (S6): ID={Num}, Lvl={Lvl}", skillId, skillLevel);
                    break;
                case TargetProtocolVersion.Version097:
                    var added095 = new SkillAdded095(packet);
                    // Need to decode SkillNumberAndLevel (assuming simple format: High byte = ID, Low byte = Level)
                    skillId = (ushort)(added095.SkillNumberAndLevel >> 8);
                    skillLevel = (byte)(added095.SkillNumberAndLevel & 0xFF);
                    _logger.LogInformation("✨ Added Skill (0.97): ID={Num}, Lvl={Lvl}", skillId, skillLevel);
                    break;
                case TargetProtocolVersion.Version075:
                    var added075 = new SkillAdded075(packet);
                    skillId = (ushort)(added075.SkillNumberAndLevel >> 8);
                    skillLevel = (byte)(added075.SkillNumberAndLevel & 0xFF);
                    _logger.LogInformation("✨ Added Skill (0.75): ID={Num}, Lvl={Lvl}", skillId, skillLevel);
                    break;
            }
            if (skillId > 0)
            {
                _characterState.AddOrUpdateSkill(new SkillEntryState { SkillId = skillId, SkillLevel = skillLevel });
                // _networkManager.UpdateSkillsDisplay(); // Odśwież widok Skills
            }
        }

        private void ParseSkillRemoved(Memory<byte> packet)
        {
            ushort skillId = 0;
            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    var removedS6 = new SkillRemoved(packet);
                    skillId = removedS6.SkillNumber;
                    _logger.LogInformation("🗑️ Removed Skill (S6): ID={Num}", skillId);
                    break;
                case TargetProtocolVersion.Version097:
                    var removed095 = new SkillRemoved095(packet);
                    skillId = (ushort)(removed095.SkillNumberAndLevel >> 8);
                    _logger.LogInformation("🗑️ Removed Skill (0.97): ID={Num}", skillId);
                    break;
                case TargetProtocolVersion.Version075:
                    var removed075 = new SkillRemoved075(packet);
                    skillId = (ushort)(removed075.SkillNumberAndLevel >> 8);
                    _logger.LogInformation("🗑️ Removed Skill (0.75): ID={Num}", skillId);
                    break;
            }
            if (skillId > 0)
            {
                _characterState.RemoveSkill(skillId);
                // _networkManager.UpdateSkillsDisplay(); // Odśwież widok Skills
            }
        }

        private void ParseFullSkillList(Memory<byte> packet, byte count)
        {
            _logger.LogInformation("✨ Received SkillListUpdate ({Version}): {Count} skills.", _targetVersion, count);
            _characterState.ClearSkillList();
            int offset = 0;
            int entrySize = 0;

            switch (_targetVersion)
            {
                case TargetProtocolVersion.Season6:
                    offset = 6; entrySize = SkillEntry.Length;
                    var listS6 = new SkillListUpdate(packet);
                    for (int i = 0; i < count; i++)
                    {
                        var entry = listS6[i];
                        _characterState.AddOrUpdateSkill(new SkillEntryState { SkillId = entry.SkillNumber, SkillLevel = entry.SkillLevel });
                        _logger.LogDebug("  -> Skill {Index}: ID={Num}, Lvl={Lvl}", entry.SkillIndex, entry.SkillNumber, entry.SkillLevel);
                    }
                    break;
                case TargetProtocolVersion.Version097:
                case TargetProtocolVersion.Version075:
                    offset = 5; entrySize = MUnique.OpenMU.Network.Packets.ServerToClient.SkillListUpdate075.SkillEntry.Length; // Use the correct struct length
                    var listLegacy = new SkillListUpdate075(packet);
                    for (int i = 0; i < count; i++)
                    {
                        var entry = listLegacy[i];
                        ushort skillId = (ushort)(entry.SkillNumberAndLevel >> 8);
                        byte skillLevel = (byte)(entry.SkillNumberAndLevel & 0xFF);
                        _characterState.AddOrUpdateSkill(new SkillEntryState { SkillId = skillId, SkillLevel = skillLevel });
                        _logger.LogDebug("  -> Skill {Index}: ID={Num}, Lvl={Lvl}", entry.SkillIndex, skillId, skillLevel);
                    }
                    break;
            }
            // _networkManager.UpdateSkillsDisplay(); // Odśwież widok Skills

        }

        [PacketHandler(0xF3, 0x50)] // MasterStatsUpdate
        public Task HandleMasterStatsUpdateAsync(Memory<byte> packet)
        {
            ushort oldMasterLevel = _characterState.MasterLevel; // Store old level before update
            try
            {
                uint maxHp = 0, maxSd = 0, maxMana = 0, maxAg = 0;
                ushort masterLevel = 0; ulong masterExp = 0, nextMasterExp = 1; ushort masterPoints = 0;

                if (packet.Length >= MasterStatsUpdateExtended.Length && _targetVersion >= TargetProtocolVersion.Season6)
                {
                    var update = new MasterStatsUpdateExtended(packet);
                    masterLevel = update.MasterLevel; masterExp = update.MasterExperience; nextMasterExp = update.MasterExperienceOfNextLevel; masterPoints = update.MasterLevelUpPoints;
                    maxHp = update.MaximumHealth; maxSd = update.MaximumShield; maxMana = update.MaximumMana; maxAg = update.MaximumAbility;
                    _logger.LogInformation("Ⓜ️ Received MasterStatsUpdate (Extended): MasterLvl={Lvl}, MasterPts={Pts}", masterLevel, masterPoints);
                }
                else if (packet.Length >= MasterStatsUpdate.Length)
                {
                    var update = new MasterStatsUpdate(packet);
                    masterLevel = update.MasterLevel; masterExp = update.MasterExperience; nextMasterExp = update.MasterExperienceOfNextLevel; masterPoints = update.MasterLevelUpPoints;
                    maxHp = update.MaximumHealth; maxSd = update.MaximumShield; maxMana = update.MaximumMana; maxAg = update.MaximumAbility;
                    _logger.LogInformation("Ⓜ️ Received MasterStatsUpdate (Standard): MasterLvl={Lvl}, MasterPts={Pts}", masterLevel, masterPoints);
                }
                else { _logger.LogWarning("⚠️ Unexpected length ({Length}) for MasterStatsUpdate packet (F3, 50).", packet.Length); return Task.CompletedTask; }

                _characterState.UpdateMasterLevelAndExperience(masterLevel, masterExp, nextMasterExp, masterPoints); // Update state
                _characterState.UpdateMaximumHealthShield(maxHp, maxSd);
                _characterState.UpdateMaximumManaAbility(maxMana, maxAg);

                // Log master level up specifically if level increased
                if (masterLevel > oldMasterLevel)
                {
                    _logger.LogInformation("Ⓜ️ MASTER LEVEL UP! Reached master level {MasterLevel}. You have {Points} master points.", masterLevel, masterPoints);
                    Console.WriteLine($"*** MASTER LEVEL UP! You are now master level {masterLevel}! ***");
                }
                else // Log if only experience/points changed
                {
                    _logger.LogInformation("Ⓜ️ Master stats updated: MLvl={Lvl}, MPts={Pts}", masterLevel, masterPoints);
                }

                // _networkManager.UpdateConsoleTitle();
                // _networkManager.UpdateStatsDisplay();
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing MasterStatsUpdate (F3, 50)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0xF3, 0x52)] // MasterSkillLevelUpdate
        public Task HandleMasterSkillLevelUpdateAsync(Memory<byte> packet)
        {
            try
            {
                if (packet.Length < MasterSkillLevelUpdate.Length) { _logger.LogWarning("⚠️ Unexpected length ({Length}) for MasterSkillLevelUpdate packet (F3, 52).", packet.Length); return Task.CompletedTask; }

                var update = new MasterSkillLevelUpdate(packet);
                _logger.LogInformation("Ⓜ️ Received MasterSkillLevelUpdate: Success={Success}, Skill={SkillId}, Lvl={Lvl}, Val={Val}, NextVal={NextVal}, PtsLeft={Pts}",
                    update.Success, update.MasterSkillNumber, update.Level, update.DisplayValue, update.DisplayValueOfNextLevel, update.MasterLevelUpPoints);

                if (update.Success)
                {
                    _characterState.MasterLevelUpPoints = update.MasterLevelUpPoints; // Update remaining points
                    _characterState.AddOrUpdateSkill(new SkillEntryState
                    {
                        SkillId = update.MasterSkillNumber,
                        SkillLevel = update.Level,
                        DisplayValue = update.DisplayValue,
                        NextDisplayValue = update.DisplayValueOfNextLevel
                    });
                    // _networkManager.UpdateConsoleTitle(); // Update title as points changed
                    // _networkManager.UpdateSkillsDisplay(); // Odśwież widok Skills
                    // _networkManager.UpdateCharacterStateDisplay(); // Zaktualizuj też punkty master w tytule itp.
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing MasterSkillLevelUpdate (F3, 52)."); }
            return Task.CompletedTask;
        }
    }
}