using System.Collections.Generic;
using SiphomeNet.Network.Models;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Scene = UnityEngine.SceneManagement.Scene;

namespace SiphomeNet.Network.Handlers
{
    public class RoomObjectSpawnHandler : NetworkBehaviour, IRoomObjectSpawnHandler
    {
        private readonly Dictionary<string, List<NetworkObject>> _roomObjects = new();
        private readonly Dictionary<ulong, NetworkObject> _spawnedObjects = new();

        public NetworkObject SpawnRoomObject(NetworkObject prefab, NetworkGuid roomGuid, Vector3 position = default, Quaternion rotation = default, Scene scene = default)
        {
            if (!IsServer) return null;

            string guidString = roomGuid.Guid.ToString();
            var networkObject = Instantiate(prefab, position, rotation);
            if (scene != default)
            {
                SceneManager.MoveGameObjectToScene(networkObject.gameObject, scene);
            }

            var roomObject = networkObject.GetComponent<RoomObject>();
            roomObject.SetRoom(roomGuid);

            networkObject.Spawn();

            RegisterRoomObject(guidString, networkObject);
            _spawnedObjects[networkObject.NetworkObjectId] = networkObject;

            return networkObject;
        }

        public NetworkObject SpawnRoomObject(NetworkObject prefab, NetworkGuid roomGuid, ulong ownerClientId, Vector3 position = default, Quaternion rotation = default, Scene scene = default)
        {
            string guidString = roomGuid.Guid.ToString();
            var networkObject = Instantiate(prefab, position, rotation);
            if (scene != default)
            {
                SceneManager.MoveGameObjectToScene(networkObject.gameObject, scene);
            }
            networkObject.GetComponent<RoomObject>().SetRoom(roomGuid);
            networkObject.SpawnWithOwnership(ownerClientId);

            RegisterRoomObject(guidString, networkObject);
            _spawnedObjects[networkObject.NetworkObjectId] = networkObject;

            return networkObject;
        }

        public void DespawnRoomObject(NetworkObject networkObject)
        {
            if (!IsServer) return;

            if (networkObject != null && networkObject.IsSpawned)
            {
                var roomObject = networkObject.GetComponent<RoomObject>();
                if (roomObject != null)
                {
                    UnregisterRoomObject(roomObject.GUID, networkObject);
                }

                _spawnedObjects.Remove(networkObject.NetworkObjectId);
                networkObject.Despawn();
                Destroy(networkObject.gameObject);
            }
        }

        public void DespawnAllRoomObjects(string roomGuid)
        {
            if (!IsServer) return;

            if (_roomObjects.TryGetValue(roomGuid, out var objects))
            {
                for (int i = objects.Count - 1; i >= 0; i--)
                {
                    var obj = objects[i];
                    if (obj != null && obj.IsSpawned)
                    {
                        DespawnRoomObject(obj);
                    }
                }
                _roomObjects.Remove(roomGuid);
            }
        }

        public IEnumerable<NetworkObject> GetRoomObjects(string roomGuid)
        {
            return _roomObjects.TryGetValue(roomGuid, out var objects) ? new List<NetworkObject>(objects) : new List<NetworkObject>();
        }

        public IEnumerable<T> GetRoomObjects<T>(string roomGuid) where T : NetworkBehaviour
        {
            var result = new List<T>();
            if (_roomObjects.TryGetValue(roomGuid, out var objects))
            {
                foreach (var obj in objects)
                {
                    var component = obj.GetComponent<T>();
                    if (component != null)
                    {
                        result.Add(component);
                    }
                }
            }
            return result;
        }

        private void RegisterRoomObject(string roomGuid, NetworkObject networkObject)
        {
            if (!_roomObjects.ContainsKey(roomGuid))
            {
                _roomObjects[roomGuid] = new List<NetworkObject>();
            }
            _roomObjects[roomGuid].Add(networkObject);
        }

        private void UnregisterRoomObject(string roomGuid, NetworkObject networkObject)
        {
            if (_roomObjects.TryGetValue(roomGuid, out var objects))
            {
                objects.Remove(networkObject);
                if (objects.Count == 0)
                {
                    _roomObjects.Remove(roomGuid);
                }
            }
        }
    }
}