using Client.Data.BMD;
using Client.Main.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Client.Main.Data
{
    public class GateTeleport
    {
        public ushort Index { get; set; }
        public BMDGate Source { get; set; }
        public BMDGate Target { get; set; }
    }
    public class GateDataManager
    {
        private static GateDataManager _instance;
        public static GateDataManager Instance => _instance ??= new GateDataManager();

        private BMDGate[] _gates;

        private Dictionary<byte, BMDGate[]> _gatesByMap = [];

        public async Task LoadData()
        {
            var reader = new BMDGateReader();
            var gatesPath = Path.Combine(Constants.DataPath, "Gate.bmd");
            _gates = await reader.Load(gatesPath);

            // Group gates by map for faster access
            _gatesByMap = _gates
                .GroupBy(g => g.Map)
                .ToDictionary(g => g.Key, g => g.ToArray());
        }

        public GateTeleport GetTeleport(byte mapId, byte x, byte y)
        {
            var gatesInMap = _gatesByMap.GetValueOrDefault(mapId);

            var sourceGate = gatesInMap.FirstOrDefault(g => g.Flag == 1 && x >= g.X1 && x <= g.X2 && y >= g.Y1 && y <= g.Y2);

            if (sourceGate.Flag == 0)
                return null;

            var targetGate = _gates[sourceGate.Target];

            var index = _gates.IndexOf(sourceGate);

            return new GateTeleport
            {
                Index = (ushort)index,
                Source = sourceGate,
                Target = targetGate
            };
        }
    }
}
