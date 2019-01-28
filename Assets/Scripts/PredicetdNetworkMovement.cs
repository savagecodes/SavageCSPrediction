using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(Rigidbody))]
public class PredicetdNetworkMovement : NetworkBehaviour {

    private short _PredictedMessageReceivedID = 1002;
    private short _StateMessageReceivedID = 1003;

    PhysicsScene LocalPhysicsScene;

    private int _correctionsMadeOnClient;
    
    public event Action<Inputs> OnInputExecutionRequest = x => { };
    public event Action<ServerState> OnServerStateExecutionRequest = x => { };

    private InputProcessor _inputProcessorComponent;
    private StateProcessor _stateProcessorComponent;

    [Header("Required Compoennts")]
    [SerializeField]
    private GameObject _serverGhostModel;
    [SerializeField]
    private GameObject _smoothedPlayerModel;

    Rigidbody _rigidbody;

    private float _currentTime;
    private uint _currentTickNumber;

    //client specific
    private float _clientTimer;
    private uint _clientLastRecivedStateTickNumber;
    private const int _clientBufferSize = 1024;
    private ClientState[] _clientStateBuffer; // client stores predicted moves here
    private Inputs[] _clientInputBuffer; // client stores predicted inputs here
    private BinaryHeap<ServerStateMessage> _clientStateMessageQueue;
    private HashSet<uint> _clientStateMessageIDs;
    private Vector3 _clientPositionError;
    private Quaternion _clientRotationError;
    private uint _clientPacketID;

    [Header("Client Replication Settings")]
    [SerializeField]
    private bool _sendRedundantInputsToServer = true;

    // server specific
    [Header("Server Replication Settings")]
    [SerializeField]
    private uint _serverSnapshotRate;
    private uint _serverTickAccumulator;
    private BinaryHeap<ClientPredictedMessage> _serverPredictedMessageQueue;
    private HashSet<uint> _serverPredictedMessagesIDs;
    private uint serverPacketID;

    //client non local player
    private Vector3 _nonLocalClientTargetPosition;
    private Quaternion _nonLocalClientTargetRotation;
    [SerializeField]
    private float _nonLocalSyncInterval = 0.1f;
    private bool _firstSyncMessageRecived;
    private bool _firstSynced;

    #region Getters

    public int Corrections { get { return _correctionsMadeOnClient; } }

    public InputProcessor InputProcessorComponent => _inputProcessorComponent;

    public StateProcessor StateProcessorComponent => _stateProcessorComponent;

    public GameObject SmoothedPlayerModel { get { return _smoothedPlayerModel; } }

    #endregion

    private void Awake()
    {
        _inputProcessorComponent = GetComponent <InputProcessor>();
        _stateProcessorComponent = GetComponent<StateProcessor>();
    }

    // Use this for initialization
    void Start () {

        _rigidbody = GetComponent<Rigidbody>();

        if (!isServer && !isLocalPlayer)
        {
            _rigidbody.isKinematic = true;
            return;
        }
        else
        {
            PhysicsNetworkUpdater.Instance.CreatePhysicsSceneForGO(this.gameObject);
        }

        _PredictedMessageReceivedID += System.Convert.ToInt16(netId.Value);
        _StateMessageReceivedID += System.Convert.ToInt16(netId.Value);

        _currentTickNumber = 0;

        #region Initializing Client Properties

        _clientTimer = 0.0f;
        _clientLastRecivedStateTickNumber = 0;
        _clientStateBuffer = new ClientState[_clientBufferSize];
        _clientInputBuffer = new Inputs[_clientBufferSize];
        _clientStateMessageQueue = new BinaryHeap<ServerStateMessage>();
        _clientStateMessageIDs = new HashSet<uint>();
        _clientPositionError = Vector3.zero;
        _clientRotationError = Quaternion.identity;

        #endregion

        #region Initializing Server properties

        _serverTickAccumulator = 0;
        _serverPredictedMessageQueue = new BinaryHeap<ClientPredictedMessage>();
        _serverPredictedMessagesIDs = new HashSet<uint>(); 

        #endregion


        if (isServer)
        {
            //-----------------
            // Visual Debug , this component only should give/Expose the positions

            _serverGhostModel.transform.SetParent(null);
            _smoothedPlayerModel.transform.SetParent(null);
            
            var renderers = _serverGhostModel.GetComponentsInChildren<MeshRenderer>();
            
            foreach (var r in renderers)
            {
                r.enabled = false;
            }
            
           // _smoothedPlayerModel.GetComponent<MeshRenderer>().enabled = false;
            //
            //-----------------

            StartCoroutine(SyncNonLocalClientTransform());
            connectionToClient.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128);
        }
        else
        {
            NetworkManager.singleton.client.RegisterHandler(_StateMessageReceivedID, OnStateMessageReceived);
            connectionToServer.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128);

