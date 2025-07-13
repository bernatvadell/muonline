using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main
{
    public static class Blendings
    {
        // Standard MonoGame Blend States
        public static readonly BlendState Additive = BlendState.Additive;
        public static readonly BlendState AlphaBlend = BlendState.AlphaBlend;
        public static readonly BlendState NonPremultiplied = BlendState.NonPremultiplied;
        public static readonly BlendState Opaque = BlendState.Opaque;

        // Custom Blend States
        public static readonly BlendState Negative = new()
        {
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.InverseSourceColor,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.InverseSourceColor
        };

        public static readonly BlendState DarkBlendState = new()
        {
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.InverseSourceColor,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.InverseSourceAlpha
        };

        public static readonly BlendState Alpha = new()
        {
            ColorSourceBlend = Blend.SourceAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaSourceBlend = Blend.SourceAlpha,
            AlphaDestinationBlend = Blend.InverseSourceAlpha
        };

        public static readonly BlendState ShadowBlend = new()
        {
            ColorSourceBlend = Blend.SourceAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.One,
            ColorBlendFunction = BlendFunction.Add,
            AlphaBlendFunction = BlendFunction.Max
        };

        public static readonly BlendState ColorState = new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.DestinationAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha,

            AlphaBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
        };

        public static BlendState MultiplyBlend = new()
        {
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.SourceColor,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One
        };

        public static BlendState InverseDestinationBlend = new()
        {
            ColorSourceBlend = Blend.InverseDestinationColor,
            ColorDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.One,
            BlendFactor = Color.White
        };
    }
}
