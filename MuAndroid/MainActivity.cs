using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using Environment = Android.OS.Environment;

namespace MuAndroid
{
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@drawable/icon",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.Landscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
    public class MainActivity : AndroidGameActivity
    {
        private Client.Main.MuGame _game;
        private View _view;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Sets a flag to prevent the screen from dimming while app is running
            Window.AddFlags(WindowManagerFlags.KeepScreenOn);

            // Global exception handling - saves crash log to the Downloads folder
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Exception ex = (Exception)e.ExceptionObject;
                string errorMessage = $"Global Exception Handler:\n{ex.Message}\n{ex.StackTrace}";
                Android.Util.Log.Error("MuAndroidCrash", errorMessage);
                string downloadsPath = Environment.GetExternalStoragePublicDirectory(Environment.DirectoryDownloads).AbsolutePath;
                string filePath = Path.Combine(downloadsPath, "MuAndroid_crash_log.txt");
                try
                {
                    File.AppendAllText(filePath, errorMessage + "\n");
                    Android.Util.Log.Debug("MuAndroidCrash", $"Crash log saved to: {filePath}");
                }
                catch (Exception ioEx)
                {
                    Android.Util.Log.Error("MuAndroidCrash", $"Error writing crash log to file: {ioEx.Message}");
                }
            };

            _game = new Client.Main.MuGame();

            // Make sure that the Data folder exists (path set in Constants.DataPath)
            if (!Directory.Exists(Client.Main.Constants.DataPath))
            {
                Directory.CreateDirectory(Client.Main.Constants.DataPath);
            }

            _view = _game.Services.GetService(typeof(View)) as View;
            SetContentView(_view);
            _game.Run();
        }
    }
}