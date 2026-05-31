using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Events;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Ensign
{
    [CreateAssetMenu(fileName = "SustainedFirePassive", menuName = "Proxy Abilities/Ensign/Sustained Fire (Passive)")]
    public class SustainedFirePassive : ScriptableObject, IAbilityEffect
    {
        [Header("Passive Settings")]
        [SerializeField] private float procChanceIncreasePerHit = 0.05f; // +5% added per consecutive hit
        [SerializeField] private float maxProcChanceBonus = 0.3f; // e.g. Max 30% additional proc chance
        [SerializeField] private float resetDelayWithoutFiring = 1.5f;

        public void Execute(GameObject instigator)
        {
            if (instigator.GetComponent<SustainedFireTracker>() == null)
            {
                var tracker = instigator.AddComponent<SustainedFireTracker>();
                tracker.Initialize(procChanceIncreasePerHit, maxProcChanceBonus, resetDelayWithoutFiring);
            }
        }
    }

    public class SustainedFireTracker : MonoBehaviour
    {
        private float _procChanceIncreasePerHit;
        private float _maxProcChanceBonus;
        private float _resetDelay;

        private CharacterEventBus _eventBus;
        private CharacterStats _stats;
        private StatModifier _currentMod;
        
        private int _consecutiveHits;
        private float _lastHitTime;

        public void Initialize(float increasePerHit, float maxBonus, float resetDelay)
        {
            _procChanceIncreasePerHit = increasePerHit;
            _maxProcChanceBonus = maxBonus;
            _resetDelay = resetDelay;

            _eventBus = GetComponent<CharacterEventBus>();
            _stats = GetComponent<CharacterStats>();

            _currentMod = new StatModifier { Type = StatModType.Flat, Value = 0f };
            if (_stats != null)
            {
                _stats.AddModifier(StatType.ProcChanceFlatAdd, _currentMod);
            }

            if (_eventBus != null)
            {
                _eventBus.OnHit += HandleHit;
            }
        }

        private void Update()
        {
            if (_consecutiveHits > 0 && Time.time - _lastHitTime > _resetDelay)
            {
                ResetBonus();
            }
        }

        private void HandleHit(DamageContext ctx)
        {
            _lastHitTime = Time.time;
            _consecutiveHits++;

            float bonus = Mathf.Min(_consecutiveHits * _procChanceIncreasePerHit, _maxProcChanceBonus);
            
            if (_stats != null)
            {
                _stats.RemoveModifier(StatType.ProcChanceFlatAdd, _currentMod);
                _currentMod.Value = bonus;
                _stats.AddModifier(StatType.ProcChanceFlatAdd, _currentMod);
            }
        }

        private void ResetBonus()
        {
            _consecutiveHits = 0;
            if (_stats != null)
            {
                _stats.RemoveModifier(StatType.ProcChanceFlatAdd, _currentMod);
                _currentMod.Value = 0f;
                _stats.AddModifier(StatType.ProcChanceFlatAdd, _currentMod);
            }
        }

        private void OnDestroy()
        {
            if (_eventBus != null) _eventBus.OnHit -= HandleHit;
            if (_stats != null) _stats.RemoveModifier(StatType.ProcChanceFlatAdd, _currentMod);
        }
    }
}