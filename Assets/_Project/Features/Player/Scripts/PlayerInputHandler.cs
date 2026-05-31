using Unity.Netcode; // Added Netcode
using UnityEngine;
using UnityEngine.InputSystem;
using _Project.Core.Interfaces;
using _Project.Core.Enums;
using _Project.Features.InGameUI;
using _Project.Features.StageSystem.Scripts;
using _Project.Features.DifficultySystem.Scripts;
using _Project.Features.LobbySystem.Scripts;
using _Project.Core.Networking;

namespace _Project.Features.Player.Scripts
{
    public class PlayerInputHandler : NetworkBehaviour, IStunnable // Changed to NetworkBehaviour
    {
        private PlayerMovement _movement;
        private PlayerCamera _camera;
        private ViewmodelSway _viewmodelSway;
        private CharacterController _controller;
        private PlayerInput _playerInput; // Added to disable for clones
        
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _isJumpPressed;
        private bool _isRunPressed;

        private IAbilityController _abilityController; // Using the interface
        private PlayerInteractor _playerInteractor;

        // UI State Handlers
        private bool _isPauseMenuOpen;
        private bool _isTabOverviewOpen;
        private float _stunEndTime;
        private bool IsStunned => Time.time < _stunEndTime;
        private bool IsInputLocked => _isPauseMenuOpen || _isTabOverviewOpen || IsStunned;

        public void Stun(float duration)
        {
            if (!IsServer) return;
            ApplyStunClientRpc(duration);
        }

        [ClientRpc]
        private void ApplyStunClientRpc(float duration)
        {
            if (!IsOwner) return;
            float candidate = Time.time + duration;
            if (candidate > _stunEndTime) _stunEndTime = candidate;
            if (_movement != null) _movement.MovementLocked = true;
        }

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _camera = GetComponentInChildren<PlayerCamera>();
            _viewmodelSway = GetComponentInChildren<ViewmodelSway>();
            _controller = GetComponent<CharacterController>();
            _playerInput = GetComponent<PlayerInput>();
            _abilityController = GetComponent<IAbilityController>();
            _playerInteractor = GetComponent<PlayerInteractor>();
        }

        public override void OnNetworkSpawn()
        {
            // If this is a remote player, shut down the input component so it doesn't listen to the local keyboard
            if (!IsOwner && _playerInput != null)
            {
                _playerInput.enabled = false;
            }

            if (IsOwner)
            {
                InGameUIController.Instance.OnResumeClicked += HandleResumeClicked;
                InGameUIController.Instance.OnLeaveMatchClicked += HandleLeaveMatchClicked;
                InGameUIController.Instance.ToggleEntireUI(true);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && InGameUIController.Instance != null)
            {
                InGameUIController.Instance.OnResumeClicked -= HandleResumeClicked;
                InGameUIController.Instance.OnLeaveMatchClicked -= HandleLeaveMatchClicked;
                InGameUIController.Instance.ToggleEntireUI(false);
            }
        }

        private void HandleResumeClicked()
        {
            if (!IsOwner) return;
            _isPauseMenuOpen = false;
            UpdateCursorAndTimeState();
        }

