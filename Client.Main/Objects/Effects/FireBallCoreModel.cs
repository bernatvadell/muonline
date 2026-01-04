#nullable enable
using System.Threading.Tasks;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Core fireball model from original MU client (Skill/Fire.bmd).
    /// </summary>
    public sealed class FireBallCoreModel : ModelObject
    {
        public FireBallCoreModel()
        {
            ContinuousAnimation = true;
            AnimationSpeed = 7f;
            BlendMesh = 1;
            BlendMeshState = BlendState.Additive;
            BlendMeshLight = 0.9f;
            LightEnabled = true;
            Light = new Vector3(1f, 0.25f, 0.08f);
            IsTransparent = true;
            DepthState = DepthStencilState.DepthRead;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Skill/Fire.bmd");
            await base.Load();
        }
    }
}
