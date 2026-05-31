using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;

namespace _Project.Features.BurdenSystem.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class ResidueZone : NetworkBehaviour
    {
        [Header("Zone Settings")]
        [Tooltip("Standard damage dealt per tick")]
        [SerializeField] private int damagePerTick = 5;
        [Tooltip("Amount of Burden added per tick")]
        [SerializeField] private float burdenPerTick = 10f;
        [Tooltip("How often (in seconds) the zone applies its effects")]
        [SerializeField] private float tickInterval = 0.5f;
        [Tooltip("If true, this zone was created by an ability — characters on the configured " +
                 "skip team take no damage (burden still applies). Leave false for environmental " +
                 "residue that hurts everyone equally.")]
        [SerializeField] private bool originatesFromAbility = false;
        [Tooltip("Which team's members are immune to this zone's damage when originatesFromAbility " +
                 "is true. Defaults to Thrive (Of The Abyss). Can be overridden at runtime by the " +
                 "spawning ability via MarkAsAbilityOriginated().")]
        [SerializeField] private TeamAffiliation skipDamageTeam = TeamAffiliation.Thrive;

        /// <summary>
        /// Server-side runtime override called by abilities when they spawn the residue. Forces
        /// originatesFromAbility on regardless of the prefab's checkbox state and pins the
        /// skipDamage team to the caster's team — so the friendly-fire shield always tracks the
        /// actual ability owner instead of relying on prefab config.
        /// </summary>
        public void MarkAsAbilityOriginated(TeamAffiliation casterTeam)
        {
            originatesFromAbility = true;
            skipDamageTeam = casterTeam;
        }

        // We use a HashSet to track who is currently standing in the zone
        private HashSet<GameObject> _entitiesInZone = new HashSet<GameObject>();
        private float _tickTimer;

        public override void OnNetworkSpawn()
        {
            // SECURITY: Clients don't need to process physics triggers for this zone.
            // All damage and burden calculations are Server-Authoritative.
            if (!IsServer)
            {
                var col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            _entitiesInZone.Add(other.gameObject);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer) return;
            _entitiesInZone.Remove(other.gameObject);
        }

        private void Update()
        {
            if (!IsServer || _entitiesInZone.Count == 0) return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer >= tickInterval)
            {
                _tickTimer = 0f;
                ApplyZoneEffects();
            }
        }

        private void ApplyZoneEffects()
        {
            // Clean up any null references in case an entity was destroyed while inside the zone
            _entitiesInZone.RemoveWhere(obj => obj == null);

            foreach (var entity in _entitiesInZone)
            {
                // 1. Apply the Environmental Damage using the DamageType.Burden we have in our enum.
                //    Walk up to the character root so colliders sitting on hitbox children still
                //    resolve to the actual CharacterStats — TryGetComponent only looks on the
                //    same GameObject, which silently dropped the team check on rigs where the
                //    collider isn't on the root.
                CharacterStats stats = entity.GetComponentInParent<CharacterStats>();
                bool friendlyToCaster = false;
                if (stats != null && !stats.IsDead)
                {
                    // Ability-spawned residue (Of The Abyss, etc.) skips BOTH damage AND burden
                    // on its caster's team. Skipping only damage isn't enough — burden's own
                    // DoT (BurdenController.ProcessDoT) deals true damage once a target crosses
                    // 25% accumulation, which let the trail hurt the caster's team via a side door.
                    friendlyToCaster = originatesFromAbility && stats.Team == skipDamageTeam;
                    if (!friendlyToCaster)
                    {
                        DamageContext ctx = new DamageContext
                        {
                            Source = this.gameObject,
                            Target = entity,
                            Damage = damagePerTick,
                            DamageType = DamageType.Presence,
                            AttackType = AttackType.Contact,
                            IsCritical = false
                        };
                        stats.TakeDamage(ctx);
                    }
                }

                // 2. Apply the Corruption/Burden Meter increase via our decoupled interface —
                //    but skip it for the caster's own team. Burden buildup turns into true-damage
                //    DoT at high enough tiers, so applying it to teammates breaks the friendly
                //    shield indirectly.
                if (!friendlyToCaster && entity.TryGetComponent<IBurdenable>(out var burdenable))
                {
                    burdenable.AddBurden(burdenPerTick);
                }
            }
        }
    }
}