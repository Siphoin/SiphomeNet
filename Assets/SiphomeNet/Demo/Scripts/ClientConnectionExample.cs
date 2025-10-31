using SiphomeNet.Network.Handlers;
using UnityEngine;
namespace SiphomeNet.Demo
{
    public class ClientConnectionExample : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                NetworkHandler.Singleton.StartClient();
            }
        }
    }

}