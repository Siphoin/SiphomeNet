using SouthPointe.Serialization.MessagePack;
using System;
using Unity.Collections;
using Unity.Netcode;

namespace SiphomeNet.Network.Models
{
    [Serializable]
    public struct NetworkPlayer : INetworkSerializable, IEquatable<NetworkPlayer>
    {
        public ulong ClientId;
        public Guid Guid;
        public byte Team;
        public bool InGame;
        public bool IsReady;
        public FixedString32Bytes Name;
        public Guid GuidRoom;



        private static readonly MessagePackFormatter _formatter = new();


        public NetworkPlayer(ulong clientId, FixedString32Bytes nickName)
        {
            ClientId = clientId;
            Name = nickName;
            Team = 0;
            Guid = Guid.NewGuid();
            InGame = false;
            GuidRoom = Guid.Empty;
            IsReady = false;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                byte[] bytes = default;
                serializer.SerializeValue(ref bytes);

                if (bytes != null && bytes.Length > 0)
                {
                    NetworkPlayer deserialized = _formatter.Deserialize<NetworkPlayer>(bytes);
                    this = deserialized;
                }
            }
            else
            {
                byte[] bytes = _formatter.Serialize(this);
                serializer.SerializeValue(ref bytes);
            }
        }

        public bool Equals(NetworkPlayer other)
        {
            return base.Equals(other);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkPlayer other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, Guid, GuidRoom, InGame);
        }

    }
}
