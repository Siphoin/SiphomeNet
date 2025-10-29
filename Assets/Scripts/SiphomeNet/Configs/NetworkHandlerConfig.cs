using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SiphomeNet.Network.Configs
{
    [CreateAssetMenu(fileName = "NetworkHandlerConfig", menuName = "SiphomeNet/Network Handler Config")]
    public class NetworkHandlerConfig : ScriptableObject
    {
        [Header("Network Settings")]
        [SerializeField] private NetworkManager _prefabNetworkManager;
        [SerializeField] private string _ipAddress = "127.0.0.1";
        [SerializeField] private ushort _port = 7777;
        [SerializeField] private ushort _clientPort = 443;
        [SerializeField] private int _maxPlayers = 4;
        [SerializeField] private float _clientConnectionTimeout = 12f;
        [SerializeField] private bool _useEncryption = true;

        [Header("Transport Settings")]
        [SerializeField] private int _connectTimeoutMS = 5000;
        [SerializeField] private int _maxPacketQueueSize = 2048;

        [Header("SSL/TLS Configuration")]
        [SerializeField] private bool _enableSSL = true;
        [SerializeField] private string _serverHostname = "example.ngrok.io";
        [SerializeField, TextArea] private string _caCertificatePath = "";
        [SerializeField, TextArea] private string _serverCertificatePath = "";
        [SerializeField, TextArea] private string _serverPrivateKeyPath = "";

        [Header("Auto Start Settings")]
        [SerializeField] private bool _autoStartInBatchMode = true;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        [SerializeField] private bool _autoStartAsHostInEditor = false;
        [SerializeField] private bool _autoStartAsClientInEditor = false;
        [SerializeField] private KeyCode _keyCodeStartHost = KeyCode.F1;
        [SerializeField] private KeyCode _keyCodeStartClient = KeyCode.F2;
#endif

        [Header("Sub Network Handlers")]
        [SerializeField] private NetworkBehaviour[] _subNetwoekHandlers;

        public NetworkManager PrefabNetworkManager => _prefabNetworkManager;
        public string IpAddress => _ipAddress;
        public ushort Port => _port;
        public ushort ClientPort => _clientPort;
        public int MaxPlayers => _maxPlayers;
        public float ClientConnectionTimeout => _clientConnectionTimeout;
        public bool UseEncryption => _useEncryption;
        public int ConnectTimeoutMS => _connectTimeoutMS;
        public int MaxPacketQueueSize => _maxPacketQueueSize;
        public bool EnableSSL => _enableSSL;
        public string ServerHostname => _serverHostname;
        public string CACertificatePath => _caCertificatePath;
        public string ServerCertificatePath => _serverCertificatePath;
        public string ServerPrivateKeyPath => _serverPrivateKeyPath;
        public bool AutoStartInBatchMode => _autoStartInBatchMode;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        public bool AutoStartAsHostInEditor => _autoStartAsHostInEditor;
        public bool AutoStartAsClientInEditor => _autoStartAsClientInEditor;
        public KeyCode KeyCodeStartHost => _keyCodeStartHost;
        public KeyCode KeyCodeStartClient => _keyCodeStartClient;
#endif

        public IEnumerable<NetworkBehaviour> SubNetwoekHandlers => _subNetwoekHandlers;
    }
}
