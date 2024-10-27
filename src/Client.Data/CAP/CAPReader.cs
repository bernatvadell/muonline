using Client.Data.MAP;
using Delizious.Ini;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.CAP
{
    public class CAPReader : BaseReader<CameraAnglePosition>
    {
        protected override CameraAnglePosition Read(byte[] buffer)
        {
            if (buffer.Length > 4 && buffer[0] == 'M' && buffer[1] == 'A' && buffer[2] == 'P' && buffer[3] == 1)
            {
                var enc = new byte[buffer.Length - 4];
                Array.Copy(buffer, 4, enc, 0, enc.Length);
                buffer = ModulusCryptor.ModulusCryptor.Decrypt(enc);
            }
            else
            {
                buffer = FileCryptor.Decrypt(buffer);
            }

            var content = Encoding.UTF8.GetString(buffer);
            using var reader = new StringReader(content);

            var document = IniDocument.LoadFrom(reader, IniDocumentConfiguration.Default);
            var culture = CultureInfo.InvariantCulture;
            var cap = new CameraAnglePosition
            {
                CameraAngle = new Vector3(
                    float.Parse(document.ReadProperty(SectionName.Create("CAMERA ANGLE"), PropertyKey.Create("Angle X")).ToString(), culture),
                    float.Parse(document.ReadProperty(SectionName.Create("CAMERA ANGLE"), PropertyKey.Create("Angle Y")).ToString(), culture),
                    float.Parse(document.ReadProperty(SectionName.Create("CAMERA ANGLE"), PropertyKey.Create("Angle Z")).ToString(), culture)
                ),
                HeroPosition = new Vector3(
                   float.Parse(document.ReadProperty(SectionName.Create("HERO POSITION"), PropertyKey.Create("Position X")).ToString(), culture),
                   float.Parse(document.ReadProperty(SectionName.Create("HERO POSITION"), PropertyKey.Create("Position Y")).ToString(), culture),
                   float.Parse(document.ReadProperty(SectionName.Create("HERO POSITION"), PropertyKey.Create("Position Z")).ToString(), culture)
                ),
                CameraPosition = new Vector3(
                   float.Parse(document.ReadProperty(SectionName.Create("CAMERA POSITION"), PropertyKey.Create("Position X")).ToString(), culture),
                   float.Parse(document.ReadProperty(SectionName.Create("CAMERA POSITION"), PropertyKey.Create("Position Y")).ToString(), culture),
                   float.Parse(document.ReadProperty(SectionName.Create("CAMERA POSITION"), PropertyKey.Create("Position Z")).ToString(), culture)
                ),
                CameraDistance = float.Parse(document.ReadProperty(SectionName.Create("CAMERA DISTANCE"), PropertyKey.Create("Distance")).ToString(), culture),
                CameraZDistance = float.Parse(document.ReadProperty(SectionName.Create("CAMERA DISTANCE"), PropertyKey.Create("Z_Distance")).ToString(), culture),
                CameraRatio = float.Parse(document.ReadProperty(SectionName.Create("CAMERA RATIO"), PropertyKey.Create("Ratio")).ToString(), culture),
                CameraFOV = float.Parse(document.ReadProperty(SectionName.Create("CAMERA FOV"), PropertyKey.Create("FOV")).ToString(), culture)
            };

            return cap;
        }
    }
}
