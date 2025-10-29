using SouthPointe.Serialization.MessagePack;
using System;
using Unity.Netcode;

namespace SiphomeNet.Network.Models
{
    [Serializable]
    public struct NetworkGuid : INetworkSerializable, IEquatable<NetworkGuid>
    {
        public Guid Guid;

        private static readonly MessagePackFormatter _formatter = new();

        public NetworkGuid(Guid guid)
        {
            Guid = guid;
        }

        public bool Equals(NetworkGuid other)
        {
            return Guid.Equals(other.Guid);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkGuid other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Guid.GetHashCode();
        }

        public static bool operator ==(NetworkGuid left, NetworkGuid right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NetworkGuid left, NetworkGuid right)
        {
            return !left.Equals(right);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                byte[] bytes = default;
                serializer.SerializeValue(ref bytes);

                if (bytes != null && bytes.Length > 0)
                {
                    NetworkGuid deserialized = _formatter.Deserialize<NetworkGuid>(bytes);
                    this = deserialized;
                }
            }
            else
            {
                byte[] bytes = _formatter.Serialize(this);
                serializer.SerializeValue(ref bytes);
            }
        }

        public static implicit operator Guid(NetworkGuid networkGuid)
        {
            return networkGuid.Guid;
        }

        public static implicit operator NetworkGuid(Guid guid)
        {
            return new NetworkGuid(guid);
        }

        public override string ToString()
        {
            return Guid.ToString();
        }
    }
}