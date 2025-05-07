namespace Client.Main.Models
{
    public enum MonsterActionType : byte
    {
        Stop1 = 0,
        Stop2 = 1, // Sometimes used as an alternate idle animation or reaction
        Walk = 2,
        Attack1 = 3,
        Attack2 = 4,
        Shock = 5, // Taking damage
        Die = 6,
        Appear = 7, // Appearing (e.g., for bosses)
        Attack3 = 8,
        Attack4 = 9,
        Run = 10, // If the monster has a separate run animation
    }
}
