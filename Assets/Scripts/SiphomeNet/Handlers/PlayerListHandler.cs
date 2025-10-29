using System;
using System.Collections.Generic;
using System.Linq;
using SiphomeNet.Network.Models;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SiphomeNet.Network.Handlers
{
    public class PlayerListHandler : NetworkBehaviour, IPlayerListHandler
    {
        public static PlayerListHandler Singleton { get; private set; }

        private readonly NetworkList<NetworkPlayer> _players = new();

        // Заменяем Action на события
        public event Action<NetworkPlayer> OnPlayerAdded;
        public event Action<NetworkPlayer> OnPlayerRemoved;
        public event Action<NetworkPlayer> OnPlayerUpdated;

        public IEnumerable<NetworkPlayer> Players
        {
            get
            {
                List<NetworkPlayer> list = new List<NetworkPlayer>();
                foreach (NetworkPlayer player in _players)
                {
                    list.Add(player);
                }
                return list;
            }
        }

        public NetworkPlayer LocalPlayer => GetPlayerByClientId(NetworkManager.LocalClientId);
        public int PlayerCount => _players.Count;

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

            _players.OnListChanged += HandlePlayersListChanged;
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            _players.OnListChanged -= HandlePlayersListChanged;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            if (Singleton == this)
            {
                Singleton = null;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // Добавляем сервер (host) при спауне
                if (!Application.isBatchMode)
                {
                    AddPlayerServerRpc(0);
                }
                else
                {
                    Debug.Log("Server started in the batch mode. Player with id 0 not be added. All it`s ok.");
                }
            }
        }

        private void HandlePlayersListChanged(NetworkListEvent<NetworkPlayer> changeEvent)
        {
            switch (changeEvent.Type)
            {
                case NetworkListEvent<NetworkPlayer>.EventType.Add:
                    OnPlayerAdded?.Invoke(changeEvent.Value);
                    break;

                case NetworkListEvent<NetworkPlayer>.EventType.Remove:
                    OnPlayerRemoved?.Invoke(changeEvent.Value);
                    break;

                case NetworkListEvent<NetworkPlayer>.EventType.RemoveAt:
                    OnPlayerRemoved?.Invoke(changeEvent.Value);
                    break;

                case NetworkListEvent<NetworkPlayer>.EventType.Value:
                    OnPlayerUpdated?.Invoke(changeEvent.Value);
                    break;
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (IsServer)
            {
                if (clientId != 0)
                {
                    AddPlayerServerRpc(clientId);
                }
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (IsServer)
            {
                RemovePlayerDirect(clientId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddPlayerServerRpc(ulong clientId)
        {
            if (!IsServer) return;

            // Проверяем, не существует ли уже игрок
            if (GetPlayerByClientId(clientId).Guid != Guid.Empty)
            {
                Debug.LogWarning($"Player already exists for client {clientId}");
                return;
            }

            // Форматируем имя по шаблону "Игрок_ID"
            string playerName = $"Player_{clientId}";
            var player = new NetworkPlayer(clientId, playerName);

            _players.Add(player);
            Debug.Log($"Player added: {playerName} (ClientId: {clientId})");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RemovePlayerServerRpc(ulong clientId)
        {
            RemovePlayerDirect(clientId);
        }

        private void RemovePlayerDirect(ulong clientId)
        {
            for (int i = _players.Count - 1; i >= 0; i--)
            {
                if (_players[i].ClientId == clientId)
                {
                    _players.RemoveAt(i);
                    Debug.Log($"Player removed: ClientId {clientId}");
                    break;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdatePlayerNameServerRpc(FixedString32Bytes newName, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    NetworkPlayer modifiedPlayer = _players[i];
                    modifiedPlayer.Name = newName;
                    _players[i] = modifiedPlayer;
                    Debug.Log($"Player name updated: {newName} for client {clientId}");
                    break;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdatePlayerTeamServerRpc(byte team, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    NetworkPlayer modifiedPlayer = _players[i];
                    modifiedPlayer.Team = team;
                    _players[i] = modifiedPlayer;
                    Debug.Log($"Player team updated: {team} for client {clientId}");
                    break;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void UpdatePlayerTeamForClientServerRpc(ulong targetClientId, byte team, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == targetClientId)
                {
                    NetworkPlayer modifiedPlayer = _players[i];
                    modifiedPlayer.Team = team;
                    _players[i] = modifiedPlayer;
                    Debug.Log($"Player team updated via RPC: {team} for client {targetClientId}");
                    break;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdatePlayerInGameStatusServerRpc(bool inGame, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    NetworkPlayer modifiedPlayer = _players[i];
                    modifiedPlayer.InGame = inGame;
                    _players[i] = modifiedPlayer;
                    Debug.Log($"Player in-game status updated: {inGame} for client {clientId}");
                    break;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdatePlayerReadyStatusServerRpc(bool isReady, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    NetworkPlayer modifiedPlayer = _players[i];
                    modifiedPlayer.IsReady = isReady;
                    _players[i] = modifiedPlayer;
                    Debug.Log($"Player in-ready status updated: {isReady} for client {clientId}");
                    break;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdatePlayerRoomServerRpc(FixedString128Bytes roomGuid, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    NetworkPlayer modifiedPlayer = _players[i];
                    modifiedPlayer.GuidRoom = Guid.Parse(roomGuid.ToString());
                    _players[i] = modifiedPlayer;
                    Debug.Log($"Player room updated: {roomGuid} for client {clientId}");
                    break;
                }
            }
        }

        // === Публичные методы для клиента ===

        public NetworkPlayer GetPlayerByClientId(ulong clientId)
        {
            foreach (var player in _players)
            {
                if (player.ClientId == clientId)
                    return player;
            }
            return default;
        }

        public NetworkPlayer GetPlayerByGuid(Guid guid)
        {
            foreach (var player in _players)
            {
                if (player.Guid == guid)
                    return player;
            }
            return default;
        }

        public void UpdatePlayerRoom(Guid roomGuid)
        {
            UpdatePlayerRoomServerRpc(new FixedString128Bytes(roomGuid.ToString()));
        }

        public void UpdatePlayerRoom(Guid roomGuid, ulong targetClientId)
        {
            if (IsServer)
            {
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i].ClientId == targetClientId)
                    {
                        NetworkPlayer modifiedPlayer = _players[i];
                        modifiedPlayer.GuidRoom = roomGuid;
                        _players[i] = modifiedPlayer;
                        Debug.Log($"Player room updated: {(roomGuid == Guid.Empty ? "Empty" : roomGuid.ToString())} for client {targetClientId}");
                        break;
                    }
                }
            }
            else
            {
                UpdatePlayerRoomServerRpc(new FixedString128Bytes(roomGuid.ToString()));
            }
        }

        public bool PlayerExists(ulong clientId)
        {
            return Players.Any(player => player.ClientId == clientId);
        }

        public void UpdatePlayerName(string newName)
        {
            UpdatePlayerNameServerRpc(newName);
        }

        public void UpdatePlayerTeam(byte team)
        {
            UpdatePlayerTeamServerRpc(team);
        }

        public void UpdatePlayerTeam(ulong clientId, byte team)
        {
            if (IsServer)
            {
                // Серверная логика — обновляем напрямую
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i].ClientId == clientId)
                    {
                        NetworkPlayer modifiedPlayer = _players[i];
                        modifiedPlayer.Team = team;
                        _players[i] = modifiedPlayer;
                        Debug.Log($"Player team updated: {team} for client {clientId}");
                        break;
                    }
                }
            }
            else
            {
                // Если клиент — шлём на сервер RPC с явным clientId
                UpdatePlayerTeamForClientServerRpc(clientId, team);
            }
        }

        public void UpdatePlayerInGameStatus(bool inGame)
        {
            UpdatePlayerInGameStatusServerRpc(inGame);
        }

        public void UpdatePlayerReadyStatus(bool isReady)
        {
            UpdatePlayerReadyStatusServerRpc(isReady);
        }

        public void UpdatePlayerReadyStatus(ulong clientId, bool isReady)
        {
            if (IsServer)
            {
                // Серверная логика - обновляем напрямую
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i].ClientId == clientId)
                    {
                        NetworkPlayer modifiedPlayer = _players[i];
                        modifiedPlayer.IsReady = isReady;
                        _players[i] = modifiedPlayer;
                        Debug.Log($"Player ready status updated: {isReady} for client {clientId}");
                        break;
                    }
                }
            }
            else
            {
                UpdatePlayerReadyStatusForClientServerRpc(clientId, isReady);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void UpdatePlayerReadyStatusForClientServerRpc(ulong targetClientId, bool isReady, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == targetClientId)
                {
                    NetworkPlayer modifiedPlayer = _players[i];
                    modifiedPlayer.IsReady = isReady;
                    _players[i] = modifiedPlayer;
                    Debug.Log($"Player ready status updated via RPC: {isReady} for client {targetClientId}");
                    break;
                }
            }
        }
    }
}