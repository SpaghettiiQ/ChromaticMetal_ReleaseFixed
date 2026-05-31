using Unity.Netcode;
using UnityEngine;
using _Project.Core.Interfaces;
using _Project.Features.WeaponCore.Scripts;
using _Project.Features.WeaponCore.Data;

namespace _Project.Features.Player.Scripts
{
    public class PlayerAnimator : NetworkBehaviour
    {
        private Animator _animator;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private CharacterController _characterController;

        // Hashes for Animator Parameters
        private readonly int _speedHash = Animator.StringToHash("Speed");
        private readonly int _runVariantHash = Animator.StringToHash("RunVariant");
        private readonly int _isGroundedHash = Animator.StringToHash("IsGrounded");
        private readonly int _slashTriggerHash = Animator.StringToHash("Slash");
        private readonly int _deathTriggerHash = Animator.StringToHash("Death");

        private IWeapon _currentWeapon;
        
        // This is network synced to ensure the variant updates for everyone
        private NetworkVariable<int> _syncedRunVariant = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private Animator GetAnimator()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            return _animator;
        }

        private void Awake()
        {
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            _syncedRunVariant.OnValueChanged += OnRunVariantChanged;
            
            if (IsOwner)
            {
                UpdateWeaponAnimationState();
            }
            else
            {
                // Ensure late joiners see the current run variant
                var anim = GetAnimator();
                if (anim != null) anim.SetInteger(_runVariantHash, _syncedRunVariant.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            _syncedRunVariant.OnValueChanged -= OnRunVariantChanged;
        }

        private void OnRunVariantChanged(int previousValue, int newValue)
        {
            var anim = GetAnimator();
            if (anim != null) anim.SetInteger(_runVariantHash, newValue);
        }

        private void Update()
        {
            var anim = GetAnimator();
            if (anim == null) return;

            // Only the owner calculates movement variables to send to Animator. 
            // In a fully predicted/authoritative setup, you might do this for clones based on their velocity.
            // For now, calculating it locally based on Character Controller velocity works well visually.

            // The speed variable in Animator decides the playback time of the run animation
            Vector3 horizontalVelocity = new Vector3(_characterController.velocity.x, 0, _characterController.velocity.z);
            float currentSpeed = horizontalVelocity.magnitude;
            
            // Map the current speed to a 0-1 range based on the run speed
            float normalizedSpeed = Mathf.Clamp01(currentSpeed / _playerMovement.runSpeed);
            
            anim.SetFloat(_speedHash, normalizedSpeed);
            anim.SetBool(_isGroundedHash, _characterController.isGrounded);

            // In air, we go to idle implicitly if IsGrounded is false in Animator State Machine
            // We can also let the animator blend speed to 0. 
        }

        public void UpdateWeaponAnimationState()
        {
            if (!IsOwner) return;

            // Find current weapon and update the variant
            _currentWeapon = GetComponentInChildren<IWeapon>();
            
            if (_currentWeapon is NetworkWeapon nw && nw.weaponData != null)
            {
                _syncedRunVariant.Value = nw.weaponData.runAnimationVariant;
            }
            else
            {
                _syncedRunVariant.Value = 1; // Default
            }
        }

        [ClientRpc]
        public void TriggerSlashClientRpc()
        {
            var anim = GetAnimator();
            if (anim != null) anim.SetTrigger(_slashTriggerHash);
        }

        [ClientRpc]
        public void TriggerDeathClientRpc()
        {
            var anim = GetAnimator();
            if (anim != null) anim.SetTrigger(_deathTriggerHash);
            // Optional: disable movement script
        }
    }
}
