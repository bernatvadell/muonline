using Client.Main.Content;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(249, "Berdysh Guard")]
    public class BerdyshGuard : NPCObject
    {
        private WeaponObject _leftHandWeapon;

        public BerdyshGuard()
        {
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 33 // Left hand bone
            };
            Children.Add(_leftHandWeapon);
        }
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 10); // Plate Set
            var item = ItemDatabase.GetItemDefinition(3, 7); // Berdysh
            _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            await base.Load();
            AnimationSpeed = 25f;
            CurrentAction = (int)PlayerAction.PlayerStopSpear; // Appropriate idle animation for spear
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Set appropriate animation based on movement state
            if (IsMoving)
            {
                if (CurrentAction != (int)PlayerAction.PlayerWalkSpear)
                {
                    CurrentAction = (int)PlayerAction.PlayerWalkSpear;
                }
            }
            else
            {
                if (CurrentAction != (int)PlayerAction.PlayerStopSpear)
                {
                    CurrentAction = (int)PlayerAction.PlayerStopSpear;
                }
            }
        }

        protected override void HandleClick() { }
    }
}
