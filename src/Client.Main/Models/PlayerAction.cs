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
        StopFlying,
        StopFlyCrossbow,
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
        WalkSwim,

        Run,
        RunSword,
        RunTwoSword,
        RunTwoHandSword,
        RunSpear,
        RunBow,
        RunCrossbow,
        RunWand,
        RunSwim,

        Fly,
        FlyCrossbow,

        RunRide,
        RunRideWeapon,

        AttackFist = 38
    }
}
