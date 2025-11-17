#if ANDROID
using Android.App;
using Android.Content;
using Android.Views.InputMethods;

namespace Client.Main.Platform.Android
{
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
        /// Shows the soft keyboard.
        /// </summary>
        public static void Show()
        {
            if (Activity == null)
            {
                return;
            }

            var imm = Activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
            var view = Activity.CurrentFocus ?? Activity.Window?.DecorView;
            if (imm != null && view != null)
            {
                imm.ShowSoftInput(view, ShowFlags.Forced);
            }
        }

        /// <summary>
        /// Hides the soft keyboard.
        /// </summary>
        public static void Hide()
        {
            if (Activity == null) return;
            var imm = Activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
            var view = Activity.CurrentFocus ?? Activity.Window.DecorView;
            imm?.HideSoftInputFromWindow(view.WindowToken, HideSoftInputFlags.None);
        }
    }
}
#endif
