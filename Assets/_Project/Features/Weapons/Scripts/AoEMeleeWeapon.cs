using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations;
using _Project.Core.Audio;
using _Project.Core.Stats;
using _Project.Core.Enums;
using _Project.Core.Structs;
using _Project.Features.WeaponCore.Scripts;
using _Project.Features.Player.Scripts;

namespace _Project.Features.Weapons.Scripts
{
    public class AoEMeleeWeapon : NetworkWeapon
    {
        [Header("Melee Settings")]
        public float attackRadius = 3f;
        public float knockbackForce = 15f;
        public float forwardOffset = 1.5f;

        [Header("Audiovisual")]
        public ParticleSystem swingVfx;
        public AudioClip swingSound;

        [Header("Swing Visual (no animator — fakes a swing by tweening the weapon's ParentConstraint offset)")]
        [Tooltip("How far the maul translates to the left-hand pose, in metres (mount-local). Most of the visible sweep comes from yaw; this just shifts the whole weapon across.")]
        [SerializeField] private float swingSideOffset = 0.6f;
        [Tooltip("Forward push at the left-hand pose, in metres.")]
        [SerializeField] private float swingForwardOffset = 0.1f;
        [Tooltip("Yaw of the wide horizontal sweep, in degrees. Dominant motion — the maul pivots around the grip so the head sweeps across instead of sliding flat.")]
        [SerializeField] private float swingYawDegrees = 100f;
        [Tooltip("Roll of the left-hand pose, in degrees. Ramps in as the maul sweeps so it lays over around its own shaft and the TOP/head tips OUTWARD, then un-rolls coming back. Baked into the pose (not a bump) so forehand/backhand stay symmetric. Negate if it rolls the wrong way.")]
        [SerializeField] private float swingRollDegrees = 35f;
        [Tooltip("Extra roll laid in only DURING the sweep — peaks mid-swing, zero at the ends — on top of the pose roll, so the tip tilts further OUTWARD through the swing without changing the parked hold. Same sign as Swing Roll Degrees and symmetric, so it doesn't bring back the up/down mismatch.")]
        [SerializeField] private float swingSweepRollBoost = 30f;
        [Tooltip("How far the head dips DOWN through the middle of the sweep, in metres (symmetric both directions; back to level by the end).")]
        [SerializeField] private float swingArcHeight = 0.06f;
        [Tooltip("Small raise as the maul cocks back during the wind-up, in metres.")]
        [SerializeField] private float swingWindupUp = 0.1f;
        [Tooltip("Seconds of anticipation cock-back before the sweep.")]
        [SerializeField] private float swingWindupDuration = 0.06f;
        [Tooltip("Seconds of the fast horizontal sweep.")]
        [SerializeField] private float swingStrikeDuration = 0.1f;
        [Tooltip("Seconds to settle from the overshoot onto the parked pose.")]
        [SerializeField] private float swingSettleDuration = 0.08f;
        [Tooltip("Fraction of the sweep vector that the wind-up cocks back against (anticipation).")]
        [SerializeField] private float swingWindupBackFraction = 0.22f;
        [Tooltip("Extra Z pull-back during the windup, in metres (negative = toward the player).")]
        [SerializeField] private float swingWindupPullbackZ = 0.06f;
        [Tooltip("Fraction past the destination the sweep overshoots before settling.")]
        [SerializeField] private float swingStrikeOvershootFraction = 0.14f;

