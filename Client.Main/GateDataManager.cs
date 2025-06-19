using Client.Main.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Client.Main
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
