using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace SiphomeNet.Network
{
    public interface INetworkSceneHandler
    {
        void LoadSceneForClients(string sceneName, LoadSceneMode mode, IEnumerable<ulong> targetClientIds);
        void UnloadSceneForClients(string sceneName, IEnumerable<ulong> targetClientIds);
    }
}