        [Header("Overhead Swing (triggered by abilities like Cleansing Strike)")]
        [Tooltip("Horizontal shift of the overhead arc relative to rest. Rest sits on the right, " +
                 "so a negative value pulls the swing toward (just right of) centre, in metres.")]
        [SerializeField] private float overheadHorizontalOffset = -0.18f;
        [Tooltip("How high the maul rises during the windup, in metres (mount-local).")]
        [SerializeField] private float overheadUpOffset = 0.4f;
        [Tooltip("Backward pull during windup, in metres (negative Z = pulled back).")]
        [SerializeField] private float overheadBackOffset = -0.15f;
        [Tooltip("Pitch up during windup, in degrees (negative X rotates the head back).")]
        [SerializeField] private float overheadWindupPitchDegrees = -60f;
        [Tooltip("Forward thrust at the bottom of the slam, in metres.")]
        [SerializeField] private float overheadStrikeForwardOffset = 0.35f;
        [Tooltip("Downward push at the bottom of the slam, in metres.")]
        [SerializeField] private float overheadStrikeDownOffset = -0.2f;
        [Tooltip("Pitch down at the bottom of the slam, in degrees.")]
        [SerializeField] private float overheadStrikePitchDegrees = 45f;
        [Tooltip("Seconds to raise the weapon overhead.")]
        [SerializeField] private float overheadWindupDuration = 0.2f;
        [Tooltip("Seconds for the slam from overhead to the impact pose.")]
        [SerializeField] private float overheadStrikeDuration = 0.1f;
        [Tooltip("Seconds to return from the impact pose to rest.")]
        [SerializeField] private float overheadReturnDuration = 0.2f;

        private ParentConstraint _swingConstraint;
        private Coroutine _swingRoutine;
        // Wide horizontal swing that parks at whichever side it ends on. `_atLeft` tracks the
        // current rest side: false = right-hand default pose, true = left extreme. Each attack
        // flips it and sweeps to the new side — so swings alternate right→left, then left→right.
        private bool _atLeft;
        private Vector3 _restPos;
        private Vector3 _restRot;
        private bool _restCaptured;

        protected override void PerformLocalCosmeticShot()
        {
            if (swingVfx != null) swingVfx.Play();
            if (swingSound != null && SfxManager.Instance != null)
            {
                SfxManager.Instance.PlayOneShot3D(swingSound, transform.position);
            }

            PlaySwingVisual();
        }

        private void PlaySwingVisual()
        {
            if (_swingConstraint == null)
            {
                // WeaponSpawner.RpcAttachWeaponClientRpc adds the ParentConstraint after spawn.
                // It may briefly not exist on the first frame after spawn; retry-fetch on each call.
                _swingConstraint = GetComponent<ParentConstraint>();
                if (_swingConstraint == null) return;
            }

            // Capture the spawn-time rest pose once so the return phase always lands back on the
            // weapon's original neutral offset (the prefab may not be perfectly zero).
            if (!_restCaptured)
            {
                _restPos = _swingConstraint.GetTranslationOffset(0);
                _restRot = _swingConstraint.GetRotationOffset(0);
                _restCaptured = true;
            }

            _atLeft = !_atLeft;

            if (_swingRoutine != null) StopCoroutine(_swingRoutine);
            _swingRoutine = StartCoroutine(SwingRoutine(_atLeft));
        }

        /// <summary>
        /// Server invokes to play the overhead-slam viewmodel sequence on every client.
        /// Used by abilities like Cleansing Strike that don't go through the normal fire path.
        /// </summary>
        [ClientRpc]
        public void PlayOverheadSwingClientRpc()
        {
            PlayOverheadSwingVisual();
        }

        private void PlayOverheadSwingVisual()
        {
            if (_swingConstraint == null)
            {
                _swingConstraint = GetComponent<ParentConstraint>();
                if (_swingConstraint == null) return;
            }
            if (!_restCaptured)
            {
                _restPos = _swingConstraint.GetTranslationOffset(0);
                _restRot = _swingConstraint.GetRotationOffset(0);
                _restCaptured = true;
            }

            // Overhead returns to the right-hand rest, so the next basic swing starts there (→ left).
            _atLeft = false;

            if (_swingRoutine != null) StopCoroutine(_swingRoutine);
            _swingRoutine = StartCoroutine(OverheadSwingRoutine());
        }

