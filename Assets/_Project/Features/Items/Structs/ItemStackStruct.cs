using System;
using Unity.Netcode;

namespace _Project.Features.Items.Structs
{
    public struct ItemStackStruct : INetworkSerializable, IEquatable<ItemStackStruct>
    {
        public ushort ItemID;
        public int Stacks;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemID);
            serializer.SerializeValue(ref Stacks);
        }

        public bool Equals(ItemStackStruct other)
        {
            return ItemID == other.ItemID && Stacks == other.Stacks;
        }
    }
}