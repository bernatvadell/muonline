using System;
using Foundation;
using UIKit;

namespace MuIos
{
    [Register("AppDelegate")]
    class Program : UIApplicationDelegate
    {
        private static Client.Main.MuGame game;

        internal static void RunGame()
        {
            game = new Client.Main.MuGame();
            game.Run();
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(Program));
        }

        public override void FinishedLaunching(UIApplication app)
        {
            RunGame();
        }
    }
}
