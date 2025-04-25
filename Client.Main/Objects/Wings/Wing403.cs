using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;


namespace Client.Main.Objects.Wings
{
    public class Wing403 : WingObject
    {

        public Wing403()
        {
            _effects.Add(new CustomEffect { BoneID = 9, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 23, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 65, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 93, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 38, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 80, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 62, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 6, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 9, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 23, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 65, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 93, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 38, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 80, EffectID = Effects.EffectType.Light, Angle = Vector3.One, Position = Vector3.Zero, Color = new Vector3(1f, 0.7f, 0.2f), Scale = 2f, });
            _effects.Add(new CustomEffect { BoneID = 65, EffectID = Effects.EffectType.TargetPosition1, Angle = Vector3.One, Position = Vector3.Zero, Color = Color.White.ToVector3(), Scale = 1.0f, });
            _effects.Add(new CustomEffect { BoneID = 93, EffectID = Effects.EffectType.TargetPosition1, Angle = Vector3.One, Position = Vector3.Zero, Color = Color.White.ToVector3(), Scale = 1.0f, });
            _effects.Add(new CustomEffect { BoneID = 38, EffectID = Effects.EffectType.TargetPosition1, Angle = Vector3.One, Position = Vector3.Zero, Color = Color.White.ToVector3(), Scale = 1.0f, });
            _effects.Add(new CustomEffect { BoneID = 80, EffectID = Effects.EffectType.TargetPosition1, Angle = new Vector3(3f, 3f, 3f), Position = Vector3.Zero, Color = Color.White.ToVector3(), Scale = 1.0f, });

        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Item/Wing403.bmd");
            await base.Load();
        }
    }
}
