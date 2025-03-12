using Client.Main.Content;
using Client.Main.Objects.Effects;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public WingObject()
        {
            RenderShadow = true;
            BlendMesh = -1;
            BlendState = BlendState.AlphaBlend;
            BlendMeshState = BlendState.Additive;
            Alpha = 1f;
            ParentBoneLink = 47; // link with bone 47 (see MuMain source -> file zzzCharacter -> line: 14628)
            // Position = new Vector3(0, 5, 140);
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
            if (_effects.Count > 0)
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
