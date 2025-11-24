using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Client.Main.Graphics
{
    /// <summary>
    /// Lightweight pool for dynamic vertex and index buffers that hides GPU allocation latency.
    /// Buffers are bucketed by capacity (rounded up) and reused on demand.
    /// </summary>
    public static class DynamicBufferPool
    {
        private const int MaxBuffersPerBucket = 8;

        // DX needs extra breathing room to avoid reusing buffers the GPU still references.
#if WINDOWS_DX
        private const int MinFramesBeforeReuse = 4;   // more conservative reuse window for DX
#else
        private const int MinFramesBeforeReuse = 2;   // avoid same-frame reuse elsewhere too
#endif
        // Increase idle window to avoid churn on iGPU: ~60s at 60 FPS
        private const int MaxIdleFrames = 3600;        // drop long-idle buffers to cap VRAM usage

        private static readonly SortedDictionary<int, Queue<PoolEntry<DynamicVertexBuffer>>> _vertexPools = new();
        private static readonly SortedDictionary<int, Queue<PoolEntry<DynamicIndexBuffer>>> _index16Pools = new();
        private static readonly SortedDictionary<int, Queue<PoolEntry<DynamicIndexBuffer>>> _index32Pools = new();

        private static readonly object _vertexLock = new();
        private static readonly object _index16Lock = new();
        private static readonly object _index32Lock = new();

        private static GraphicsDevice _graphicsDevice;
        private static int _frameId;

        // Explicit VertexDeclaration to ensure correct layout on DirectX
        private static VertexDeclaration _explicitVertexDeclaration;

        private readonly struct PoolEntry<TBuffer> where TBuffer : GraphicsResource
        {
            public PoolEntry(TBuffer buffer, int frameId)
            {
                Buffer = buffer;
                LastUsedFrame = frameId;
            }

            public TBuffer Buffer { get; }
            public int LastUsedFrame { get; }
        }

        private static int CurrentFrameId => Volatile.Read(ref _frameId);

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
            if (_graphicsDevice != null)
            {
                _graphicsDevice.DeviceResetting -= OnDeviceResetting;
                _graphicsDevice.DeviceReset -= OnDeviceReset;
                _graphicsDevice.DeviceLost -= OnDeviceLost;
            }

            _graphicsDevice = graphicsDevice;
            Volatile.Write(ref _frameId, 0);

            if (_graphicsDevice != null)
            {
                _graphicsDevice.DeviceResetting += OnDeviceResetting;
                _graphicsDevice.DeviceReset += OnDeviceReset;
                _graphicsDevice.DeviceLost += OnDeviceLost;
            }

            ClearPools();
        }

        /// <summary>
        /// Toggle pooling at runtime; disabling flushes existing pooled buffers.
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            Constants.ENABLE_DYNAMIC_BUFFER_POOL = enabled;
            if (!enabled)
                ClearPools();
        }

        /// <summary>
        /// Call once per frame (from the render thread) so pooling can enforce a safe reuse delay.
        /// </summary>
        public static void BeginFrame(int frameIndex)
        {
            if (!Constants.ENABLE_DYNAMIC_BUFFER_POOL)
                return;

            if (frameIndex < 0)
                frameIndex = 0;

            int current = Volatile.Read(ref _frameId);
            if (frameIndex <= current)
                frameIndex = current + 1; // guarantee monotonic progression

            Volatile.Write(ref _frameId, frameIndex);
            PruneStaleBuffers();
        }

        public static DynamicVertexBuffer RentVertexBuffer(int requiredVertexCount)
        {
            if (_graphicsDevice == null || requiredVertexCount <= 0)
                return null;

            if (!Constants.ENABLE_DYNAMIC_BUFFER_POOL)
            {
                return new DynamicVertexBuffer(
                    _graphicsDevice,
                    GetExplicitVertexDeclaration(),
                    requiredVertexCount,
                    BufferUsage.WriteOnly);
            }

            var pooled = RentFromPool(_vertexPools, _vertexLock, requiredVertexCount);
            if (pooled != null)
                return pooled;

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

            if (!Constants.ENABLE_DYNAMIC_BUFFER_POOL)
            {
                return new DynamicIndexBuffer(
                    _graphicsDevice,
                    prefer16Bit ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits,
                    requiredIndexCount,
                    BufferUsage.WriteOnly);
            }

            var targetPools = prefer16Bit ? _index16Pools : _index32Pools;
            var targetLock = prefer16Bit ? _index16Lock : _index32Lock;

            var pooled = RentFromPool(targetPools, targetLock, requiredIndexCount);
            if (pooled != null)
                return pooled;

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

            if (!Constants.ENABLE_DYNAMIC_BUFFER_POOL)
            {
                buffer.Dispose();
                return;
            }

            if (buffer.GraphicsDevice != _graphicsDevice)
            {
                buffer.Dispose();
                return;
            }

            ReturnToPool(_vertexPools, _vertexLock, buffer.VertexCount, buffer);
        }

        public static void ReturnIndexBuffer(DynamicIndexBuffer buffer)
        {
            if (buffer == null || buffer.IsDisposed)
                return;

            if (!Constants.ENABLE_DYNAMIC_BUFFER_POOL)
            {
                buffer.Dispose();
                return;
            }

            if (buffer.GraphicsDevice != _graphicsDevice)
            {
                buffer.Dispose();
                return;
            }

            var pools = buffer.IndexElementSize == IndexElementSize.SixteenBits ? _index16Pools : _index32Pools;
            var targetLock = buffer.IndexElementSize == IndexElementSize.SixteenBits ? _index16Lock : _index32Lock;

            ReturnToPool(pools, targetLock, buffer.IndexCount, buffer);
        }

        private static void ClearPools()
        {
            ClearPool(_vertexPools, _vertexLock);
            ClearPool(_index16Pools, _index16Lock);
            ClearPool(_index32Pools, _index32Lock);
        }

        private static void OnDeviceResetting(object sender, EventArgs e) => ClearPools();
        private static void OnDeviceReset(object sender, EventArgs e) => ClearPools();
        private static void OnDeviceLost(object sender, EventArgs e) => ClearPools();

        private static void ClearPool<TBuffer>(SortedDictionary<int, Queue<PoolEntry<TBuffer>>> pools, object poolLock)
            where TBuffer : GraphicsResource
        {
            lock (poolLock)
            {
                foreach (var queue in pools.Values)
                {
                    while (queue.Count > 0)
                    {
                        var entry = queue.Dequeue();
                        entry.Buffer?.Dispose();
                    }
                }
                pools.Clear();
            }
        }

        private static TBuffer RentFromPool<TBuffer>(SortedDictionary<int, Queue<PoolEntry<TBuffer>>> pools, object poolLock, int requiredCount)
            where TBuffer : GraphicsResource
        {
            int currentFrame = CurrentFrameId;

            lock (poolLock)
            {
                if (!pools.TryGetValue(requiredCount, out var queue) || queue.Count == 0)
                    return null;

                while (queue.Count > 0)
                {
                    var entry = queue.Peek();
                    if (entry.Buffer == null || entry.Buffer.IsDisposed || entry.Buffer.GraphicsDevice != _graphicsDevice)
                    {
                        queue.Dequeue();
                        entry.Buffer?.Dispose();
                        continue;
                    }

                    if (!IsReusable(currentFrame, entry.LastUsedFrame))
                        return null; // oldest entry is still hot; keep it for later

                    queue.Dequeue();
                    return entry.Buffer;
                }
            }

            return null;
        }

        private static void ReturnToPool<TBuffer>(SortedDictionary<int, Queue<PoolEntry<TBuffer>>> pools,
                                                  object poolLock,
                                                  int bucketSize,
                                                  TBuffer buffer)
            where TBuffer : GraphicsResource
        {
            int currentFrame = CurrentFrameId;

            lock (poolLock)
            {
                if (!pools.TryGetValue(bucketSize, out var queue))
                {
                    queue = new Queue<PoolEntry<TBuffer>>();
                    pools[bucketSize] = queue;
                }

                if (queue.Count >= MaxBuffersPerBucket)
                {
                    buffer.Dispose();
                    return;
                }

                queue.Enqueue(new PoolEntry<TBuffer>(buffer, currentFrame));
            }
        }

        private static void PruneStaleBuffers()
        {
            PrunePool(_vertexPools, _vertexLock);
            PrunePool(_index16Pools, _index16Lock);
            PrunePool(_index32Pools, _index32Lock);
        }

        private static void PrunePool<TBuffer>(SortedDictionary<int, Queue<PoolEntry<TBuffer>>> pools, object poolLock)
            where TBuffer : GraphicsResource
        {
            int currentFrame = CurrentFrameId;
            List<int> emptyBuckets = null;
            const int MaxDisposePerFrame = 2;
            int disposedCount = 0;

            lock (poolLock)
            {
                foreach (var kvp in pools)
                {
                    var queue = kvp.Value;
                    int count = queue.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var entry = queue.Dequeue();
                        bool drop = false;

                        if (entry.Buffer == null || entry.Buffer.IsDisposed || entry.Buffer.GraphicsDevice != _graphicsDevice)
                        {
                            entry.Buffer?.Dispose();
                            drop = true;
                        }
                        else if (disposedCount < MaxDisposePerFrame && ShouldTrim(currentFrame, entry.LastUsedFrame))
                        {
                            entry.Buffer.Dispose();
                            disposedCount++;
                            drop = true;
                        }

                        if (!drop)
                            queue.Enqueue(entry);

                        if (disposedCount >= MaxDisposePerFrame)
                            break;
                    }

                    if (queue.Count == 0)
                    {
                        emptyBuckets ??= new List<int>();
                        emptyBuckets.Add(kvp.Key);
                    }

                    if (disposedCount >= MaxDisposePerFrame)
                        break;
                }

                if (emptyBuckets != null)
                {
                    foreach (var key in emptyBuckets)
                        pools.Remove(key);
                }
            }
        }

        private static bool IsReusable(int currentFrame, int lastUsedFrame)
        {
            int age = unchecked(currentFrame - lastUsedFrame);
            return age >= MinFramesBeforeReuse || age < 0;
        }

        private static bool ShouldTrim(int currentFrame, int lastUsedFrame)
        {
            int age = unchecked(currentFrame - lastUsedFrame);
            return age < 0 || age > MaxIdleFrames;
        }
    }
}
