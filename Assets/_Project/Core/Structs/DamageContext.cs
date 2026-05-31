using _Project.Core.Enums;
using UnityEngine;

namespace _Project.Core.Structs
{
    public struct DamageContext
    {
        public GameObject Source;
        public GameObject Target;
        public int Damage;
        public DamageType DamageType;
        public AttackType AttackType;
        public bool IsCritical;
    }
}