using Client.Data.ATT;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main
{
    public class PathNode
    {
        public Vector2 Position { get; }
        public PathNode Parent { get; set; }
        public float GCost { get; set; } // Costo desde el nodo de inicio
        public float HCost { get; set; } // Estimación de costo hacia el objetivo (heurística)
        public float FCost => GCost + HCost; // Costo total

        public PathNode(Vector2 position)
        {
            Position = position;
            GCost = 0;
            HCost = 0;
        }
    }

    public static class Pathfinding
    {
        public static List<Vector2> FindPath(Vector2 start, Vector2 goal, WorldControl world)
        {
            List<PathNode> openList = new List<PathNode>();
            HashSet<PathNode> closedList = new HashSet<PathNode>();

            PathNode startNode = new PathNode(start);
            PathNode goalNode = new PathNode(goal);

            openList.Add(startNode);

            while (openList.Count > 0)
            {
                // Encuentra el nodo con el menor FCost
                PathNode currentNode = openList.OrderBy(n => n.FCost).First();

                // Si hemos alcanzado el nodo objetivo
                if (currentNode.Position == goalNode.Position)
                {
                    return RetracePath(startNode, currentNode);
                }

                openList.Remove(currentNode);
                closedList.Add(currentNode);

                // Evalúa nodos vecinos
                foreach (Vector2 neighborPosition in GetNeighbors(currentNode.Position, world))
                {
                    if (closedList.Any(n => n.Position == neighborPosition)) continue;

                    PathNode neighborNode = new PathNode(neighborPosition);
                    float newMovementCost = currentNode.GCost + Vector2.Distance(currentNode.Position, neighborNode.Position);

                    if (newMovementCost < neighborNode.GCost || !openList.Contains(neighborNode))
                    {
                        neighborNode.GCost = newMovementCost;
                        neighborNode.HCost = Vector2.Distance(neighborNode.Position, goalNode.Position);
                        neighborNode.Parent = currentNode;

                        if (!openList.Contains(neighborNode))
                            openList.Add(neighborNode);
                    }
                }
            }

            return null;
        }

        private static IEnumerable<Vector2> GetNeighbors(Vector2 position, WorldControl world)
        {
            List<Vector2> neighbors = new List<Vector2>();

            Vector2[] directions = new []
            {
                new Vector2(0, -1), // Norte
                new Vector2(0, 1),  // Sur
                new Vector2(-1, 0), // Oeste
                new Vector2(1, 0),  // Este
                new Vector2(-1, -1), // Noroeste
                new Vector2(1, -1),  // Noreste
                new Vector2(-1, 1),  // Suroeste
                new Vector2(1, 1)   // Sureste
            };

            foreach (Vector2 direction in directions)
            {
                Vector2 neighborPosition = position + direction;

                if (IsWithinMapBounds(neighborPosition, world) && world.IsWalkable(neighborPosition))
                {
                    neighbors.Add(neighborPosition);
                }
            }

            return neighbors;
        }

        private static bool IsWithinMapBounds(Vector2 position, WorldControl world)
        {
            return position.X >= 0 && position.X < Constants.TERRAIN_SIZE && position.Y >= 0 && position.Y < Constants.TERRAIN_SIZE;
        }
        private static List<Vector2> RetracePath(PathNode startNode, PathNode endNode)
        {
            List<Vector2> path = new List<Vector2>();
            PathNode currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode.Position);
                currentNode = currentNode.Parent;
            }

            path.Reverse(); // La ruta se construye hacia atrás, así que la invertimos
            return path;
        }

    }
}
