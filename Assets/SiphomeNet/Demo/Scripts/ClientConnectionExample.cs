using SiphomeNet.Network.Handlers;
using UnityEngine;

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
