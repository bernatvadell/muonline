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
    public class PlayerHelmObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/HelmClass03.bmd");
            await base.Load();
        }
    }

    public class PlayerMaskHelmObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/MaskHelmMale01.bmd");
            await base.Load();
        }
    }


    public class PlayerArmorObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/ArmorClass03.bmd");
            await base.Load();
        }
    }

    public class PlayerPantObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/PantClass03.bmd");
            await base.Load();
        }
    }

    public class PlayerGloveObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/GloveClass03.bmd");
            await base.Load();
        }
    }

    public class PlayerBootObject : ModelObject
    {
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
        private PlayerMaskHelmObject _helmMask;
        private PlayerHelmObject _helm;
        private PlayerArmorObject _armor;
        private PlayerPantObject _pant;
        private PlayerGloveObject _glove;
        private PlayerBootObject _boot;
        private WingObject _wing;

        public PlayerObject()
        {
            BoundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 120));
            Children.Add(_armor = new PlayerArmorObject() { LinkParent = true });
            Children.Add(_helmMask = new PlayerMaskHelmObject() { LinkParent = true });
            Children.Add(_helm = new PlayerHelmObject() { LinkParent = true });
            Children.Add(_pant = new PlayerPantObject() { LinkParent = true });
            Children.Add(_glove = new PlayerGloveObject() { LinkParent = true });
            Children.Add(_boot = new PlayerBootObject() { LinkParent = true });
            Children.Add(_wing = new WingObject() { Position = new Vector3(0, 5, 140) });
            CurrentAction = 4;
            Scale = 0.85f;

            _helmMask.Hidden = true;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
