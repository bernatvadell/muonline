// PlayerObject.cs
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Wings;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Diagnostics; // For Debug.WriteLine
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    public class PlayerObject : WalkerObject
    {
        // Fields
        private CharacterClassNumber _characterClass;

        // Properties
        // State properties
        public bool IsResting { get; set; } = false;
        public bool IsSitting { get; set; } = false;
        public Vector2? RestPlaceTarget { get; set; }
        public Vector2? SitPlaceTarget { get; set; }

        // Identification and Network Class
        public string Name { get; set; } = "Character";
        public new ushort NetworkId { get; set; }  // ID from server packets (masked 0x7FFF)

        public CharacterClassNumber CharacterClass
        {
            get => _characterClass;
            set
            {
                if (_characterClass != value)
                {
                    Debug.WriteLine($"PlayerObject {Name}: Setting CharacterClass from {_characterClass} to {value}");
                    _characterClass = value;
                }
                else
                {
                    Debug.WriteLine($"PlayerObject {Name}: CharacterClass is already {value}. Skipping UpdateBodyPartClasses.");
                }
            }
        }

        // References to child equipment objects
        public PlayerMaskHelmObject HelmMask { get; private set; }
        public PlayerHelmObject Helm { get; private set; }
        public PlayerArmorObject Armor { get; private set; }
        public PlayerPantObject Pants { get; private set; }
        public PlayerGloveObject Gloves { get; private set; }
        public PlayerBootObject Boots { get; private set; }
        public WingObject Wings { get; private set; }
        // TODO: Add properties for Weapons, Shields etc. if needed

        // PlayerAction property (uses base int CurrentAction)
        public new PlayerAction CurrentAction
        {
            get => (PlayerAction)base.CurrentAction;
            set => base.CurrentAction = (int)value;
        }

        // Constructors
        public PlayerObject()
        {
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-40, -40, 0),
                new Vector3(40, 40, 120));

            Scale = 0.85f;
            AnimationSpeed = 10f;
            CurrentAction = PlayerAction.StopMale; // Default action
            _characterClass = CharacterClassNumber.DarkWizard; // Initialize with a default

            // Initialize children IMMEDIATELY
            HelmMask = new PlayerMaskHelmObject { LinkParentAnimation = true, Hidden = true };
            Helm = new PlayerHelmObject { LinkParentAnimation = true };
            Armor = new PlayerArmorObject { LinkParentAnimation = true };
            Pants = new PlayerPantObject { LinkParentAnimation = true };
            Gloves = new PlayerGloveObject { LinkParentAnimation = true };
            Boots = new PlayerBootObject { LinkParentAnimation = true };
            Wings = new Wing403 { LinkParentAnimation = false, Hidden = true }; // Example Wing

            // Add children AFTER they are created
            Children.Add(HelmMask);
            Children.Add(Helm);
            Children.Add(Armor);
            Children.Add(Pants);
            Children.Add(Gloves);
            Children.Add(Boots);
            Children.Add(Wings);
        }

        // Public Methods
        public override async Task Load()
        {
            // NOTE: _characterClass should already be set BEFORE calling Load()
            //       (usually by GameScene.Load or constructor)
            Debug.WriteLine($"PlayerObject {Name}: Load() started. Current _characterClass: {_characterClass}");

            // 1. Load the base player model
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            if (Model == null)
            {
                Debug.WriteLine($"PlayerObject {Name}: Failed to load base model 'Player/Player.bmd'");
                Status = GameControlStatus.Error;
                return; // Cannot proceed without base model
            }
            Debug.WriteLine($"PlayerObject {Name}: Base model prepared.");

            // 2. CRITICAL: Ensure children have the correct class BEFORE their Load is called
            await UpdateBodyPartClassesAsync();
            Debug.WriteLine($"PlayerObject {Name}: UpdateBodyPartClassesAsync() called within Load().");

            // 3. Load base content, which WILL trigger Load() on children (including equipment)
            await base.Load(); // base.Load -> base.LoadContent -> children's LoadContent
            Debug.WriteLine($"PlayerObject {Name}: base.Load() completed.");

            // 4. Verify children status after load
            foreach (var child in Children)
            {
                if (child is ModelObject modelChild)
                {
                    Debug.WriteLine($"{modelChild.GetType().Name}: Status={modelChild.Status}, Model={(modelChild.Model != null ? "OK" : "NULL")}");
                }
            }
            Debug.WriteLine($"PlayerObject {Name}: Status after load: {Status}");
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime); // Handles movement, camera for main walker, base object updates

            if (World is not WalkableWorldControl worldControl) // Use pattern matching for cleaner cast
                return;

            // --- State-based Animation Logic ---
            if (RestPlaceTarget.HasValue)
            {
                float restDistance = Vector2.Distance(Location, RestPlaceTarget.Value);
                if (restDistance < 0.1f) // Threshold for being 'at' the rest place
                {
                    // Select appropriate resting animation based on world/context
                    CurrentAction = (worldControl.WorldIndex == 4) ? PlayerAction.PlayerFlyingRest : PlayerAction.PlayerStandingRest;
                    return; // Stay in resting state animation
                }
                else if (restDistance > 1.0f) // Threshold for moving away
                {
                    RestPlaceTarget = null;
                    IsResting = false;
                }
            }

            if (SitPlaceTarget.HasValue)
            {
                float sitDistance = Vector2.Distance(Location, SitPlaceTarget.Value);
                if (sitDistance < 0.1f) // Threshold for being 'at' the sit place
                {
                    CurrentAction = PlayerAction.PlayerSit1; // Or cycle through sit animations?
                    return; // Stay in sitting state animation
                }
                else if (sitDistance > 1.0f) // Threshold for moving away
                {
                    SitPlaceTarget = null;
                    IsSitting = false;
                }
            }

            // --- Animation Logic for Select Screen ---
            if (World is SelectWorld)
            {
                CurrentAction = PlayerAction.StopMale;
            }
            // --- Standard In-Game Animation Logic ---
            else
            {
                if (IsMoving)
                {
                    CurrentAction = worldControl.WorldIndex switch
                    {
                        8 => PlayerAction.RunSwim,    // Atlans
                        11 => PlayerAction.Fly,   // Icarus
                        _ => PlayerAction.WalkMale // Default walking/running
                    };
                }
                else // Not moving
                {
                    CurrentAction = (worldControl.WorldIndex == 8 || worldControl.WorldIndex == 11)
                        ? PlayerAction.StopFlying // Atlans or Icarus
                        : PlayerAction.StopMale;  // Default standing
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }

        public override void OnClick()
        {
            base.OnClick();
        }

        // Private Methods
        /// <summary>
        /// Updates the PlayerClass property of all child equipment objects based on the current network _characterClass.
        /// This method handles the mapping between network enum and model enum.
        /// </summary>
        private void UpdateBodyPartClasses()
        {
            PlayerClass mappedClass = MapNetworkClassToModelClass(_characterClass);
            Debug.WriteLine($"PlayerObject {Name}: UpdateBodyPartClasses mapping network class {_characterClass} to model class {mappedClass} ({(int)mappedClass})");

            HelmMask?.SetPlayerClass(mappedClass);
            Helm?.SetPlayerClass(mappedClass);
            Armor?.SetPlayerClass(mappedClass);
            Pants?.SetPlayerClass(mappedClass);
            Gloves?.SetPlayerClass(mappedClass);
            Boots?.SetPlayerClass(mappedClass);
            Wings?.SetPlayerClass(mappedClass); // Assuming Wings might need it too
            // TODO: Update other parts (Weapons, Shield)
        }

        /// <summary>
        /// Maps the network CharacterClassNumber enum to the local PlayerClass enum used for model loading.
        /// </summary>
        private PlayerClass MapNetworkClassToModelClass(CharacterClassNumber networkClass)
        {
            return networkClass switch
            {
                CharacterClassNumber.DarkWizard => PlayerClass.DarkWizard,
                CharacterClassNumber.SoulMaster => PlayerClass.SoulMaster,
                CharacterClassNumber.GrandMaster => PlayerClass.GrandMaster,

                CharacterClassNumber.DarkKnight => PlayerClass.DarkKnight,
                CharacterClassNumber.BladeKnight => PlayerClass.BladeKnight,
                CharacterClassNumber.BladeMaster => PlayerClass.BladeMaster,

                CharacterClassNumber.FairyElf => PlayerClass.FairyElf,
                CharacterClassNumber.MuseElf => PlayerClass.MuseElf,
                CharacterClassNumber.HighElf => PlayerClass.HighElf,

                CharacterClassNumber.MagicGladiator => PlayerClass.MagicGladiator,
                CharacterClassNumber.DuelMaster => PlayerClass.DuelMaster,

                CharacterClassNumber.DarkLord => PlayerClass.DarkLord,
                CharacterClassNumber.LordEmperor => PlayerClass.LordEmperor,

                CharacterClassNumber.Summoner => PlayerClass.Summoner,
                CharacterClassNumber.BloodySummoner => PlayerClass.BloodySummoner,
                CharacterClassNumber.DimensionMaster => PlayerClass.DimensionMaster,

                CharacterClassNumber.RageFighter => PlayerClass.RageFighter,
                CharacterClassNumber.FistMaster => PlayerClass.FistMaster,

                _ => PlayerClass.DarkWizard // Default fallback
            };
        }

        // New async version: ensures all parts are loaded for the correct class before loading content
        public async Task UpdateBodyPartClassesAsync()
        {
            PlayerClass mappedClass = MapNetworkClassToModelClass(_characterClass);
            if (HelmMask != null) await HelmMask.SetPlayerClassAsync(mappedClass);
            if (Helm != null) await Helm.SetPlayerClassAsync(mappedClass);
            if (Armor != null) await Armor.SetPlayerClassAsync(mappedClass);
            if (Pants != null) await Pants.SetPlayerClassAsync(mappedClass);
            if (Gloves != null) await Gloves.SetPlayerClassAsync(mappedClass);
            if (Boots != null) await Boots.SetPlayerClassAsync(mappedClass);
            // Wings do not support async class setting yet; add if needed in the future
        }
    }

    // Extension method to set class on various parts
    public static class PlayerPartExtensions
    {
        public static void SetPlayerClass(this ModelObject part, PlayerClass playerClass)
        {
            if (part == null)
            {
                Debug.WriteLine($"SetPlayerClass Warning: Attempted to set class on a null part.");
                return;
            }

            switch (part)
            {
                case PlayerArmorObject armor: armor.PlayerClass = playerClass; break;
                case PlayerBootObject boot: boot.PlayerClass = playerClass; break;
                case PlayerGloveObject glove: glove.PlayerClass = playerClass; break;
                case PlayerHelmObject helm: helm.PlayerClass = playerClass; break;
                case PlayerMaskHelmObject maskHelm: maskHelm.PlayerClass = playerClass; break;
                case PlayerPantObject pant: pant.PlayerClass = playerClass; break;
                case WingObject wing:
                    // Wings might not have a PlayerClass property directly.
                    // If they do, uncomment: wing.PlayerClass = playerClass;
                    // Or handle wing appearance based on playerClass differently.
                    break;
                // Add other types like weapons, shields etc. here
                default:
                    // Debug.WriteLine($"SetPlayerClass Warning: Unhandled part type {part.GetType().Name} for setting PlayerClass.");
                    break;
            }
        }
    }
}