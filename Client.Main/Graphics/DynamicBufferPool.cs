using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Client.Main.Graphics
{
    /// <summary>
    /// Lightweight pool for dynamic vertex and index buffers that hides GPU allocation latency.
    /// Buffers are bucketed by capacity (rounded up) and reused on demand.
    /// </summary>
    public static class DynamicBufferPool
    {
        private const int MaxBuffersPerBucket = 8;

        private static readonly SortedDictionary<int, Stack<DynamicVertexBuffer>> _vertexPools = new();
        private static readonly SortedDictionary<int, Stack<DynamicIndexBuffer>> _index16Pools = new();
        private static readonly SortedDictionary<int, Stack<DynamicIndexBuffer>> _index32Pools = new();

        private static readonly object _vertexLock = new();
        private static readonly object _index16Lock = new();
        private static readonly object _index32Lock = new();

        private static GraphicsDevice _graphicsDevice;

        public static void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            ClearPools();
        }

        public static DynamicVertexBuffer RentVertexBuffer(int requiredVertexCount)
        {
            if (_graphicsDevice == null || requiredVertexCount <= 0)
                return null;

            lock (_vertexLock)
            {
                foreach (var kvp in _vertexPools)
                {
                    if (kvp.Key < requiredVertexCount)
                        continue;

                    var stack = kvp.Value;
                    if (stack.Count == 0)
                        continue;

                    var buffer = stack.Pop();
                    if (buffer != null && !buffer.IsDisposed)
                        return buffer;
                }
            }

            return new DynamicVertexBuffer(
                _graphicsDevice,
                VertexPositionColorNormalTexture.VertexDeclaration,
                requiredVertexCount,
                BufferUsage.WriteOnly);
        }

        public static DynamicIndexBuffer RentIndexBuffer(int requiredIndexCount, bool prefer16Bit)
        {
            if (_graphicsDevice == null || requiredIndexCount <= 0)
                return null;

            var targetPools = prefer16Bit ? _index16Pools : _index32Pools;
            var targetLock = prefer16Bit ? _index16Lock : _index32Lock;

            lock (targetLock)
            {
                foreach (var kvp in targetPools)
                {
                    if (kvp.Key < requiredIndexCount)
                        continue;

                    var stack = kvp.Value;
                    if (stack.Count == 0)
                        continue;

                    var buffer = stack.Pop();
                    if (buffer != null && !buffer.IsDisposed)
                        return buffer;
                }
            }

            return new DynamicIndexBuffer(
                _graphicsDevice,
                prefer16Bit ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits,
                requiredIndexCount,
                BufferUsage.WriteOnly);
        }

        public static void ReturnVertexBuffer(DynamicVertexBuffer buffer)
        {
            if (buffer == null || buffer.IsDisposed)
                return;

            lock (_vertexLock)
            {
                if (!_vertexPools.TryGetValue(buffer.VertexCount, out var stack))
                {
                    stack = new Stack<DynamicVertexBuffer>();
                    _vertexPools[buffer.VertexCount] = stack;
                }

                if (stack.Count >= MaxBuffersPerBucket)
                {
                    buffer.Dispose();
                    return;
                }

                stack.Push(buffer);
            }
        }

        public static void ReturnIndexBuffer(DynamicIndexBuffer buffer)
        {
            if (buffer == null || buffer.IsDisposed)
                return;

            var pools = buffer.IndexElementSize == IndexElementSize.SixteenBits ? _index16Pools : _index32Pools;
            var targetLock = buffer.IndexElementSize == IndexElementSize.SixteenBits ? _index16Lock : _index32Lock;

            lock (targetLock)
            {
                if (!pools.TryGetValue(buffer.IndexCount, out var stack))
                {
                    stack = new Stack<DynamicIndexBuffer>();
                    pools[buffer.IndexCount] = stack;
                }

                if (stack.Count >= MaxBuffersPerBucket)
                {
                    buffer.Dispose();
                    return;
                }

                stack.Push(buffer);
            }
        }

        private static void ClearPools()
        {
            lock (_vertexLock)
            {
                foreach (var stack in _vertexPools.Values)
                {
                    while (stack.Count > 0)
                        stack.Pop().Dispose();
                }
                _vertexPools.Clear();
            }

            ClearIndexPool(_index16Pools, _index16Lock);
            ClearIndexPool(_index32Pools, _index32Lock);
        }

        private static void ClearIndexPool(SortedDictionary<int, Stack<DynamicIndexBuffer>> pools, object poolLock)
        {
            lock (poolLock)
            {
                foreach (var stack in pools.Values)
                {
                    while (stack.Count > 0)
                        stack.Pop().Dispose();
                }
                pools.Clear();
            }
        }
    }
}
