using System;
using System.IO;
using Client.Main.Objects;
using Client.Main.Objects.Effects;

namespace Client.Main.Core.Utilities
{
    public static class Utils
    {
        public static string GetActualPath(string path)
        {
            if (File.Exists(path))
                return path;
            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
            return path;
        }
        public static SpriteObject GetEffectByCode(EffectType e)
        {
            switch (e)
            {
                case EffectType.Light:
                    return new LightEffect();
                case EffectType.TargetPosition1:
                    return new TargetPosition1();
                default:
                    throw new Exception("Effect code now exists");
            }
        }

    }
}