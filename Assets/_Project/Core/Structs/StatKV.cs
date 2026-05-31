using System;
using Unity.Netcode;

namespace _Project.Core.Structs
{
    /// <summary>
    /// Network-serializable (StatType, total) entry for replicating computed stat totals to clients.
    /// Server writes; clients read. See CharacterStats._netStats.
    /// </summary>
    public struct StatKV : INetworkSerializable, IEquatable<StatKV>
    {
        public int Stat;    // (int)StatType
        public float Value; // computed total

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Stat);
            serializer.SerializeValue(ref Value);
        }

        public bool Equals(StatKV other) => Stat == other.Stat && Value.Equals(other.Value);

        public override int GetHashCode() => HashCode.Combine(Stat, Value);
    }
}
