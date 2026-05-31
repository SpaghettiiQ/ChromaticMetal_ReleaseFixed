using Unity.Netcode;
using UnityEngine;
using _Project.Core.Events;

namespace _Project.Core.Networking
{
    /// <summary>
    /// Routes NetworkManager disconnect / server-stopped / transport-failure callbacks
    /// to a single OnSessionEnded event Features can listen to. Lives on the persistent
    /// bootstrap GameObject. Call MarkIntentionalShutdown() right before any voluntary
    /// Shutdown() so the resulting callback is suppressed.
    /// </summary>
    public class NetworkSessionWatcher : MonoBehaviour
    {
        public static NetworkSessionWatcher Singleton { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Singleton != null) return;
            var go = new GameObject(nameof(NetworkSessionWatcher));
            go.AddComponent<NetworkSessionWatcher>();
        }

        private bool _intentionalShutdown;
        private bool _wasConnectedClient;
        private float _connectionLostAt = -1f;

        private NetworkManager _subscribedTo;

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
            TrySubscribe();
        }

        private void OnDisable()
        {
            TryUnsubscribe();
        }

        private void TrySubscribe()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || _subscribedTo == nm) return;
            TryUnsubscribe();
            nm.OnClientDisconnectCallback += HandleClientDisconnect;
            nm.OnServerStopped += HandleServerStopped;
            nm.OnTransportFailure += HandleTransportFailure;
            _subscribedTo = nm;
        }

        private void TryUnsubscribe()
        {
            if (_subscribedTo == null) return;
            _subscribedTo.OnClientDisconnectCallback -= HandleClientDisconnect;
            _subscribedTo.OnServerStopped -= HandleServerStopped;
            _subscribedTo.OnTransportFailure -= HandleTransportFailure;
            _subscribedTo = null;
        }

        public void MarkIntentionalShutdown() => _intentionalShutdown = true;

        private void HandleClientDisconnect(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (clientId != nm.LocalClientId) return;
            if (_intentionalShutdown) { _intentionalShutdown = false; return; }

            var reasonStr = nm.DisconnectReason;
            var reason = string.IsNullOrEmpty(reasonStr) ? DisconnectReason.HostLost : DisconnectReason.KickedByHost;
            NetworkSessionEvents.RaiseSessionEnded(reason);
        }

        private void HandleServerStopped(bool wasHost)
        {
            if (_intentionalShutdown) { _intentionalShutdown = false; return; }
            NetworkSessionEvents.RaiseSessionEnded(DisconnectReason.HostShutdown);
        }

        private void HandleTransportFailure()
        {
            if (_intentionalShutdown) { _intentionalShutdown = false; return; }
            NetworkSessionEvents.RaiseSessionEnded(DisconnectReason.TransportFailure);
        }

        private void Update()
        {
            if (_subscribedTo == null) { TrySubscribe(); return; }

            var nm = _subscribedTo;
            bool isConnectedNow = nm.IsClient && !nm.IsServer && nm.IsConnectedClient;

            if (_wasConnectedClient && !isConnectedNow && nm.IsClient && !nm.IsServer)
            {
                if (_connectionLostAt < 0f) _connectionLostAt = Time.unscaledTime;
                else if (Time.unscaledTime - _connectionLostAt > 2f)
                {
                    _connectionLostAt = -1f;
                    _wasConnectedClient = false;
                    if (!_intentionalShutdown)
                    {
                        NetworkSessionEvents.RaiseSessionEnded(DisconnectReason.ConnectionLostClient);
                    }
                    else
                    {
                        _intentionalShutdown = false;
                    }
                }
            }
            else
            {
                _connectionLostAt = -1f;
                _wasConnectedClient = isConnectedNow;
            }
        }
    }
}
