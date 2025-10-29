using System;

namespace SiphomeNet.Network.Handlers
{
    public interface INetworkHandler : IRoomObjectSpawnHandler
    {
        IRoomListHandler RoomsHandler { get; }
        IPlayerListHandler Players { get; }
        INetworkSceneHandler SceneHandler { get; }
        IPingHandler PingHandler { get; }

        bool StartHost();
        bool StartClient();
        void StopNetwork();

        event Action<ulong> OnConnected;
        event Action<ulong> OnDisconnected;

        bool IsConnected { get; }
    }
}