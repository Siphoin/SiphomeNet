using SiphomeNet.Network.Handlers;
using UnityEngine;
using UnityEngine.UI;
namespace SiphomeNet.Demo
{
    public class ConnectButtons : MonoBehaviour
    {
        [SerializeField] private Button _buttonStartHost;
        [SerializeField] private Button _buttonStartClient;

        private void Awake()
        {
            _buttonStartHost.onClick.AddListener(StartHost);
            _buttonStartClient.onClick.AddListener(StartClient);
        }

        private void OnDisable()
        {
            _buttonStartHost.onClick.RemoveAllListeners();
            _buttonStartClient.onClick.RemoveAllListeners();
        }

        private void StartHost() => NetworkHandler.Singleton.StartHost();
        private void StartClient () => NetworkHandler.Singleton.StartClient();
    }
}
