using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SiphomeNet.Network.Configs;
using SiphomeNet.Network.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SiphomeNet.Network.Handlers
{
    public class NetworkHandler : MonoBehaviour, INetworkHandler
    {
        public static INetworkHandler Singleton { get; private set; }

        [SerializeField] private NetworkHandlerConfig _config;
        private NetworkManager _networkManager;
        private bool _isInitialized;
        private CancellationTokenSource _connectionTimeoutCts;
        private bool _isConnecting;
        private bool _isDisposed;

        private RoomListHandler _roomListHandler;
        private PlayerListHandler _playerListHandler;
        private NetworkSceneHandler _networkSceneHandler;
        private RoomObjectSpawnHandler _roomObjectSpawnHandler;
        private PingHandler _pingHandler;

        public event Action<ulong> OnConnected;
        public event Action<ulong> OnDisconnected;

        public IRoomListHandler RoomsHandler => _roomListHandler ??= FindAnyObjectByType<RoomListHandler>();
        public IPlayerListHandler Players => _playerListHandler ??= FindAnyObjectByType<PlayerListHandler>();
        public INetworkSceneHandler SceneHandler => _networkSceneHandler ??= FindAnyObjectByType<NetworkSceneHandler>();
        public IPingHandler PingHandler => _pingHandler ??= FindAnyObjectByType<PingHandler>();
        private IRoomObjectSpawnHandler RoomSpawner => _roomObjectSpawnHandler ??= FindAnyObjectByType<RoomObjectSpawnHandler>();

        public bool IsConnected
        {
            get
            {
                if (_networkManager == null)
                    return false;

                if (_networkManager.IsHost)
                    return _networkManager.IsListening;

                if (_networkManager.IsClient)
                    return _networkManager.IsConnectedClient;

                return false;
            }
        }

        private void Awake()
        {
            if (Singleton == null)
            {
                Singleton = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (Application.isBatchMode && _config.AutoStartInBatchMode)
                StartHost();
        }

        private void Start()
        {
            InitializeNetwork();
            HandleAutoStart();
        }

        private void InitializeNetwork()
        {
            if (_isInitialized) return;
            if (_config == null || _config.PrefabNetworkManager == null)
            {
                Debug.LogError("NetworkHandlerConfig is not assigned or missing NetworkManager prefab.");
                return;
            }

            _networkManager = Instantiate(_config.PrefabNetworkManager);
            if (!_networkManager.TryGetComponent<UnityTransport>(out var unityTransport))
            {
                Debug.LogError("NetworkManager prefab missing UnityTransport.");
                return;
            }

            SetupTransport(unityTransport);

            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            _isInitialized = true;
            LogNetworkConfiguration();
        }

        private void SetupTransport(UnityTransport transport)
        {
            string portEnv = Environment.GetEnvironmentVariable("UNITY_SERVER_PORT");
            ushort port = string.IsNullOrEmpty(portEnv) ? _config.Port : ushort.Parse(portEnv);
            bool ssl = _config.EnableSSL;
            string ngrokAddress = _config.ServerHostname;

            if (Application.isBatchMode)
            {
                ssl = false;
                transport.UseEncryption = false;
                transport.UseWebSockets = true;
                transport.SetConnectionData("0.0.0.0", port);
            }
            else
            {
                transport.UseEncryption = _config.UseEncryption;
                transport.UseWebSockets = true;
                transport.SetConnectionData(ngrokAddress, _config.ClientPort);
            }

            transport.ConnectTimeoutMS = _config.ConnectTimeoutMS;
            transport.MaxPacketQueueSize = _config.MaxPacketQueueSize;

            if (ssl)
                SetupSSL(transport);
            else if (!Application.isBatchMode)
                transport.SetClientSecrets(serverCommonName: ngrokAddress);
        }

        private void SetupSSL(UnityTransport transport)
        {
            try
            {
                if (Application.isBatchMode)
                    SetupServerSSL(transport);
                else
                    SetupClientSSL(transport);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SSL setup failed: {ex.Message}");
            }
        }

        private void SetupServerSSL(UnityTransport transport)
        {
            string certPath = GetServerCertificatePath();
            string keyPath = GetServerPrivateKeyPath();

            if (!File.Exists(certPath) || !File.Exists(keyPath))
            {
                Debug.LogError($"Missing SSL files.\nCert: {certPath}\nKey: {keyPath}");
                return;
            }

            string serverCertificate = File.ReadAllText(certPath);
            string serverPrivateKey = File.ReadAllText(keyPath);

            transport.SetServerSecrets(serverCertificate, serverPrivateKey);
        }

        private void SetupClientSSL(UnityTransport transport)
        {
            string serverHostname = _config.ServerHostname;

            if (!string.IsNullOrEmpty(_config.CACertificatePath))
            {
                string caCertificate = _config.CACertificatePath;
                transport.SetClientSecrets(serverCommonName: serverHostname, caCertificate: caCertificate);
            }
            else
            {
                string caCertificatePath = GetCACertificatePath();
                if (!string.IsNullOrEmpty(caCertificatePath) && File.Exists(caCertificatePath))
                {
                    string caCertificate = File.ReadAllText(caCertificatePath);
                    transport.SetClientSecrets(serverCommonName: serverHostname, caCertificate: caCertificate);
                }
                else
                {
                    transport.SetClientSecrets(serverCommonName: serverHostname);
                }
            }
        }

        private string GetServerCertificatePath()
        {
            string envPath = Environment.GetEnvironmentVariable("SSL_SERVER_CERT_PATH");
            return string.IsNullOrEmpty(envPath) ? _config.ServerCertificatePath : envPath;
        }

        private string GetServerPrivateKeyPath()
        {
            string envPath = Environment.GetEnvironmentVariable("SSL_SERVER_KEY_PATH");
            return string.IsNullOrEmpty(envPath) ? _config.ServerPrivateKeyPath : envPath;
        }

        private string GetCACertificatePath()
        {
            string envPath = Environment.GetEnvironmentVariable("SSL_CA_CERT_PATH");
            return string.IsNullOrEmpty(envPath) ? _config.CACertificatePath : envPath;
        }

        private void LogNetworkConfiguration()
        {
            if (_networkManager.TryGetComponent<UnityTransport>(out var transport))
            {
                Debug.Log("=== Network Configuration ===");
                Debug.Log($"Address: {_config.IpAddress}:{_config.Port}");
                Debug.Log($"SSL Enabled: {_config.EnableSSL}");
                Debug.Log($"Batch Mode: {Application.isBatchMode}");
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        private void Update()
        {
            if (Input.GetKeyDown(_config.KeyCodeStartHost))
                StartHostAsLocalhost();
            if (Input.GetKeyDown(_config.KeyCodeStartClient))
                StartClientAsLocalhost();
        }
#endif

        private bool StartHostAsLocalhost()
        {
            if (!_isInitialized || _networkManager.IsListening) return false;
            if (_networkManager.TryGetComponent<UnityTransport>(out var transport))
            {
                transport.UseEncryption = false;
                transport.SetConnectionData("127.0.0.1", _config.Port);
            }
            bool success = _networkManager.StartHost();
            if (success)
                StartCoroutine(CreateSubNetworkHandlersDelayed());
            return success;
        }

        private bool StartClientAsLocalhost()
        {
            if (!_isInitialized || _networkManager.IsListening || _isConnecting) return false;
            SafeCancelAndDispose(ref _connectionTimeoutCts);
            _connectionTimeoutCts = new CancellationTokenSource();
            _isConnecting = true;

            if (_networkManager.TryGetComponent<UnityTransport>(out var transport))
            {
                transport.UseEncryption = false;
                transport.SetConnectionData("127.0.0.1", _config.Port);
            }

            bool success = _networkManager.StartClient();
            if (success)
                StartCoroutine(StartConnectionTimeout());
            else
                HandleConnectionFailed();
            return success;
        }

        private void HandleAutoStart()
        {
#if UNITY_EDITOR
            if (_config.AutoStartAsHostInEditor)
                StartHostAsLocalhost();
            else if (_config.AutoStartAsClientInEditor)
                StartClient();
#else
            if (_config.AutoStartInBatchMode && Application.isBatchMode)
                StartHost();
#endif
        }

        public bool StartHost()
        {
            if (!_isInitialized || _networkManager.IsListening) return false;
            bool success = _networkManager.StartHost();
            if (success)
                StartCoroutine(CreateSubNetworkHandlersDelayed());
            return success;
        }

        public bool StartClient()
        {
            if (!_isInitialized || _networkManager.IsListening || _isConnecting) return false;
            SafeCancelAndDispose(ref _connectionTimeoutCts);
            _connectionTimeoutCts = new CancellationTokenSource();
            _isConnecting = true;

            bool success = _networkManager.StartClient();
            if (success)
                StartCoroutine(StartConnectionTimeout());
            else
                HandleConnectionFailed();
            return success;
        }

        public void StopNetwork()
        {
            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.OnClientConnectedCallback -= HandleClientConnected;
                _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                Destroy(_networkManager.gameObject);
                _networkManager = null;
            }
        }

        private void CreateSubNetworkHandlers()
        {
            if (_config.SubNetwoekHandlers == null) return;
            foreach (var handlerPrefab in _config.SubNetwoekHandlers)
            {
                if (handlerPrefab == null) continue;
                var handlerInstance = Instantiate(handlerPrefab);
                var networkObject = handlerInstance.GetComponent<NetworkObject>();
                if (networkObject != null)
                    networkObject.Spawn();
            }
        }

        private IEnumerator CreateSubNetworkHandlersDelayed()
        {
            // Ждем пока NetworkManager начнет прослушивание
            yield return new WaitUntil(() => _networkManager != null && _networkManager.IsListening);


            CreateSubNetworkHandlers();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (_networkManager.IsClient && !_networkManager.IsHost && clientId == _networkManager.LocalClientId)
            {
                _isConnecting = false;
                SafeCancelAndDispose(ref _connectionTimeoutCts);
            }
            OnConnected?.Invoke(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (_networkManager.IsClient && !_networkManager.IsHost && clientId == _networkManager.LocalClientId)
                HandleConnectionFailed();
            OnDisconnected?.Invoke(clientId);
        }

        private IEnumerator StartConnectionTimeout()
        {
            float timeout = _config.ClientConnectionTimeout;
            float elapsed = 0f;

            while (elapsed < timeout && _isConnecting)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_isConnecting && _networkManager != null && _networkManager.IsClient)
                HandleConnectionTimeout();
        }

        private void HandleConnectionTimeout()
        {
            if (_networkManager != null && _networkManager.IsClient)
                _networkManager.Shutdown();
            HandleConnectionFailed();
        }

        private void HandleConnectionFailed()
        {
            if (_isDisposed) return;
            _isConnecting = false;
            SafeCancelAndDispose(ref _connectionTimeoutCts);
        }

        private void SafeCancelAndDispose(ref CancellationTokenSource cts)
        {
            if (cts == null) return;
            try
            {
                if (!cts.IsCancellationRequested)
                    cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // уже уничтожен — игнорируем
            }
            finally
            {
                try { cts.Dispose(); } catch { }
                cts = null;
            }
        }

        public NetworkObject SpawnRoomObject(NetworkObject prefab, NetworkGuid roomGuid, Vector3 position = default, Quaternion rotation = default, Scene scene = default)
            => RoomSpawner.SpawnRoomObject(prefab, roomGuid, position, rotation, scene);

        public NetworkObject SpawnRoomObject(NetworkObject prefab, NetworkGuid roomGuid, ulong ownerClientId, Vector3 position = default, Quaternion rotation = default, Scene scene = default)
            => RoomSpawner.SpawnRoomObject(prefab, roomGuid, ownerClientId, position, rotation, scene);

        public void DespawnRoomObject(NetworkObject networkObject) => RoomSpawner.DespawnRoomObject(networkObject);
        public void DespawnAllRoomObjects(string roomGuid) => RoomSpawner.DespawnAllRoomObjects(roomGuid);
        public IEnumerable<NetworkObject> GetRoomObjects(string roomGuid) => RoomSpawner.GetRoomObjects(roomGuid);
        public IEnumerable<T> GetRoomObjects<T>(string roomGuid) where T : NetworkBehaviour => RoomSpawner.GetRoomObjects<T>(roomGuid);

        private void OnDestroy()
        {
            _isDisposed = true;
            SafeCancelAndDispose(ref _connectionTimeoutCts);
            Singleton = null;
        }
    }
}