using Unity.Netcode;
using UnityEngine;

namespace _Project.Features.Player.Scripts
{
    public class PlayerCamera : NetworkBehaviour
    {
        [Header("Multiplayer Visuals")]
        [Tooltip("Objects only the local player should see (e.g. viewmodel).")]
        public GameObject[] localOnlyVisibleObjects;
        
        [Tooltip("Objects everyone BUT the local player should see (e.g. 3D player model).")]
        public GameObject[] remoteOnlyVisibleObjects;

        [Header("Sensitivity")]
        [Tooltip("A base multiplier so you don't have to use tiny decimals like 0.08.")]
        public float baseSensitivity = 10f; 
        [Tooltip("Your actual raw sensitivity setting (e.g., 1.5, 2.0).")]
        public float mouseSensitivity = 1f; 
        
        [Header("References")]
        public Transform playerBody;

        [Header("Game Feel")]
        [Tooltip("How fast the camera recovers from recoil.")]
        public float recoilRecoveryRate = 10f;
        [Tooltip("How sharp the recoil snap is.")]
        public float recoilSnapiness = 6f;

        [Header("Spring Camera — Jump / Land")]
        [Tooltip("Stiffness/damping of the positional spring (jump kick & landing dip).")]
        public float posStiffness = 170f;
        public float posDamping = 14f;
        [Tooltip("Stiffness/damping of the rotational spring (pitch kick & dip).")]
        public float rotStiffness = 200f;
        public float rotDamping = 16f;
        [Tooltip("Upward camera kick on jump — metres of position, degrees of pitch.")]
        public float jumpKickPos = 0.25f;
        public float jumpKickPitch = 4f;
        [Tooltip("Downward camera dip on landing (scaled by fall speed) — metres, degrees.")]
        public float landDipPos = 0.45f;
        public float landDipPitch = 6f;
        [Tooltip("Fall speed (units/s) that produces the maximum landing dip.")]
        public float landSpeedForMax = 18f;

        [Header("Spring Camera — Head Bob")]
        public float bobSpeed = 9f;
        [Tooltip("Bob frequency multiplier while sprinting.")]
        public float runBobScale = 1.4f;
        [Tooltip("Vertical / horizontal bob amplitude (metres).")]
        public float bobAmountY = 0.06f;
        public float bobAmountX = 0.045f;
        [Tooltip("Roll (degrees) synced to the gait bob. 0 to disable.")]
        public float bobRoll = 0.3f;
        [Tooltip("How fast the bob offset eases in/out.")]
        public float bobSmooth = 10f;

        [Header("Spring Camera — Lean / FOV")]
        [Tooltip("Roll (degrees) when strafing sideways.")]
        public float strafeRoll = 2f;
        [Tooltip("Roll per unit of yaw-look delta (turn lean).")]
        public float turnRoll = 0.05f;
        [Tooltip("Maximum total roll lean (degrees).")]
        public float maxRoll = 4f;
        [Tooltip("How fast the roll lean eases toward its target.")]
        public float rollSmooth = 9f;
        [Tooltip("FOV added while sprinting.")]
        public float sprintFovKick = 12f;
        public float fovLerp = 7f;

        private Camera _cam;
        private AudioListener _audioListener;
        private float _xRotation;

        // Recoil state
        private Vector3 _currentRecoil;
        private Vector3 _targetRecoil;

        // Camera Shake state
        private float _shakeDuration;
        private float _shakeMagnitude;
        private Vector3 _shakeOffset;

        // Spectate state
        private Transform _spectateTarget;
        private Camera _spectateCamera;

        // Spring-camera game feel state
        private Vector3 _restLocalPos;
        private float _baseFov;
        private readonly CameraSpring _posSpring = new CameraSpring();
        private readonly CameraSpring _rotSpring = new CameraSpring();
        private float _bobTimer;
        private Vector3 _bobPos;
        private float _bobRollOffset;
        private float _rollLean;
        private float _lastYawDelta;

