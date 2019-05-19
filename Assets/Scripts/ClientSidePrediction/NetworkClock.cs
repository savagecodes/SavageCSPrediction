using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace SavageCodes.Networking.ClientSidePrediction
{
    public class NetworkClock : NetworkBehaviour
    {
        [Header("Network Clock Settings")] [SerializeField]
        private bool _useAverageLatency;

        [SerializeField] private int _latencyBufferSize = 4;
        [SerializeField] private bool _useAverageTimeDelta;
        [SerializeField] private int _timeDeltaBufferSize = 4;

        [SerializeField] private bool _showDebugClock = true;

        private int _latency;
        private int _averageLatency;
        private int _averageTimeDelta;
        private int _roundTripTime;
        private int _timeDelta;

        private Queue<int> _latencyBuffer;
        private Queue<int> _timeDeltaBuffer;

        private short _timeReceivedFromClientID = 2002;
        private short _timeReceivedFromServerID = 2003;

        #region Getters

        public int Latency => _useAverageLatency ? _averageLatency : _latency;

        public int RoundTripTime => _roundTripTime;

        public int TimeDelta => _useAverageTimeDelta ? _averageTimeDelta : _timeDelta;

        public int CurrentTimeInInt =>
            (int) ((isServer ? DateTime.UtcNow : GetSyncedTime()) - new DateTime(1970, 1, 1, 0, 0, 0))
            .TotalMilliseconds;

        public DateTime GetSyncedTime()
        {
            DateTime dateNow = DateTime.UtcNow;
            return dateNow.AddMilliseconds(_timeDelta).ToLocalTime();
        }

        #endregion

        void Start()
        {
            _latencyBuffer = new Queue<int>();
            _timeDeltaBuffer = new Queue<int>();

            _timeReceivedFromClientID += Convert.ToInt16(netId.Value);
            _timeReceivedFromServerID += Convert.ToInt16(netId.Value);

            if (isLocalPlayer)
            {
                StartCoroutine(SendTimeStamp());
            }

            if (isServer)
            {
                NetworkServer.RegisterHandler(_timeReceivedFromClientID, OnTimeReceivedFromClient);
            }
            else
            {
                NetworkManager.singleton.client.RegisterHandler(_timeReceivedFromServerID, OnTimeReceivedFromServer);
            }

        }

        void OnTimeReceivedFromClient(NetworkMessage netMsg)
        {
            var timeMessage = netMsg.ReadMessage<TimeMessage>();

            timeMessage.serverTimeStamp =
                (long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds;

            NetworkServer.SendToClientOfPlayer(gameObject, _timeReceivedFromServerID, timeMessage);
        }

        void OnTimeReceivedFromServer(NetworkMessage netMsg)
        {
            var timeMessage = netMsg.ReadMessage<TimeMessage>();

            CalculateTimeDelta(timeMessage);
            CalculateAverage(ref _latencyBuffer, _latencyBufferSize, _latency, out _averageLatency);
            CalculateAverage(ref _timeDeltaBuffer, _timeDeltaBufferSize, _timeDelta, out _averageTimeDelta);
        }

        IEnumerator SendTimeStamp()
        {
            while (true)
            {
                connectionToServer.Send(_timeReceivedFromClientID, CreateTimePacket());
                yield return new WaitForSeconds(5f);
            }
        }

        TimeMessage CreateTimePacket()
        {
            var timePacket = new TimeMessage
            {
                clientTimeStamp = (long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds
            };

            return timePacket;
        }

        void CalculateTimeDelta(TimeMessage timeMessage)
        {
            _roundTripTime = (int) ((long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds -
                                    timeMessage.clientTimeStamp);
            _latency = _roundTripTime / 2;
            int serverDelta = (int) (timeMessage.serverTimeStamp -
                                     (long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds);
            _timeDelta = serverDelta + _latency;
        }

        void CalculateAverage(ref Queue<int> buffer, int bufferSize, int value, out int average)
        {
            buffer.Enqueue(value);

            if (buffer.Count > bufferSize)
            {
                buffer.Dequeue();
            }

            var accumulator = 0;
            foreach (var val in buffer)
            {
                accumulator += val;
            }

            average = accumulator /
                      (bufferSize < buffer.Count ? buffer.Count : bufferSize);
        }

        void OnGUI()
        {

            if (!_showDebugClock) return;

            if (isServer)
            {
                GUI.Label(new Rect(10, 250, 400, 30), "Server Time:" + System.DateTime.Now.TimeOfDay);
                return;
            }

            if (!isLocalPlayer)
                return;
            GUI.Label(new Rect(10, 250, 400, 30), "Server Time:" + GetSyncedTime().TimeOfDay);
            GUI.Label(new Rect(10, 270, 400, 30), "Latency:" + Latency.ToString() + "ms");
            GUI.Label(new Rect(10, 290, 400, 30), "Time Delta:" + TimeDelta.ToString() + "ms");
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }

    public class TimeMessage : MessageBase
    {
        public long clientTimeStamp;
        public long serverTimeStamp;
    }
}
