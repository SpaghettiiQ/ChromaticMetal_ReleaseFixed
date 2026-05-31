using System;
using _Project.Core.Enums;

namespace _Project.Core.Interfaces
{
    public interface ITeamPhased
    {
        TeamPhase Phase { get; }
        event Action<TeamPhase> PhaseChanged;
    }
}
