using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Microsoft.Extensions.Logging;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneUiPreloadController
    {
        private readonly GameScene _scene;
        private readonly ILogger _logger;

        public GameSceneUiPreloadController(GameScene scene, ILogger logger)
        {
            _scene = scene;
            _logger = logger;
        }

        public Task StartPreloadAsync()
        {
            return PreloadCommonUIAssetsAsync();
        }

        /// <summary>
        /// Pre-loads common UI textures in background to prevent freezes when opening windows.
        /// This runs async with low priority to avoid impacting gameplay FPS.
        /// </summary>
        private async Task PreloadCommonUIAssetsAsync()
        {
            try
            {
                _logger?.LogInformation("Starting UI asset pre-loading...");

                var texturePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var control in EnumerateUiControls())
                {
                    if (control is not IUiTexturePreloadable preloadable)
                    {
                        continue;
                    }

                    var paths = preloadable.GetPreloadTexturePaths();
                    if (paths == null)
                    {
                        continue;
                    }

                    foreach (var path in paths)
                    {
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            texturePaths.Add(path);
                        }
                    }
                }

                if (texturePaths.Count == 0)
                {
                    _logger?.LogInformation("No UI textures registered for pre-loading.");
                    return;
                }

                foreach (var assetPath in texturePaths)
                {
                    var path = assetPath;
                    MuGame.TaskScheduler.QueueTask(async () =>
                    {
                        try
                        {
                            await TextureLoader.Instance.Prepare(path);
                            _ = TextureLoader.Instance.GetTexture2D(path);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogTrace(ex, "Failed to pre-load UI asset: {Asset}", path);
                        }
                    }, Controllers.TaskScheduler.Priority.Low);

                    await Task.Delay(10);
                }

                _logger?.LogInformation("UI asset pre-loading completed. {Count} assets queued for background loading.", texturePaths.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during UI asset pre-loading (non-critical).");
            }
        }

        private IEnumerable<GameControl> EnumerateUiControls()
        {
            var rootControls = _scene.Controls?.ToArray();
            if (rootControls == null || rootControls.Length == 0)
            {
                yield break;
            }

            var stack = new Stack<GameControl>(rootControls);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;

                var children = current.Controls?.ToArray();
                if (children == null || children.Length == 0)
                {
                    continue;
                }

                for (int i = 0; i < children.Length; i++)
                {
                    stack.Push(children[i]);
                }
            }
        }
    }
}
