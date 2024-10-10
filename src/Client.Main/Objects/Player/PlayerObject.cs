using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerShadowObject : ModelObject
    {
        public PlayerShadowObject()
        {
            BlendMeshState = BlendState.AlphaBlend;
            BlendMesh = 0;
            Alpha = 0.5f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Shadow01.bmd");
            await base.Load();
        }
    }

    public class PlayerHelmObject : ModelObject
    {
        public PlayerHelmObject()
        {
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/HelmClass03.bmd");
            await base.Load();
        }
    }

    public class PlayerMaskHelmObject : ModelObject
    {
        public PlayerMaskHelmObject()
        {
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/MaskHelmMale01.bmd");
            await base.Load();
        }
    }


    public class PlayerArmorObject : ModelObject
    {
        public PlayerArmorObject()
        {
            RenderShadow = true;
        }


        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/ArmorClass03.bmd");
            await base.Load();
        }
    }

    public class PlayerPantObject : ModelObject
    {
        public PlayerPantObject()
        {
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/PantClass03.bmd");
            await base.Load();
        }
    }

    public class PlayerGloveObject : ModelObject
    {
        public PlayerGloveObject()
        {
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/GloveClass03.bmd");
            await base.Load();
        }
    }

    public class PlayerBootObject : ModelObject
    {
        public PlayerBootObject()
        {
            RenderShadow = true;
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/BootClass03.bmd");
            await base.Load();
        }
    }

    public class WingObject : ModelObject
    {
        public WingObject()
        {
            RenderShadow = true;
            BlendMesh = -1;
            BlendMeshState = BlendState.Additive;
            Alpha = 1f;
            // se vincula con el hueso 47 (ver zzzCharacter->14628)
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Item/Wing03.bmd");
            await base.Load();
        }
    }

    public class PlayerObject : ModelObject
    {
        private PlayerShadowObject _shadowObject;
        private PlayerMaskHelmObject _helmMask;
        private PlayerHelmObject _helm;
        private PlayerArmorObject _armor;
        private PlayerPantObject _pant;
        private PlayerGloveObject _glove;
        private PlayerBootObject _boot;
        private WingObject _wing;

        public PlayerObject()
        {
            var color = new Color(255, (byte)(255 * 0.1f), (byte)(255 * 0.1f));
            color = Color.White;
            BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 120));
            Children.Add(_shadowObject = new PlayerShadowObject() { LinkParent = true, Hidden = true });
            Children.Add(_armor = new PlayerArmorObject() { LinkParent = true, Color = color });
            Children.Add(_helmMask = new PlayerMaskHelmObject() { LinkParent = true, Color = color, Hidden = true });
            Children.Add(_helm = new PlayerHelmObject() { LinkParent = true, Color = color });
            Children.Add(_pant = new PlayerPantObject() { LinkParent = true, Color = color });
            Children.Add(_glove = new PlayerGloveObject() { LinkParent = true, Color = color });
            Children.Add(_boot = new PlayerBootObject() { LinkParent = true, Color = color });
            Children.Add(_wing = new WingObject() { Position = new Vector3(0, 5, 140), Color = color });
            CurrentAction = 4;
            Scale = 0.85f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            _shadowObject.Position = new Vector3(0, 0, 0);
            _shadowObject.Angle = new Vector3(60, 60, 60);
            base.Draw(gameTime);
        }
    }
}
