#if DEBUG
using Client.Main;

Constants.DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";

#endif

using var game = new MuGame();
game.Run();
