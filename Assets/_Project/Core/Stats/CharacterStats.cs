using System;
using System.Collections.Generic;
using Unity.Netcode; // Added Netcode
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Structs;

namespace _Project.Core.Stats
{
    // CHANGED: Inherit from NetworkBehaviour
    public class CharacterStats : NetworkBehaviour 
    {
        public event Action<int, int> OnHealthChanged; // Current, Max
        public event Action<DamageContext> OnDamageTaken;
        public event Action OnDeath;

        // CHANGED: Converted to NetworkVariables. Read by everyone, Written ONLY by the Server.
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(0, 
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            
        public NetworkVariable<int> CurrentMoney = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Replicated MaxHealth so clients can render HP UI correctly. (The server's
        // _baseStats / _modifiers dicts aren't synced — clients can't compute it locally.)
        public NetworkVariable<int> NetMaxHealth = new NetworkVariable<int>(100,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Overshield pools (server-authoritative, replicated to clients so they render off-host
        // — sidesteps the latent bug where _baseStats/_modifiers aren't synced). Both sit in
        // FRONT of CurrentHealth in TakeDamage. Shield (blue) recharges out of combat and does
        // not decay; Barrier (amber) decays over time (see Update). Per-item caps are the item's
        // responsibility — Core just owns the pools and the absorption order.
        public NetworkVariable<int> CurrentShield = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> CurrentBarrier = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Tooltip("Barrier (amber overshield) lost per second while above zero.")]
        [SerializeField] private float barrierDecayPerSecond = 8f;
        private float _barrierDecayAccumulator;

        /// <summary>Use this from UI / non-server code instead of GetStat(MaxHealth) — works on clients.</summary>
        public int GetEffectiveMaxHealth() => IsServer ? (int)GetStat(StatType.MaxHealth) : NetMaxHealth.Value;

        // Updated to read the .Value
        public bool IsDead => CurrentHealth.Value <= 0;

        public TeamAffiliation Team = TeamAffiliation.None;

        private Dictionary<StatType, float> _baseStats = new();
        private Dictionary<StatType, List<StatModifier>> _modifiers = new();

        // --- Stat replication (fixes the latent "stats not synced to clients" bug) ---
        // The _baseStats/_modifiers dicts are server-only, so a non-host client's GetStat returns 0.
        // We replicate the computed TOTAL of the stats that are actually read owner/client-side, so
        // GetStat works on remote clients too. Only these are replicated to keep the footprint tiny
        // (enemies have CharacterStats as well). MaxHealth is excluded — it already has NetMaxHealth.
        private static readonly HashSet<StatType> ClientRelevantStats = new()
        {
            StatType.MovementSpeed, StatType.AttackSpeed, StatType.ReloadSpeedMultiplier,
            StatType.RecoilReductionMultiplier, StatType.ExtraJumps, StatType.SpecialCharges
        };
        private NetworkList<StatKV> _netStats;
        private readonly Dictionary<StatType, float> _replicatedStats = new();

        private void Awake()
        {
            // NGO requires NetworkList instances to be created in Awake (mirrors CharacterInventory).
            _netStats = new NetworkList<StatKV>();
        }

        // NEW: Listen for network changes to update local events (like UI)
        public override void OnNetworkSpawn()
        {
            CurrentHealth.OnValueChanged += HandleHealthChanged;

            // Clients mirror the server's replicated stat totals into a local cache for GetStat.
            // (Host/server reads from _baseStats/_modifiers directly, so it skips this.)
            if (!IsServer)
            {
                _netStats.OnListChanged += HandleNetStatsChanged;
                RebuildReplicatedStats(); // late-join: list is already synced by now
            }

            if(IsServer) Initialize(100);
        }

        public override void OnNetworkDespawn()
        {
            CurrentHealth.OnValueChanged -= HandleHealthChanged;
            if (!IsServer && _netStats != null) _netStats.OnListChanged -= HandleNetStatsChanged;
        }

        private void HandleNetStatsChanged(NetworkListEvent<StatKV> _) => RebuildReplicatedStats();

        private void RebuildReplicatedStats()
        {
            _replicatedStats.Clear();
            for (int i = 0; i < _netStats.Count; i++)
            {
                var kv = _netStats[i];
                _replicatedStats[(StatType)kv.Stat] = kv.Value;
            }
        }

        // Server-only: push the computed total of a client-relevant stat into the replicated list.
        private void ReplicateStat(StatType stat)
        {
            if (!IsServer || !IsSpawned) return;
            if (!ClientRelevantStats.Contains(stat)) return;

            float total = ComputeStat(stat);
            int key = (int)stat;
            for (int i = 0; i < _netStats.Count; i++)
            {
                if (_netStats[i].Stat == key)
                {
                    if (!_netStats[i].Value.Equals(total))
                        _netStats[i] = new StatKV { Stat = key, Value = total };
                    return;
                }
            }
            _netStats.Add(new StatKV { Stat = key, Value = total });
        }

        private void HandleHealthChanged(int previousValue, int newValue)
        {
            float maxHealth = GetEffectiveMaxHealth();
            OnHealthChanged?.Invoke(newValue, (int)maxHealth);

            if (newValue <= 0 && previousValue > 0)
            {
                OnDeath?.Invoke();
            }

            // Raise LowHealth condition when crossing the 25% threshold in either direction
            if (maxHealth > 0 && TryGetComponent(out _Project.Core.Events.CharacterEventBus bus))
            {
                bool isNowLow  = newValue  > 0 && (float)newValue  / maxHealth <= 0.25f;
                bool wasLow    = previousValue > 0 && (float)previousValue / maxHealth <= 0.25f;
                if (isNowLow != wasLow)
                    bus.RaiseOnConditionChanged(_Project.Core.Enums.ConditionType.LowHealth, isNowLow);
            }
        }

        public void Initialize(int maxHealth)
        {
            SetBaseStat(StatType.MaxHealth, maxHealth);
            // Multiplier-style stats: base of 1 so Percent modifiers compound cleanly via (base + flat) * (1 + percentSum).
            SetBaseStat(StatType.MovementSpeed, 1f);
            SetBaseStat(StatType.AttackSpeed, 1f);
            // Default crits do 2x damage; items / abilities add Flat to push it higher.
            SetBaseStat(StatType.CritDamageMultiplier, 2f);
            // WeaponDamageMultiplier seeded at 1 (identity) — Presence Power adds Flat on top.
            SetBaseStat(StatType.WeaponDamageMultiplier, 1f);
            // FireDamageMultiplier seeded at 1 (identity) — Thermite Fuel adds Percent on top.
            SetBaseStat(StatType.FireDamageMultiplier, 1f);
            // Seeded at 1 (identity) so items can stack multiplicatively via Flat modifiers:
            // ReloadSpeedMultiplier (Autoloader), ContainerCostMultiplier (Discount Chip).
            SetBaseStat(StatType.ReloadSpeedMultiplier, 1f);
            SetBaseStat(StatType.ContainerCostMultiplier, 1f);

            // Only the server can set initial network values
            if (IsServer)
            {
                CurrentHealth.Value = maxHealth;
                CurrentMoney.Value = 0;
                NetMaxHealth.Value = maxHealth;
            }

            OnHealthChanged?.Invoke(CurrentHealth.Value, maxHealth);
        }

        public ulong LastDamageDealerId { get; private set; }

        // Fully restores health, bypassing the IsDead guard — used for stage-start revives.
        public void ReviveAndHeal()
        {
            if (!IsServer) return;
            CurrentHealth.Value = (int)GetStat(StatType.MaxHealth);
        }

        /// <summary>Heals up to MaxHealth and returns the clipped overheal (requested minus applied).
        /// Items like Excess Membrane convert that wasted portion into Barrier.</summary>
        public int Heal(int amount)
        {
            // SECURITY: Only the server calculates and applies healing
            if (!IsServer || IsDead) return 0;

            int maxHealth = (int)GetStat(StatType.MaxHealth);
            int before = CurrentHealth.Value;
            CurrentHealth.Value = Mathf.Min(before + amount, maxHealth);
            int applied = CurrentHealth.Value - before;
            int overheal = Mathf.Max(0, (before + amount) - maxHealth);

            // Let items react to any heal (Excess Membrane converts overheal → Barrier).
            if (amount > 0 && TryGetComponent(out _Project.Core.Events.CharacterEventBus bus))
                bus.RaiseOnHealed(applied, overheal);

            return overheal;
        }

        // --- Overshield mutators (server-only). Callers enforce their own caps. ---
        public void AddShield(int amount)  { if (!IsServer) return; CurrentShield.Value  = Mathf.Max(0, CurrentShield.Value  + amount); }
        public void SetShield(int value)   { if (!IsServer) return; CurrentShield.Value  = Mathf.Max(0, value); }
        public void AddBarrier(int amount) { if (!IsServer) return; CurrentBarrier.Value = Mathf.Max(0, CurrentBarrier.Value + amount); }

        // Barrier (amber) decays toward zero on the server. Float accumulator keeps the integer
        // pool framerate-independent. Shield (blue) intentionally does NOT decay here — its
        // out-of-combat recharge is owned by the Ward Projector item.
        private void Update()
        {
            if (!IsServer) return;
            if (CurrentBarrier.Value <= 0) { _barrierDecayAccumulator = 0f; return; }

            _barrierDecayAccumulator += barrierDecayPerSecond * Time.deltaTime;
            if (_barrierDecayAccumulator >= 1f)
            {
                int dec = Mathf.FloorToInt(_barrierDecayAccumulator);
                _barrierDecayAccumulator -= dec;
                CurrentBarrier.Value = Mathf.Max(0, CurrentBarrier.Value - dec);
            }
        }

        public void TakeDamage(DamageContext ctx)
        {
            // SECURITY: Only the server calculates and applies damage
            if (!IsServer || IsDead) return;

            // Phase isolation: in PvP PvE stages, opposing-team players are physically near
            // each other but should not be able to damage cross-shard. Reject early if the
            // attacker's phase is incompatible with ours.
            if (ctx.Source != null)
            {
                var srcPhased = ctx.Source.GetComponentInParent<_Project.Core.Networking.PhasedNetworkObject>();
                var myPhased = GetComponent<_Project.Core.Networking.PhasedNetworkObject>();
                Debug.Log($"[TakeDamage] target='{name}' tgtPhase={(myPhased != null ? myPhased.Phase.ToString() : "<null>")} " +
                          $"src='{ctx.Source.name}' srcPhase={(srcPhased != null ? srcPhased.Phase.ToString() : "<null>")} " +
                          $"dmg={ctx.Damage}");
                if (srcPhased != null && myPhased != null && !srcPhased.Phase.VisibleTo(myPhased.Phase))
                {
                    Debug.Log($"[TakeDamage] REJECTED by phase mismatch.");
                    return;
                }
            }

            if (TryGetComponent(out _Project.Core.Events.CharacterEventBus bus))
            {
                bus.RaiseOnBeforeTakeDamage(ref ctx);
            }

            // Crit roll: skips True damage, DOT ticks (bleed/burn/poison shouldn't randomly crit),
            // already-crit contexts (so explicit forced-crit items still honor their own multiplier
            // choices), and self-damage. Reads from the attacker's CritChance / CritDamageMultiplier
            // stats so any source that goes through TakeDamage benefits without each damage path
            // needing its own roll.
            if (ctx.DamageType != DamageType.True && ctx.AttackType != AttackType.DamageOverTime &&
                !ctx.IsCritical && ctx.Source != null && ctx.Source != gameObject)
            {
                CharacterStats attackerStats = ctx.Source.GetComponentInParent<CharacterStats>();
                if (attackerStats != null && attackerStats != this)
                {
                    float critChance = attackerStats.GetStat(StatType.CritChance);
                    if (critChance > 0f && UnityEngine.Random.value < critChance)
                    {
                        ctx.IsCritical = true;
                        float critMult = attackerStats.GetStat(StatType.CritDamageMultiplier);
                        if (critMult <= 0f) critMult = 2f; // Safety in case the stat wasn't seeded.
                        ctx.Damage = Mathf.Max(1, Mathf.RoundToInt(ctx.Damage * critMult));
                    }
                }
            }

            // Attacker's WeaponDamageMultiplier — applied to "direct" attacks only. Item-proc
            // damage is tagged AttackType.Secondary; we skip those so the multiplier doesn't
            // double-stack on chain/ricochet hits. Conduit's Presence Power scales this stat.
            if (ctx.AttackType != AttackType.Secondary && ctx.Source != null && ctx.Source != gameObject)
            {
                CharacterStats attackerStats = ctx.Source.GetComponentInParent<CharacterStats>();
                if (attackerStats != null && attackerStats != this)
                {
                    float weaponMult = attackerStats.GetStat(StatType.WeaponDamageMultiplier);
                    if (weaponMult > 0f && Mathf.Abs(weaponMult - 1f) > 0.001f)
                    {
                        ctx.Damage = Mathf.Max(1, Mathf.RoundToInt(ctx.Damage * weaponMult));
                    }
                }
            }

            // Attacker's FireDamageMultiplier — amplifies Fire damage only (Thermite Fuel). Applied
            // centrally so every fire source (currently Molotov's burn DOT) benefits, including DOT
            // ticks. Read off the attacker's stats on the server, so it's immune to the off-host
            // stat-replication bug.
            if (ctx.DamageType == DamageType.Fire && ctx.Source != null && ctx.Source != gameObject)
            {
                CharacterStats attackerStats = ctx.Source.GetComponentInParent<CharacterStats>();
                if (attackerStats != null && attackerStats != this)
                {
                    float fireMult = attackerStats.GetStat(StatType.FireDamageMultiplier);
                    if (fireMult > 0f && Mathf.Abs(fireMult - 1f) > 0.001f)
                    {
                        ctx.Damage = Mathf.Max(1, Mathf.RoundToInt(ctx.Damage * fireMult));
                    }
                }
            }

            // NEW: Bypass armor calculations if it's True Damage
            if (ctx.DamageType != DamageType.True)
            {
                float damageResistance = GetStat(StatType.DamageReduction);
                if (damageResistance > 0f)
                {
                    float multiplier = 1f - Mathf.Clamp01(damageResistance / 100f);
                    ctx.Damage = Mathf.Max(1, Mathf.RoundToInt(ctx.Damage * multiplier));
                }

                float vulnerability = GetStat(StatType.DamageVulnerability);
                if (vulnerability > 0f)
                {
                    ctx.Damage = Mathf.RoundToInt(ctx.Damage * (1f + vulnerability));
                }
            }

            // Overshield absorption. Barrier (amber, temporary/decaying) drains first and absorbs
            // EVERYTHING. Shield (blue, persistent/recharging) drains next but only stops "direct"
            // hits — Bleed and Burden DOTs LEAK THROUGH it straight to health (after Barrier).
            // Leftover hits health last.
            bool leaksShield = ctx.DamageType == DamageType.Bleed || ctx.AttackType == AttackType.Burden;
            int remaining = ctx.Damage;
            if (CurrentBarrier.Value > 0 && remaining > 0)
            {
                int absorbed = Mathf.Min(CurrentBarrier.Value, remaining);
                CurrentBarrier.Value -= absorbed;
                remaining -= absorbed;
            }
            if (!leaksShield && CurrentShield.Value > 0 && remaining > 0)
            {
                int absorbed = Mathf.Min(CurrentShield.Value, remaining);
                CurrentShield.Value -= absorbed;
                remaining -= absorbed;
            }

            CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - remaining);
            bool wasKilled = CurrentHealth.Value <= 0;

            // Extract the NetworkObjectId from the Source GameObject (if it exists)
            ulong sourceNetworkId = 0;
            if (ctx.Source != null && ctx.Source.TryGetComponent<NetworkObject>(out var sourceNetObj))
            {
                sourceNetworkId = sourceNetObj.NetworkObjectId;
            }

            LastDamageDealerId = sourceNetworkId;

            // Tell all clients that damage was taken using network-safe primitive types!
            TakeDamageClientRpc(sourceNetworkId, ctx.Damage, ctx.DamageType, ctx.AttackType, ctx.IsCritical, wasKilled);
        }

        // We replaced DamageContext with its basic parts so NGO can serialize it natively
        [ClientRpc]
        private void TakeDamageClientRpc(ulong sourceNetworkId, int damage, DamageType damageType, AttackType attackType, bool isCritical, bool wasKilled)
        {
            // Attempt to find the Source GameObject on this client using the Network ID
            GameObject sourceGo = null;
            if (sourceNetworkId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(sourceNetworkId, out NetworkObject sourceObj))
            {
                sourceGo = sourceObj.gameObject;
            }

            // Rebuild the pure DamageContext locally
            DamageContext localCtx = new DamageContext
            {
                Source = sourceGo,
                Target = gameObject,
                Damage = damage,
                DamageType = damageType,
                AttackType = attackType,
                IsCritical = isCritical
            };

            OnDamageTaken?.Invoke(localCtx);

            if (TryGetComponent(out _Project.Core.Events.CharacterEventBus targetBus))
            {
                targetBus.RaiseOnDamageTaken(localCtx);
            }

            if (sourceGo != null && sourceGo.TryGetComponent(out _Project.Core.Events.CharacterEventBus attackerBus))
            {
                attackerBus.RaiseOnHit(localCtx);
                if (wasKilled) attackerBus.RaiseOnKill(localCtx);
            }

            RaiseLocalSfxFeedback(localCtx, sourceGo, wasKilled);
        }

        private void RaiseLocalSfxFeedback(DamageContext ctx, GameObject sourceGo, bool wasKilled)
        {
            var netManager = NetworkManager.Singleton;
            if (netManager == null) return;

            ulong localId = netManager.LocalClientId;

            bool localIsTarget = TryGetComponent<NetworkObject>(out var targetNet)
                                 && targetNet.IsOwner;
            bool localIsSource = sourceGo != null
                                 && sourceGo.TryGetComponent<NetworkObject>(out var sourceNet)
                                 && sourceNet.OwnerClientId == localId;

            if (localIsSource)
            {
                _Project.Core.Events.GameFeelEvents.OnLocalPlayerHit?.Invoke(ctx, wasKilled);
            }
            if (localIsTarget)
            {
                _Project.Core.Events.GameFeelEvents.OnLocalPlayerHurt?.Invoke(ctx);
            }
        }

        public void SetBaseStat(StatType stat, float value)
        {
            _baseStats[stat] = value;
            if (stat == StatType.MaxHealth) RefreshNetMaxHealth();
            ReplicateStat(stat);
        }

        public void AddModifier(StatType stat, StatModifier modifier)
        {
            if (!_modifiers.ContainsKey(stat)) _modifiers[stat] = new List<StatModifier>();
            _modifiers[stat].Add(modifier);
            if (stat == StatType.MaxHealth) RefreshNetMaxHealth();
            ReplicateStat(stat);
        }

        public void RemoveModifier(StatType stat, StatModifier modifier)
        {
            if (_modifiers.ContainsKey(stat)) _modifiers[stat].Remove(modifier);
            if (stat == StatType.MaxHealth) RefreshNetMaxHealth();
            ReplicateStat(stat);
        }

        private void RefreshNetMaxHealth()
        {
            if (!IsServer || !IsSpawned) return;
            int mh = (int)GetStat(StatType.MaxHealth);
            if (NetMaxHealth.Value != mh) NetMaxHealth.Value = mh;
        }

        public float GetStat(StatType stat)
        {
            // Server computes from the authoritative dicts. Clients read the replicated total for
            // client-relevant stats; any other stat returns 0 on clients exactly as before (those
            // are only ever read server-side anyway).
            if (IsServer) return ComputeStat(stat);
            return _replicatedStats.TryGetValue(stat, out var v) ? v : 0f;
        }

        private float ComputeStat(StatType stat)
        {
            float baseValue = _baseStats.TryGetValue(stat, out var val) ? val : 0f;
            if (!_modifiers.ContainsKey(stat)) return baseValue;

            float flatAdd = 0f;
            float percentMult = 1f;

            foreach (var mod in _modifiers[stat])
            {
                if (mod.Type == StatModType.Flat) flatAdd += mod.Value;
                else if (mod.Type == StatModType.Percent) percentMult += mod.Value;
            }

            return (baseValue + flatAdd) * percentMult;
        }

        // Updated money functions to respect Server authority and .Value
        public void AddMoney(int amount) 
        { 
            if (IsServer) 
            {
                float multiplier = GetStat(StatType.MoneyMultiplier);
                if (multiplier == 0f) multiplier = 1f; // default
                CurrentMoney.Value += Mathf.RoundToInt(amount * multiplier); 
            }
        }
        
        public void RemoveMoney(int amount) 
        { 
            if (IsServer) CurrentMoney.Value = Mathf.Max(0, CurrentMoney.Value - amount); 
        }
    }
}