using System.Collections.Generic;

namespace Client.Main.Controls.UI
{
    /// <summary>
    /// Provides a collection of texture asset paths that should be preloaded
    /// before the control is displayed to the user.
    /// </summary>
    public interface IUiTexturePreloadable
    {
        /// <summary>
        /// Gets the texture asset paths that should be preloaded for the control.
        /// Duplicate or null entries may be returned; callers are expected to
        /// handle filtering if needed.
        /// </summary>
        IEnumerable<string> GetPreloadTexturePaths();
    }
}