            //-----------------
            // Visual Debug , this component only should give/Expose the positions
            
           
            
            _serverGhostModel.transform.SetParent(null);
            _smoothedPlayerModel.transform.SetParent(null);
            var renderers = GetComponentsInChildren<MeshRenderer>();
            
            foreach (var r in renderers)
            {
                r.enabled = false;
            }
            //-----------------
        }


        NetworkServer.RegisterHandler(_PredictedMessageReceivedID, OnPredictedMessageReceived);

    }

    #region Network Messages Handlers

    void OnPredictedMessageReceived(NetworkMessage netMsg)
    {
        var message = netMsg.ReadMessage<ClientPredictedMessage>();

        if (_serverPredictedMessagesIDs.Contains(message.packetId)) return;

        _serverPredictedMessagesIDs.Add(message.packetId);

        _serverPredictedMessageQueue.Enqueue(new HeapElement<ClientPredictedMessage>(message,message.packetId));

    }

    void OnStateMessageReceived(NetworkMessage netMsg)
    {
        var message = netMsg.ReadMessage<ServerStateMessage>();

        if (_clientStateMessageIDs.Contains(message.packetId)) return;
        _clientStateMessageIDs.Add(message.packetId);

        _clientStateMessageQueue.Enqueue(new HeapElement<ServerStateMessage>(message, message.packetId));
    }

    #endregion

    #region Server Logic

    IEnumerator SyncNonLocalClientTransform()
    {
        var wait = new WaitForSeconds(_nonLocalSyncInterval);

        while (true)
        {
            RpcTransformUpdate(transform.position, transform.rotation);
            yield return wait;
        }
    }

    public void OnPhysiscsUpdated()
    {            
        _serverTickAccumulator++;

        if (_serverTickAccumulator >= _serverSnapshotRate)
        {
            _serverTickAccumulator = 0;
                        
            ServerStateMessage serverStateMsg = new ServerStateMessage();
            serverStateMsg.packetId = serverPacketID;
            serverStateMsg.deliveryTime = _currentTime + _serverPredictedMessageQueue.Peek().Element.rtt / 2;
            serverStateMsg.tickNumber = _currentTickNumber;
            
            serverStateMsg.serverState = _stateProcessorComponent.GetServerState();
            //Send Message To Client
            NetworkServer.SendToClientOfPlayer(this.gameObject, _StateMessageReceivedID, serverStateMsg);
            serverPacketID++;
                    
        }
    }


    void ServerUpdate()
    {
        _currentTime += Time.deltaTime;

        while (_serverPredictedMessageQueue.Count > 0 && _currentTime >= _serverPredictedMessageQueue.Peek().Element.deliveryTime)
        {
            ClientPredictedMessage clientPredictedMessage = _serverPredictedMessageQueue.Dequeue().Element;

            // message contains an array of inputs, calculate what tick the final one is
            uint maxTick = clientPredictedMessage.startTickNumber + (uint)clientPredictedMessage.inputs.Length - 1;

            // if that tick is greater than or equal to the current tick we're on, then it
            // has inputs which are new
            if (maxTick >= _currentTickNumber)
            {
                // there may be some inputs in the array that we've already had,
                // so figure out where to start
                uint startIndex = _currentTickNumber > clientPredictedMessage.startTickNumber ? (_currentTickNumber - clientPredictedMessage.startTickNumber) : 0;

                // run through all relevant inputs, and step player forward
                for (int i = (int)startIndex; i < clientPredictedMessage.inputs.Length; ++i)
                {
                    OnInputExecutionRequest(clientPredictedMessage.inputs[i]);

                    PhysicsNetworkUpdater.Instance.UpdatePhysics(this);

                    _currentTickNumber++;
                   
                }
            }
        }

    }

    #endregion

    #region Client Logic

    void ClientUpdate()
    {
        _currentTime += Time.deltaTime;

        _clientTimer += Time.deltaTime;

        while (_clientTimer >= Time.fixedDeltaTime)
        {
            _clientTimer -= Time.fixedDeltaTime;

            uint buffer_slot = _currentTickNumber % _clientBufferSize;

            _clientInputBuffer[buffer_slot] = InputProcessorComponent.GetCurrentInputs();

            // store state for this tick, then use current state + input to step simulation
            ClientStoreCurrentStateAndStep(ref _clientStateBuffer[buffer_slot],_rigidbody,InputProcessorComponent.GetCurrentInputs(), Time.fixedDeltaTime);
       
            ClientPredictedMessage clientPredictedMessage = new ClientPredictedMessage();
            var rtt = (NetworkManager.singleton.client.GetRTT() / 1000f);
            clientPredictedMessage.packetId = _clientPacketID;
            clientPredictedMessage.deliveryTime = _currentTime + rtt / 2 ;
            clientPredictedMessage.rtt = rtt;
            clientPredictedMessage.startTickNumber = _sendRedundantInputsToServer ? _clientLastRecivedStateTickNumber : _currentTickNumber;

            var inputList = new List<Inputs>();

            for (uint tick = clientPredictedMessage.startTickNumber; tick <= _currentTickNumber; tick++)
            {
                inputList.Add(_clientInputBuffer[tick % _clientBufferSize]);
            }

            clientPredictedMessage.inputs = inputList.ToArray();

            //Send Input Message To Server
            connectionToServer.Send(_PredictedMessageReceivedID, clientPredictedMessage);

            _clientPacketID++;

            _currentTickNumber++;
        }

        if (ClientHasStateMessage())
        {
            ServerStateMessage serverStateMessage = _clientStateMessageQueue.Dequeue().Element;
            while (ClientHasStateMessage()) // make sure if there are any newer state messages available, we use those instead
            {
                serverStateMessage = _clientStateMessageQueue.Dequeue().Element;
            }

            _clientLastRecivedStateTickNumber = serverStateMessage.tickNumber;

            _serverGhostModel.transform.position = serverStateMessage.serverState.position;
            _serverGhostModel.transform.rotation = serverStateMessage.serverState.rotation;

            uint bufferSlot = serverStateMessage.tickNumber % _clientBufferSize;

            Vector3 positionError = serverStateMessage.serverState.position - _clientStateBuffer[bufferSlot].position;
            float rotationError = 1.0f - Quaternion.Dot(serverStateMessage.serverState.rotation, _clientStateBuffer[bufferSlot].rotation);
            
            OnServerStateExecutionRequest(serverStateMessage.serverState);
            
            if (positionError.sqrMagnitude > 0.0000001f || rotationError > 0.00001f)
            {
                ApplyCorrectionsWithServerState(serverStateMessage,bufferSlot);
            }
    
        }
        
       _clientPositionError *= 0.9f;
       _clientRotationError = Quaternion.Slerp(_clientRotationError, Quaternion.identity, 0.1f);
        
       _smoothedPlayerModel.transform.position = _rigidbody.position + _clientPositionError;
       _smoothedPlayerModel.transform.rotation = _rigidbody.rotation * _clientRotationError;
    }

    public void ApplyCorrectionsWithServerState(ServerStateMessage serverStateMessage,uint bufferSlot)
    {
       
            OnServerStateExecutionRequest(serverStateMessage.serverState);
             
            _correctionsMadeOnClient++;

            // capture the current predicted pos for smoothing
            Vector3 prevPosition = _rigidbody.position + _clientPositionError;
            Quaternion prevRotation = _rigidbody.rotation * _clientRotationError;

            // rewind & replay
            _rigidbody.position = serverStateMessage.serverState.position;
            _rigidbody.rotation = serverStateMessage.serverState.rotation;
            _rigidbody.velocity = serverStateMessage.serverState.velocity;
            _rigidbody.angularVelocity = serverStateMessage.serverState.angularVelocity;
            _rigidbody.drag = serverStateMessage.serverState.drag;
            _rigidbody.angularDrag = serverStateMessage.serverState.angularDrag;
              

            uint rewindTickNumber = serverStateMessage.tickNumber;

            while (rewindTickNumber < _currentTickNumber)
            {
                bufferSlot = rewindTickNumber % _clientBufferSize;

                ClientStoreCurrentStateAndStep(ref _clientStateBuffer[bufferSlot],_rigidbody,_clientInputBuffer[bufferSlot],Time.fixedDeltaTime);

                rewindTickNumber++;
            }

            // if more than 2mts apart, just snap
            if ((prevPosition - _rigidbody.position).sqrMagnitude >= 4.0f)
            {
                _clientPositionError = Vector3.zero;
                _clientRotationError = Quaternion.identity;
            }
            else
            {
                _clientPositionError = prevPosition - _rigidbody.position;
                _clientRotationError = Quaternion.Inverse(_rigidbody.rotation) * prevRotation;
            }
          
    }
    

    private bool ClientHasStateMessage()
    {
        if (_clientStateMessageQueue.Peek() == null) return false;

        return _clientStateMessageQueue.Count > 0 && _currentTime >= _clientStateMessageQueue.Peek().Element.deliveryTime;
    }

    private void ClientStoreCurrentStateAndStep(ref ClientState currentState, Rigidbody rigidbody, Inputs inputs, float deltaTime)
    {
        currentState.position = rigidbody.position;
        currentState.rotation = rigidbody.rotation;

        //PrePhysicsStep(rigidbody, inputs);
        OnInputExecutionRequest(inputs);
        PhysicsNetworkUpdater.Instance.UpdatePhysics(this);
    }

    [ClientRpc]
    void RpcTransformUpdate(Vector3 position, Quaternion rotation)
    {
        _nonLocalClientTargetPosition = position;
        _nonLocalClientTargetRotation = rotation;
        _firstSyncMessageRecived = true;
    }

    //Only should be called on non-local Clients Players
    private void InterpolateTransform()
    {
        if (!_firstSyncMessageRecived) return;
        if (!_firstSynced)
        {
            transform.position = _nonLocalClientTargetPosition;
            transform.rotation = _nonLocalClientTargetRotation;
            _firstSynced = true;
        }

        if (transform.position != _nonLocalClientTargetPosition)
        {
            transform.position = Vector3.Lerp(transform.position, _nonLocalClientTargetPosition, 4f * Time.deltaTime);
        }

        if (transform.rotation != _nonLocalClientTargetRotation && _nonLocalClientTargetRotation != new Quaternion(0, 0, 0, 0))
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, _nonLocalClientTargetRotation, 14f * Time.deltaTime);
        }
    }

    #endregion

    void Update ()
    {

        if (isServer) ServerUpdate();
        else if (isLocalPlayer) ClientUpdate();
        else InterpolateTransform();
	}

    private void OnDestroy()
    {
        PhysicsNetworkUpdater.Instance.DestroyPhysicsSceneOfGO(this.gameObject);
    }

}

class ClientPredictedMessage : MessageBase
{
    public uint packetId;
    public float deliveryTime;
    public float rtt;
    public uint startTickNumber;
    public Inputs[] inputs;
}


public class ServerStateMessage : MessageBase
{
    public uint packetId;
    public float deliveryTime;
    public uint tickNumber;
    
    public ServerState serverState;

}

