using Unity.Netcode;
using UnityEngine;
using _Project.Core.Enums;
using _Project.Core.Interfaces;
using _Project.Core.Stats;

namespace _Project.Features.StageSystem.Scripts
{
    public class ExtractionTerminal : NetworkBehaviour, IInteractable
    {
        [Header("Extraction Settings")]
        [Tooltip("Cost to activate the terminal.")]
        public int activationCost;

        [Tooltip("How many seconds the door takes to open after activation.")]
        public float chargeTime = 15f; 
        
        [Tooltip("The door object to move down when opened.")]
        public Transform extractionDoor;
        [Tooltip("Speed at which the door moves down.")]
        public float doorOpenSpeed = 2f;

        [Header("Visuals")]
        [Tooltip("Spotlight indicating terminal status.")]
        public Light statusLight;

        [Header("Networked State")]
        public NetworkVariable<bool> IsCharging = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsDoorOpen = new NetworkVariable<bool>(false);
        public NetworkVariable<float> ChargeProgress = new NetworkVariable<float>(0f);

        private Vector3 _targetDoorPosition;
        private bool _isOpeningDoor = false;
        private bool _clientHasOpenedDoor = false;
        private AudioSource _chargeLoopSource;

        public override void OnNetworkSpawn()
        {
            IsCharging.OnValueChanged += HandleChargingChanged;
            IsDoorOpen.OnValueChanged += HandleDoorOpenChanged;
            if (IsCharging.Value) StartChargeLoop();
        }

        public override void OnNetworkDespawn()
        {
            IsCharging.OnValueChanged -= HandleChargingChanged;
            IsDoorOpen.OnValueChanged -= HandleDoorOpenChanged;
            StopChargeLoop();
        }

        private void HandleChargingChanged(bool previous, bool current)
        {
            if (current) StartChargeLoop();
            else StopChargeLoop();

            // Broadcast to Items (Danger Close) via Core, on every peer. Fires on charge start and
            // on stop (door completion at chargeTime, or any reset of IsCharging).
            if (current) _Project.Core.Events.GameFeelEvents.OnExtractionChargeStarted?.Invoke();
            else _Project.Core.Events.GameFeelEvents.OnExtractionChargeStopped?.Invoke();
        }

        private void HandleDoorOpenChanged(bool previous, bool current)
        {
            if (!current) return;
            StopChargeLoop();
            var sfx = _Project.Core.Audio.SfxManager.Instance;
            if (sfx != null && sfx.Library != null && sfx.Library.doorChargeComplete != null)
            {
                sfx.PlayOneShot3D(sfx.Library.doorChargeComplete, transform.position, 1f);
            }
        }

        private void StartChargeLoop()
        {
            var sfx = _Project.Core.Audio.SfxManager.Instance;
            if (sfx == null || sfx.Library == null || sfx.Library.doorChargeLoop == null) return;

            if (_chargeLoopSource == null)
            {
                var go = new GameObject("ChargeLoopSource");
                go.transform.SetParent(transform, false);
                _chargeLoopSource = go.AddComponent<AudioSource>();
                _chargeLoopSource.spatialBlend = 1f;
                _chargeLoopSource.loop = true;
                _chargeLoopSource.playOnAwake = false;
                _chargeLoopSource.minDistance = 2f;
                _chargeLoopSource.maxDistance = 30f;
                _chargeLoopSource.volume = 0.8f;
            }
            _chargeLoopSource.clip = sfx.Library.doorChargeLoop;
            if (!_chargeLoopSource.isPlaying) _chargeLoopSource.Play();
        }

        private void StopChargeLoop()
        {
            if (_chargeLoopSource != null && _chargeLoopSource.isPlaying)
            {
                _chargeLoopSource.Stop();
            }
        }

        // --- IInteractable Implementation ---
        public string GetPromptSuffix()
        {
            if (IsDoorOpen.Value) 
                return "Doors are open";
            
            if (IsCharging.Value) 
            {
                int percentage = Mathf.Clamp(Mathf.CeilToInt((ChargeProgress.Value / chargeTime) * 100), 0, 100);
                return $"Charging... {percentage}%";
            }
            
            if (activationCost > 0)
                return $"start the charging sequence (${activationCost})";
                
            return "start the charging sequence";
        }

        public bool CanInteract(GameObject interactor)
        {
            // Only interactable if it hasn't started charging yet AND the door isn't open
            if (IsCharging.Value || IsDoorOpen.Value) return false;
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!IsServer) return; // Strict server authority

            if (CanInteract(interactor))
            {
                if (activationCost > 0 && interactor.TryGetComponent(out CharacterStats stats))
                {
                    // Per-player interactable discount (Discount Chip). ContainerCostMultiplier is
                    // seeded to 1 (no discount) so this is a no-op without the item.
                    float costMult = stats.GetStat(StatType.ContainerCostMultiplier);
                    if (costMult <= 0f) costMult = 1f;
                    int cost = Mathf.Max(1, Mathf.FloorToInt(activationCost * costMult));
                    if (stats.CurrentMoney.Value < cost) return;
                    stats.RemoveMoney(cost);
                }

                IsCharging.Value = true;
                Debug.Log("[StageSystem] Extraction sequence initiated!");
            }
        }

        // --- Server-Side Logic ---
        private void Update()
        {
            if (statusLight != null)
            {
                if (IsDoorOpen.Value) statusLight.color = Color.green;
                else if (IsCharging.Value) statusLight.color = Color.red;
                else statusLight.color = Color.white;
            }

            // Client and Server synchronize the door animation
            if (IsDoorOpen.Value && !_clientHasOpenedDoor && extractionDoor != null)
            {
                _clientHasOpenedDoor = true;
                _targetDoorPosition = extractionDoor.position + new Vector3(0, -5f, 0);
                _isOpeningDoor = true;
            }

            // Handle door movement for everyone
            if (_isOpeningDoor && extractionDoor != null)
            {
                extractionDoor.position = Vector3.MoveTowards(extractionDoor.position, _targetDoorPosition, doorOpenSpeed * Time.deltaTime);
                if (extractionDoor.position == _targetDoorPosition)
                {
                    _isOpeningDoor = false;
                }
            }

            if (!IsServer) return;

            if (!IsCharging.Value || IsDoorOpen.Value) return;

            // Advance the timer
            ChargeProgress.Value += Time.deltaTime;

            // Check completion
            if (ChargeProgress.Value >= chargeTime)
            {
                IsDoorOpen.Value = true;
                IsCharging.Value = false;
                Debug.Log("[StageSystem] Extraction Door is now OPEN.");
            }
        }
    }
}