        private IEnumerator OverheadSwingRoutine()
        {
            // Both windup and strike share the same horizontal anchor so the maul reads as moving
            // through a vertical plane just right of centre, not arcing out from the right rest.
            Vector3 windupPos = _restPos + new Vector3(overheadHorizontalOffset, overheadUpOffset, overheadBackOffset);
            Vector3 windupRot = _restRot + new Vector3(overheadWindupPitchDegrees, 0f, 0f);

            Vector3 strikePos = _restPos + new Vector3(overheadHorizontalOffset, overheadStrikeDownOffset, overheadStrikeForwardOffset);
            Vector3 strikeRot = _restRot + new Vector3(overheadStrikePitchDegrees, 0f, 0f);

            // Phase 1: rest → overhead (windup).
            Vector3 startPos = _swingConstraint.GetTranslationOffset(0);
            Vector3 startRot = _swingConstraint.GetRotationOffset(0);
            yield return TweenConstraint(startPos, windupPos, startRot, windupRot, overheadWindupDuration);

            // Phase 2: overhead → strike (slam).
            yield return TweenConstraint(windupPos, strikePos, windupRot, strikeRot, overheadStrikeDuration);

            // Phase 3: strike → rest (return to default).
            yield return TweenConstraint(strikePos, _restPos, strikeRot, _restRot, overheadReturnDuration);

            _swingConstraint.SetTranslationOffset(0, _restPos);
            _swingConstraint.SetRotationOffset(0, _restRot);
            _swingRoutine = null;
        }

        private IEnumerator TweenConstraint(Vector3 fromPos, Vector3 toPos, Vector3 fromRot, Vector3 toRot, float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                _swingConstraint.SetTranslationOffset(0, Vector3.Lerp(fromPos, toPos, a));
                _swingConstraint.SetRotationOffset(0, Vector3.Lerp(fromRot, toRot, a));
                yield return null;
            }
            _swingConstraint.SetTranslationOffset(0, toPos);
            _swingConstraint.SetRotationOffset(0, toRot);
        }

        private IEnumerator SwingRoutine(bool toLeft)
        {
            // Wide HORIZONTAL swing that toggles between two parked poses: the right-hand rest
            // (the prefab's default) and a left-hand extreme. A right→left swing goes rest→left
            // and STAYS left; the next attack sweeps left→rest and stays right. The motion is
            // yaw-dominant — the maul pivots around the grip so the head sweeps across, rather
            // than the whole weapon sliding sideways — laying over (head outward) via the pose
            // roll and dipping slightly through the middle. Anticipation (cock back) and
            // overshoot give it weight. Poses
            // are anchored off the captured _restPos/_restRot so the right pose lands exactly on
            // the prefab neutral.
            Vector3 leftPos = _restPos + new Vector3(-swingSideOffset, 0f, swingForwardOffset);
            // Roll is baked into the left pose so it ramps in/out monotonically with the sweep
            // (lays the head outward, then un-rolls) — no transient bump, so no circular loop.
            Vector3 leftRot = _restRot + new Vector3(0f, -swingYawDegrees, swingRollDegrees);

            Vector3 targetPos = toLeft ? leftPos : _restPos;
            Vector3 targetRot = toLeft ? leftRot : _restRot;

            Vector3 startPos = _swingConstraint.GetTranslationOffset(0);
            Vector3 startRot = _swingConstraint.GetRotationOffset(0);

            Vector3 sweepDelta = targetPos - startPos;
            Vector3 sweepDeltaRot = targetRot - startRot;

            // Windup: cock back AGAINST the sweep (and a touch up) for anticipation.
            Vector3 windupPos = startPos - sweepDelta * swingWindupBackFraction
                                         + new Vector3(0f, swingWindupUp, -swingWindupPullbackZ);
            Vector3 windupRot = startRot - sweepDeltaRot * swingWindupBackFraction;

            // Overshoot past the destination before settling, for follow-through weight.
            Vector3 overshootPos = targetPos + sweepDelta * swingStrikeOvershootFraction;
            Vector3 overshootRot = targetRot + sweepDeltaRot * swingStrikeOvershootFraction;

            // Phase 1: current → windup (cock back, anticipation).
            yield return TweenConstraint(startPos, windupPos, startRot, windupRot, swingWindupDuration);

            // Phase 2: windup → overshoot — the fast sweep. Yaw + the pose roll make the maul
            // pivot and lay over (head outward) as it travels; an extra roll bump tilts the tip
            // even further outward at mid-swing (peaks in the middle, back to the pose value at
            // the ends); and a shallow DOWNWARD dip gives it a chopping arc instead of a flat
            // slide. The roll bump uses the SAME sign for both directions (rollSign), so unlike
            // the old direction-flipped version it stays symmetric — forehand and backhand trace
            // the same path in reverse, no up/down mismatch.
            float rollSign = swingRollDegrees < 0f ? -1f : 1f;
            float dur = Mathf.Max(0.01f, swingStrikeDuration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                float bump = Mathf.Sin(a * Mathf.PI);
                Vector3 pos = Vector3.Lerp(windupPos, overshootPos, a);
                pos.y -= bump * swingArcHeight;
                Vector3 rot = Vector3.Lerp(windupRot, overshootRot, a);
                rot.z += bump * swingSweepRollBoost * rollSign;
                _swingConstraint.SetTranslationOffset(0, pos);
                _swingConstraint.SetRotationOffset(0, rot);
                yield return null;
            }

            // Phase 3: overshoot → destination (settle and park there until the next attack).
            yield return TweenConstraint(overshootPos, targetPos, overshootRot, targetRot, swingSettleDuration);

            _swingConstraint.SetTranslationOffset(0, targetPos);
            _swingConstraint.SetRotationOffset(0, targetRot);

            _swingRoutine = null;
        }

