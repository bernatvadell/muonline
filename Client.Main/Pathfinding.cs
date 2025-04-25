using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Client.Main
{
    public class PathNode
    {
        public Vector2 Position { get; }
        public PathNode Parent { get; set; }
        public float GCost { get; set; } // Cost from the start
        public float HCost { get; set; } // Heuristic to the goal
        public float FCost => GCost + HCost;

        public PathNode(Vector2 position)
        {
            Position = position;
            GCost = float.MaxValue;
            HCost = 0;
        }
    }

    public static class Pathfinding
    {
        public static List<Vector2> FindPath(Vector2 start, Vector2 goal, WorldControl world)
        {
            var openSet = new PriorityQueue<PathNode, float>();
            var allNodes = new Dictionary<Vector2, PathNode>();
            var closedSet = new HashSet<Vector2>();

            PathNode startNode = GetOrCreateNode(start, allNodes);
            PathNode goalNode = GetOrCreateNode(goal, allNodes);

            startNode.GCost = 0;
            startNode.HCost = Heuristic(start, goal);
            openSet.Enqueue(startNode, startNode.FCost);

            while (openSet.Count > 0)
            {
                PathNode currentNode = openSet.Dequeue();

                if (currentNode.Position == goalNode.Position)
                {
                    return RetracePath(startNode, currentNode);
                }

                if (!closedSet.Add(currentNode.Position))
                {
                    continue; // Node has already been processed
                }

                foreach (Vector2 neighborPos in GetNeighbors(currentNode.Position, world))
                {
                    if (closedSet.Contains(neighborPos))
                        continue;

                    PathNode neighborNode = GetOrCreateNode(neighborPos, allNodes);
                    float tentativeGCost = currentNode.GCost + Distance(currentNode.Position, neighborPos);

                    if (tentativeGCost < neighborNode.GCost)
                    {
                        neighborNode.GCost = tentativeGCost;
                        neighborNode.HCost = Heuristic(neighborPos, goal);
                        neighborNode.Parent = currentNode;

                        openSet.Enqueue(neighborNode, neighborNode.FCost);
                    }
                }
            }

            return null; // Path not found
        }

        private static PathNode GetOrCreateNode(Vector2 position, Dictionary<Vector2, PathNode> allNodes)
        {
            if (!allNodes.TryGetValue(position, out PathNode node))
            {
                node = new PathNode(position);
                allNodes[position] = node;
            }
            return node;
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
            Vector2[] directions = new[]
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

            foreach (var direction in directions)
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
    }
}
