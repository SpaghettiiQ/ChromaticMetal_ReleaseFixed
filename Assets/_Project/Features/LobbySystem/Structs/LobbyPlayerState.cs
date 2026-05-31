using System;
using Unity.Collections;
using Unity.Netcode;
using _Project.Core.Enums;

namespace _Project.Features.LobbySystem.Structs
{
    public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
    {
        public ulong ClientId;
        public FixedString32Bytes PlayerName; // Fixed strings are required for NGO structs
        public TeamAffiliation Team;
        public int CharacterIndex; // Which ProxyCharacter they are hovering over/selected
        public bool IsReady;       // Ready to launch from Lobby -> Character Select
        public bool IsLockedIn;    // Locked character choice in Character Select

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref Team);
            serializer.SerializeValue(ref CharacterIndex);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsLockedIn);
        }

        public bool Equals(LobbyPlayerState other)
        {
            return ClientId == other.ClientId &&
                   PlayerName == other.PlayerName &&
                   Team == other.Team &&
                   CharacterIndex == other.CharacterIndex &&
                   IsReady == other.IsReady &&
                   IsLockedIn == other.IsLockedIn;
        }
    }
}