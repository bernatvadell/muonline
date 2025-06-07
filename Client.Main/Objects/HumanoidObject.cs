using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    /// <summary>
    /// Abstract base class for character-like objects composed of multiple body parts,
    /// such as players and certain humanoid NPCs. This class manages the loading
    /// and animation linkage of individual equipment parts.
    /// </summary>
    public abstract class HumanoidObject : WalkerObject
    {
        protected ILogger _logger;

        public PlayerMaskHelmObject HelmMask { get; private set; }
        public PlayerHelmObject Helm { get; private set; }
        public PlayerArmorObject Armor { get; private set; }
        public PlayerPantObject Pants { get; private set; }
        public PlayerGloveObject Gloves { get; private set; }
        public PlayerBootObject Boots { get; private set; }

        protected HumanoidObject()
        {
            _logger = AppLoggerFactory?.CreateLogger(GetType());

            // Initialize body part objects and link their animations to this parent object
            HelmMask = new PlayerMaskHelmObject { LinkParentAnimation = true, Hidden = true };
            Helm = new PlayerHelmObject { LinkParentAnimation = true };
            Armor = new PlayerArmorObject { LinkParentAnimation = true };
            Pants = new PlayerPantObject { LinkParentAnimation = true };
            Gloves = new PlayerGloveObject { LinkParentAnimation = true };
            Boots = new PlayerBootObject { LinkParentAnimation = true };

            Children.Add(HelmMask);
            Children.Add(Helm);
            Children.Add(Armor);
            Children.Add(Pants);
            Children.Add(Gloves);
            Children.Add(Boots);
        }

        /// <summary>
        /// Loads the models for all body parts based on a specified path prefix, part prefixes, and a file suffix.
        /// Example: ("Npc/", "FemaleHead", "FemaleUpper", ..., 2) -> "Data/Npc/FemaleHead02.bmd"
        /// </summary>
        protected async Task SetBodyPartsAsync(
            string pathPrefix, string helmPrefix, string armorPrefix, string pantPrefix,
            string glovePrefix, string bootPrefix, int skinIndex)
        {
            // Format skin index to two digits (e.g., 1 -> "01", 10 -> "10")
            string fileSuffix = skinIndex.ToString("D2");

            var tasks = new List<Task>
            {
                LoadPartAsync(Helm, $"{pathPrefix}{helmPrefix}{fileSuffix}.bmd"),
                LoadPartAsync(Armor, $"{pathPrefix}{armorPrefix}{fileSuffix}.bmd"),
                LoadPartAsync(Pants, $"{pathPrefix}{pantPrefix}{fileSuffix}.bmd"),
                LoadPartAsync(Gloves, $"{pathPrefix}{glovePrefix}{fileSuffix}.bmd"),
                LoadPartAsync(Boots, $"{pathPrefix}{bootPrefix}{fileSuffix}.bmd")
            };

            await Task.WhenAll(tasks);
        }

        private async Task LoadPartAsync(ModelObject part, string modelPath)
        {
            if (part != null)
            {
                part.Model = await BMDLoader.Instance.Prepare(modelPath);
                if (part.Model == null)
                {
                    _logger?.LogWarning("Failed to load model for {PartType} at path: {Path}", part.GetType().Name, modelPath);
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            bool wasMoving = IsMoving;
            base.Update(gameTime);

            if (wasMoving && !IsMoving && !IsOneShotPlaying)
            {
                // Humanoid NPCs use PlayerAction enums.
                if (CurrentAction == (int) PlayerAction.WalkMale || CurrentAction == (int) PlayerAction.WalkFemale)
                {
                    PlayAction((ushort) PlayerAction.StopMale);
                }
            }
         }
    }
}
