using Android.App;
using Android.Content;
using Android.Views.InputMethods;
using Client.Main;
using Microsoft.Xna.Framework.Input;
using System;

namespace MuAndroid
{
    /// <summary>
    /// Event args for text input on Android.
    /// </summary>
    public class TextInputEventArgs : EventArgs
    {
        public char Character { get; }
        public Keys Key { get; }

        public TextInputEventArgs(char character, Keys key)
        {
            Character = character;
            Key = key;
        }
    }

    /// <summary>
    /// Helper for showing and hiding the Android soft keyboard.
    /// </summary>
    public static class AndroidKeyboard
    {
        /// <summary>
        /// Gets or sets the activity used to access the InputMethodManager.
        /// </summary>
        public static Activity Activity { get; set; }

        /// <summary>
        /// Event fired when text is input on Android (from soft keyboard or physical keyboard).
        /// </summary>
        public static event EventHandler<TextInputEventArgs> TextInput;

        /// <summary>
        /// Shows the soft keyboard.
        /// </summary>
        public static void Show()
        {
            if (Activity == null)
            {
                return;
            }

            // Keyboard methods must be called on the Android UI thread
            Activity.RunOnUiThread(() =>
            {
                var imm = Activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
                var view = Activity.CurrentFocus ?? Activity.Window?.DecorView;

                if (imm != null && view != null)
                {
                    // FIX: Force view to accept focus in touch mode
                    // Sometimes MonoGame's SurfaceView doesn't want to accept focus in touch mode by default,
                    // which causes RequestFocus() to fail and keyboard to show/hide immediately.
                    view.Focusable = true;
                    view.FocusableInTouchMode = true;

                    // Request focus to ensure the view can accept keyboard input
                    view.RequestFocus();
                    // Use Forced flag to ensure keyboard shows even if system thinks otherwise
                    imm.ShowSoftInput(view, ShowFlags.Forced);
                }
            });
        }

        /// <summary>
        /// Hides the soft keyboard.
        /// </summary>
        public static void Hide()
        {
            if (Activity == null) return;

            // Keyboard methods must be called on the Android UI thread
            Activity.RunOnUiThread(() =>
            {
                var imm = Activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
                var view = Activity.CurrentFocus ?? Activity.Window?.DecorView;

                if (imm != null && view != null)
                {
                    imm.HideSoftInputFromWindow(view.WindowToken, HideSoftInputFlags.None);
                }
            });
        }

        /// <summary>
        /// Raises TextInput event (called from MainActivity keyboard handler).
        /// </summary>
        public static void RaiseTextInput(char character, Keys key)
        {
            var args = new TextInputEventArgs(character, key);

            // Android activity callbacks run on the UI thread; marshal to the game thread
            // before touching game/UI state to avoid races with the update/draw loop.
            if (MuGame.Instance == null)
            {
                TextInput?.Invoke(null, args);
                return;
            }

            MuGame.ScheduleOnMainThread(() => TextInput?.Invoke(null, args));
        }
    }
}
