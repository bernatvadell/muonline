using Client.Data.Texture;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main
{
    public static class XNA
    {
        public static SurfaceFormat ToXNA(this TextureSurfaceFormat fmt) => fmt switch
        {
            TextureSurfaceFormat.Color => SurfaceFormat.Color,
            TextureSurfaceFormat.Dxt1 => SurfaceFormat.Dxt1,
            TextureSurfaceFormat.Dxt3 => SurfaceFormat.Dxt3,
            TextureSurfaceFormat.Dxt5 => SurfaceFormat.Dxt5,
            _ => throw new ArgumentOutOfRangeException(nameof(fmt), fmt, "Unknown format")
        };
    }

}
