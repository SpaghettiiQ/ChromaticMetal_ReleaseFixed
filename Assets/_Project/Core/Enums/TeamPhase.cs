namespace _Project.Core.Enums
{
    public enum TeamPhase : byte
    {
        Cleansers = 0,
        Thrive = 1,
        Both = 2
    }

    public static class TeamPhaseExtensions
    {
        public static TeamPhase ToPhase(this TeamAffiliation team)
        {
            return team == TeamAffiliation.Thrive ? TeamPhase.Thrive : TeamPhase.Cleansers;
        }

        public static bool VisibleTo(this TeamPhase phase, TeamPhase observerPhase)
        {
            return phase == TeamPhase.Both || observerPhase == TeamPhase.Both || phase == observerPhase;
        }
    }
}
