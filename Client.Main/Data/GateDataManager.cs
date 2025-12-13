using Client.Main.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Client.Main.Data
{
    public class GateDataManager
    {
        private static GateDataManager _instance;
        public static GateDataManager Instance => _instance ??= new GateDataManager();

        private List<GateInfo> _gates;

        private GateDataManager()
        {
            LoadData();
        }

        private void LoadData()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Client.Main.Data.gates.json";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    _gates = new List<GateInfo>();
                    return;
                }
                using (StreamReader reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    _gates = JsonSerializer.Deserialize<List<GateInfo>>(json);
                }
            }

            // Ensure that exit gates link back to their entrances so players can
            // return through a warp in both directions.
            CreateReverseGateLinks();
        }

        /// <summary>
        /// Finds the gate with the specified identifier.
        /// </summary>
        public GateInfo GetGateById(int id)
        {
            return _gates.FirstOrDefault(g => g.Id == id);
        }

        /// <summary>
        /// Updates exit gates so that they lead back to their entrance gates.
        /// Many gate definitions only point from an entrance to an exit. This
        /// method scans for such pairs and assigns the reverse target on the
        /// exit gate if it is missing.
        /// </summary>
        private void CreateReverseGateLinks()
        {
            foreach (var entrance in _gates.Where(g => g.Flag == 1 && g.Target > 0))
            {
                var exitGate = GetGateById(entrance.Target);
                if (exitGate != null && exitGate.Target == 0)
                {
                    exitGate.Target = entrance.Id;
                }
            }
        }

        public GateInfo GetGate(int mapId, int x, int y)
        {
            return _gates.FirstOrDefault(g =>
                g.Map == mapId &&
                x >= g.X1 && x <= g.X2 &&
                y >= g.Y1 && y <= g.Y2);
        }
    }
}
