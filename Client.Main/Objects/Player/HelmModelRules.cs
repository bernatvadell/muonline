using Client.Data.BMD;
using System;
using System.Collections.Generic;
using System.IO;

namespace Client.Main.Objects.Player
{
    internal static class HelmModelRules
    {
        // Helms where the shell mesh is index 1 (and therefore base head must stay visible).
        private static readonly HashSet<string> BaseHeadHelmNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "HelmMale01",
            "HelmMale03",
            "HelmElf01",
            "HelmElf02",
            "HelmElf03",
            "HelmElf04"
        };

        // Explicit overrides for shell mesh indices. Value = mesh index for the helmet shell.
        private static readonly Dictionary<string, int> ShellMeshIndexOverrides = new(StringComparer.OrdinalIgnoreCase)
        {
            // Classic mesh-1 shells (no face mesh inside)
            ["HelmMale01"] = 1,
            ["HelmMale03"] = 1,
            ["HelmElf01"] = 1,
            ["HelmElf02"] = 1,
            ["HelmElf03"] = 1,
            ["HelmElf04"] = 1,

            // Helm with face on mesh 0, shell on mesh 1
            ["HelmMale25"] = 1,
        };

        /// <summary>
        /// Returns true if this helm should keep the base head visible (face lives outside the helm).
        /// </summary>
        public static bool RequiresBaseHead(string helmPath, BMD model)
        {
            var candidate = GetModelNameCandidate(helmPath, model);
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            return IsBaseHeadHelm(candidate);
        }

        /// <summary>
        /// Returns the mesh index where the helmet shell lives for item material application.
        /// </summary>
        public static int GetHelmetShellMeshIndex(string modelName)
        {
            var candidate = GetFileNameWithoutExtension(modelName);

            if (ShellMeshIndexOverrides.TryGetValue(candidate, out var index))
                return index;

            if (candidate.StartsWith("new_helm", StringComparison.OrdinalIgnoreCase))
                return 1;

            // Default shell on mesh 0.
            return 0;
        }

        private static bool IsBaseHeadHelm(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            if (BaseHeadHelmNames.Contains(candidate))
                return true;

            // Lucky Item helms: new_helmXX under LuckyItem/<id>/
            if (candidate.StartsWith("new_helm", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string GetModelNameCandidate(string helmPath, BMD model)
        {
            var candidate = GetFileNameWithoutExtension(helmPath);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;

            if (model == null)
                return string.Empty;

            var name = model.Name ?? string.Empty;
            candidate = GetFileNameWithoutExtension(name);
            return string.IsNullOrWhiteSpace(candidate) ? name : candidate;
        }

        private static string GetFileNameWithoutExtension(string pathOrName)
        {
            if (string.IsNullOrWhiteSpace(pathOrName))
                return string.Empty;

            var fileName = Path.GetFileNameWithoutExtension(pathOrName);
            return string.IsNullOrWhiteSpace(fileName) ? pathOrName : fileName;
        }
    }
}
