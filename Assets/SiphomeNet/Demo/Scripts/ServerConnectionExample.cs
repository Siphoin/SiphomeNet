using SiphomeNet.Network.Handlers;
using UnityEngine;
namespace SiphomeNet.Demo
{
    public class ServerConnectionExample : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                NetworkHandler.Singleton.StartHost();
            }
        }
    }

}