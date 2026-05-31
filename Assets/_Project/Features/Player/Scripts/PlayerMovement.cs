using Unity.Netcode; // Added Netcode namespace
using UnityEngine;

namespace _Project.Features.Player.Scripts
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour // Changed from MonoBehaviour to NetworkBehaviour
    {
        [Header("Movement Speeds")]
        public float walkSpeed = 7f;
        public float runSpeed = 11f;
        
        [Header("Ground Settings")]
        public float acceleration = 10f;
        public float friction = 6f;
        public float stopSpeed = 1f;
        
        [Header("Air Settings (CS:GO Air Strafe)")]
        [Tooltip("Higher values make strafing/turning more responsive.")]
        public float airAcceleration = 100f; 
        [Tooltip("Limits how much speed you can add while steering/moving with momentum.")]
        public float airCap = 3.0f;          
        
        [Header("Physics")]
        public float gravity = -20f;
        public float jumpHeight = 2.5f;

        public bool MovementLocked { get; set; }
        public bool IsGrounded => _controller.isGrounded;
        public bool IsSliding { get; private set; }

        [Header("Slide")]
        [Tooltip("Friction multiplier applied while IsSliding is true. 0 = frictionless ice.")]
        public float slideFrictionMultiplier = 0.05f;

        [Header("Game Feel")]
        [Tooltip("Minimum downward speed on landing before the camera-dip OnLand event fires. " +
                 "Keeps tiny step-downs from triggering a dip.")]
        public float landMinSpeed = 3f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private bool _isGrounded;
        private int _airJumpsRemaining;
        private bool _wasGroundedLastFrame;
        private _Project.Core.Events.CharacterEventBus _eventBus;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            // Same lookup PlayerCamera uses — the bus lives somewhere under the player root.
            _eventBus = transform.root.GetComponentInChildren<_Project.Core.Events.CharacterEventBus>();
        }

        // NEW: This is called by NGO when the object spawns on the network
        public override void OnNetworkSpawn()
        {
            // If we do not own this player, disable the CharacterController.
            // This prevents local physics from fighting the NetworkTransform sync.
            if (!IsOwner)
            {
                _controller.enabled = false;
            }
        }

        public void Move(Vector2 input, bool isRunning, bool attemptJump)
        {
            // NEW: Strict ownership check so other clients' inputs don't move this character
            if (!IsOwner) return;

            if (MovementLocked)
            {
                input = Vector2.zero;
                attemptJump = false;
            }

            _isGrounded = _controller.isGrounded;
            Vector3 inputDir = new Vector3(input.x, 0, input.y).normalized;

            float speedMult = 1f;
            int extraJumps = 0;
            if (TryGetComponent(out _Project.Core.Stats.CharacterStats _movementStats))
            {
                float m = _movementStats.GetStat(_Project.Core.Enums.StatType.MovementSpeed);
                if (m > 0f) speedMult = m;
                extraJumps = Mathf.Max(0, Mathf.RoundToInt(_movementStats.GetStat(_Project.Core.Enums.StatType.ExtraJumps)));
            }
            float walk = walkSpeed * speedMult;
            float run  = runSpeed  * speedMult;

            // Refill the air-jump pool on landing. Done on the landing edge (not every grounded
            // frame) so items that raise ExtraJumps mid-air don't immediately replenish.
            if (_isGrounded && !_wasGroundedLastFrame)
            {
                _airJumpsRemaining = extraJumps;

                // Landing edge: _velocity.y still holds the real fall speed here (it's reset to -2f
                // further down). Fire the camera-dip event scaled by how hard we hit the ground.
                float impactSpeed = Mathf.Abs(_velocity.y);
                if (impactSpeed > landMinSpeed) _eventBus?.RaiseOnLand(impactSpeed);
            }
            // Also clamp down if the pool shrinks (item removed) while still on the ground.
            if (_isGrounded && _airJumpsRemaining > extraJumps)
            {
                _airJumpsRemaining = extraJumps;
            }

            if (_isGrounded)
            {
                ApplyFriction();
                float targetSpeed = isRunning ? run : walk;
                Accelerate(inputDir, targetSpeed, acceleration);

                if (_velocity.y < 0) _velocity.y = -2f;

                if (attemptJump)
                {
                    _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    _eventBus?.RaiseOnJump();
                }
            }
            else
            {
                // Air Movement logic
                Accelerate(inputDir, airCap, airAcceleration);

                if (attemptJump && _airJumpsRemaining > 0)
                {
                    _airJumpsRemaining--;
                    // Hard reset vertical velocity so the double-jump feels snappy whether the
                    // player is rising or already falling.
                    _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    _eventBus?.RaiseOnJump();
                }
            }

            _wasGroundedLastFrame = _isGrounded;

            UpdateSustainedForce();

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void Accelerate(Vector3 inputDir, float targetSpeed, float accel)
        {
            // Calculate world-space direction the player wants to go
            Vector3 wishDir = transform.right * inputDir.x + transform.forward * inputDir.z;
            
            // Determine how much of our current speed is going in the wish direction
            float currentSpeedInWishDir = Vector3.Dot(_velocity, wishDir);
            
            // --- THE COUNTER-STRAFE SECRET ---
            // If currentSpeedInWishDir is negative, it means we are pressing a key 
            // OPPOSITE to our current movement (Braking).
            float effectiveTargetSpeed = targetSpeed;
            if (currentSpeedInWishDir < 0)
            {
                // When braking, we allow the acceleration to use our full Walk Speed 
                // as a multiplier. This allows us to "stop" the momentum much faster.
                effectiveTargetSpeed = walkSpeed;
            }

            // Calculate how much speed we can add
            float addSpeed = effectiveTargetSpeed - currentSpeedInWishDir;
            
            if (addSpeed <= 0) return;

            // Calculate acceleration force for this frame
            float accelSpeed = accel * Time.deltaTime * effectiveTargetSpeed;
            
            // Clamp to ensure we don't overshoot our target
            if (accelSpeed > addSpeed) accelSpeed = addSpeed;

            _velocity += wishDir * accelSpeed;
        }

        private void ApplyFriction()
        {
            Vector3 horizontalVel = new Vector3(_velocity.x, 0, _velocity.z);
            float currentSpeed = horizontalVel.magnitude;

            if (currentSpeed < 0.01f)
            {
                _velocity.x = 0; _velocity.z = 0;
                return;
            }

            float control = currentSpeed < stopSpeed ? stopSpeed : currentSpeed;
            float effectiveFriction = IsSliding ? friction * slideFrictionMultiplier : friction;
            float drop = control * effectiveFriction * Time.deltaTime;

            float newSpeed = currentSpeed - drop;
            if (newSpeed < 0) newSpeed = 0;

            newSpeed /= currentSpeed;
            _velocity.x *= newSpeed;
            _velocity.z *= newSpeed;
        }

        private Vector3 _sustainedForce;
        private float _sustainedForceEndTime;

        public void ApplySustainedForce(Vector3 force, float duration)
        {
            if (IsServer && !IsOwner)
            {
                ApplySustainedForceClientRpc(force, duration);
            }
            else
            {
                ApplySustainedForceLocal(force, duration);
            }
        }

        [ClientRpc]
        private void ApplySustainedForceClientRpc(Vector3 force, float duration)
        {
            if (!IsOwner) return;
            ApplySustainedForceLocal(force, duration);
        }

        private void ApplySustainedForceLocal(Vector3 force, float duration)
        {
            _sustainedForce = force;
            _sustainedForceEndTime = Time.time + duration;
        }

        private void UpdateSustainedForce()
        {
            if (Time.time < _sustainedForceEndTime)
            {
                _velocity += _sustainedForce * Time.deltaTime;
            }
        }

        public void ApplyForce(Vector3 force, bool resetVelocity = false)
        {
            if (IsServer && !IsOwner)
            {
                ApplyForceClientRpc(force, resetVelocity);
            }
            else
            {
                ApplyForceLocal(force, resetVelocity);
            }
        }

        [ClientRpc]
        private void ApplyForceClientRpc(Vector3 force, bool resetVelocity)
        {
            if (!IsOwner) return; // Only process on the owner
            ApplyForceLocal(force, resetVelocity);
        }

        private void ApplyForceLocal(Vector3 force, bool resetVelocity)
        {
            if (resetVelocity)
            {
                _velocity = Vector3.zero;
            }
            _velocity += force;
        }

        // Routed through ClientRpc so abilities running server-side (e.g. Of The Abyss) can
        // temporarily boost the OWNER's jump height — the field is only read by the owner's
        // PlayerMovement so a direct server-side assignment doesn't help non-host clients.
        public void SetJumpHeight(float newJumpHeight)
        {
            if (IsServer && !IsOwner)
            {
                SetJumpHeightClientRpc(newJumpHeight);
            }
            else
            {
                jumpHeight = newJumpHeight;
            }
        }

        [ClientRpc]
        private void SetJumpHeightClientRpc(float newJumpHeight)
        {
            if (!IsOwner) return;
            jumpHeight = newJumpHeight;
        }

        public void SetSliding(bool sliding)
        {
            IsSliding = sliding;
        }

        public void Teleport(Vector3 position, Quaternion rotation = default)
        {
            if (rotation == default) rotation = transform.rotation;

            if (IsServer && !IsOwner)
            {
                TeleportClientRpc(position, rotation);
            }
            else
            {
                TeleportLocal(position, rotation);
            }
        }

        [ClientRpc]
        private void TeleportClientRpc(Vector3 position, Quaternion rotation)
        {
            if (!IsOwner) return; 
            TeleportLocal(position, rotation);
        }

        private void TeleportLocal(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.position = position;
            transform.rotation = rotation;
            _controller.enabled = true;
            _velocity = Vector3.zero; // Usually you want to stop momentum after a teleport
        }
    }
}