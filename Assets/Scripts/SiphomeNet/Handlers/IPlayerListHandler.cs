using System;
using System.Collections.Generic;
using SiphomeNet.Network.Models;

namespace SiphomeNet.Network.Handlers
{
    public interface IPlayerListHandler
    {
        event Action<NetworkPlayer> OnPlayerAdded;
        event Action<NetworkPlayer> OnPlayerRemoved;
        event Action<NetworkPlayer> OnPlayerUpdated;

        NetworkPlayer LocalPlayer { get; }
        int PlayerCount { get; }
        IEnumerable<NetworkPlayer> Players { get; }

        NetworkPlayer GetPlayerByClientId(ulong clientId);
        NetworkPlayer GetPlayerByGuid(Guid guid);
        bool PlayerExists(ulong clientId);

        void UpdatePlayerReadyStatus(ulong clientId, bool isReady);
        void UpdatePlayerReadyStatus(bool isReady);
        void UpdatePlayerTeam(ulong clientId, byte team);
        void UpdatePlayerTeam(byte team);
        void UpdatePlayerName(string newName);
        void UpdatePlayerInGameStatus(bool inGame);
        void UpdatePlayerRoom(Guid roomGuid);
        void UpdatePlayerRoom(Guid roomGuid, ulong targetClientId);
    }
}