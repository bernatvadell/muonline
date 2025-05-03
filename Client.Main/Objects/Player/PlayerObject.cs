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
        // State properties
        public bool IsResting { get; set; } = false;
        public bool IsSitting { get; set; } = false;
        public Vector2? RestPlaceTarget { get; set; }
        public Vector2? SitPlaceTarget { get; set; }

        // Identification and Network Class
        public string Name { get; set; } = "Character";
        public ushort NetworkId { get; set; }  // ID z pakietów serwera (masked 0x7FFF)

        private CharacterClassNumber _characterClass;
        public CharacterClassNumber CharacterClass
        {
            get => _characterClass;
            set
            {
                if (_characterClass != value)
                {
                    Debug.WriteLine($"PlayerObject {Name}: Setting CharacterClass from {_characterClass} to {value}");
                    _characterClass = value;
                    // *** CALL DIRECTLY ***
                    UpdateBodyPartClasses(); // Ensure update happens immediately when property changes
                }
                // *** ADDED ELSE BLOCK FOR LOGGING ***
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

        public PlayerObject()
        {
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-40, -40, 0),
                new Vector3(40, 40, 120));

            Scale = 0.85f;
            AnimationSpeed = 8f;
            // *** START WITH A MORE GENERIC DEFAULT or ensure it's set correctly BEFORE Load ***
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

            // Ensure initial class is applied to children during construction
            // This helps if Load isn't called immediately or if class is set late.
            UpdateBodyPartClasses();
        }

        public override async Task Load()
        {
            // *** UWAGA: Klasa _characterClass powinna być już ustawiona PRZED wywołaniem Load() ***
            //          (zazwyczaj przez GameScene.Load lub konstruktor)
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

            // 2. *** CRITICAL: Ensure children have the correct class BEFORE their Load is called ***
            //    Setter CharacterClass już powinien był to wywołać, ale dla pewności:
            UpdateBodyPartClasses();
            Debug.WriteLine($"PlayerObject {Name}: UpdateBodyPartClasses() called within Load().");

            // 3. Load base content, which WILL trigger Load() on children (including equipment)
            await base.Load(); // base.Load -> base.LoadContent -> children's LoadContent
            Debug.WriteLine($"PlayerObject {Name}: base.Load() completed.");

            // 4. Verify children status after load
            foreach (var child in Children)
            {
                if (child is ModelObject modelChild && modelChild.Status == GameControlStatus.Error)
                {
                    Debug.WriteLine($"PlayerObject {Name}: Child {child.GetType().Name} failed to load (Status: Error). Check model paths and class mapping.");
                }
            }
        }

        /// <summary>
        /// Updates the PlayerClass property of all child equipment objects based on the current network _characterClass.
        /// This method handles the mapping between network enum and model enum.
        /// </summary>
        private void UpdateBodyPartClasses()
        {
            // Map the CharacterClassNumber (network) to the PlayerClass (models)
            PlayerClass mappedClass = MapNetworkClassToModelClass(_characterClass);
            Debug.WriteLine($"PlayerObject {Name}: UpdateBodyPartClasses mapping network class {_characterClass} to model class {mappedClass} ({(int)mappedClass})");

            // Update children with the CORRECTLY MAPPED PlayerClass
            // Use null-conditional operator ?. for safety
            // *** Ensure children are not null before setting the class ***
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
            // Ensure this mapping is correct for your game version and PlayerClass enum definitions
            return networkClass switch
            {
                CharacterClassNumber.DarkWizard => PlayerClass.DarkWizard,         // 0 -> 1
                CharacterClassNumber.SoulMaster => PlayerClass.SoulMaster,         // 1 -> 201 (or DarkWizard if models are shared)
                CharacterClassNumber.GrandMaster => PlayerClass.GrandMaster,       // 2 -> 301 (or DarkWizard)

                CharacterClassNumber.DarkKnight => PlayerClass.DarkKnight,         // 4 -> 2
                CharacterClassNumber.BladeKnight => PlayerClass.BladeKnight,       // 5 -> 202
                CharacterClassNumber.BladeMaster => PlayerClass.BladeMaster,       // 6 -> 302

                CharacterClassNumber.FairyElf => PlayerClass.FairyElf,             // 8 -> 3
                CharacterClassNumber.MuseElf => PlayerClass.MuseElf,               // 9 -> 203
                CharacterClassNumber.HighElf => PlayerClass.HighElf,               // 10 -> 303

                CharacterClassNumber.MagicGladiator => PlayerClass.MagicGladiator, // 12 -> 4
                CharacterClassNumber.DuelMaster => PlayerClass.DuelMaster,         // 13 -> 304

                CharacterClassNumber.DarkLord => PlayerClass.DarkLord,             // 16 -> 5
                CharacterClassNumber.LordEmperor => PlayerClass.LordEmperor,       // 17 -> 305

                CharacterClassNumber.Summoner => PlayerClass.Summoner,             // 20 -> 6
                CharacterClassNumber.BloodySummoner => PlayerClass.BloodySummoner, // 21 -> 206
                CharacterClassNumber.DimensionMaster => PlayerClass.DimensionMaster,// 22 -> 306

                CharacterClassNumber.RageFighter => PlayerClass.RageFighter,       // 24 -> 7
                CharacterClassNumber.FistMaster => PlayerClass.FistMaster,         // 25 -> 307

                // Add others if necessary
                _ => PlayerClass.DarkWizard // Default fallback
            };
        }

        // --- Keep Update, Draw, OnClick ---
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime); // Handles movement, camera for main walker, base object updates

            if (World is not WalkableWorldControl)
                return;

            // --- State-based Animation Logic ---
            if (RestPlaceTarget.HasValue)
            {
                float restDistance = Vector2.Distance(Location, RestPlaceTarget.Value);
                if (restDistance < 0.1f) // Threshold for being 'at' the rest place
                {
                    // Select appropriate resting animation based on world/context
                    CurrentAction = (World.WorldIndex == 4) ? PlayerAction.PlayerFlyingRest : PlayerAction.PlayerStandingRest;
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

            // *** ANIMATION LOGIC FOR SELECT SCREEN ***
            // If the parent is SelectWorld, force an idle animation
            if (World is SelectWorld)
            {
                // You might want different idle animations based on class later
                CurrentAction = PlayerAction.StopMale;
                // No return here, allow base.Update to handle animation frames
            }
            // *** END OF SELECT SCREEN LOGIC ***
            else // Standard in-game animation logic
            {
                if (IsMoving)
                {
                    // Select walking/running/flying/swimming animation based on world context
                    if (World.WorldIndex == 8) // Atlans
                        CurrentAction = PlayerAction.RunSwim;
                    else if (World.WorldIndex == 11) // Icarus
                        CurrentAction = PlayerAction.Fly;
                    else // Default walking/running animation
                        CurrentAction = PlayerAction.WalkMale; // Or Run? Check your actions enum/logic
                }
                else // Not moving
                {
                    // Select idle/stop animation based on world context
                    if (World.WorldIndex == 8 || World.WorldIndex == 11) // Atlans or Icarus
                        CurrentAction = PlayerAction.StopFlying;
                    else // Default standing animation
                        CurrentAction = PlayerAction.StopMale; // Or StopSword, StopFemale etc. based on class/equipment?
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
    }

    // Extension method to set class on various parts
    public static class PlayerPartExtensions
    {
        // *** Dodano sprawdzanie null i logowanie ***
        public static void SetPlayerClass(this ModelObject part, PlayerClass playerClass)
        {
            if (part == null)
            {
                Debug.WriteLine($"SetPlayerClass Warning: Attempted to set class on a null part.");
                return;
            }

            // Log the attempt before the switch
            // Debug.WriteLine($"SetPlayerClass: Attempting to set class {playerClass} on part {part.GetType().Name} ({part.Name ?? "NoName"})");


            switch (part)
            {
                case PlayerArmorObject armor: armor.PlayerClass = playerClass; break;
                case PlayerBootObject boot: boot.PlayerClass = playerClass; break;
                case PlayerGloveObject glove: glove.PlayerClass = playerClass; break;
                case PlayerHelmObject helm: helm.PlayerClass = playerClass; break;
                case PlayerMaskHelmObject maskHelm: maskHelm.PlayerClass = playerClass; break;
                case PlayerPantObject pant: pant.PlayerClass = playerClass; break;
                // Add WingObject case if it has a PlayerClass property
                case WingObject wing:
                    // Example: Assuming WingObject has a PlayerClass property or method
                    // wing.PlayerClass = playerClass;
                    // Since WingObject doesn't have PlayerClass property, log or ignore
                    // Debug.WriteLine($"SetPlayerClass: WingObject ({part.Name}) does not have a PlayerClass property to set.");
                    break;
                // Add other types like weapons, shields etc. here
                default:
                    // Debug.WriteLine($"SetPlayerClass Warning: Unhandled part type {part.GetType().Name} for setting PlayerClass.");
                    break;
            }
        }
    }
}