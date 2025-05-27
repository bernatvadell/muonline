namespace Client.Main.Models
{

    public enum ServerPlayerActionType : byte
    {
        Attack1 = 120,   // AT_ATTACK1
        Attack2 = 121,   // AT_ATTACK2

        Stand1 = 122,   // AT_STAND1
        Stand2 = 123,   // AT_STAND2
        Move1 = 124,   // AT_MOVE1
        Move2 = 125,   // AT_MOVE2
        Damage1 = 126,   // AT_DAMAGE1
        Die1 = 127,   // AT_DIE1

        // *** DOTYCHCZASOWE WPISY – NIE ZMIENIAMY KOLEJNOŚCI ***
        Sit = 128,   // AT_SIT1
        Pose = 129,   // AT_POSE1
        Healing = 130,   // AT_HEALING1

        Greeting = 131,   // AT_GREETING1
        Goodbye = 132,   // AT_GOODBYE1
        Clap = 133,   // AT_CLAP1
        Gesture = 134,   // AT_GESTURE1
        Direction = 135,   // AT_DIRECTION1
        Unknown = 136,   // AT_UNKNOWN1
        Cry = 137,   // AT_CRY1
        Cheer = 138,   // AT_CHEER1
        Awkward = 139,   // AT_AWKWARD1
        See = 140,   // AT_SEE1
        Win = 141,   // AT_WIN1
        Smile = 142,   // AT_SMILE1
        Sleep = 143,   // AT_SLEEP1
        Cold = 144,   // AT_COLD1
        Again = 145,   // AT_AGAIN1
        Respect = 146,   // AT_RESPECT1
        Salute = 147,   // AT_SALUTE1
        Rush = 148,   // AT_RUSH1
        Scissors = 149,   // AT_SCISSORS
        Rock = 150,   // AT_ROCK
        Paper = 151,   // AT_PAPER
        Hustle = 152,   // AT_HUSTLE
        Provocation = 153,   // AT_PROVOCATION
        LookAround = 154,   // AT_LOOK_AROUND
        Cheers = 155,   // AT_CHEERS

        Jack1 = 156,   // AT_JACK1
        Jack2 = 157,   // AT_JACK2
        Santa1_1 = 158,   // AT_SANTA1_1
        Santa1_2 = 159,   // AT_SANTA1_2
        Santa1_3 = 160,   // AT_SANTA1_3
        Santa2_1 = 161,   // AT_SANTA2_1
        Santa2_2 = 162,   // AT_SANTA2_2
        Santa2_3 = 163    // AT_SANTA2_3
    }
}
