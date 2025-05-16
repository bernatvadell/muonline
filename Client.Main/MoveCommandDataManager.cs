using Client.Main.Models;
using System.Collections.Generic;
using System.Linq;

namespace Client.Main
{
    public class MoveCommandDataManager
    {
        private static MoveCommandDataManager _instance;
        public static MoveCommandDataManager Instance => _instance ??= new MoveCommandDataManager();

        private List<MoveCommandInfo> _moveCommands;

        private MoveCommandDataManager()
        {
            LoadData();
        }

        private void LoadData()
        {
            _moveCommands = new List<MoveCommandInfo>
            {
                new MoveCommandInfo { Index = 2, ServerMapName = "Lorencia", DisplayName = "Lorencia", RequiredLevel = 10, RequiredZen = 2000 },
                new MoveCommandInfo { Index = 3, ServerMapName = "Noria", DisplayName = "Noria", RequiredLevel = 10, RequiredZen = 2000 },
                new MoveCommandInfo { Index = 4, ServerMapName = "Devias", DisplayName = "Devias", RequiredLevel = 20, RequiredZen = 2000 }, // Zgodnie z S6
                new MoveCommandInfo { Index = 5, ServerMapName = "Devias2", DisplayName = "Devias 2", RequiredLevel = 20, RequiredZen = 2500 },
                new MoveCommandInfo { Index = 6, ServerMapName = "Devias3", DisplayName = "Devias 3", RequiredLevel = 20, RequiredZen = 3000 },
                new MoveCommandInfo { Index = 7, ServerMapName = "Devias4", DisplayName = "Devias 4", RequiredLevel = 20, RequiredZen = 3500 },
                new MoveCommandInfo { Index = 8, ServerMapName = "Dungeon", DisplayName = "Dungeon", RequiredLevel = 30, RequiredZen = 3000 },
                new MoveCommandInfo { Index = 9, ServerMapName = "Dungeon2", DisplayName = "Dungeon 2", RequiredLevel = 40, RequiredZen = 3500 },
                new MoveCommandInfo { Index = 10, ServerMapName = "Dungeon3", DisplayName = "Dungeon 3", RequiredLevel = 50, RequiredZen = 4000 },
                new MoveCommandInfo { Index = 11, ServerMapName = "Atlans", DisplayName = "Atlans", RequiredLevel = 70, RequiredZen = 4000 },
                new MoveCommandInfo { Index = 12, ServerMapName = "Atlans2", DisplayName = "Atlans 2", RequiredLevel = 80, RequiredZen = 4500 },
                new MoveCommandInfo { Index = 13, ServerMapName = "Atlans3", DisplayName = "Atlans 3", RequiredLevel = 90, RequiredZen = 5000 },
                new MoveCommandInfo { Index = 14, ServerMapName = "LostTower", DisplayName = "LostTower", RequiredLevel = 50, RequiredZen = 5000 },
                new MoveCommandInfo { Index = 15, ServerMapName = "LostTower2", DisplayName = "LostTower 2", RequiredLevel = 50, RequiredZen = 5500 },
                new MoveCommandInfo { Index = 16, ServerMapName = "LostTower3", DisplayName = "LostTower 3", RequiredLevel = 50, RequiredZen = 6000 },
                new MoveCommandInfo { Index = 17, ServerMapName = "LostTower4", DisplayName = "LostTower 4", RequiredLevel = 60, RequiredZen = 6500 },
                new MoveCommandInfo { Index = 18, ServerMapName = "LostTower5", DisplayName = "LostTower 5", RequiredLevel = 60, RequiredZen = 7000 },
                new MoveCommandInfo { Index = 19, ServerMapName = "LostTower6", DisplayName = "LostTower 6", RequiredLevel = 70, RequiredZen = 7500 },
                new MoveCommandInfo { Index = 20, ServerMapName = "LostTower7", DisplayName = "LostTower 7", RequiredLevel = 70, RequiredZen = 8000 },
                new MoveCommandInfo { Index = 21, ServerMapName = "Tarkan", DisplayName = "Tarkan", RequiredLevel = 140, RequiredZen = 8000 },
                new MoveCommandInfo { Index = 22, ServerMapName = "Tarkan2", DisplayName = "Tarkan 2", RequiredLevel = 140, RequiredZen = 8500 },
                new MoveCommandInfo { Index = 23, ServerMapName = "Icarus", DisplayName = "Icarus", RequiredLevel = 170, RequiredZen = 10000, IsEventMap = true }, //TODO: special requirements (wings)
                new MoveCommandInfo { Index = 1, ServerMapName = "Arena", DisplayName = "Arena", RequiredLevel = 50, RequiredZen = 2000, IsEventMap = true },
                // (VersionSeasonSix/Gates.cs)
                // Aida, Kanturu, Elveland, etc.
                new MoveCommandInfo { Index = 25, ServerMapName = "Aida1", DisplayName = "Aida", RequiredLevel = 150, RequiredZen = 8500 }, // Aida1
                new MoveCommandInfo { Index = 27, ServerMapName = "Aida2", DisplayName = "Aida 2", RequiredLevel = 150, RequiredZen = 8500 },// Aida2

                new MoveCommandInfo { Index = 31, ServerMapName = "Elveland", DisplayName = "Elbeland", RequiredLevel = 10, RequiredZen = 2000 },
                new MoveCommandInfo { Index = 32, ServerMapName = "Elveland2", DisplayName = "Elbeland 2", RequiredLevel = 10, RequiredZen = 2500 },
                new MoveCommandInfo { Index = 43, ServerMapName = "Elveland3", DisplayName = "Elbeland 3", RequiredLevel = 10, RequiredZen = 3000 },
            };

            _moveCommands = _moveCommands.OrderBy(m => m.RequiredLevel).ThenBy(m => m.DisplayName).ToList();
        }

        public List<MoveCommandInfo> GetMoveCommandDataList()
        {
            // W przyszłości można tu dodać logikę odświeżania danych
            return _moveCommands.ToList(); // Zwróć kopię, aby uniknąć modyfikacji oryginalnej listy
        }
    }
}