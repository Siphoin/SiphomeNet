using SiphomeNet.Network.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SiphomeNet.Network.Handlers
{
    public class RoomListHandler : NetworkBehaviour, IRoomListHandler
    {
        public static RoomListHandler Singleton { get; private set; }

        private readonly NetworkList<NetworkRoom> _rooms = new();

        public event Action<NetworkRoom> OnRoomAdded;
        public event Action<NetworkRoom> OnRoomRemoved;
        public event Action<NetworkRoom> OnRoomUpdated;
        public event Action<NetworkRoom> OnRoomCreated;
        public event Action<NetworkRoom> OnRoomJoined;
        public event Action<NetworkRoom> OnRoomLeft;
        public event Action<NetworkRoom> OnRoomAboutToBeRemoved;

        private readonly Dictionary<ulong, Guid> _playerToRoom = new();

        public IEnumerable<NetworkRoom> Rooms
        {
            get
            {
                var list = new List<NetworkRoom>();
                foreach (NetworkRoom room in _rooms)
                {
                    list.Add(room);
                }
                return list;
            }
        }

        public NetworkRoom CurrentRoom => GetRoomByClientId(NetworkManager.LocalClientId);

        private RoomObjectSpawnHandler _roomObjectSpawnHandler;

        private void Awake()
        {
            if (Singleton == null)
            {
                Singleton = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            _rooms.OnListChanged += HandleRoomsListChanged;
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            _roomObjectSpawnHandler = FindObjectOfType<RoomObjectSpawnHandler>();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            _rooms.OnListChanged -= HandleRoomsListChanged;
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;

            if (Singleton == this)
            {
                Singleton = null;
            }
        }

        private void HandleRoomsListChanged(NetworkListEvent<NetworkRoom> e)
        {
            switch (e.Type)
            {
                case NetworkListEvent<NetworkRoom>.EventType.Add:
                    OnRoomAdded?.Invoke(e.Value);
                    break;
                case NetworkListEvent<NetworkRoom>.EventType.Remove:
                case NetworkListEvent<NetworkRoom>.EventType.RemoveAt:
                    OnRoomRemoved?.Invoke(e.Value);
                    break;
                case NetworkListEvent<NetworkRoom>.EventType.Value:
                    OnRoomUpdated?.Invoke(e.Value);
                    break;
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (IsServer) RemoveRoomByClientId(clientId);
        }

        // === RPCs (server determines who called via rpcParams.Receive.SenderClientId) ===

        [ServerRpc(RequireOwnership = false)]
        public void CreateRoomServerRpc(FixedString32Bytes roomName, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[RoomListHandler] CreateRoomServerRpc called by {clientId} with name '{roomName}'");

            if (_playerToRoom.ContainsKey(clientId))
            {
                Debug.LogWarning($"Client {clientId} already in a room");
                return;
            }

            var room = new NetworkRoom(clientId, roomName);
            _rooms.Add(room);

            _playerToRoom[clientId] = room.Guid;

            var playerList = FindObjectOfType<PlayerListHandler>();
            playerList?.UpdatePlayerRoom(room.Guid, clientId);

            if (IsClientConnected(clientId))
            {
                RoomCreatedClientRpc(room, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }

            Debug.Log($"[RoomListHandler] Room created: {room.Guid} by client {clientId}");
        }

        [ClientRpc]
        private void RoomCreatedClientRpc(NetworkRoom room, ClientRpcParams rpcParams = default)
        {
            OnRoomCreated?.Invoke(room);
            OnRoomJoined?.Invoke(room);
        }

        [ServerRpc(RequireOwnership = false)]
        public void JoinRoomServerRpc(FixedString128Bytes guid, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[RoomListHandler] JoinRoomServerRpc called by {clientId} for guid '{guid}'");

            if (!FixedString128BytesToGuid(guid, out Guid roomGuid))
            {
                Debug.LogWarning("Invalid guid in JoinRoomServerRpc");
                return;
            }

            if (!Rooms.Any(r => r.Guid == roomGuid))
            {
                Debug.LogWarning($"Room {roomGuid} not found");
                return;
            }

            _playerToRoom[clientId] = roomGuid;

            var playerList = FindObjectOfType<PlayerListHandler>();
            playerList?.UpdatePlayerRoom(roomGuid, clientId);

            if (IsClientConnected(clientId))
            {
                var room = Rooms.First(r => r.Guid == roomGuid);
                RoomJoinedClientRpc(room, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }

            Debug.Log($"[RoomListHandler] Client {clientId} joined room {roomGuid}");
        }

        [ClientRpc]
        private void RoomJoinedClientRpc(NetworkRoom room, ClientRpcParams rpcParams = default)
        {
            OnRoomJoined?.Invoke(room);
        }

        [ServerRpc(RequireOwnership = false)]
        public void LeaveRoomServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[RoomListHandler] LeaveRoomServerRpc called by {clientId}");

            if (!_playerToRoom.TryGetValue(clientId, out Guid roomGuid) || roomGuid == Guid.Empty)
            {
                Debug.LogWarning($"Client {clientId} not in a room");
                return;
            }

            var room = Rooms.FirstOrDefault(r => r.Guid == roomGuid);

            _playerToRoom.Remove(clientId);

            var playerList = FindObjectOfType<PlayerListHandler>();
            playerList?.UpdatePlayerRoom(Guid.Empty, clientId);

            if (IsClientConnected(clientId))
            {
                RoomLeftClientRpc(room, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }

            if (room.ClientId == clientId)
            {
                RemoveRoomByClientId(clientId);
                Debug.Log($"Owner {clientId} left room {room.Name}, room removed");
            }
            else
            {
                Debug.Log($"Client {clientId} left room {roomGuid}");
            }
        }

        [ClientRpc]
        private void RoomLeftClientRpc(NetworkRoom room, ClientRpcParams rpcParams = default)
        {
            OnRoomLeft?.Invoke(room);
        }

        // === Helpers ===

        private bool IsClientConnected(ulong clientId)
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId);
        }

        private void RemoveRoomByClientId(ulong ownerClientId)
        {
            NetworkRoom? roomToRemove = null;

            for (int i = _rooms.Count - 1; i >= 0; i--)
            {
                if (_rooms[i].ClientId == ownerClientId)
                {
                    roomToRemove = _rooms[i];

                    OnRoomAboutToBeRemoved?.Invoke(roomToRemove.Value);

                    _rooms.RemoveAt(i);
                    break;
                }
            }

            if (roomToRemove == null) return;
            var roomGuid = roomToRemove.Value.Guid;

            var clientsInRoom = _playerToRoom
                .Where(kvp => kvp.Value == roomGuid)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var clientId in clientsInRoom)
            {
                _playerToRoom.Remove(clientId);

                var playerList = FindObjectOfType<PlayerListHandler>();
                playerList?.UpdatePlayerRoom(Guid.Empty, clientId);

                if (IsClientConnected(clientId))
                {
                    RoomLeftClientRpc(roomToRemove.Value, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                    });
                }
            }

            var spawnHandler = FindObjectOfType<RoomObjectSpawnHandler>();
            spawnHandler?.DespawnAllRoomObjects(roomGuid.ToString());

            Debug.Log($"[RoomListHandler] Room {roomGuid} removed, all objects despawned");
        }

        public NetworkRoom GetRoomByClientId(ulong clientId)
        {
            var playerList = FindObjectOfType<PlayerListHandler>();
            if (playerList == null)
                return default;

            var player = playerList.Players.FirstOrDefault(x => x.ClientId == clientId);
            if (player.GuidRoom == Guid.Empty)
                return default;
            return Rooms.FirstOrDefault(r => r.Guid == player.GuidRoom);
        }

        public bool RoomExists(Guid guid) => Rooms.Any(r => r.Guid == guid);

        // === API ===

        public void CreateRoom(string roomName)
        {
            if (CurrentRoom.Guid != Guid.Empty)
            {
                Debug.LogWarning("Already in a room");
                return;
            }

            // Host (server+client) — обрабатываем локально, без RPC
            if (IsServer && IsClient)
            {
                ulong clientId = NetworkManager.LocalClientId;
                Debug.Log($"[RoomListHandler] CreateRoom called on host. LocalClientId={clientId}");
                if (_playerToRoom.ContainsKey(clientId))
                {
                    Debug.LogWarning($"Client {clientId} already in a room");
                    return;
                }

                var room = new NetworkRoom(clientId, new FixedString32Bytes(roomName));
                _rooms.Add(room);
                _playerToRoom[clientId] = room.Guid;

                var playerList = FindObjectOfType<PlayerListHandler>();
                // Передаём clientId явно
                playerList?.UpdatePlayerRoom(room.Guid, clientId);

                OnRoomCreated?.Invoke(room);
                OnRoomJoined?.Invoke(room);
                return;
            }

            // Обычный клиентский путь — вызываем ServerRpc (без передачи clientId)
            CreateRoomServerRpc(new FixedString32Bytes(roomName));
        }

        public void JoinRoom(Guid roomGuid)
        {
            if (CurrentRoom.Guid != Guid.Empty)
            {
                Debug.LogWarning("Already in a room");
                return;
            }
            if (!RoomExists(roomGuid))
            {
                Debug.LogWarning($"Room {roomGuid} does not exist");
                return;
            }

            // Host — обрабатываем локально
            if (IsServer && IsClient)
            {
                ulong clientId = NetworkManager.LocalClientId;
                Debug.Log($"[RoomListHandler] JoinRoom called on host. LocalClientId={clientId}, room={roomGuid}");
                _playerToRoom[clientId] = roomGuid;

                var playerList = FindObjectOfType<PlayerListHandler>();
                // Передаём clientId явно
                playerList?.UpdatePlayerRoom(roomGuid, clientId);

                var room = Rooms.FirstOrDefault(r => r.Guid == roomGuid);
                OnRoomJoined?.Invoke(room);
                return;
            }

            // Client -> ServerRpc
            JoinRoomServerRpc(new FixedString128Bytes(roomGuid.ToString()));
        }

        public void LeaveRoom()
        {
            var playerList = FindObjectOfType<PlayerListHandler>();
            Debug.Log($"[RoomListHandler] LeaveRoom called. LocalPlayerRoom={playerList?.LocalPlayer.GuidRoom}");

            if (CurrentRoom.Guid == Guid.Empty)
            {
                Debug.LogWarning("Not in a room");
                return;
            }

            // Host -> локально
            if (IsServer && IsClient)
            {
                ulong clientId = NetworkManager.LocalClientId;
                Debug.Log($"[RoomListHandler] LeaveRoom called on host. LocalClientId={clientId}");

                if (!_playerToRoom.TryGetValue(clientId, out Guid roomGuid) || roomGuid == Guid.Empty)
                {
                    Debug.LogWarning($"Client {clientId} not in a room");
                    return;
                }

                var room = Rooms.FirstOrDefault(r => r.Guid == roomGuid);

                _playerToRoom.Remove(clientId);

                var pl = FindObjectOfType<PlayerListHandler>();
                // Передаём clientId явно
                pl?.UpdatePlayerRoom(Guid.Empty, clientId);

                OnRoomLeft?.Invoke(room);

                if (room.ClientId == clientId)
                {
                    RemoveRoomByClientId(clientId);
                    Debug.Log($"Owner {clientId} left room {room.Name}, room removed");
                }
                return;
            }

            LeaveRoomServerRpc();
        }

        public bool IsInRoom() => CurrentRoom.Guid != Guid.Empty;

        public bool IsRoomHidden(Guid roomGuid)
        {
            var room = Rooms.FirstOrDefault(r => r.Guid == roomGuid);
            return room.Guid != Guid.Empty && room.IsHidden;
        }

        public IEnumerable<NetworkRoom> GetVisibleRooms()
        {
            return Rooms.Where(room => !room.IsHidden);
        }

        public IEnumerable<NetworkRoom> GetHiddenRooms()
        {
            return Rooms.Where(room => room.IsHidden);
        }

        public void SetRoomHidden(bool isHidden)
        {
            SetRoomHidden(CurrentRoom.Guid, isHidden);
        }

        public void SetRoomHidden(Guid guid, bool isHidden)
        {
            var currentRoom = Rooms.FirstOrDefault(_ => _.Guid == guid);
            if (currentRoom.IsEmpty)
            {
                Debug.LogWarning("Not in a room");
                return;
            }

            if (IsServer && IsClient)
            {
                ulong clientId = NetworkManager.LocalClientId;
                Debug.Log($"[RoomListHandler] SetRoomHidden called on host. LocalClientId={clientId}, isHidden={isHidden}");

                if (!_playerToRoom.TryGetValue(clientId, out Guid roomGuid) || roomGuid == Guid.Empty)
                {
                    Debug.LogWarning($"Client {clientId} not in a room");
                    return;
                }

                var room = Rooms.FirstOrDefault(r => r.Guid == roomGuid);
                if (room.Guid == Guid.Empty || room.ClientId != clientId)
                {
                    Debug.LogWarning($"Client {clientId} is not room owner");
                    return;
                }

                UpdateRoomInternal(roomGuid, isHidden);
                OnRoomUpdated?.Invoke(Rooms.First(r => r.Guid == roomGuid));
                return;
            }

            // Client -> ServerRpc
            UpdateRoomHiddenStatusServerRpc(isHidden);
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdateRoomHiddenStatusServerRpc(bool isHidden, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[RoomListHandler] UpdateRoomHiddenStatusServerRpc called by {clientId}, isHidden={isHidden}");

            if (!_playerToRoom.TryGetValue(clientId, out Guid roomGuid) || roomGuid == Guid.Empty)
            {
                Debug.LogWarning($"Client {clientId} not in a room");
                return;
            }

            var room = Rooms.FirstOrDefault(r => r.Guid == roomGuid);
            if (room.Guid == Guid.Empty)
            {
                Debug.LogWarning($"Room not found for client {clientId}");
                return;
            }

            if (room.ClientId != clientId)
            {
                Debug.LogWarning($"Client {clientId} is not room owner, cannot change hidden status");
                return;
            }

            UpdateRoomInternal(roomGuid, isHidden);

            Debug.Log($"[RoomListHandler] Room {roomGuid} hidden status updated to {isHidden}");
        }

        private void UpdateRoomInternal(Guid roomGuid, bool? isHidden = null)
        {
            for (int i = 0; i < _rooms.Count; i++)
            {
                if (_rooms[i].Guid == roomGuid)
                {
                    NetworkRoom modifiedRoom = _rooms[i];

                    if (isHidden.HasValue)
                        modifiedRoom.IsHidden = isHidden.Value;

                    _rooms[i] = modifiedRoom;
                    break;
                }
            }
        }

        public int GetPlayerCountInRoom(Guid roomGuid)
        {
            var playerList = FindObjectOfType<PlayerListHandler>();
            if (playerList == null)
                return 0;

            return playerList.Players.Count(player => player.GuidRoom == roomGuid);
        }

        public int GetPlayerCountInCurrentRoom()
        {
            var currentRoom = CurrentRoom;
            if (currentRoom.Guid == Guid.Empty)
                return 0;

            return GetPlayerCountInRoom(currentRoom.Guid);
        }

        public IEnumerable<NetworkPlayer> GetPlayersInRoom(Guid roomGuid)
        {
            var playerList = FindObjectOfType<PlayerListHandler>();
            return playerList.Players.Where(X => X.GuidRoom == roomGuid);
        }

        // --- Utilities ---

        private static bool FixedString128BytesToGuid(FixedString128Bytes fs, out Guid guid)
        {
            guid = Guid.Empty;
            try
            {
                string s = fs.ToString();
                if (string.IsNullOrEmpty(s)) return false;
                return Guid.TryParse(s, out guid);
            }
            catch
            {
                return false;
            }
        }

        public void DestroyRoom(Guid roomGuid)
        {
            if (!IsServer)
            {
                Debug.LogWarning("Only server can destroy rooms");
                return;
            }

            if (roomGuid == Guid.Empty)
            {
                Debug.LogWarning("Invalid room guid");
                return;
            }

            var room = Rooms.FirstOrDefault(r => r.Guid == roomGuid);
            if (room.Guid == Guid.Empty)
            {
                Debug.LogWarning($"Room {roomGuid} not found");
                return;
            }

            // Получаем всех игроков
            var playersInRoom = GetPlayersInRoom(roomGuid).ToList();

            var spawnHandler = FindObjectOfType<RoomObjectSpawnHandler>();
            spawnHandler?.DespawnAllRoomObjects(roomGuid.ToString());

            // Удаляем комнату
            for (int i = _rooms.Count - 1; i >= 0; i--)
            {
                if (_rooms[i].Guid == roomGuid)
                {
                    OnRoomAboutToBeRemoved?.Invoke(_rooms[i]);
                    _rooms.RemoveAt(i);
                    break;
                }
            }

            var playerList = FindObjectOfType<PlayerListHandler>();
            foreach (var player in playersInRoom)
            {
                _playerToRoom.Remove(player.ClientId);
                playerList?.UpdatePlayerRoom(Guid.Empty, player.ClientId);

                RoomLeftClientRpc(room, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { player.ClientId } }
                });
            }

            Debug.Log($"[RoomListHandler] Room {roomGuid} destroyed, {playersInRoom.Count} players removed, objects despawned");
        }

        public void UpdateRoomData(string key, string value)
        {
            if (CurrentRoom.Guid == Guid.Empty)
            {
                return;
            }

            if (IsServer)
            {
                ulong clientId = NetworkManager.LocalClientId;
                if (!_playerToRoom.TryGetValue(clientId, out Guid roomGuid) || roomGuid == Guid.Empty)
                {
                    Debug.LogWarning($"Client {clientId} not in a room");
                    return;
                }

                UpdateRoomDataInternal(roomGuid, key, value);
                OnRoomUpdated?.Invoke(Rooms.First(r => r.Guid == roomGuid));
                return;
            }

            UpdateRoomDataServerRpc(new FixedString128Bytes(key), new FixedString128Bytes(value));
        }

        [ServerRpc(RequireOwnership = false)]
        private void UpdateRoomDataServerRpc(FixedString128Bytes key, FixedString128Bytes value, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!_playerToRoom.TryGetValue(clientId, out Guid roomGuid) || roomGuid == Guid.Empty) return;

            UpdateRoomDataInternal(roomGuid, key.ToString(), value.ToString());
            OnRoomUpdated?.Invoke(Rooms.First(r => r.Guid == roomGuid));
        }

        private void UpdateRoomDataInternal(Guid roomGuid, string key, string value)
        {
            for (int i = 0; i < _rooms.Count; i++)
            {
                if (_rooms[i].Guid == roomGuid)
                {
                    NetworkRoom modifiedRoom = _rooms[i];
                    modifiedRoom.AddStringData((FixedString32Bytes)key, value);
                    _rooms[i] = modifiedRoom;
                    break;
                }
            }
        }
    }
}