        protected override void PerformServerShot()
        {
            // Server calculates the overlap sphere in front of the player
            Vector3 impactCenter = transform.position + transform.forward * forwardOffset;
            Collider[] hitColliders = Physics.OverlapSphere(impactCenter, attackRadius);

            TeamAffiliation ownerTeam = TeamAffiliation.None;
            if (TryGetComponentInParent(out CharacterStats ownerStats))
            {
                ownerTeam = ownerStats.Team;
            }

            // Dedupe by character root so an enemy with several child colliders inside the sphere
            // isn't damaged/knocked multiple times (and so we never start two knockback coroutines
            // on the same NavMeshAgent, which would race on enabling/disabling it).
            System.Collections.Generic.HashSet<GameObject> processed = new System.Collections.Generic.HashSet<GameObject>();

            foreach (var hitCollider in hitColliders)
            {
                // Don't hit self
                if (hitCollider.transform.root == transform.root) continue;

                if (hitCollider.TryGetComponent(out CharacterStats targetStats) && !targetStats.IsDead)
                {
                    if (!processed.Add(targetStats.gameObject)) continue;

                    // Check teams
                    if (ownerTeam != TeamAffiliation.None && targetStats.Team == ownerTeam) continue;

                    // Apply Damage
                    DamageContext ctx = new DamageContext
                    {
                        Source = transform.root.gameObject,
                        Target = hitCollider.gameObject,
                        Damage = weaponData.baseDamage,
                        DamageType = DamageType.Physical,
                        AttackType = AttackType.Melee,
                        IsCritical = false // could implement crit rules
                    };
                    targetStats.TakeDamage(ctx);

                    // Don't bother knocking a corpse — re-enabling a dead enemy's agent would
                    // fight its death handling.
                    if (targetStats.IsDead) continue;

                    // Apply Knockback
                    Vector3 pushDir = (hitCollider.transform.position - impactCenter).normalized;
                    pushDir.y = 0.2f; // Slight lift

                    if (hitCollider.TryGetComponent(out PlayerMovement targetMovement))
                    {
                        targetMovement.ApplyForce(pushDir * knockbackForce, false);
                    }
                    else if (targetStats.TryGetComponent(out UnityEngine.AI.NavMeshAgent targetAgent) && targetAgent.isActiveAndEnabled)
                    {
                        // Enemies live on the NavMesh and can't take a physics impulse — slide the
                        // transform in a short, collision-aware arc instead. Tuned LIGHTER than
                        // Bulwark (shorter duration, stronger drag) so the maul shoves a shorter
                        // distance than the advance.
                        StartCoroutine(_Project.Core.Managers.EnemyKnockback.Run(
                            targetAgent, pushDir, knockbackForce,
                            duration: 0.5f, gravityMultiplier: 2f, horizontalDrag: 4f));
                    }
                    else if (hitCollider.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
                    {
                        rb.AddForce(pushDir * knockbackForce, ForceMode.Impulse);
                    }
                }
            }
        }

        private bool TryGetComponentInParent<T>(out T component) where T : Component
        {
            component = GetComponentInParent<T>();
            return component != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 impactCenter = transform.position + transform.forward * forwardOffset;
            Gizmos.DrawWireSphere(impactCenter, attackRadius);
        }
    }
}
