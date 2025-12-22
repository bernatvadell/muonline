using Client.Main.Content;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(247, "Crossbow Guard")]
    public class CrossbowGuard : NPCObject
    {
        private WeaponObject _backWeapon; // For bolt on back
        private WeaponObject _leftHandWeapon; // For crossbow

        public CrossbowGuard()
        {
            _backWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 47 // Back bone for bolt
            };
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 33
            };
            Children.Add(_backWeapon);
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 10); // Plate Set
            var crossbowItem = ItemDatabase.GetItemDefinition(4, 10); // Light Crossbow
            var boltItem = ItemDatabase.GetItemDefinition(4, 7); // Bolt
            _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(crossbowItem.TexturePath);
            _backWeapon.Model = await BMDLoader.Instance.Prepare(boltItem.TexturePath);
            // Set bolt position and rotation on back, similar to weapon holster
            _backWeapon.Position = new Vector3(-10f, 8f, 0f); // Left side holster offset
            _backWeapon.Angle = new Vector3(MathHelper.ToRadians(80f), 0f, MathHelper.ToRadians(90f)); // Left side holster rotation
            await base.Load();
            AnimationSpeed = 25f;
            CurrentAction = (int)PlayerAction.PlayerStopCrossbow; // Appropriate idle animation for crossbow
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Set appropriate animation based on movement state
            if (IsMoving)
            {
                if (CurrentAction != (int)PlayerAction.PlayerWalkCrossbow)
                {
                    CurrentAction = (int)PlayerAction.PlayerWalkCrossbow;
                }
            }
            else
            {
                if (CurrentAction != (int)PlayerAction.PlayerStopCrossbow)
                {
                    CurrentAction = (int)PlayerAction.PlayerStopCrossbow;
                }
            }
        }

        protected override void HandleClick() { }
    }
}
