using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace SavageCodes.Networking.ClientSidePrediction
{
    public class PredictedNetworkMovement : NetworkBehaviour
    {

        private short _predictedMessageReceivedID = 1002;
        private short _stateMessageReceivedID = 1003;

        private int _correctionsMadeOnClient;

        private InputProcessor _inputProcessorComponent;
        private StateProcessor _stateProcessorComponent;

        public Action<PredictedSmoothedTransform> OnSmoothedPositionReady;
        public Action<ServerState> OnValidSercerStateReceived;

        private uint _currentTickNumber;
        private NetworkClock _networkClock;

        //client specific
        private float _clientTimer;
        private uint _clientLastReceivedStateTickNumber;
        private const int _clientBufferSize = 1024;
        private ServerState[] _clientStateBuffer; // client stores predicted moves here
        private Inputs[] _clientInputBuffer; // client stores predicted inputs here

        private BinaryHeap<ServerStateMessage>
            _clientServerStateMessageBuffer; // client store server states messages for validation 

        private HashSet<uint> _clientStateMessageIDs;
        private Vector3 _clientPositionError;
        private Quaternion _clientRotationError;
        private uint _clientPacketID;

        [Header("Client Replication Settings")] [SerializeField]
        private bool _sendRedundantInputsToServer = true;

        // server specific
        [Header("Server Replication Settings")] [SerializeField]
        private uint _serverSnapshotRate;

        private uint _serverTickAccumulator;

        private BinaryHeap<ClientPredictedMessage>
            _serverPredictedMessageBuffer; //server stores the input messages in this buffer

        private HashSet<uint> _serverPredictedMessagesIDs;
        private uint _serverPacketID;

        //client non local player   
        private PredictedSmoothedTransform _lastInterpolatedTransform;
        private PredictedSmoothedTransform _lastReceivedFromServerTransform;

        private bool _firstSyncMessageReceived;
        private bool _firstSynced;

        #region Getters

        public int Corrections => _correctionsMadeOnClient;

        public InputProcessor InputProcessorComponent => _inputProcessorComponent;

        public StateProcessor StateProcessorComponent => _stateProcessorComponent;

        #endregion

        private void Awake()
        {
            _networkClock = GetComponent<NetworkClock>();
            Application.targetFrameRate = 60;
            _inputProcessorComponent = GetComponent<InputProcessor>();
            _stateProcessorComponent = GetComponent<StateProcessor>();
        }

        void Start()
        {

            _predictedMessageReceivedID += System.Convert.ToInt16(netId.Value);
            _stateMessageReceivedID += System.Convert.ToInt16(netId.Value);

            _currentTickNumber = 0;

            #region Initializing Client Properties

            _clientTimer = 0.0f;
            _clientLastReceivedStateTickNumber = 0;
            _clientStateBuffer = new ServerState[_clientBufferSize];
            _clientInputBuffer = new Inputs[_clientBufferSize];
            _clientServerStateMessageBuffer = new BinaryHeap<ServerStateMessage>();
            _clientStateMessageIDs = new HashSet<uint>();
            _clientPositionError = Vector3.zero;
            _clientRotationError = Quaternion.identity;

            #endregion

            #region Initializing Server properties

            _serverTickAccumulator = 0;
            _serverPredictedMessageBuffer = new BinaryHeap<ClientPredictedMessage>();
            _serverPredictedMessagesIDs = new HashSet<uint>();

            #endregion



            if (!isServer)
            {
                NetworkManager.singleton.client.RegisterHandler(_stateMessageReceivedID, OnStateMessageReceived);
            }


            NetworkServer.RegisterHandler(_predictedMessageReceivedID, OnPredictedMessageReceived);


        }

        #region Network Messages Handlers

        void OnPredictedMessageReceived(NetworkMessage netMsg)
        {
           // netMsg.reader.SeekZero();
            var message = netMsg.ReadMessage<ClientPredictedMessage>();

            //Discard if is a duplicated packet
            if (_serverPredictedMessagesIDs.Contains(message.packetId))
                return;

            _serverPredictedMessagesIDs.Add(message.packetId);

            //The binary heap will order the packets if they came out of order
            _serverPredictedMessageBuffer.Enqueue(new HeapElement<ClientPredictedMessage>(message, message.packetId));

        }

        void OnStateMessageReceived(NetworkMessage netMsg)
        {
           // netMsg.reader.SeekZero();
            var message = netMsg.ReadMessage<ServerStateMessage>();

            //Discard if is a duplicated packet
            if (_clientStateMessageIDs.Contains(message.packetId))
                return;

            _clientStateMessageIDs.Add(message.packetId);

            //The binary heap will order the packets if the came out of order
            _clientServerStateMessageBuffer.Enqueue(new HeapElement<ServerStateMessage>(message, message.packetId));
        }

        #endregion

        #region Server Logic

        public void OnServerStateUpdated()
        {
            _serverTickAccumulator++;

            if (_serverTickAccumulator >= _serverSnapshotRate)
            {
                _serverTickAccumulator = 0;

                ServerStateMessage serverStateMsg = new ServerStateMessage();
                serverStateMsg.packetId = _serverPacketID;
                serverStateMsg.deliveryTime = _networkClock.CurrentTime;
                serverStateMsg.tickNumber = _currentTickNumber;
                serverStateMsg.serverState = StateProcessorComponent.GetCurrentState();

                //Send Message To Client
                NetworkServer.SendToClientOfPlayer(this.gameObject, _stateMessageReceivedID, serverStateMsg);
                _serverPacketID++;

            }

            //Send RPC Call to sync non local clients positions
            //TODO: Optimize this 
            RpcTransformUpdate(StateProcessorComponent.GetCurrentState().position,
                StateProcessorComponent.GetCurrentState().rotation);
        }

        void ServerUpdate()
        {

            while (_serverPredictedMessageBuffer.Count > 0 && _networkClock.CurrentTime >=
                   _serverPredictedMessageBuffer.Peek().Element.deliveryTime)
            {
                ClientPredictedMessage clientPredictedMessage = _serverPredictedMessageBuffer.Dequeue().Element;

                // message contains an array of inputs, calculate what tick the final one is
                uint maxTick = clientPredictedMessage.startTickNumber + (uint) clientPredictedMessage.inputs.Length - 1;

                // if that tick is greater than or equal to the current tick we're on, then it
                // has inputs which are new
                if (maxTick >= _currentTickNumber)
                {
                    // there may be some inputs in the array that we've already had,
                    // so figure out where to start
                    uint startIndex = _currentTickNumber > clientPredictedMessage.startTickNumber
                        ? (_currentTickNumber - clientPredictedMessage.startTickNumber)
                        : 0;

                    // run through all relevant inputs, and step player forward
                    for (int i = (int) startIndex; i < clientPredictedMessage.inputs.Length; ++i)
                    {
                        _currentTickNumber++;
                        InputProcessorComponent.ExecuteInputs(clientPredictedMessage.inputs[i]);
                    }
                }
            }

        }

        #endregion

        #region Client Logic

        void ClientUpdate()
        {
            _clientTimer += Time.deltaTime;

            while (_clientTimer >= Time.fixedDeltaTime)
            {
                _clientTimer -= Time.deltaTime;

                uint bufferSlot = _currentTickNumber % _clientBufferSize;

                _clientInputBuffer[bufferSlot] = InputProcessorComponent.GetCurrentInputs();

                // store state for this tick, then use current state + input to step simulation
                ClientStoreCurrentStateAndStep(ref _clientStateBuffer[bufferSlot],
                    InputProcessorComponent.GetCurrentInputs());

                ClientPredictedMessage clientPredictedMessage = new ClientPredictedMessage();
                clientPredictedMessage.packetId = _clientPacketID;
                clientPredictedMessage.deliveryTime = _networkClock.CurrentTime;
                clientPredictedMessage.startTickNumber = _sendRedundantInputsToServer
                    ? _clientLastReceivedStateTickNumber
                    : _currentTickNumber;

                var inputList = new List<Inputs>();

                for (uint tick = clientPredictedMessage.startTickNumber; tick <= _currentTickNumber; tick++)
                {
                    inputList.Add(_clientInputBuffer[tick % _clientBufferSize]);
                }

                clientPredictedMessage.inputs = inputList.ToArray();

                //Send Input Message To Server
                connectionToServer.Send(_predictedMessageReceivedID, clientPredictedMessage);

                _clientPacketID++;

                _currentTickNumber++;
            }

            if (ClientHasStateMessage())
            {
                ServerStateMessage serverStateMessage = _clientServerStateMessageBuffer.Dequeue().Element;

                while (ClientHasStateMessage()
                ) // make sure if there are any newer state messages available, we use those instead
                {
                    serverStateMessage = _clientServerStateMessageBuffer.Dequeue().Element;
                }

                _clientLastReceivedStateTickNumber = serverStateMessage.tickNumber;

                //Broadcast this server state for easy access for debugging purposes
                if (OnValidSercerStateReceived != null)
                {
                    OnValidSercerStateReceived(serverStateMessage.serverState);
                }
                //-----------------------------

                uint bufferSlot = serverStateMessage.tickNumber % _clientBufferSize;

                Vector3 positionError =
                    serverStateMessage.serverState.position - _clientStateBuffer[bufferSlot].position;

                float rotationError = 1.0f - Quaternion.Dot(serverStateMessage.serverState.rotation,
                                          _clientStateBuffer[bufferSlot].rotation);

                //Validating position,rotation and extras rules defined in de State processor        
                if (positionError.sqrMagnitude > 0.0000001f || rotationError > 0.00001f ||
                    !StateProcessorComponent.IsValidateState(ref serverStateMessage.serverState,
                        ref _clientStateBuffer[bufferSlot]))
                {
                    ApplyCorrectionsWithServerState(serverStateMessage, bufferSlot);
                }

            }

            SmoothTransformForModels();

        }

        void SmoothTransformForModels()
        {
            _clientPositionError *= 0.9f;
            _clientRotationError = Quaternion.Slerp(_clientRotationError, Quaternion.identity, 0.1f);

            PredictedSmoothedTransform smoothedTransform;
            smoothedTransform.position = StateProcessorComponent.GetCurrentState().position + _clientPositionError;
            smoothedTransform.rotation = StateProcessorComponent.GetCurrentState().rotation * _clientRotationError;

            if (OnSmoothedPositionReady != null)
            {
                OnSmoothedPositionReady(smoothedTransform);
            }
        }

        private void ApplyCorrectionsWithServerState(ServerStateMessage serverStateMessage, uint bufferSlot)
        {
            // rewind & replay

            StateProcessorComponent.ExecuteState(serverStateMessage.serverState);

            _correctionsMadeOnClient++;

            // capture the current predicted pos for smoothing
            Vector3 prevPosition = StateProcessorComponent.GetCurrentState().position + _clientPositionError;
            Quaternion prevRotation = StateProcessorComponent.GetCurrentState().rotation * _clientRotationError;

            uint rewindTickNumber = serverStateMessage.tickNumber;

            while (rewindTickNumber < _currentTickNumber)
            {
                bufferSlot = rewindTickNumber % _clientBufferSize;

                ClientStoreCurrentStateAndStep(ref _clientStateBuffer[bufferSlot], _clientInputBuffer[bufferSlot]);

                rewindTickNumber++;
            }

            //if the position error is greater than 2 meters, just snap 
            if ((prevPosition - StateProcessorComponent.GetCurrentState().position).sqrMagnitude >= 4.0f)
            {
                _clientPositionError = Vector3.zero;
                _clientRotationError = Quaternion.identity;
            }
            else
            {
                _clientPositionError = prevPosition - StateProcessorComponent.GetCurrentState().position;
                _clientRotationError = Quaternion.Inverse(StateProcessorComponent.GetCurrentState().rotation) *
                                       prevRotation;
            }

        }


        private bool ClientHasStateMessage()
        {
            if (_clientServerStateMessageBuffer.Peek() == null) return false;

            return _clientServerStateMessageBuffer.Count > 0 && _networkClock.CurrentTime >=
                   _clientServerStateMessageBuffer.Peek().Element.deliveryTime;
        }

        private void ClientStoreCurrentStateAndStep(ref ServerState currentState, Inputs inputs)
        {
            currentState.position = StateProcessorComponent.GetCurrentState().position;
            currentState.rotation = StateProcessorComponent.GetCurrentState().rotation;

            InputProcessorComponent.ExecuteInputs(inputs);
        }

        [ClientRpc]
        void RpcTransformUpdate(Vector3 position, Quaternion rotation)
        {
            _lastReceivedFromServerTransform.position = position;
            _lastReceivedFromServerTransform.rotation = rotation;
            _firstSyncMessageReceived = true;
        }

        //Only should be called on non-local Clients Players
        private void InterpolateTransform()
        {
            if (!_firstSyncMessageReceived)
                return;

            if (!_firstSynced)
            {

                OnSmoothedPositionReady(_lastReceivedFromServerTransform);

                _firstSynced = true;

                return;
            }
            
            _lastInterpolatedTransform.position = Utility.InterpTo(_lastInterpolatedTransform.position,
                _lastReceivedFromServerTransform.position, 25f);
            _lastInterpolatedTransform.rotation = Quaternion.Lerp(_lastInterpolatedTransform.rotation,
                _lastReceivedFromServerTransform.rotation, 14f * Time.fixedDeltaTime);

            OnSmoothedPositionReady(_lastInterpolatedTransform);

        }

        #endregion

        void Update()
        {
            if (isServer) ServerUpdate();
            else if (isLocalPlayer) ClientUpdate();
            else InterpolateTransform();
        }

    }

    public struct PredictedSmoothedTransform
    {
        public Vector3 position;
        public Quaternion rotation;

    }

    class ClientPredictedMessage : MessageBase
    {
        public uint packetId;
        public double deliveryTime;
        public uint startTickNumber;
        public Inputs[] inputs;
    }

    public class ServerStateMessage : MessageBase
    {
        public uint packetId;
        public double deliveryTime;
        public uint tickNumber;
        public ServerState serverState;
    }
}

