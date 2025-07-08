using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.LANG
{
    public abstract class BaseLangReader<T>
    {

        public async Task<T> Load(ZipFile zFile, string path)
        {
            var zEntry = zFile.GetEntry(path);
            if (zEntry == null)
            {
                throw new Exception($"Entry {path} not found");
            }

            using var ms = new MemoryStream();
            await zFile.GetInputStream(zEntry).CopyToAsync(ms);
            return Read(ms.ToArray());
        }

        protected abstract T Read(byte[] buffer);
    }
}
