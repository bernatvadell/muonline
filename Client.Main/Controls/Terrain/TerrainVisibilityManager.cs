using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Client.Main.Controls.Terrain
{
    public class TerrainBlock
    {
        public BoundingBox Bounds;
        public float MinZ, MaxZ;
        public int LODLevel;
        public Vector2 Center;
        public bool IsVisible;
        public int Xi, Yi;
        
        // Hierarchical culling data
        public bool[] TileVisibility = new bool[16]; // 4x4 tiles per block
        public int VisibleTileCount;
        public bool FullyVisible; // All tiles in block are visible (skip individual tile tests)
    }

    /// <summary>
    /// Manages terrain culling, LOD selection, and determines visible blocks.
    /// </summary>
    public class TerrainVisibilityManager
    {
        private const int BlockSize = 4;
        private const int MaxLodLevels = 2;
        private const float LodDistanceMultiplier = 3000f;
        private const float CameraMoveThreshold = 32f;

        private readonly TerrainData _data;
        private readonly TerrainBlockCache _blockCache;
        private readonly Queue<TerrainBlock> _visibleBlocks = new(64);
        private Vector2 _lastCameraPosition;
        private readonly int[] _lodSteps = { 1, 4 };

        public IReadOnlyCollection<TerrainBlock> VisibleBlocks => _visibleBlocks;
        public int[] LodSteps => _lodSteps;

        public TerrainVisibilityManager(TerrainData data)
        {
            _data = data;
            _blockCache = new TerrainBlockCache(BlockSize, Constants.TERRAIN_SIZE);
            PrecomputeBlockHeights();
        }

        private void PrecomputeBlockHeights()
        {
            if (_data.HeightMap == null) return;
            int blocksPerSide = Constants.TERRAIN_SIZE / BlockSize;

            for (int by = 0; by < blocksPerSide; by++)
            {
                for (int bx = 0; bx < blocksPerSide; bx++)
                {
                    var block = _blockCache.GetBlock(bx, by);
                    block.MinZ = float.MaxValue;
                    block.MaxZ = float.MinValue;

                    for (int y = 0; y < BlockSize; y++)
                    {
                        for (int x = 0; x < BlockSize; x++)
                        {
                            int idx = GetTerrainIndexRepeat(block.Xi + x, block.Yi + y);
                            float h = _data.HeightMap[idx].B * 1.5f;
                            if (h < block.MinZ) block.MinZ = h;
                            if (h > block.MaxZ) block.MaxZ = h;
                        }
                    }

                    float sx = block.Xi * Constants.TERRAIN_SCALE;
                    float sy = block.Yi * Constants.TERRAIN_SCALE;
                    float ex = (block.Xi + BlockSize) * Constants.TERRAIN_SCALE;
                    float ey = (block.Yi + BlockSize) * Constants.TERRAIN_SCALE;

                    block.Bounds = new BoundingBox(
                        new Vector3(sx, sy, block.MinZ),
                        new Vector3(ex, ey, block.MaxZ));
                }
            }
        }

        public void Update(Vector2 cameraPosition)
        {
            const float thrSq = CameraMoveThreshold * CameraMoveThreshold;
            if (Vector2.DistanceSquared(_lastCameraPosition, cameraPosition) < thrSq)
                return;

            _lastCameraPosition = cameraPosition;
            _visibleBlocks.Clear();

            float renderDist = Camera.Instance.ViewFar * 1.7f;
            float renderDistSq = renderDist * renderDist;
            int cellWorld = (int)(BlockSize * Constants.TERRAIN_SCALE);

            const int Extra = 4;
            int tilesPerAxis = Constants.TERRAIN_SIZE / BlockSize;

            int startX = Math.Max(0, (int)((cameraPosition.X - renderDist) / cellWorld) - Extra);
            int startY = Math.Max(0, (int)((cameraPosition.Y - renderDist) / cellWorld) - Extra);
            int endX = Math.Min(tilesPerAxis - 1, (int)((cameraPosition.X + renderDist) / cellWorld) + Extra);
            int endY = Math.Min(tilesPerAxis - 1, (int)((cameraPosition.Y + renderDist) / cellWorld) + Extra);

            var frustum = Camera.Instance.Frustum;
            var visible = new List<TerrainBlock>((endX - startX + 1) * (endY - startY + 1));

            for (int gy = startY; gy <= endY; gy++)
            {
                for (int gx = startX; gx <= endX; gx++)
                {
                    var block = _blockCache.GetBlock(gx, gy);
                    block.Center = new Vector2(
                        (block.Xi + BlockSize * 0.5f) * Constants.TERRAIN_SCALE,
                        (block.Yi + BlockSize * 0.5f) * Constants.TERRAIN_SCALE);

                    float distSq = Vector2.DistanceSquared(block.Center, cameraPosition);
                    if (distSq > renderDistSq)
                    {
                        block.IsVisible = false;
                        continue;
                    }

                    block.LODLevel = GetLodLevel(MathF.Sqrt(distSq));
                    
                    var containment = frustum.Contains(block.Bounds);
                    block.IsVisible = containment != ContainmentType.Disjoint;
                    
                    if (block.IsVisible)
                    {
                        // Hierarchical culling: check individual tiles within the block
                        if (containment == ContainmentType.Contains)
                        {
                            // Block is fully inside frustum - all tiles are visible
                            block.FullyVisible = true;
                            block.VisibleTileCount = 16;
                            for (int i = 0; i < 16; i++)
                                block.TileVisibility[i] = true;
                        }
                        else
                        {
                            // Block is partially inside frustum - test individual tiles
                            block.FullyVisible = false;
                            PerformTileCulling(block, frustum);
                        }
                        
                        visible.Add(block);
                    }
                }
            }

            foreach (var block in visible)
                _visibleBlocks.Enqueue(block);
        }

        private int GetLodLevel(float distance)
        {
            float f = distance / LodDistanceMultiplier;
            int l = (int)Math.Floor(f);
            float blend = f - l;
            l = (int)MathHelper.Lerp(l, l + 1, blend);
            return Math.Min(l, MaxLodLevels - 1);
        }

        private void PerformTileCulling(TerrainBlock block, BoundingFrustum frustum)
        {
            block.VisibleTileCount = 0;
            
            // For partially visible blocks, be more conservative - assume all tiles are visible
            // This prevents black holes on frustum edges at minimal performance cost
            for (int tileY = 0; tileY < BlockSize; tileY++)
            {
                for (int tileX = 0; tileX < BlockSize; tileX++)
                {
                    int tileIdx = tileY * BlockSize + tileX;
                    
                    // Conservative approach: if block is partially visible, render all its tiles
                    // This eliminates black holes on edges with minimal performance impact
                    block.TileVisibility[tileIdx] = true;
                    block.VisibleTileCount++;
                }
            }
        }

        private static int GetTerrainIndexRepeat(int x, int y)
            => ((y & Constants.TERRAIN_SIZE_MASK) * Constants.TERRAIN_SIZE)
             + (x & Constants.TERRAIN_SIZE_MASK);

        private class TerrainBlockCache
        {
            private readonly TerrainBlock[,] _blocks;
            private readonly int _gridSize;

            public TerrainBlockCache(int blockSize, int terrainSize)
            {
                _gridSize = terrainSize / blockSize;
                _blocks = new TerrainBlock[_gridSize, _gridSize];

                for (int y = 0; y < _gridSize; y++)
                    for (int x = 0; x < _gridSize; x++)
                        _blocks[y, x] = new TerrainBlock { Xi = x * blockSize, Yi = y * blockSize };
            }

            public TerrainBlock GetBlock(int x, int y) => _blocks[y, x];
        }
    }
}
