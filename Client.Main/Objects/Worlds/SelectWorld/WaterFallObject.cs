using Client.Data.BMD;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.SelectWrold
{
    public class WaterFallObject : ModelObject
    {
        private const float TEXTURE_SCROLL_SPEED = 0.5f;
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
            base.Update(gameTime);

            if (Model?.Meshes == null || Model.Meshes.Length == 0 || _originalTexCoords == null)
                return;

            _accumulatedTime += gameTime.ElapsedGameTime.TotalSeconds;

            float totalOffset = (float)(_accumulatedTime * TEXTURE_SCROLL_SPEED);

            for (int meshIndex = 0; meshIndex < Model.Meshes.Length; meshIndex++)
            {
                var mesh = Model.Meshes[meshIndex];
                if (mesh.TexCoords == null || _originalTexCoords[meshIndex] == null)
                    continue;

                int texCoordCount = Math.Min(mesh.TexCoords.Length, _originalTexCoords[meshIndex].Length);

                for (int i = 0; i < texCoordCount; i++)
                {
                    var originalCoord = _originalTexCoords[meshIndex][i];
                    var newCoord = originalCoord;

                    newCoord.V = originalCoord.V - totalOffset;

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