        private _Project.Core.Events.CharacterEventBus _eventBus;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _audioListener = GetComponent<AudioListener>();
            _baseFov = _cam != null ? _cam.fieldOfView : 65f;
        }

        private void Start()
        {
            _restLocalPos = transform.localPosition;

            // Use transform.root to ensure we grab it from the main capsule
            _eventBus = transform.root.GetComponentInChildren<_Project.Core.Events.CharacterEventBus>();

            if (_eventBus != null)
            {
                _eventBus.OnWeaponFiredRecoil += ApplyRecoil;
                _eventBus.OnCameraShake += ApplyShake;
                _eventBus.OnJump += HandleJump;
                _eventBus.OnLand += HandleLand;
            }
            else
            {
                Debug.LogWarning("PlayerCamera could not find CharacterEventBus on the root object!");
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_eventBus != null)
            {
                _eventBus.OnWeaponFiredRecoil -= ApplyRecoil;
                _eventBus.OnCameraShake -= ApplyShake;
                _eventBus.OnJump -= HandleJump;
                _eventBus.OnLand -= HandleLand;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                if (_cam != null) _cam.enabled = false;
                if (_audioListener != null) _audioListener.enabled = false;

                // They are NOT the owner. Hide the local-only stuff, show the remote-only stuff.
                SetGameObjectsActive(localOnlyVisibleObjects, false);
                SetGameObjectsActive(remoteOnlyVisibleObjects, true);
            }
            else
            {
                // Disable any existing Main Camera (like the one in the Main Menu scene)
                Camera mainCam = Camera.main;
                if (mainCam != null && mainCam != _cam)
                {
                    mainCam.gameObject.SetActive(false);
                }

                if (_cam != null) _cam.enabled = true;
                if (_audioListener != null) _audioListener.enabled = true;

                // They ARE the owner. Show the local-only stuff, hide the remote-only stuff.
                SetGameObjectsActive(localOnlyVisibleObjects, true);
                SetGameObjectsActive(remoteOnlyVisibleObjects, false);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void SetGameObjectsActive(GameObject[] objects, bool isActive)
        {
            if (objects == null) return;
            foreach (var obj in objects)
            {
                if (obj != null)
                {
                    obj.SetActive(isActive);
                }
            }
        }

        // --- Spectating ---

        public void SetSpectateTarget(Transform target)
        {
            _spectateTarget = target;

            // Disable own camera/audio, enable floating spectate camera
            if (_cam != null) _cam.enabled = false;
            if (_audioListener != null) _audioListener.enabled = false;

            if (_spectateCamera == null)
            {
                var go = new GameObject("SpectateCamera");
                _spectateCamera = go.AddComponent<Camera>();
                _spectateCamera.fieldOfView = _cam != null ? _cam.fieldOfView : 75f;
                _spectateCamera.nearClipPlane = 0.01f;
                Object.DontDestroyOnLoad(go);
            }

            _spectateCamera.enabled = true;
        }

        public void ClearSpectateTarget()
        {
            _spectateTarget = null;

            if (_spectateCamera != null)
                _spectateCamera.enabled = false;

            if (_cam != null) _cam.enabled = true;
            if (_audioListener != null) _audioListener.enabled = true;
        }

        private void LateUpdate()
        {
            if (!IsOwner || _spectateTarget == null || _spectateCamera == null) return;

            _spectateCamera.transform.SetPositionAndRotation(
                _spectateTarget.position,
                _spectateTarget.rotation);
        }

        public void Look(Vector2 mouseInput)
        {
            if (!IsOwner) return;

            // Handle Shake Update
            if (_shakeDuration > 0)
            {
                _shakeOffset = Random.insideUnitSphere * _shakeMagnitude;
                _shakeDuration -= Time.deltaTime;
            }
            else
            {
                _shakeOffset = Vector3.zero;
                _shakeMagnitude = 0f;
            }

            // Spring recoil returning to zero
            _targetRecoil = Vector3.Lerp(_targetRecoil, Vector3.zero, recoilRecoveryRate * Time.deltaTime);
            _currentRecoil = Vector3.Slerp(_currentRecoil, _targetRecoil, recoilSnapiness * Time.fixedDeltaTime);

            // Multiply by the base scalar and the user's sensitivity choice.
            // Still NO Time.deltaTime, keeping true raw 1:1 input.
            float finalSensitivity = mouseSensitivity * baseSensitivity;
            
            float mouseX = mouseInput.x * finalSensitivity * 0.01f; 
            float mouseY = mouseInput.y * finalSensitivity * 0.01f;
            
            // --- Recoil Compensation ---
            // If the player moves the mouse to counter the recoil, absorb the recoil instead of moving the base camera.
            // This completely prevents the "looking at the floor" bug after shooting.
            if (_targetRecoil.x > 0 && mouseY < 0)
            {
                float comp = Mathf.Min(-mouseY, _targetRecoil.x);
                _targetRecoil.x -= comp;
                _currentRecoil.x -= comp;
                mouseY += comp;
            }
            else if (_targetRecoil.x < 0 && mouseY > 0)
            {
                float comp = Mathf.Min(mouseY, -_targetRecoil.x);
                _targetRecoil.x += comp;
                _currentRecoil.x += comp;
                mouseY -= comp;
            }

            if (_targetRecoil.y > 0 && mouseX < 0)
            {
                float comp = Mathf.Min(-mouseX, _targetRecoil.y);
                _targetRecoil.y -= comp;
                _currentRecoil.y -= comp;
                mouseX += comp;
            }
            else if (_targetRecoil.y < 0 && mouseX > 0)
            {
                float comp = Mathf.Min(mouseX, -_targetRecoil.y);
                _targetRecoil.y += comp;
                _currentRecoil.y += comp;
                mouseX -= comp;
            }
            // ---------------------------
            
            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -89f, 89f);

            // Apply vertical rotation to the camera, composing every additive layer in one write:
            // look pitch, recoil, shake, and the spring-camera game-feel offsets (jump/land springs,
            // gait roll, strafe/turn lean). Look() is the single writer of localRotation/localPosition.
            float rollOffset = _rotSpring.Value.z + _rollLean + _bobRollOffset;
            transform.localRotation = Quaternion.Euler(
                _xRotation - _currentRecoil.x + _shakeOffset.x + _rotSpring.Value.x,
                _currentRecoil.y + _shakeOffset.y + _rotSpring.Value.y,
                _currentRecoil.z + _shakeOffset.z + rollOffset);
            transform.localPosition = _restLocalPos + _posSpring.Value + _bobPos;

            if (playerBody != null)
            {
                playerBody.Rotate(Vector3.up * mouseX);
            }

            // Feed this frame's yaw delta to next frame's turn-lean.
            _lastYawDelta = mouseX;
        }

        // --- Spring Camera game feel ---

        private void HandleJump()
        {
            if (!IsOwner) return;
            _posSpring.AddImpulse(Vector3.up * jumpKickPos);
            _rotSpring.AddImpulse(new Vector3(-jumpKickPitch, 0f, 0f)); // pitch up
        }

        private void HandleLand(float impactSpeed)
        {
            if (!IsOwner) return;
            float t = Mathf.Clamp01(impactSpeed / Mathf.Max(0.01f, landSpeedForMax));
            _posSpring.AddImpulse(Vector3.down * landDipPos * t);
            _rotSpring.AddImpulse(new Vector3(landDipPitch * t, 0f, 0f)); // pitch down
        }

        /// <summary>
        /// Advances the procedural game-feel state (springs, head bob, roll lean, sprint FOV) once
        /// per frame. Driven from PlayerInputHandler.Update() and called unconditionally (even when
        /// input is locked, with zeroed args) so the springs keep settling. The resulting offsets
        /// are written to the transform by Look().
        /// </summary>
        public void UpdateGameFeel(Vector2 moveInput, bool isRunning, bool isMoving)
        {
            if (!IsOwner) return;

            float dt = Time.deltaTime;

            // Jump/land impulse springs decay toward rest.
            _posSpring.Update(dt, posStiffness, posDamping);
            _rotSpring.Update(dt, rotStiffness, rotDamping);

            // Head bob — sin/cos gait, eased in/out. Compounds with the viewmodel's own bob.
            Vector3 bobTarget = Vector3.zero;
            float bobRollTarget = 0f;
            if (isMoving)
            {
                _bobTimer += dt * bobSpeed * (isRunning ? runBobScale : 1f);
                bobTarget = new Vector3(Mathf.Cos(_bobTimer * 0.5f) * bobAmountX,
                                        Mathf.Sin(_bobTimer) * bobAmountY, 0f);
                bobRollTarget = Mathf.Cos(_bobTimer * 0.5f) * bobRoll;
            }
            else
            {
                _bobTimer = 0f;
            }
            _bobPos = Vector3.Lerp(_bobPos, bobTarget, bobSmooth * dt);
            _bobRollOffset = Mathf.Lerp(_bobRollOffset, bobRollTarget, bobSmooth * dt);

            // Roll lean — tilt into strafes and fast turns.
            float targetRoll = Mathf.Clamp(-moveInput.x * strafeRoll - _lastYawDelta * turnRoll,
                                           -maxRoll, maxRoll);
            _rollLean = Mathf.Lerp(_rollLean, targetRoll, rollSmooth * dt);

            // Sprint FOV kick — never touch the FOV while a spectate camera is active.
            if (_cam != null && _spectateTarget == null)
            {
                float targetFov = _baseFov + (isRunning && isMoving ? sprintFovKick : 0f);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, fovLerp * dt);
            }
        }
        
