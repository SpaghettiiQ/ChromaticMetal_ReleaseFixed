using _Project.Core.Enums;

namespace _Project.Core.Interfaces
{
    public interface IRunManager
    {
        // Setup
        void GenerateNewLoopItinerary(GameMode mode);
        string GetCurrentMapForTeam(TeamAffiliation team);

        // Progression
        void AdvanceTeamToNextStage(TeamAffiliation team);
        
        // Loop Management
        void SetPvERunEndVote(bool wantsToEnd); // Players interact with object to end run
        void ResolvePvPMatch(TeamAffiliation winningTeam); // Called when a team wins the final arena
    }
}