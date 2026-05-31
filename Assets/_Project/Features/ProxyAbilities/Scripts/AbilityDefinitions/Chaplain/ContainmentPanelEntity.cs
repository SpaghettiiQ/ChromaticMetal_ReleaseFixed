using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Core.Stats;
using _Project.Core.Enums;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Chaplain
{
    [RequireComponent(typeof(BoxCollider))]
    public class ContainmentPanelEntity : NetworkBehaviour
    {
        [Header("Panel Settings")]
        [SerializeField] private float lifetime = 10f;
        [Tooltip("How long the stun lingers after an enemy leaves the panel volume. Re-applied " +
                 "continuously while they remain inside.")]
        [SerializeField] private float stunDuration = 1f;
        [Tooltip("Seconds between server-side overlap scans. Lower = snappier re-stun, more cost.")]
        [SerializeField] private float scanInterval = 0.15f;

        private TeamAffiliation _ownerTeam = TeamAffiliation.None;
        private BoxCollider _box;
        private float _nextScanTime;
        // Reusable buffers so the per-tick scan doesn't allocate.
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private readonly System.Collections.Generic.HashSet<CharacterStats> _stunnedThisTick = new();

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
        }

        public void Initialize(GameObject owner)
        {
            if (owner != null && owner.TryGetComponent(out CharacterStats ownerStats))
            {
                _ownerTeam = ownerStats.Team;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Invoke(nameof(DespawnSelf), lifetime);
                // Stun anyone already standing in the volume on this same frame instead of
                // waiting for them to step out and back in.
                ScanAndStun();
                _nextScanTime = Time.time + scanInterval;
            }
        }

        private void Update()
        {
            if (!IsServer) return;
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + scanInterval;
            ScanAndStun();
        }

        private void ScanAndStun()
        {
            if (_box == null) return;

            // Box's world-space oriented bounds. We avoid relying on OnTrigger callbacks because
            // NavMeshAgent enemies don't carry a Rigidbody, so Unity's trigger system can miss
            // them entirely depending on collider setup.
            Vector3 worldCenter = transform.TransformPoint(_box.center);
            Vector3 halfExtents = Vector3.Scale(_box.size * 0.5f, transform.lossyScale);
            // Inflate slightly so the volume actually reaches enemies grazing the edge.
            halfExtents += Vector3.one * 0.05f;

            int count = Physics.OverlapBoxNonAlloc(worldCenter, halfExtents, _overlapBuffer, transform.rotation, ~0, QueryTriggerInteraction.Ignore);

            _stunnedThisTick.Clear();
            for (int i = 0; i < count; i++)
            {
                var hit = _overlapBuffer[i];
                if (hit == null) continue;

                CharacterStats targetStats = hit.GetComponentInParent<CharacterStats>();
                if (targetStats == null || targetStats.IsDead) continue;
                if (!_stunnedThisTick.Add(targetStats)) continue; // Dedupe per character root.

                // Can't stun teammates (or the chaplain themselves).
                if (_ownerTeam != TeamAffiliation.None && targetStats.Team == _ownerTeam) continue;

                IStunnable stunnable = targetStats.GetComponent<IStunnable>();
                if (stunnable != null)
                {
                    // EnemyControllerBase.Stun extends _stunEndTime monotonically, so re-applying
                    // every tick keeps targets locked down as long as they're in the volume.
                    stunnable.Stun(stunDuration);
                }
            }
        }

        private void DespawnSelf()
        {
            if (TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
            {
                netObj.Despawn();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
