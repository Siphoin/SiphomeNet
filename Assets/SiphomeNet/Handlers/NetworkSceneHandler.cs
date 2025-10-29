using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SiphomeNet.Network
{
    public class NetworkSceneHandler : NetworkBehaviour, INetworkSceneHandler
    {


        public void LoadSceneForClients(string sceneName, LoadSceneMode mode, IEnumerable<ulong> targetClientIds)
        {
            if (!IsServer)
            {
                Debug.LogWarning("LoadSceneForClients должен вызываться только с сервера");
                return;
            }

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = targetClientIds.ToArray()
                }
            };

            LoadSceneClientRpc(sceneName, (int)mode, rpcParams);
        }

        [ClientRpc]
        private void LoadSceneClientRpc(string sceneName, int mode, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"[NetworkSceneController] Loading scene {sceneName} in mode {(LoadSceneMode)mode}");
            SceneManager.LoadScene(sceneName, (LoadSceneMode)mode);
        }

        public void UnloadSceneForClients(string sceneName, IEnumerable<ulong> targetClientIds)
        {
            if (!IsServer) return;

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = targetClientIds.ToArray()
                }
            };

            UnloadSceneClientRpc(sceneName, rpcParams);
        }

        [ClientRpc]
        private void UnloadSceneClientRpc(string sceneName, ClientRpcParams rpcParams = default)
        {
            Debug.Log($"[NetworkSceneController] Unloading scene {sceneName}");
            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                SceneManager.UnloadSceneAsync(sceneName);
            }
        }
    }
}
