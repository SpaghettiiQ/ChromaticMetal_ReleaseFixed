using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;

namespace _Project.Features.BurdenSystem.Scripts
{
    [RequireComponent(typeof(CharacterStats))]
    public class BurdenController : NetworkBehaviour, IBurdenable
    {
        [Header("Burden Meter Settings")]
        public NetworkVariable<float> CurrentBurden = new NetworkVariable<float>(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Presence Power (Burden Filter, Thrive only)")]
        [Tooltip("Replicated 0-100 Presence Power level. Increases when Burden Filter converts " +
                 "incoming burden into power. Boosts the attacker's WeaponDamageMultiplier.")]
        public NetworkVariable<float> CurrentPresencePower = new NetworkVariable<float>(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        [Tooltip("Replicated flag — UI binders only show the Power bar when this is true.")]
        public NetworkVariable<bool> PresencePowerEnabled = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public float MaxPresencePower = 100f;
        [Tooltip("Maximum Flat add applied to WeaponDamageMultiplier at full Power. 0.5 = +50% damage at 100/100.")]
        public float MaxPresenceDamageBonus = 0.5f;
        [Tooltip("Presence Power lost per point of damage taken. 0.2 = 10 power lost per 50 damage. " +
                 "Power only decays from getting hit — it does NOT bleed off over time.")]
        public float PresencePowerLossPerDamage = 0.2f;
        // Conversion factor from "burden that the filter shaved off" to Presence Power. Set
        // by BurdenFilterItem.ApplyPassive based on stack count. Zero by default.
        private float _powerConversionRate;

        [Header("Decay Rates (Per Second)")]
        [SerializeField] private float decayTier1 = 5.0f;  // 0-25%: Fast
        [SerializeField] private float decayTier2 = 2.5f;  // 25-50%: Moderate
        [SerializeField] private float decayTier3 = 0.8f;  // 50-80%: Slow
        [SerializeField] private float decayTier4 = 0.1f;  // 80%+: Almost unnoticeable

        [Header("Damage Over Time Settings")]
        [SerializeField] private float dotTickInterval = 1.0f;
        [SerializeField] private float percentMaxHealthPerStack = 0.01f; // 1% per stack
        [SerializeField] private float tier4EscalationRate = 5f; // Every 5 burden over 80 adds another stack

        private CharacterStats _stats;
        
        private float _dotTimer = 0f;
        private bool _hasMutatedInTier4 = false;

        public float AccumulationMultiplier = 1f;
        public float DecayMultiplier = 1f;
        public bool SuppressPenalties = false;

        private void OnDestroy()
        {
            // Defensive cleanup — if we get destroyed while the Presence Power mod is still
            // installed, clear it so the damage buff doesn't outlive this controller.
            ClearPresencePowerDamageModifier();
        }

        private _Project.Core.Events.CharacterEventBus _eventBus;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                _eventBus = GetComponent<_Project.Core.Events.CharacterEventBus>();
                if (_eventBus != null) _eventBus.OnDamageTaken += HandleDamageTakenForPower;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (_eventBus != null)
            {
                _eventBus.OnDamageTaken -= HandleDamageTakenForPower;
                _eventBus = null;
            }
        }

        private void Update()
        {
            // SECURITY: Only the server processes decay, DoT, and Mutations
            if (!IsServer || _stats.IsDead) return;

            ProcessDecay();
            ProcessDoT();
            CheckMutationTrigger();
            ProcessPresencePower();
        }

        private void ProcessPresencePower()
        {
            if (!PresencePowerEnabled.Value) return;

            // No time-based decay anymore — power persists until the player takes damage
            // (handled by HandleDamageTakenForPower wired up via CharacterEventBus).
            RefreshPresencePowerDamageModifier();
        }

        private void HandleDamageTakenForPower(_Project.Core.Structs.DamageContext ctx)
        {
            if (!IsServer) return;
            if (!PresencePowerEnabled.Value) return;
            if (CurrentPresencePower.Value <= 0f) return;
            if (ctx.Damage <= 0) return;

            float loss = ctx.Damage * PresencePowerLossPerDamage;
            CurrentPresencePower.Value = Mathf.Max(0f, CurrentPresencePower.Value - loss);
        }

        public void AddBurden(float amount)
        {
            if (!IsServer) return;

            float effective = amount * AccumulationMultiplier;
            CurrentBurden.Value += effective;

            // Burden Filter conversion: the burden the filter "shaved off" (the difference
            // between the raw amount and what actually landed) becomes Presence Power, scaled
            // by the conversion rate the item set when applied.
            if (PresencePowerEnabled.Value && _powerConversionRate > 0f)
            {
                float filtered = Mathf.Max(0f, amount - effective);
                if (filtered > 0f)
                {
                    CurrentPresencePower.Value = Mathf.Min(MaxPresencePower, CurrentPresencePower.Value + filtered * _powerConversionRate);
                }
            }
        }

        /// <summary>
        /// Called by BurdenFilterItem on Thrive players. Enables the Power bar + state, sets
        /// the conversion factor from "filtered burden" to "Presence Power" gained per stack.
        /// </summary>
        public void EnablePresencePower(float conversionRate)
        {
            if (!IsServer) return;
            PresencePowerEnabled.Value = true;
            _powerConversionRate = Mathf.Max(0f, conversionRate);
        }

        /// <summary>
        /// Called by BurdenFilterItem when removed. Disables the bar, drains current power
        /// to zero, and clears the WeaponDamageMultiplier modifier so the buff doesn't leak.
        /// </summary>
        public void DisablePresencePower()
        {
            if (!IsServer) return;
            PresencePowerEnabled.Value = false;
            _powerConversionRate = 0f;
            CurrentPresencePower.Value = 0f;
            ClearPresencePowerDamageModifier();
        }

        // Tracks the current WeaponDamageMultiplier modifier so we can swap it cleanly each
        // frame as power changes. CharacterStats.RemoveModifier uses value equality, so we
        // hold onto the exact struct we registered.
        private CharacterStats _statsCached;
        private _Project.Core.Structs.StatModifier _appliedPowerMod;
        private bool _powerModApplied;

        private void RefreshPresencePowerDamageModifier()
        {
            if (_statsCached == null) _statsCached = _stats;
            if (_statsCached == null) return;

            float fraction = Mathf.Clamp01(CurrentPresencePower.Value / Mathf.Max(0.01f, MaxPresencePower));
            float targetBonus = fraction * MaxPresenceDamageBonus;

            if (_powerModApplied)
            {
                if (Mathf.Approximately(_appliedPowerMod.Value, targetBonus)) return;
                _statsCached.RemoveModifier(_Project.Core.Enums.StatType.WeaponDamageMultiplier, _appliedPowerMod);
                _powerModApplied = false;
            }

            if (targetBonus > 0.0001f)
            {
                _appliedPowerMod = new _Project.Core.Structs.StatModifier(_Project.Core.Enums.StatModType.Flat, targetBonus);
                _statsCached.AddModifier(_Project.Core.Enums.StatType.WeaponDamageMultiplier, _appliedPowerMod);
                _powerModApplied = true;
            }
        }

        private void ClearPresencePowerDamageModifier()
        {
            if (_powerModApplied && _statsCached != null)
            {
                _statsCached.RemoveModifier(_Project.Core.Enums.StatType.WeaponDamageMultiplier, _appliedPowerMod);
                _powerModApplied = false;
            }
        }

        public void RemoveBurden(float amount)
        {
            if (IsServer) CurrentBurden.Value = Mathf.Max(0f, CurrentBurden.Value - amount);
        }

        public float GetCurrentBurden() => CurrentBurden.Value;

        private void ProcessDecay()
        {
            if (CurrentBurden.Value <= 0) return;

            float decayRate = 0f;
            float b = CurrentBurden.Value;

            if (b <= 25f) decayRate = decayTier1;
            else if (b <= 50f) decayRate = decayTier2;
            else if (b <= 80f) decayRate = decayTier3;
            else decayRate = decayTier4;

            CurrentBurden.Value = Mathf.Max(0f, CurrentBurden.Value - (decayRate * DecayMultiplier * Time.deltaTime));
        }

        private void ProcessDoT()
        {
            if (SuppressPenalties) return;
        
            _dotTimer += Time.deltaTime;
            if (_dotTimer < dotTickInterval) return;
            
            _dotTimer = 0f;
            int stacks = CalculateBurdenStacks();

            if (stacks > 0)
            {
                float maxHealth = _stats.GetStat(StatType.MaxHealth);
                int damageAmount = Mathf.Max(1, Mathf.RoundToInt(maxHealth * percentMaxHealthPerStack * stacks));

                DamageContext dotCtx = new DamageContext
                {
                    Source = this.gameObject,
                    Target = this.gameObject,
                    Damage = damageAmount,
                    DamageType = DamageType.True, // True damage to bypass armor
                    // Burden: leaks through Shield (only Barrier/health stop it) and is ignored by
                    // proc items (no longer accidentally proccable as a generic Effect hit).
                    AttackType = AttackType.Burden
                };

                _stats.TakeDamage(dotCtx);
            }
        }

        private int CalculateBurdenStacks()
        {
            float b = CurrentBurden.Value;
            if (b < 25f) return 0;       // Tier 1: Safe Zone
            if (b < 50f) return 1;       // Tier 2: 1 Stack
            if (b < 80f) return 2;       // Tier 3: 2 Stacks
            
            // Tier 4: Base 3 stacks + 1 extra stack for every 'tier4EscalationRate' above 80
            int extraStacks = Mathf.FloorToInt((b - 80f) / tier4EscalationRate);
            return 3 + extraStacks;
        }

        private void CheckMutationTrigger()
        {
            float b = CurrentBurden.Value;

            // Trigger mutation once when crossing into Tier 4
            if (b >= 80f && !_hasMutatedInTier4)
            {
                _hasMutatedInTier4 = true;
                MutateRandomItem();
            }
            // Reset the lock if we drop back down, allowing for a future mutation
            else if (b < 80f && _hasMutatedInTier4)
            {
                _hasMutatedInTier4 = false;
            }
        }

        private void MutateRandomItem()
        {
            if (TryGetComponent(out IItemMutator mutator))
            {
                Debug.Log("[SERVER] Burden breached 80%! Requesting item mutation...");
                mutator.TriggerRandomMutation();
            }
        }
    }
}