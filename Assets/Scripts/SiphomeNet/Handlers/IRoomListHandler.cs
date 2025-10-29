using System;
using System.Collections.Generic;
using SiphomeNet.Network.Models;

namespace SiphomeNet.Network.Handlers
{
    public interface IRoomListHandler
    {
        event Action<NetworkRoom> OnRoomAdded;
        event Action<NetworkRoom> OnRoomRemoved;
        event Action<NetworkRoom> OnRoomUpdated;
        event Action<NetworkRoom> OnRoomCreated;
        event Action<NetworkRoom> OnRoomJoined;
        event Action<NetworkRoom> OnRoomAboutToBeRemoved;
        event Action<NetworkRoom> OnRoomLeft;

        void CreateRoom(string roomName);
        void LeaveRoom();
        bool IsInRoom();
        void JoinRoom(Guid roomGuid);
        int GetPlayerCountInRoom(Guid roomGuid);
        int GetPlayerCountInCurrentRoom();
        void SetRoomHidden(Guid guid, bool isHidden);
        void SetRoomHidden(bool isHidden);
        IEnumerable<NetworkRoom> GetVisibleRooms();
        IEnumerable<NetworkRoom> GetHiddenRooms();
        IEnumerable<NetworkPlayer> GetPlayersInRoom(Guid roomGuid);
        void DestroyRoom(Guid roomGuid);
        void UpdateRoomData(string v1, string v2);

        NetworkRoom CurrentRoom { get; }
        IEnumerable<NetworkRoom> Rooms { get; }
    }
}