        private void ApplyRecoil(Vector3 recoilForce)
        {
            if (!IsOwner) return;
            // Randomize recoil slightly
            _targetRecoil += new Vector3(
                recoilForce.x + Random.Range(-recoilForce.x * 0.1f, recoilForce.x * 0.1f),
                Random.Range(-recoilForce.y, recoilForce.y),
                Random.Range(-recoilForce.z, recoilForce.z)
            );
        }

        private void ApplyShake(float magnitude, float duration)
        {
            if (!IsOwner) return;
            // Combine concurrent shakes (e.g. weapon fire + on-hit feedback raised the same
            // frame) by taking the max, rather than letting the last writer clobber the others.
            // Without this, the short on-hit shake overwrote the weapon's fire shake whenever a
            // shot connected, so hitting an enemy looked like it killed the shake entirely.
            _shakeMagnitude = Mathf.Max(_shakeMagnitude, magnitude);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
        }

        /// <summary>
        /// Snaps the player's look (camera pitch + body yaw) to face the given world-space
        /// direction. Routed through ClientRpc so abilities running server-side (e.g. Infernum)
        /// can rotate the owner's view between auto-fired shots — the camera transform lives
        /// only on the owning client.
        /// </summary>
        public void SnapLookAt(Vector3 worldDirection)
        {
            if (IsServer && !IsOwner)
            {
                SnapLookAtClientRpc(worldDirection);
            }
            else
            {
                SnapLookAtLocal(worldDirection);
            }
        }

        [ClientRpc]
        private void SnapLookAtClientRpc(Vector3 worldDirection)
        {
            if (!IsOwner) return;
            SnapLookAtLocal(worldDirection);
        }

        private void SnapLookAtLocal(Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude < 0.0001f) return;
            Vector3 dir = worldDirection.normalized;

            // Pitch: camera tilts up/down based on the Y component of the look vector.
            float pitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            _xRotation = Mathf.Clamp(pitch, -89f, 89f);
            // Apply pitch immediately so the snap is visible this frame instead of waiting for
            // the next Look() call. Drop transient recoil/shake so they don't fight the snap.
            _currentRecoil = Vector3.zero;
            _targetRecoil = Vector3.zero;
            _shakeOffset = Vector3.zero;
            transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

            // Yaw: rotate the player body so its forward matches the horizontal projection of
            // the look vector. The camera's transform is parented under playerBody, so spinning
            // the body also spins the camera in world space.
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude > 0.0001f && playerBody != null)
            {
                playerBody.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
        }
    }
}

