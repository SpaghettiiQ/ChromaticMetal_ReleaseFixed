using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using _Project.Core.Events;

namespace _Project.Features.LobbySystem.Scripts
{
    /// <summary>
    /// Bridges Core's NetworkSessionEvents.OnSessionEnded to the lobby UI: tears down the
    /// session if still alive, returns to MainMenu, and stashes a reason that
    /// LobbyUIController consumes once the main-menu scene rebinds.
    /// </summary>
    public class SessionEndHandler : MonoBehaviour
    {
        public static SessionEndHandler Singleton { get; private set; }
        public static DisconnectReason? PendingReason { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Singleton != null) return;
            var go = new GameObject(nameof(SessionEndHandler));
            go.AddComponent<SessionEndHandler>();
        }

        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Awake()
        {
            if (Singleton != null && Singleton != this)
            {
                Destroy(gameObject);
                return;
            }
            Singleton = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            NetworkSessionEvents.OnSessionEnded += HandleSessionEnded;
        }

        private void OnDisable()
        {
            NetworkSessionEvents.OnSessionEnded -= HandleSessionEnded;
        }

        public static DisconnectReason? ConsumePendingReason()
        {
            var r = PendingReason;
            PendingReason = null;
            return r;
        }

        public static string ReasonToMessage(DisconnectReason reason)
        {
            switch (reason)
            {
                case DisconnectReason.HostLost:
                case DisconnectReason.TransportFailure:
                    return "Connection to host lost.";
                case DisconnectReason.HostShutdown:
                    return "The host ended the game.";
                case DisconnectReason.KickedByHost:
                    return "You were removed from the game.";
                case DisconnectReason.ConnectionLostClient:
                    return "Your connection was interrupted.";
                default:
                    return "Disconnected.";
            }
        }

        private void HandleSessionEnded(DisconnectReason reason)
        {
            // Stash for the main menu to display once it rebinds.
            PendingReason = reason;

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsClient || nm.IsServer || nm.IsListening))
            {
                nm.Shutdown();
            }

            if (SceneManager.GetActiveScene().name != mainMenuSceneName)
            {
                SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            }
        }
    }
}
