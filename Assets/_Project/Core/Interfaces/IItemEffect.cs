using UnityEngine;

namespace  _Project.Core.Interfaces
{
    public interface IItemEffect
    {
        void ApplyEffect(GameObject target);
        void RemoveEffect(GameObject target);
    }
}