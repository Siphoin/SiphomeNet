using System;
using System.Linq;
using SiphomeNet.Network.Handlers;
using SiphomeNet.Network.Models;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SiphomeNet.Network
{
    public abstract class RoomObject : NetworkBehaviour
    {
        private INetworkHandler _networkHandler;

        private NetworkVariable<NetworkGuid> _guidRoom =
            new(writePerm: NetworkVariableWritePermission.Server,
                readPerm: NetworkVariableReadPermission.Everyone);

        private NetworkGuid _guidRoomServer;

        public string GUID => _guidRoom.Value.Guid.ToString();

        public Guid GuidRoom => _guidRoom.Value.Guid;
        public bool IsOwnedByPlayer => IsOwner || NetworkManager is null;
        public Scene Scene => gameObject.scene;

        protected INetworkHandler Handler => _networkHandler;

        private void Awake()
        {
            _networkHandler = NetworkHandler.Singleton;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _guidRoom.Value = _guidRoomServer;
                Debug.Log($"[RoomObject] Spawned with RoomGuid={_guidRoom.Value.Guid}");

                ApplyInitialVisibility();

                NetworkManager.OnClientConnectedCallback += OnClientConnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            }
        }

        public void SetRoom(NetworkGuid roomGuid)
        {
            _guidRoomServer = roomGuid;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            // Сервер (clientId = 0) всегда должен видеть все объекты
            if (clientId == 0) return;

            var allowedClients = _networkHandler.RoomsHandler
                .GetPlayersInRoom(_guidRoom.Value.Guid)
                .Select(p => p.ClientId)
                .ToHashSet();

            if (allowedClients.Contains(clientId))
            {
                if (!NetworkObject.IsNetworkVisibleTo(clientId))
                    NetworkObject.NetworkShow(clientId);
            }
            else
            {
                if (NetworkObject.IsNetworkVisibleTo(clientId))
                    NetworkObject.NetworkHide(clientId);
            }
        }

        private void ApplyInitialVisibility()
        {
            if (!IsServer) return;

            var allowedClients = _networkHandler.RoomsHandler
                .GetPlayersInRoom(_guidRoom.Value.Guid)
                .Select(p => p.ClientId)
                .ToHashSet();

            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                // Сервер (clientId = 0) всегда должен видеть все объекты
                if (clientId == 0)
                {
                    if (!NetworkObject.IsNetworkVisibleTo(clientId))
                        NetworkObject.NetworkShow(clientId);
                    continue;
                }

                if (allowedClients.Contains(clientId))
                {
                    if (!NetworkObject.IsNetworkVisibleTo(clientId))
                        NetworkObject.NetworkShow(clientId);
                }
                else
                {
                    if (NetworkObject.IsNetworkVisibleTo(clientId))
                        NetworkObject.NetworkHide(clientId);
                }
            }
        }
    }
}