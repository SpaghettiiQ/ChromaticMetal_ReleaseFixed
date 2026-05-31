using UnityEngine;

namespace _Project.Core.Interfaces
{
    public interface IWeaponData
    {
        string WeaponName { get; }
        GameObject WeaponPrefab { get; }
        int BaseDamage { get; }
    }
}