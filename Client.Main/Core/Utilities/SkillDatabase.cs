#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Client.Data.BMD;
using Microsoft.Extensions.Logging;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Static database for skill definitions loaded from skill_eng.bmd.
    /// </summary>
    public static class SkillDatabase
    {
        /// <summary>Lookup cache: SkillId → skill definition.</summary>
        private static readonly Dictionary<int, SkillBMD> _skillDefinitions;

        private static readonly ILogger? _logger = MuGame.AppLoggerFactory?.CreateLogger("SkillDatabase");

        static SkillDatabase() => _skillDefinitions = InitializeSkillData();

        /// <summary>
        /// Loads skill_eng.bmd from an embedded resource and builds the definition table.
        /// </summary>
        private static Dictionary<int, SkillBMD> InitializeSkillData()
        {
            var data = new Dictionary<int, SkillBMD>();

            var assembly = Assembly.GetExecutingAssembly();

            // Find resource whose name ends with "skill_eng.bmd"
            var resourceName = assembly.GetManifestResourceNames()
                                       .SingleOrDefault(n =>
                                           n.EndsWith("skill_eng.bmd", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                _logger?.LogError(
                    "Embedded resource 'skill_eng.bmd' not found. " +
                    "Verify Build Action = Embedded Resource and correct RootNamespace.");
                return data;
            }

            try
            {
                using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                {
                    _logger?.LogError($"Failed to open resource stream '{resourceName}'.");
                    return data;
                }

                // Copy the resource to a temporary file and load it from disk
                Dictionary<int, SkillBMD> skills;
                var tempPath = Path.GetTempFileName();
                try
                {
                    using (var tempFs = File.OpenWrite(tempPath))
                        resourceStream.CopyTo(tempFs);

                    skills = SkillBMDReader.LoadSkills(tempPath);
                }
                finally
                {
                    try { File.Delete(tempPath); } catch { /* ignore IO errors */ }
                }

                _logger?.LogInformation($"Loaded {skills.Count} skills from skill_eng.bmd");

                return skills;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while loading 'skill_eng.bmd'");
            }

            return data;
        }

        #region Public API ------------------------------------------------------

        /// <summary>
        /// Gets skill definition by skill ID.
        /// </summary>
        public static SkillBMD? GetSkillDefinition(int skillId)
        {
            _skillDefinitions.TryGetValue(skillId, out var def);
            return def;
        }

        /// <summary>
        /// Gets skill name by skill ID.
        /// </summary>
        public static string GetSkillName(int skillId) =>
            GetSkillDefinition(skillId)?.Name ?? $"Unknown Skill {skillId}";

        /// <summary>
        /// Gets skill type (AREA/TARGET/SELF) by skill ID.
        /// </summary>
        public static SkillType GetSkillType(int skillId) =>
            SkillDefinitions.GetSkillType(skillId);

        /// <summary>
        /// Gets animation ID for skill by skill ID.
        /// Returns -1 if no specific animation.
        /// </summary>
        public static int GetSkillAnimation(int skillId) =>
            SkillDefinitions.GetSkillAnimation(skillId);

        /// <summary>
        /// Checks if skill is area type.
        /// </summary>
        public static bool IsAreaSkill(int skillId) =>
            SkillDefinitions.IsAreaSkill(skillId);

        /// <summary>
        /// Checks if skill is target type.
        /// </summary>
        public static bool IsTargetSkill(int skillId) =>
            SkillDefinitions.IsTargetSkill(skillId);

        /// <summary>
        /// Checks if skill is self-cast type.
        /// </summary>
        public static bool IsSelfSkill(int skillId) =>
            SkillDefinitions.IsSelfSkill(skillId);

        /// <summary>
        /// Gets all loaded skills.
        /// </summary>
        public static IReadOnlyDictionary<int, SkillBMD> GetAllSkills() => _skillDefinitions;

        /// <summary>
        /// Gets skill mana cost.
        /// </summary>
        public static ushort GetSkillManaCost(int skillId) =>
            GetSkillDefinition(skillId)?.ManaCost ?? 0;

        /// <summary>
        /// Gets skill AG cost.
        /// </summary>
        public static ushort GetSkillAGCost(int skillId) =>
            GetSkillDefinition(skillId)?.AbilityGaugeCost ?? 0;

        /// <summary>
        /// Gets skill range/distance.
        /// </summary>
        public static uint GetSkillRange(int skillId) =>
            GetSkillDefinition(skillId)?.Distance ?? 0;

        /// <summary>
        /// Gets skill cooldown delay in milliseconds.
        /// </summary>
        public static int GetSkillCooldown(int skillId) =>
            GetSkillDefinition(skillId)?.Delay ?? 0;

        /// <summary>
        /// Gets required level for skill.
        /// </summary>
        public static ushort GetRequiredLevel(int skillId) =>
            GetSkillDefinition(skillId)?.RequiredLevel ?? 0;

        #endregion
    }
}
