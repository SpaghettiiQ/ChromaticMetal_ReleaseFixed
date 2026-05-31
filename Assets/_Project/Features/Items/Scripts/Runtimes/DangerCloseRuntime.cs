using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Events;
using _Project.Core.Stats;
using _Project.Core.Structs;

namespace _Project.Features.Items.Scripts.ItemDefinitions
{
    // Per-owner runtime for DangerCloseItem (missiles while the exit door charges).
    public class DangerCloseRuntime : ItemEffectBehaviour<DangerCloseItem>
    {
        private CharacterStats _stats;
        private Coroutine _sequence;

        protected override void OnActivate()
        {
            TryGetComponent(out _stats);
            GameFeelEvents.OnExtractionChargeStarted += HandleChargeStarted;
            GameFeelEvents.OnExtractionChargeStopped += HandleChargeStopped;
        }

        protected override void OnDeactivate()
        {
            GameFeelEvents.OnExtractionChargeStarted -= HandleChargeStarted;
            GameFeelEvents.OnExtractionChargeStopped -= HandleChargeStopped;
            StopSequence();
        }

        private void HandleChargeStarted()
        {
            // Server-authoritative: only the host spawns/deals the missiles.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            StopSequence();
            _sequence = StartCoroutine(FireSequence());
        }

        private void HandleChargeStopped() => StopSequence();

        private void StopSequence()
        {
            if (_sequence != null) { StopCoroutine(_sequence); _sequence = null; }
        }

        private IEnumerator FireSequence()
        {
            var wait = new WaitForSeconds(Config.missileInterval);
            for (int i = 0; i < Config.missileCount; i++)
            {
                yield return wait;
                if (TryPickTarget(out Vector3 targetPos))
                    StartCoroutine(MissileRoutine(targetPos));
            }
            _sequence = null;
        }

        // Pick a random live enemy within searchRadius; target its ground position.
        private bool TryPickTarget(out Vector3 targetPos)
        {
            targetPos = Vector3.zero;
            TeamAffiliation ownerTeam = _stats != null ? _stats.Team : TeamAffiliation.None;

            Collider[] hits = Physics.OverlapSphere(transform.position, Config.searchRadius);
            var candidates = new List<Vector3>();
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                var targetStats = hit.GetComponentInParent<CharacterStats>();
                if (targetStats == null || targetStats.IsDead) continue;
                if (ownerTeam != TeamAffiliation.None && targetStats.Team != TeamAffiliation.None && targetStats.Team == ownerTeam) continue;
                candidates.Add(targetStats.transform.position);
            }

            if (candidates.Count == 0) return false;
            targetPos = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        // Spawn above the target, fall straight down (no enemy collision), explode on ground impact.
        private IEnumerator MissileRoutine(Vector3 groundPos)
        {
            Vector3 pos = groundPos + Vector3.up * Config.spawnHeight;
            GameObject visual = Config.missilePrefab != null
                ? Instantiate(Config.missilePrefab, pos, Quaternion.LookRotation(Vector3.down))
                : null;

            while (pos.y > groundPos.y)
            {
                pos += Vector3.down * Config.missileSpeed * Time.deltaTime;
                if (visual != null) visual.transform.position = pos;
                yield return null;
            }

            if (visual != null) Destroy(visual);
            Explode(groundPos);
        }

        private void Explode(Vector3 center)
        {
            TeamAffiliation ownerTeam = _stats != null ? _stats.Team : TeamAffiliation.None;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Config.missileDamageBase * (1f + Config.missileDamageBonusPerStack * (Stacks - 1))));

            Collider[] hits = Physics.OverlapSphere(center, Config.aoeRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                var targetStats = hit.GetComponentInParent<CharacterStats>();
                if (targetStats == null || targetStats.IsDead) continue;
                if (ownerTeam != TeamAffiliation.None && targetStats.Team != TeamAffiliation.None && targetStats.Team == ownerTeam) continue;

                targetStats.TakeDamage(new DamageContext
                {
                    Source = gameObject,
                    Target = targetStats.gameObject,
                    Damage = damage,
                    DamageType = DamageType.Explosive,
                    AttackType = AttackType.MissileImpact,
                    IsCritical = false
                });
            }

            if (Config.explosionVfxPrefab != null)
            {
                GameObject vfx = Instantiate(Config.explosionVfxPrefab, center, Quaternion.identity);
                Destroy(vfx, 2f);
            }
        }
    }
}
