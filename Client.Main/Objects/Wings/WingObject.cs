using Client.Main.Content;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Client.Main.Models;
namespace Client.Main.Objects.Wings
{
    public class CustomEffect
    {
        public int BoneID { get; set; }
        public EffectType EffectID { get; set; }
        public Vector3 Angle { get; set; }
        public Vector3 Position { get; set; }
        public float Scale { get; set; }
        public Vector3 Color { get; set; }
        public SpriteObject Effect { get; set; }
    }

    public class WingObject : ModelObject
    {
        public List<CustomEffect> _effects { get; set; } = new List<CustomEffect>();

        private short _type;
        public new short Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    _ = OnChangeType();
                }
            }
        }

        public WingObject()
        {
            RenderShadow = true;
            IsTransparent = true;
            AffectedByTransparency = false;
            BlendState = BlendState.AlphaBlend;
            BlendMesh = -1;
            BlendMeshState = BlendState.Additive;
            Alpha = 1f;
            LinkParentAnimation = false;
            ParentBoneLink = 47;
        }

        private async Task OnChangeType()
        {
            if (Type <= 0)
            {
                Model = null;
                return;
            }

            string modelPath = Path.Combine("Item", $"Wing{Type:D2}.bmd");

            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model == null)
            {
                modelPath = Path.Combine("Item", $"Wing{Type}.bmd");
                Model = await BMDLoader.Instance.Prepare(modelPath);
            }

            if (Model == null)
            {
                Status = GameControlStatus.Error;
            }
            else if (Status == GameControlStatus.Error)
            {
                Status = GameControlStatus.Ready;
            }
        }

        public override async Task Load()
        {
            if (_effects.Count > 0)
            {
                foreach (var effect in _effects)
                {
                    effect.Effect = Utils.GetEffectByCode(effect.EffectID);
                    effect.Effect.Light = effect.Color;
                    effect.Effect.Scale = effect.Scale;
                    Children.Add(effect.Effect);
                }
            }
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            if (_effects.Count > 0 && BoneTransform != null)
            {
                foreach (var effect in _effects)
                {
                    effect.Effect.Position = effect.Position + BoneTransform[effect.BoneID].Translation;
                    effect.Effect.Angle = effect.Angle + Angle;
                }
            }
            base.Update(gameTime);
        }
    }
}
