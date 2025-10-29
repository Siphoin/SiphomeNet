using SiphomeNet.Network.Handlers;
using UnityEngine;

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
