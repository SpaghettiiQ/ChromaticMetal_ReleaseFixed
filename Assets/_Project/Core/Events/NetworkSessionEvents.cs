using System;

namespace _Project.Core.Events
{
    public enum DisconnectReason
    {
        Unknown,
        LocalShutdown,
        HostShutdown,
        HostLost,
        KickedByHost,
        TransportFailure,
        ConnectionLostClient
    }

    public static class NetworkSessionEvents
    {
        public static event Action<DisconnectReason> OnSessionEnded;
        public static void RaiseSessionEnded(DisconnectReason reason) => OnSessionEnded?.Invoke(reason);
    }
}
