using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System;
using System.Collections;

namespace SiphomeNet.Network.Handlers
{
    public class PingHandler : NetworkBehaviour, IPingHandler
    {

        public event Action<int> OnPingChanged;

        [SerializeField] private float _pingCheckInterval = 1.0f;

        private bool _waitingPong;
        private double _sendTime;
        private int _currentPing;
        private Coroutine _pingCoroutine;

        private const string PingMessage = "PingMsg";
        private const string PongMessage = "PongMsg";

        public int GetCurrentPing() => _currentPing;
        public override void OnNetworkSpawn()
        {
            var cm = NetworkManager.CustomMessagingManager;

            if (IsServer)
            {
                cm.RegisterNamedMessageHandler(PingMessage, OnPingReceived);
            }
            else if (IsClient)
            {
                cm.RegisterNamedMessageHandler(PongMessage, OnPongReceived);
                _pingCoroutine = StartCoroutine(CheckPingLoop());
            }
        }

        private IEnumerator CheckPingLoop()
        {
            while (true)
            {
                if (!_waitingPong && IsClient)
                {
                    SendPing();
                }

                yield return new WaitForSeconds(_pingCheckInterval);
            }
        }

        private void SendPing()
        {
            _sendTime = Time.realtimeSinceStartupAsDouble;
            _waitingPong = true;

            using var writer = new FastBufferWriter(sizeof(double), Allocator.Temp);
            writer.WriteValueSafe(_sendTime);

            NetworkManager?.CustomMessagingManager?.SendNamedMessage(
                PingMessage,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.UnreliableSequenced
            );
        }

        private void OnPingReceived(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out double ts);

            using var writer = new FastBufferWriter(sizeof(double), Allocator.Temp);
            writer.WriteValueSafe(ts);

            NetworkManager.CustomMessagingManager.SendNamedMessage(
                PongMessage,
                senderClientId,
                writer,
                NetworkDelivery.UnreliableSequenced
            );
        }

        private void OnPongReceived(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out double ts);

            if (!_waitingPong || Math.Abs(ts - _sendTime) > 0.001)
                return;

            _waitingPong = false;
            var rtt = (Time.realtimeSinceStartupAsDouble - ts) * 1000.0;
            _currentPing = (int)(rtt / 2);
            OnPingChanged?.Invoke(_currentPing);
        }

        public override void OnNetworkDespawn()
        {
            if (_pingCoroutine != null)
            {
                StopCoroutine(_pingCoroutine);
                _pingCoroutine = null;
            }

            var cm = NetworkManager.CustomMessagingManager;
            cm?.UnregisterNamedMessageHandler(PingMessage);
            cm?.UnregisterNamedMessageHandler(PongMessage);
        }
    }
}