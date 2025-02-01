using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Models
{
    public enum PlayerAction
    {
        Set,

        StopMale,
        StopFemale,
        StopSummoner,
        StopSword,
        StopTwoHandSword,
        StopSpear,
        StopScythe,
        StopBow,
        StopCrossbow,
        StopWand,
        StopArm1,
        StopArm2,
        StopArm3,
        StopFlying = 17,
        StopFlyCrossbow = 18,
        StopRide,
        StopRideWeapon,

        WalkMale = 47,
        WalkFemale,
        WalkSword,
        WalkTwoHandSword,
        WalkSpear,
        WalkScythe,
        WalkBow,
        WalkCrossbow,
        WalkWand,
        WalkSwim = 58,

        Run,
        RunSword,
        RunTwoSword,
        RunTwoHandSword,
        RunSpear,
        RunBow,
        RunCrossbow,
        RunWand,
        RunSwim = 84,

        Fly = 85,
        FlyCrossbow,

        RunRide,
        RunRideWeapon,

        AttackFist = 38,

        BlowSkill = 134,

        TwistingSlashSkill = 138,
        RegularBlowSkill = 139, //154?
        GreaterFortitudeSkill = 150, // or 152?

        EvilSpiritSkill = 157,
        FlameSkill = 158,


    }
}
