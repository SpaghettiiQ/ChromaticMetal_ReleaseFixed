using UnityEngine;
using Unity.Netcode;
using _Project.Core.Stats;
using _Project.Core.Enums;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Conduit.Tracking
{
    /// <summary>
    /// Casts Hollow Heart — channels the buff onto self by default, or onto a same-team ally
    /// the Conduit is aiming at within range. Server-side only; the actual ticking buff
    /// (HollowHeartBuff) is added to the target's GameObject. Also forwards the optional
    /// VFX prefab the ability SO references.
    /// </summary>
    public class HollowHeartCaster : MonoBehaviour
    {
        public void ExecuteAbility(float duration, float range, float healMult, GameObject vfxPrefab)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            CharacterStats myStats = GetComponent<CharacterStats>();
            TeamAffiliation myTeam = myStats != null ? myStats.Team : TeamAffiliation.None;

            GameObject target = gameObject; // default: self

            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                CharacterStats hitStats = hit.collider.GetComponentInParent<CharacterStats>();
                if (hitStats != null && !hitStats.IsDead
                    && hitStats.gameObject != gameObject
                    && hitStats.Team == myTeam
                    && myTeam != TeamAffiliation.None)
                {
                    target = hitStats.gameObject;
                }
            }

            if (!target.TryGetComponent(out HollowHeartBuff buff))
            {
                buff = target.AddComponent<HollowHeartBuff>();
            }
            buff.Initialize(duration, healMult, vfxPrefab);
        }
    }
}
