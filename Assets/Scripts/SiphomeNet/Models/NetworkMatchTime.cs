using System;
using Unity.Netcode;
namespace SiphomeNet.Network.Models
{
    [Serializable]
    public struct NetworkMatchTime : INetworkSerializable, IEquatable<NetworkMatchTime>
    {
        private const int CYCLE_24 = 86400;
        private int _totalSeconds;
        public DateTime DateTime
        {
            get => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(_totalSeconds);
            set => _totalSeconds = (int)(value - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public int Hours => _totalSeconds / 3600;
        public int Minutes => (_totalSeconds % 3600) / 60;
        public int Seconds => _totalSeconds % 60;

        public NetworkMatchTime(int hours, int minutes, int seconds)
        {
            _totalSeconds = hours * 3600 + minutes * 60 + seconds;
        }

        public void AddSeconds(int seconds)
        {
            _totalSeconds += seconds;
            _totalSeconds %= CYCLE_24;
            if (_totalSeconds < 0)
            {
                _totalSeconds += CYCLE_24;
            }
        }
        public void Reset()
        {
            _totalSeconds = 0;
        }

        public bool Equals(NetworkMatchTime other)
        {
            return _totalSeconds == other._totalSeconds;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _totalSeconds);
        }

        public override string ToString()
        {
            if (Hours > 0)
            {
                return $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}";
            }

            return $"{Minutes:D2}:{Seconds:D2}";
        }

        public string ToShortTime()
        {
            return $"{Hours:D2}:{Minutes:D2}";
        }
    }
}