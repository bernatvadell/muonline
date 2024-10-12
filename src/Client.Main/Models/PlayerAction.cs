using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Models
{
    public enum PlayerAction
    {
        Set = 0,

        StopMale = 1,
        StopFemale = 2,
        StopSummoner = 3,
        StopSword = 4,
        StopTwoHandSword = 5,
        StopSpear = 6,
        StopScythe = 7,
        StopBow = 8,
        StopCrossbow = 9,
        StopWand = 10,
        StopFlying = 11,
        StopFlyCrossbow = 12,
        StopRide = 13,
        StopRideWeapon = 14,

        WalkMale = 15,
        WalkFemale = 16,
        WalkSword = 17,
        WalkTwoHandSword = 18,
        WalkSpear = 19,
        WalkScythe = 20,
        WalkBow = 21,
        WalkCrossbow = 22,
        WalkWand = 23,
        WalkSwim = 24,

        Run = 25,
        RunSword = 26,
        RunTwoSword = 27,
        RunTwoHandSword = 28,
        RunSpear = 29,
        RunBow = 30,
        RunCrossbow = 31,
        RunWand = 32,
        RunSwim = 33,

        Fly = 34,
        FlyCrossbow = 35,

        RunRide = 36,
        RunRideWeapon = 37,

        AttackFist = 38
    }
}
