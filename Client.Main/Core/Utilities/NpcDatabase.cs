using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Main.Objects;

namespace Client.Main.Core.Utilities
{
    public static class NpcDatabase
    {
        private static readonly Dictionary<ushort, string> NpcNames;
        private static readonly Dictionary<ushort, Type> NpcTypes;
        public static IReadOnlyDictionary<ushort, string> Names => NpcNames;

        static NpcDatabase()
        {
            NpcNames = new Dictionary<ushort, string>();
            NpcTypes = new Dictionary<ushort, Type>();

            // Zakres skanowania: bieżące assembly
            var asm = Assembly.GetExecutingAssembly();

            foreach (var type in asm.GetTypes()
                                     .Where(t => typeof(WalkerObject).IsAssignableFrom(t)))
            {
                var attr = type.GetCustomAttribute<NpcInfoAttribute>();
                if (attr != null)
                {
                    // jeśli powtórka ID, ostatni wpis wygrywa
                    NpcNames[attr.TypeId] = attr.DisplayName;
                    NpcTypes[attr.TypeId] = type;
                }
            }
        }

        /// <summary>Zwraca nazwę NPC/Monstera po jego TypeId.</summary>
        public static string GetNpcName(ushort typeId)
            => NpcNames.TryGetValue(typeId, out var name)
               ? name
               : $"Unknown NPC/Monster ({typeId})";

        /// <summary>Próbuje zwrócić klasę (Type) skojarzoną z danym TypeId.</summary>
        public static bool TryGetNpcType(ushort typeId, out Type npcType)
            => NpcTypes.TryGetValue(typeId, out npcType);

        /// <summary>Pełna mapa typów (do iteracji lub debugowania).</summary>
        public static IReadOnlyDictionary<ushort, Type> AllNpcTypes => NpcTypes;
    }
}
