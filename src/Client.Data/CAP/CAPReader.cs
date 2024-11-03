using Delizious.Ini;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Client.Data.CAP
{
    public class CAPReader : BaseReader<CameraAnglePosition>
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        
        private static readonly SectionName CameraAngleSection = SectionName.Create("CAMERA ANGLE");
        private static readonly SectionName HeroPositionSection = SectionName.Create("HERO POSITION");
        private static readonly SectionName CameraPositionSection = SectionName.Create("CAMERA POSITION");
        private static readonly SectionName CameraDistanceSection = SectionName.Create("CAMERA DISTANCE");
        private static readonly SectionName CameraRatioSection = SectionName.Create("CAMERA RATIO");
        private static readonly SectionName CameraFOVSection = SectionName.Create("CAMERA FOV");

        private static readonly PropertyKey AngleXKey = PropertyKey.Create("Angle X");
        private static readonly PropertyKey AngleYKey = PropertyKey.Create("Angle Y");
        private static readonly PropertyKey AngleZKey = PropertyKey.Create("Angle Z");
        private static readonly PropertyKey PositionXKey = PropertyKey.Create("Position X");
        private static readonly PropertyKey PositionYKey = PropertyKey.Create("Position Y");
        private static readonly PropertyKey PositionZKey = PropertyKey.Create("Position Z");
        private static readonly PropertyKey DistanceKey = PropertyKey.Create("Distance");
        private static readonly PropertyKey ZDistanceKey = PropertyKey.Create("Z_Distance");
        private static readonly PropertyKey RatioKey = PropertyKey.Create("Ratio");
        private static readonly PropertyKey FOVKey = PropertyKey.Create("FOV");

        protected override CameraAnglePosition Read(byte[] buffer)
        {
            try
            {
                buffer = (buffer.Length > 4 && buffer[0] == 'M' && buffer[1] == 'A' && buffer[2] == 'P' && buffer[3] == 1)
                    ? ModulusCryptor.ModulusCryptor.Decrypt(buffer.AsSpan(4).ToArray())
                    : FileCryptor.Decrypt(buffer);

                var content = Encoding.UTF8.GetString(buffer);
                
                using var reader = new StringReader(content);
                var document = IniDocument.LoadFrom(reader, IniDocumentConfiguration.Default);

                float GetFloat(SectionName section, PropertyKey key)
                {
                    var property = document.ReadProperty(section, key);
                    if (property == null)
                    {
                        throw new InvalidDataException($"Property '{key}' not found in section '{section}'.");
                    }

                    if (float.TryParse(property.ToString(), InvariantCulture, out float result))
                    {
                        return result;
                    }
                    else
                    {
                        throw new FormatException($"Property '{key}' in section '{section}' is not a valid float.");
                    }
                }

                return new CameraAnglePosition
                {
                    CameraAngle = new Vector3(
                        GetFloat(CameraAngleSection, AngleXKey),
                        GetFloat(CameraAngleSection, AngleYKey),
                        GetFloat(CameraAngleSection, AngleZKey)
                    ),
                    HeroPosition = new Vector3(
                        GetFloat(HeroPositionSection, PositionXKey),
                        GetFloat(HeroPositionSection, PositionYKey),
                        GetFloat(HeroPositionSection, PositionZKey)
                    ),
                    CameraPosition = new Vector3(
                        GetFloat(CameraPositionSection, PositionXKey),
                        GetFloat(CameraPositionSection, PositionYKey),
                        GetFloat(CameraPositionSection, PositionZKey)
                    ),
                    CameraDistance = GetFloat(CameraDistanceSection, DistanceKey),
                    CameraZDistance = GetFloat(CameraDistanceSection, ZDistanceKey),
                    CameraRatio = GetFloat(CameraRatioSection, RatioKey),
                    CameraFOV = GetFloat(CameraFOVSection, FOVKey)
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to read CameraAnglePosition data.", ex);
            }
        }
    }
}
