using System;
using _Project.Core.Enums;
using Unity.Netcode;

namespace _Project.Features.LobbySystem.Structs
{
    public struct LobbySettings : INetworkSerializable, IEquatable<LobbySettings>
    {
        public GameMode Mode;
        public AllowedFactions Factions;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Mode);
            serializer.SerializeValue(ref Factions);
        }

        public bool Equals(LobbySettings other)
        {
            return Mode == other.Mode && Factions == other.Factions;
        }
    }
}