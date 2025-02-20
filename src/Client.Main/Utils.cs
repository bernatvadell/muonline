using System;
using System.IO;

namespace Client.Main
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
    }
}