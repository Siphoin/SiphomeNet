using System.Collections.Generic;
using SiphomeNet.Network.Models;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SiphomeNet.Network.Handlers
{
    public interface IRoomObjectSpawnHandler
    {
        NetworkObject SpawnRoomObject(NetworkObject prefab, NetworkGuid roomGuid, Vector3 position = default, Quaternion rotation = default, Scene scene = default);
        NetworkObject SpawnRoomObject(NetworkObject prefab, NetworkGuid roomGuid, ulong ownerClientId, Vector3 position = default, Quaternion rotation = default, Scene scene = default);
        void DespawnRoomObject(NetworkObject networkObject);
        void DespawnAllRoomObjects(string roomGuid);
        IEnumerable<NetworkObject> GetRoomObjects(string roomGuid);
        IEnumerable<T> GetRoomObjects<T>(string roomGuid) where T : NetworkBehaviour;
    }
}