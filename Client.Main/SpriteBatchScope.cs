using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Helpers
{
    /// <summary>
    /// Manages nested SpriteBatch.Begin/End calls, preserving and restoring all parameters.
    /// </summary>
    public sealed class SpriteBatchScope : IDisposable
    {
        private readonly SpriteBatch _batch;
        private static readonly Stack<SavedState> _stack = new();

        private readonly SavedState _myState;
        private readonly DepthStencilState _prevDepth;
        private readonly RasterizerState _prevRaster;
        private readonly SamplerState _prevSampler; // NEW

        /// <summary>
        /// True if there is currently an open SpriteBatch (anywhere in the stack).
        /// </summary>
        public static bool BatchIsBegun => _stack.Count > 0;

        public SpriteBatchScope(
            SpriteBatch batch,
            SpriteSortMode sort = SpriteSortMode.Deferred,
            BlendState blend = null,
            SamplerState sampler = null,
            DepthStencilState depth = null,
            RasterizerState raster = null,
            Effect effect = null,
            Matrix? transform = null)
        {
            _batch = batch ?? throw new ArgumentNullException(nameof(batch));

            var gd = batch.GraphicsDevice;

            _prevDepth = gd.DepthStencilState;
            _prevRaster = gd.RasterizerState;
            _prevSampler = gd.SamplerStates[0];

            _myState = new SavedState(
                sort,
                blend ?? BlendState.AlphaBlend,
                sampler ?? SamplerState.PointClamp,
                depth,
                raster,
                effect,
                transform
            );

            if (_stack.Count > 0)
                _stack.Peek().End(_batch);

            _myState.Begin(_batch);
            _stack.Push(_myState);
        }

        public void Dispose()
        {
            _stack.Pop().End(_batch);

            var gd = _batch.GraphicsDevice;

            // 🔵 Przywróć wszystkie stany
            gd.DepthStencilState = _prevDepth;
            gd.RasterizerState = _prevRaster;
            gd.SamplerStates[0] = _prevSampler; // NEW

            if (_stack.Count > 0)
                _stack.Peek().Begin(_batch);
        }

        /// <summary>
        /// Holds all parameters necessary to Begin/End a SpriteBatch with the same settings.
        /// </summary>
        private readonly struct SavedState
        {
            public readonly SpriteSortMode Sort;
            public readonly BlendState Blend;
            public readonly SamplerState Sampler;
            public readonly DepthStencilState Depth;
            public readonly RasterizerState Raster;
            public readonly Effect Effect;
            public readonly Matrix? Transform;

            public SavedState(
                SpriteSortMode sort,
                BlendState blend,
                SamplerState sampler,
                DepthStencilState depth,
                RasterizerState raster,
                Effect effect,
                Matrix? transform)
            {
                Sort = sort;
                Blend = blend;
                Sampler = sampler;
                Depth = depth;
                Raster = raster;
                Effect = effect;
                Transform = transform;
            }

            public void Begin(SpriteBatch batch)
            {
                batch.Begin(
                    Sort,
                    Blend,
                    Sampler,
                    Depth,
                    Raster,
                    Effect,
                    Transform
                );
            }

            public void End(SpriteBatch batch)
            {
                batch.End();
            }
        }
    }
}
