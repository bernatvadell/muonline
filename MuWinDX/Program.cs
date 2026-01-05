using System.Windows.Forms;
using Client.Main;

#if DEBUG
Constants.DataPath = @"C:\Users\Usuario\Documents\Mu Mono and Open mu project\MU_Red_1_20_61_Full\Data";
#endif

Application.SetHighDpiMode(HighDpiMode.SystemAware);

using var game = new MuGame();
game.Run();
