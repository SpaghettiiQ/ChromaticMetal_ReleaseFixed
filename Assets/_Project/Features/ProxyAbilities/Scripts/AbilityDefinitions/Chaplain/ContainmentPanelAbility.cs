using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.ProxyAbilities.Scripts.AbilityDefinitions.Chaplain
{
    [CreateAssetMenu(fileName = "ContainmentPanel", menuName = "Proxy Abilities/Chaplain/Containment Panel")]
    public class ContainmentPanelAbility : ScriptableObject, IAbilityEffect
    {
        [Header("Panel Settings")]
        [SerializeField] private GameObject panelPrefab;
        [SerializeField] private float placeDistance = 3f;

        public void Execute(GameObject instigator)
        {
            if (panelPrefab == null) return;

            if (NetworkManager.Singleton.IsServer)
            {
                // Place it flat on the ground in front of the player
                Vector3 spawnPos = instigator.transform.position + instigator.transform.forward * placeDistance;
                spawnPos.y = instigator.transform.position.y; // Keep vertical alignment grounded relative to player. Could also raycast down to find floor.

                // Match player's horizontal rotation but keep it upright. Add a 90° yaw so the
                // panel faces perpendicular to the player rather than aligned with their forward.
                Quaternion spawnRot = Quaternion.Euler(0, instigator.transform.eulerAngles.y + 90f, 0);

                GameObject panelInstance = Instantiate(panelPrefab, spawnPos, spawnRot);

                if (panelInstance.TryGetComponent(out ContainmentPanelEntity panelEntity))
                {
                    panelEntity.Initialize(instigator);
                }

                if (panelInstance.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();
                }
            }

            Debug.Log($"[{nameof(ContainmentPanelAbility)}] {instigator.name} deployed a Containment Panel!");
        }
    }
}
