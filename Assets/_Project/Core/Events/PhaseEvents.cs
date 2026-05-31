using System;
using _Project.Core.Enums;

namespace _Project.Core.Events
{
    /// <summary>
    /// Static channel that the local player's PhasedNetworkObject raises when its
    /// phase changes. Cameras / UI subscribe to update culling masks etc.
    /// </summary>
    public static class PhaseEvents
    {
        public static event Action<TeamPhase> OnLocalPlayerPhaseChanged;
        public static TeamPhase CurrentLocalPhase { get; private set; } = TeamPhase.Cleansers;

        public static void RaiseLocalPlayerPhaseChanged(TeamPhase phase)
        {
            CurrentLocalPhase = phase;
            OnLocalPlayerPhaseChanged?.Invoke(phase);
        }
    }
}
