#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Ghosted weapon model used by Twisting Slash to mimic the original wheel weapon shadow.
    /// </summary>
    public sealed class TwistingSlashWeaponShadow : ModelObject
    {
        public TwistingSlashWeaponShadow()
        {
            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.AlphaBlend;
            BlendMeshState = BlendState.AlphaBlend;
            BlendMesh = -2;
            BlendMeshLight = 0.35f;
            Alpha = 0.35f;
            LightEnabled = false;
            Light = new Vector3(0.35f, 0.3f, 0.25f);
            RenderShadow = false;
            UseSunLight = false;
            DepthState = DepthStencilState.DepthRead;
        }
    }
}
