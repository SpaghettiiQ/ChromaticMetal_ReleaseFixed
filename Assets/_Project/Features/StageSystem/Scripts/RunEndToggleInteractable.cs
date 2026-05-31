using Unity.Netcode;
using UnityEngine;
using TMPro;
using _Project.Core.Interfaces;

namespace _Project.Features.StageSystem.Scripts
{
    public class RunEndToggleInteractable : NetworkBehaviour, IInteractable
    {
        [Header("Visuals")]
        [Tooltip("The text component to display LOOP or END.")]
        public TMP_Text displayText;
        [Tooltip("The light component to match the text color.")]
        public Light statusLight;

        [Header("Networked State")]
        public NetworkVariable<bool> WantsToEndRun = new NetworkVariable<bool>(false);

        public override void OnNetworkSpawn()
        {
            WantsToEndRun.OnValueChanged += HandleVoteChanged;
            UpdateVisuals(WantsToEndRun.Value);
        }

        public override void OnNetworkDespawn()
        {
            WantsToEndRun.OnValueChanged -= HandleVoteChanged;
        }

        private void HandleVoteChanged(bool previousValue, bool newValue)
        {
            UpdateVisuals(newValue);
        }

        private void UpdateVisuals(bool wantsToEnd)
        {
            Color targetColor = wantsToEnd ? Color.red : Color.green;

            if (displayText != null)
            {
                displayText.text = wantsToEnd ? "END" : "LOOP";
                displayText.color = targetColor;
            }

            if (statusLight != null)
            {
                statusLight.color = targetColor;
            }
        }

        // --- IInteractable Implementation ---
        public string GetPromptSuffix()
        {
            return WantsToEndRun.Value ? "switch mode to LOOP" : "switch mode to END";
        }

        public bool CanInteract(GameObject interactor)
        {
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!IsServer) return;

            WantsToEndRun.Value = !WantsToEndRun.Value;
            
            if (RunNetworkController.Singleton != null)
            {
                RunNetworkController.Singleton.SetPvERunEndVote(WantsToEndRun.Value);
            }
        }
    }
}
