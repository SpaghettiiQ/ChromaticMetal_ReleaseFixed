using System;
using _Project.Core.Enums;

namespace _Project.Core.Structs
{
    public struct StatModifier : IEquatable<StatModifier>
    {
        public StatModType Type;
        public float Value;

        public StatModifier(StatModType type, float value)
        {
            Type = type;
            Value = value;
        }

        public bool Equals(StatModifier other)
        {
            return Type == other.Type && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is StatModifier other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Type, Value);
        }
    }
}