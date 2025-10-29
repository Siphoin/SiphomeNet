using UnityEngine;
using SiphomeNet.Network.Handlers;
using Unity.Netcode;
using System.Collections;

#if UNITY_EDITOR
namespace SiphomeNet.Network.Observers
{
    public class NetworkPingDisplayObserver : MonoBehaviour
    {
        private INetworkHandler _networkHandler;
        private GUIStyle _pingStyle;
        private int _currentPing;
        private bool _isConnected = false;
        private Coroutine _pingCheckCoroutine;

        private void Start()
        {
            _networkHandler = NetworkHandler.Singleton;
            if (_networkHandler == null)
            {
                Debug.LogError("NetworkHandler not found");
                return;
            }

            _pingStyle = new GUIStyle
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _networkHandler.OnConnected += HandleConnected;
            _networkHandler.OnDisconnected += HandleDisconnected;
        }

        private void OnDestroy()
        {
            if (_networkHandler != null)
            {
                _networkHandler.OnConnected -= HandleConnected;
                _networkHandler.OnDisconnected -= HandleDisconnected;
            }

            if (_pingCheckCoroutine != null)
            {
                StopCoroutine(_pingCheckCoroutine);
            }
        }

        private void HandleConnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null &&
                clientId == NetworkManager.Singleton.LocalClientId)
            {
                _isConnected = true;
                SetupPingSubscription();
            }
        }

        private void HandleDisconnected(ulong clientId)
        {
            if (clientId == 0 ||
                (NetworkManager.Singleton != null &&
                 clientId == NetworkManager.Singleton.LocalClientId))
            {
                _isConnected = false;
                _currentPing = 0;

                if (_pingCheckCoroutine != null)
                {
                    StopCoroutine(_pingCheckCoroutine);
                    _pingCheckCoroutine = null;
                }
            }
        }

        private void SetupPingSubscription()
        {
            if (_pingCheckCoroutine != null)
            {
                StopCoroutine(_pingCheckCoroutine);
            }
            _pingCheckCoroutine = StartCoroutine(PingCheckRoutine());
        }

        private IEnumerator PingCheckRoutine()
        {
            while (_isConnected && _networkHandler.PingHandler != null)
            {
                _currentPing = _networkHandler.PingHandler.GetCurrentPing();
                yield return new WaitForSeconds(1f);
            }
        }

        private void OnGUI()
        {
            if (_isConnected &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsClient &&
                !NetworkManager.Singleton.IsServer)
            {
                Rect pingRect = new Rect(10, 10, 200, 30);
                UpdatePingColor();
                GUI.Label(pingRect, $"Ping: {_currentPing}ms", _pingStyle);
            }
        }

        private void UpdatePingColor()
        {
            if (_currentPing == 0)
            {
                _pingStyle.normal.textColor = Color.white;
            }
            else if (_currentPing < 50)
            {
                _pingStyle.normal.textColor = Color.green;
            }
            else if (_currentPing < 100)
            {
                _pingStyle.normal.textColor = Color.yellow;
            }
            else if (_currentPing < 200)
            {
                _pingStyle.normal.textColor = new Color(1.0f, 0.5f, 0.0f);
            }
            else
            {
                _pingStyle.normal.textColor = Color.red;
            }
        }
    }
}
#endif