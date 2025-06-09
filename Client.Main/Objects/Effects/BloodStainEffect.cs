using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Simple sprite effect representing blood on the ground.
    /// </summary>
    public class BloodStainEffect : SpriteObject
    {
        private readonly string _texturePath;

        /// <summary>
        /// Path to the blood texture.
        /// </summary>
        public override string TexturePath => _texturePath;

        public BloodStainEffect()
        {
            string[] textures =
            {
                "Effect/blood.tga",
                "Effect/blood01.tga"
            };

            _texturePath = textures[Random.Shared.Next(textures.Length)];

            BlendState = BlendState.AlphaBlend;
            LightEnabled = false;
        }
    }
}