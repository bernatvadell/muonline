using System.Windows.Forms;
using Client.Main;
using Microsoft.Extensions.DependencyInjection;

#if DEBUG
Constants.DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
#endif

Application.SetHighDpiMode(HighDpiMode.SystemAware);

var bootstrap = new GameBootstrap();

var serviceProvider = bootstrap.Build();

using var game = serviceProvider.GetRequiredService<MuGame>();

game.Run();
