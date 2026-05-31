using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;

namespace _Project.Features.Player.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class PlayerInteractor : NetworkBehaviour
    {
        private IInteractable _currentInteractable;
        private NetworkObject _currentInteractableNetworkObject;

        public IInteractable CurrentInteractable => _currentInteractable;

        // Called strictly by PlayerInputHandler
        public void TryInteract()
        {
            if (!IsOwner) return;

            if (_currentInteractable != null)
            {
                // Local prediction/check to save bandwidth
                if (_currentInteractable.CanInteract(gameObject))
                {
                    // Send request to the Server with the Target's NetworkObjectId
                    RequestInteractServerRpc(_currentInteractableNetworkObject.NetworkObjectId);
                }
                else
                {
                    Debug.Log("[PlayerInteractor] Cannot interact (e.g., missing requirements).");
                }
            }
        }

        [ServerRpc]
        private void RequestInteractServerRpc(ulong targetNetworkObjectId)
        {
            // Server validates the object exists
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObject))
            {
                if (targetObject.TryGetComponent(out IInteractable interactable))
                {
                    // Server final validation
                    if (interactable.CanInteract(gameObject))
                    {
                        interactable.Interact(gameObject);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOwner) return;
            
            if (other.TryGetComponent(out IInteractable interactable) && other.TryGetComponent(out NetworkObject netObj))
            {
                _currentInteractable = interactable;
                _currentInteractableNetworkObject = netObj;
                Debug.Log($"[PlayerInteractor] Entered interaction range of: {other.name} - Press Interact to trigger!"); 
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsOwner) return;
            
            if (other.TryGetComponent(out IInteractable interactable) && interactable == _currentInteractable)
            {
                _currentInteractable = null;
                _currentInteractableNetworkObject = null;
                Debug.Log($"[PlayerInteractor] Left interaction range of: {other.name}");
            }
        }
    }
}