        private void HandleLeaveMatchClicked()
        {
            if (!IsOwner) return;

            // Ensure singleplayer time is unpaused, cursor is unlocked for the main menu, and menus are hidden
            _isPauseMenuOpen = false;
            _isTabOverviewOpen = false;
            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (InGameUIController.Instance != null)
            {
                InGameUIController.Instance.TogglePauseMenu(false);
                InGameUIController.Instance.ToggleEntireUI(false);
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkSessionWatcher.Singleton?.MarkIntentionalShutdown();
                NetworkManager.Singleton.Shutdown();
            }

            if (InGameUIController.Instance != null)
            {
                Destroy(InGameUIController.Instance.gameObject);
            }
            
            if (RunNetworkController.Singleton != null)
            {
                Destroy(RunNetworkController.Singleton.gameObject);
            }

            if (DifficultyNetworkController.Singleton != null)
            {
                Destroy(DifficultyNetworkController.Singleton.gameObject);
            }

            if (MatchNetworkController.Singleton != null)
            {
                Destroy(MatchNetworkController.Singleton.gameObject);
            }

            if (LobbyNetworkController.Singleton != null)
            {
                Destroy(LobbyNetworkController.Singleton.gameObject);
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        public void OnMove(InputValue value) { if (IsOwner) _moveInput = value.Get<Vector2>(); }
        public void OnLook(InputValue value) { if (IsOwner) _lookInput = value.Get<Vector2>(); }
        public void OnJump(InputValue value) { if (IsOwner) _isJumpPressed = value.isPressed; }
        public void OnRun(InputValue value) { if (IsOwner) _isRunPressed = value.isPressed; }
        public void OnFire(InputValue value) { if (IsOwner && !IsInputLocked) GetComponentInChildren<IWeapon>()?.HandleInput(value.isPressed); }
        public void OnReload(InputValue value) { if (IsOwner && !IsInputLocked) GetComponentInChildren<IWeapon>()?.Reload(); }

        public void OnSecondaryAbility(InputValue value)
        {
            if (IsOwner && value.isPressed && !IsInputLocked) _abilityController?.TryUseAbility(AbilitySlot.Secondary);
        }

        public void OnUtilityAbility(InputValue value)
        {
            if (IsOwner && value.isPressed && !IsInputLocked) _abilityController?.TryUseAbility(AbilitySlot.Utility);
        }

        public void OnSpecialAbility(InputValue value)
        {
            if (IsOwner && value.isPressed && !IsInputLocked) _abilityController?.TryUseAbility(AbilitySlot.Special);
        }

        public void OnInteract(InputValue value) { if (IsOwner && value.isPressed && !IsInputLocked) _playerInteractor.TryInteract(); }

        public void OnPause(InputValue value)
        {
            if (!IsOwner || !value.isPressed) return;

            _isPauseMenuOpen = !_isPauseMenuOpen;
            InGameUIController.Instance.TogglePauseMenu(_isPauseMenuOpen);
            
            UpdateCursorAndTimeState();
        }

        public void OnScoreboard(InputValue value)
        {
            if (!IsOwner) return;

            // Scoreboard is typically held, but we can treat it as a toggle or hold depending on value.isPressed
            // We'll treat it as standard hold to show, release to hide, or you can change to toggle.
            _isTabOverviewOpen = value.isPressed;
            InGameUIController.Instance.ToggleTabOverview(_isTabOverviewOpen);
            
            UpdateCursorAndTimeState();
        }

        private void UpdateCursorAndTimeState()
        {
            bool showCursor = _isPauseMenuOpen || _isTabOverviewOpen;
            Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = showCursor;

            // Handle pausing game time for singleplayer
            if (_isPauseMenuOpen)
            {
                if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsList.Count <= 1)
                {
                    Time.timeScale = 0f;
                }
            }
            else
            {
                Time.timeScale = 1f;
            }
        }

        private bool _wasStunnedLastFrame;

        private void Update()
        {
            // CRITICAL: Prevent remote players from executing the local update loop
            if (!IsOwner) return;

            // Release stun-induced movement lock when the stun timer expires.
            bool stunnedNow = IsStunned;
            if (_wasStunnedLastFrame && !stunnedNow && _movement != null)
            {
                _movement.MovementLocked = false;
            }
            _wasStunnedLastFrame = stunnedNow;

            bool isMoving = _moveInput.magnitude > 0.1f && _controller.isGrounded;
    
            _movement.Move(_moveInput, _isRunPressed, _isJumpPressed);

            // Advance spring-camera game feel every frame (even when locked, with zeroed input) so
            // the springs keep settling. Must run before Look(), which composes the offsets.
            if (_camera != null)
            {
                _camera.UpdateGameFeel(IsInputLocked ? Vector2.zero : _moveInput,
                                       _isRunPressed,
                                       !IsInputLocked && isMoving);
            }

            // Lock camera/look input when a menu is open
            if (!IsInputLocked)
            {
                _camera.Look(_lookInput);
            }

            _isJumpPressed = false;
    
            if (_viewmodelSway)
            {
                // Note: In the future, you may want to completely hide the Viewmodel for non-owners 
                // so they only see the 3rd person body, but skipping the update here is a good start.
                _viewmodelSway.UpdateSway(IsInputLocked ? Vector2.zero : _lookInput, isMoving);
            }
        }
    }
}