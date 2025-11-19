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

        // Explicit VertexDeclaration to ensure correct layout on DirectX
        private static VertexDeclaration _explicitVertexDeclaration;

        private static VertexDeclaration GetExplicitVertexDeclaration()
        {
            if (_explicitVertexDeclaration == null)
            {
                _explicitVertexDeclaration = new VertexDeclaration(
                    new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),           // 12 bytes
                    new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),               // 4 bytes
                    new VertexElement(16, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),            // 12 bytes
                    new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0) // 8 bytes
                );                                                                                                // Total: 36 bytes
            }
            return _explicitVertexDeclaration;
        }

        public static void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            ClearPools();
        }

        public static DynamicVertexBuffer RentVertexBuffer(int requiredVertexCount)
        {
            if (_graphicsDevice == null || requiredVertexCount <= 0)
                return null;

            // TEMPORARILY DISABLED: Pooling causes race conditions in DirectX
            // where GPU is still using a buffer while CPU writes new data to it
            // TODO: Implement proper GPU fence synchronization for DirectX
            /*
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
            */

            return new DynamicVertexBuffer(
                _graphicsDevice,
                GetExplicitVertexDeclaration(),
                requiredVertexCount,
                BufferUsage.WriteOnly);
        }

        public static DynamicIndexBuffer RentIndexBuffer(int requiredIndexCount, bool prefer16Bit)
        {
            if (_graphicsDevice == null || requiredIndexCount <= 0)
                return null;

            // TEMPORARILY DISABLED: Pooling causes race conditions in DirectX
            /*
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
            */

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

            // TEMPORARILY DISABLED: Pooling causes race conditions in DirectX
            // Just dispose the buffer immediately to avoid GPU/CPU synchronization issues
            buffer.Dispose();

            /*
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
            */
        }

        public static void ReturnIndexBuffer(DynamicIndexBuffer buffer)
        {
            if (buffer == null || buffer.IsDisposed)
                return;

            // TEMPORARILY DISABLED: Pooling causes race conditions in DirectX
            // Just dispose the buffer immediately to avoid GPU/CPU synchronization issues
            buffer.Dispose();

            /*
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
            */
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
