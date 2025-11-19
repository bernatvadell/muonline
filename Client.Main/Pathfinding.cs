using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Client.Main
{
    public class PathNode
    {
        public Vector2 Position { get; private set; }
        public PathNode Parent { get; set; }
        public float GCost { get; set; } // Cost from the start
        public float HCost { get; set; } // Heuristic to the goal
        public float FCost => GCost + HCost;

        public PathNode(Vector2 position)
        {
            Reset(position);
        }

        public void Reset(Vector2 position)
        {
            Position = position;
            GCost = float.MaxValue;
            HCost = 0;
            Parent = null;
        }
    }

    public static class Pathfinding
    {
        // Reused direction offsets to avoid per-call array allocations in GetNeighbors
        private static readonly Vector2[] Directions = new[]
        {
            new Vector2(0, -1),  // North
            new Vector2(0, 1),   // South
            new Vector2(-1, 0),  // West
            new Vector2(1, 0),   // East
            new Vector2(-1, -1), // Northwest
            new Vector2(1, -1),  // Northeast
            new Vector2(-1, 1),  // Southwest
            new Vector2(1, 1)    // Southeast
        };

        // Thread-local buffers to avoid per-call allocations in A*
        private static readonly ThreadLocal<PathfindingContext> _ctx = new(() => new PathfindingContext());

        public static List<Vector2> FindPath(Vector2 start, Vector2 goal, WorldControl world)
        {
            var ctx = _ctx.Value;
            ctx.Clear();

            PathNode startNode = ctx.GetOrCreateNode(start);
            PathNode goalNode = ctx.GetOrCreateNode(goal);

            startNode.GCost = 0;
            startNode.HCost = Heuristic(start, goal);
            ctx.OpenSet.Enqueue(startNode, startNode.FCost);

            while (ctx.OpenSet.Count > 0)
            {
                PathNode currentNode = ctx.OpenSet.Dequeue();

                if (currentNode.Position == goalNode.Position)
                {
                    var path = RetracePath(startNode, currentNode);
                    ctx.ReleaseNodes();
                    return path;
                }

                if (!ctx.ClosedSet.Add(currentNode.Position))
                {
                    continue; // Node has already been processed
                }

                foreach (Vector2 neighborPos in GetNeighbors(currentNode.Position, world))
                {
                    if (ctx.ClosedSet.Contains(neighborPos))
                        continue;

                    PathNode neighborNode = ctx.GetOrCreateNode(neighborPos);
                    float tentativeGCost = currentNode.GCost + Distance(currentNode.Position, neighborPos);

                    if (tentativeGCost < neighborNode.GCost)
                    {
                        neighborNode.GCost = tentativeGCost;
                        neighborNode.HCost = Heuristic(neighborPos, goal);
                        neighborNode.Parent = currentNode;

                        ctx.OpenSet.Enqueue(neighborNode, neighborNode.FCost);
                    }
                }
            }

            ctx.ReleaseNodes();
            return null; // Path not found
        }

        private static float Heuristic(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(a, b);
        }

        private static float Distance(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(a, b);
        }

        private static IEnumerable<Vector2> GetNeighbors(Vector2 position, WorldControl world)
        {
            foreach (var direction in Directions)
            {
                Vector2 neighbor = position + direction;
                if (IsWithinMapBounds(neighbor, world) && world.IsWalkable(neighbor))
                {
                    yield return neighbor;
                }
            }
        }

        private static bool IsWithinMapBounds(Vector2 position, WorldControl world)
        {
            return position.X >= 0 && position.X < Constants.TERRAIN_SIZE &&
                   position.Y >= 0 && position.Y < Constants.TERRAIN_SIZE;
        }

        private static List<Vector2> RetracePath(PathNode startNode, PathNode endNode)
        {
            List<Vector2> path = new();
            PathNode currentNode = endNode;

            while (currentNode != null && currentNode != startNode)
            {
                path.Add(currentNode.Position);
                currentNode = currentNode.Parent;
            }

            path.Reverse(); // Reverse the path to go from start to goal
            return path;
        }

        /// <summary>
        /// Builds a simple straight-line path ignoring obstacles. Used as a
        /// fallback when A* fails but we still need visible movement.
        /// </summary>
        public static List<Vector2> BuildDirectPath(Vector2 start, Vector2 goal)
        {
            List<Vector2> path = new();
            Vector2 current = start;
            while (current != goal)
            {
                if (current.X != goal.X)
                    current.X += (float)Math.Sign(goal.X - current.X);
                if (current.Y != goal.Y)
                    current.Y += (float)Math.Sign(goal.Y - current.Y);
                path.Add(current);
            }

            return path;
        }

        private sealed class PathfindingContext
        {
            public PriorityQueue<PathNode, float> OpenSet { get; } = new();
            public Dictionary<Vector2, PathNode> AllNodes { get; } = new();
            public HashSet<Vector2> ClosedSet { get; } = new();
            private readonly Stack<PathNode> _nodePool = new();

            public void Clear()
            {
                OpenSet.Clear();
                ClosedSet.Clear();
                // AllNodes cleared in ReleaseNodes
            }

            public PathNode GetOrCreateNode(Vector2 position)
            {
                if (AllNodes.TryGetValue(position, out PathNode node))
                {
                    return node;
                }

                node = _nodePool.Count > 0 ? _nodePool.Pop() : new PathNode(position);
                node.Reset(position);
                AllNodes[position] = node;
                return node;
            }

            public void ReleaseNodes()
            {
                foreach (var node in AllNodes.Values)
                {
                    _nodePool.Push(node);
                }
                AllNodes.Clear();
            }
        }
    }
}
