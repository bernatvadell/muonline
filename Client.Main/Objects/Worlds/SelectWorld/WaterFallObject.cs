using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.SelectWrold
{
    public class WaterFallObject : ModelObject
    {
        private const float TEXTURE_SCROLL_SPEED = 0.3f;
        private double _accumulatedTime = 0.0;
        private BMDTexCoord[][] _originalTexCoords;
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object94/Object{idx}.bmd");
            await base.Load();

            if (Model?.Meshes != null && Model.Meshes.Length > 0)
            {
                _originalTexCoords = new BMDTexCoord[Model.Meshes.Length][];

                for (int meshIndex = 0; meshIndex < Model.Meshes.Length; meshIndex++)
                {
                    var mesh = Model.Meshes[meshIndex];
                    if (mesh.TexCoords != null && mesh.TexCoords.Length > 0)
                    {
                        _originalTexCoords[meshIndex] = new BMDTexCoord[mesh.TexCoords.Length];
                        Array.Copy(mesh.TexCoords, _originalTexCoords[meshIndex], mesh.TexCoords.Length);
                    }
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            //TODO fix texture animation
            base.Update(gameTime);

            if (Model?.Meshes == null || Model.Meshes.Length == 0 || _originalTexCoords == null)
                return;

            _accumulatedTime += gameTime.ElapsedGameTime.TotalSeconds;

            float currentOffset = (float)(_accumulatedTime * TEXTURE_SCROLL_SPEED);
            currentOffset -= (float)Math.Floor(currentOffset); 

            for (int meshIndex = 0; meshIndex < Model.Meshes.Length; meshIndex++)
            {
                var mesh = Model.Meshes[meshIndex];
                if (mesh.TexCoords == null || _originalTexCoords[meshIndex] == null)
                    continue;

                for (int i = 0; i < mesh.TexCoords.Length; i++)
                {
                    if (i >= _originalTexCoords[meshIndex].Length)
                        continue;

                    var originalCoord = _originalTexCoords[meshIndex][i];
                    var newCoord = originalCoord;

                    float newV = originalCoord.V - currentOffset;

                    if (newV < 0)
                        newV += 1.0f;

                    newCoord.V = newV;
                    mesh.TexCoords[i] = newCoord;
                }
            }

            InvalidateBuffers();
        }

        public override void Draw(GameTime gameTime)
        {
            var prevSamplerState = GraphicsDevice.SamplerStates[0];
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            base.Draw(gameTime);

            GraphicsDevice.SamplerStates[0] = prevSamplerState;
        }
    }
}
