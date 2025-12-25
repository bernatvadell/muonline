#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Main.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Registry for skill visual effects. Auto-discovers effect classes decorated with
    /// <see cref="SkillVisualEffectAttribute"/> at startup.
    /// </summary>
    public static class SkillVisualEffectRegistry
    {
        private static readonly Dictionary<ushort, ISkillVisualEffect> _effects = new();
        private static readonly ILogger? _logger;
        private static bool _initialized;

        static SkillVisualEffectRegistry()
        {
            _logger = MuGame.AppLoggerFactory?.CreateLogger("SkillVisualEffectRegistry");
        }

        /// <summary>
        /// Initializes the registry by discovering all effect classes.
        /// Call this once during game startup.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            DiscoverEffects();
        }

        /// <summary>
        /// Tries to spawn a visual effect for the given skill.
        /// </summary>
        /// <param name="skillId">The skill ID.</param>
        /// <param name="context">The effect context with caster, target, and world.</param>
        /// <param name="effect">The spawned effect, or null if no effect registered.</param>
        /// <returns>True if an effect was spawned, false otherwise.</returns>
        public static bool TrySpawn(ushort skillId, SkillEffectContext context, out WorldObject? effect)
        {
            if (!_initialized)
                Initialize();

            if (_effects.TryGetValue(skillId, out var factory))
            {
                try
                {
                    effect = factory.CreateEffect(context);
                    if (effect != null)
                    {
                        _logger?.LogDebug("Spawned visual effect for skill {SkillId}: {EffectType}",
                            skillId, effect.GetType().Name);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error creating visual effect for skill {SkillId}", skillId);
                }
            }

            effect = null;
            return false;
        }

        /// <summary>
        /// Checks if a visual effect is registered for the given skill.
        /// </summary>
        public static bool HasEffect(ushort skillId)
        {
            if (!_initialized)
                Initialize();

            return _effects.ContainsKey(skillId);
        }

        /// <summary>
        /// Gets all registered skill IDs with visual effects.
        /// </summary>
        public static IEnumerable<ushort> GetRegisteredSkillIds()
        {
            if (!_initialized)
                Initialize();

            return _effects.Keys;
        }

        private static void DiscoverEffects()
        {
            var assembly = Assembly.GetExecutingAssembly();
            int registered = 0;

            var effectTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => typeof(ISkillVisualEffect).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttributes<SkillVisualEffectAttribute>().Any());

            foreach (var type in effectTypes)
            {
                try
                {
                    var instance = (ISkillVisualEffect)Activator.CreateInstance(type)!;
                    var attributes = type.GetCustomAttributes<SkillVisualEffectAttribute>();

                    foreach (var attr in attributes)
                    {
                        if (_effects.TryAdd(attr.SkillId, instance))
                        {
                            registered++;
                            _logger?.LogTrace("Registered skill effect: {SkillId} => {Type}",
                                attr.SkillId, type.Name);
                        }
                        else
                        {
                            _logger?.LogWarning(
                                "Duplicate skill effect for ID {SkillId}: {Type} (already registered)",
                                attr.SkillId, type.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to instantiate skill effect: {Type}", type.Name);
                }
            }

            _logger?.LogInformation("Registered {Count} skill visual effects.", registered);
        }
    }
}
