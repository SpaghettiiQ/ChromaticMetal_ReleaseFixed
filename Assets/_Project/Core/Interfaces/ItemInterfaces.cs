using UnityEngine;
using _Project.Core.Structs;

namespace _Project.Core.Interfaces
{
    public interface IPassiveItem
    {
        // Fired whenever the inventory updates (stack added/removed)
        void ApplyPassive(GameObject owner, int stacks);
        void RemovePassive(GameObject owner, int stacks);
    }

    public interface IOnHitItem
    {
        void OnHitEnemy(GameObject owner, int stacks, DamageContext ctx);
    }

    public interface IOnDamageTakenItem
    {
        void OnDamageTaken(GameObject owner, int stacks, DamageContext ctx);
    }
    
    public interface IOnKillItem
    {
        void OnKillEnemy(GameObject owner, int stacks, DamageContext ctx);
    }
    
    public interface IUsableItem
    {
        void UseItem(GameObject owner, int stackToRemove);
    }
}