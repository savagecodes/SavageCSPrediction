using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(Rigidbody))]
public class NetworkedMovement : NetworkBehaviour {

    private short _InputMessageReceivedID = 1002;
    private short _StateMessageReceivedID = 1003;

    PhysicsScene LocalPhysicsScene;

    private int _correctionsMadeOnClient;

    [Header("Required Compoennts")]
    [SerializeField]
    private Transform _cameraTransform;
    [SerializeField]
    private GameObject _serverGhostModel;
    [SerializeField]
    private GameObject _smoothedPlayerModel;

    Rigidbody _rigidbody;

    [Header("Movement Settings")]
    [SerializeField]
    private float _movementImpulse;
    [SerializeField]
    private float _jumpThresholdY;

    private float _currentTime;
    private uint _currentTickNumber;

    //client specific
    private float _clientTimer;
    private uint _clientLastRecivedStateTickNumber;
    private const int _clientBufferSize = 1024;
    private ClientState[] _clientStateBuffer; // client stores predicted moves here
    private Inputs[] _clientInputBuffer; // client stores predicted inputs here
    private BinaryHeap<StateMessage> _clientStateMessageQueue;
    private HashSet<uint> _clientStateMessageIDs;
    private Vector3 _clientPositionError;
    private Quaternion _clientRotationError;
    private uint _clientPacketID;

    [Header("Client Replication Settings")]
    [SerializeField]
    private bool _enableCorrectionsInClient = true;
    [SerializeField]
    private bool _enableCorrectionSmoothing = true;
    [SerializeField]
    private bool _sendRedundantInputsToServer = true;

    // server specific
    [Header("Server Replication Settings")]
    [SerializeField]
    private uint _serverSnapshotRate;
    // private uint _serverTickNumber;
    private uint _serverTickAccumulator;
    private BinaryHeap<InputMessage> _serverInputMessageQueue;
    private HashSet<uint> _serverInputMessagesIDs;
    private uint serverPacketID;

    //inputs
    private bool _isPressingUp;
    private bool _isPressingDown;
    private bool _isPressingLeft;
    private bool _isPressingRight;
    private bool _isPressingJump;

    //client non local player
    private Vector3 _nonLocalClientTargetPosition;
    private Quaternion _nonLocalClientTargetRotation;
    [SerializeField]
    private float _nonLocalSyncInterval = 0.1f;
    private bool _firstSyncMessageRecived;
    private bool _firstSynced;

    #region Getters

    public bool IsPressingUp { set { _isPressingUp = value; } }
    public bool IsPressingDown { set { _isPressingDown = value; } }
    public bool IsPressingLeft { set { _isPressingLeft = value; } }
    public bool IsPressingRight { set { _isPressingRight = value; } }
    public bool IsPressingJump { set { _isPressingJump = value; } }

    public int Corrections { get { return _correctionsMadeOnClient; } }

    public GameObject SmoothedPlayerModel { get { return _smoothedPlayerModel; } }

    #endregion

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

        _InputMessageReceivedID += System.Convert.ToInt16(netId.Value);
        _StateMessageReceivedID += System.Convert.ToInt16(netId.Value);

        _currentTickNumber = 0;

        #region Initializing Client Properties

        _clientTimer = 0.0f;
        _clientLastRecivedStateTickNumber = 0;
        _clientStateBuffer = new ClientState[_clientBufferSize];
        _clientInputBuffer = new Inputs[_clientBufferSize];
        _clientStateMessageQueue = new BinaryHeap<StateMessage>();
        _clientStateMessageIDs = new HashSet<uint>();
        _clientPositionError = Vector3.zero;
        _clientRotationError = Quaternion.identity;

        #endregion

        #region Initializing Server properties

        _serverTickAccumulator = 0;
        _serverInputMessageQueue = new BinaryHeap<InputMessage>();
        _serverInputMessagesIDs = new HashSet<uint>(); 

        #endregion


        if (isServer)
        {
            //-----------------
            // Visual Debug , this component only should give/Expose the positions

            _serverGhostModel.transform.SetParent(null);
            _smoothedPlayerModel.transform.SetParent(null);
            _serverGhostModel.GetComponent<MeshRenderer>().enabled = false;
            _smoothedPlayerModel.GetComponent<MeshRenderer>().enabled = false;
            //
            //-----------------

            StartCoroutine(SyncNonLocalClientTransform());
            PhysicsNetworkUpdater.Instance._movementComponents.Add(this);
            connectionToClient.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128);
        }
        else
        {
            NetworkManager.singleton.client.RegisterHandler(_StateMessageReceivedID, OnStateMessageReceived);
            connectionToServer.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128);

            //-----------------
            // Visual Debug , this component only should give/Expose the positions
            GetComponent<MeshRenderer>().enabled = false;
            _serverGhostModel.transform.SetParent(null);
            _smoothedPlayerModel.transform.SetParent(null);
            //
            //-----------------
        }


        NetworkServer.RegisterHandler(_InputMessageReceivedID, OnInputMessageReceived);

    }

    #region Network Messages Handlers

    void OnInputMessageReceived(NetworkMessage netMsg)
    {
        var message = netMsg.ReadMessage<InputMessage>();

        if (_serverInputMessagesIDs.Contains(message.packetId)) return;

        _serverInputMessagesIDs.Add(message.packetId);

        _serverInputMessageQueue.Enqueue(new HeapElement<InputMessage>(message,message.packetId));

    }

    void OnStateMessageReceived(NetworkMessage netMsg)
    {
        var message = netMsg.ReadMessage<StateMessage>();

        if (_clientStateMessageIDs.Contains(message.packetId)) return;
        _clientStateMessageIDs.Add(message.packetId);

        _clientStateMessageQueue.Enqueue(new HeapElement<StateMessage>(message, message.packetId));
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
                        
            StateMessage state_msg = new StateMessage();
            state_msg.packetId = serverPacketID;
            state_msg.deliveryTime = _currentTime + _serverInputMessageQueue.Peek().Element.rtt / 2;
            state_msg.tickNumber = _currentTickNumber;
            state_msg.position = _rigidbody.position;
            state_msg.rotation = _rigidbody.rotation;
            state_msg.velocity = _rigidbody.velocity;
            state_msg.angularVelocity = _rigidbody.angularVelocity;

            //Send Message To Client
            NetworkServer.SendToClientOfPlayer(this.gameObject, _StateMessageReceivedID, state_msg);
            serverPacketID++;
                    
        }
        
        _smoothedPlayerModel.transform.position = _rigidbody.position;
        _smoothedPlayerModel.transform.rotation = _rigidbody.rotation;
    }


    void ServerUpdate()
    {
        _currentTime += Time.deltaTime;

        while (_serverInputMessageQueue.Count > 0 && _currentTime >= _serverInputMessageQueue.Peek().Element.deliveryTime)
        {
            InputMessage inputMessage = _serverInputMessageQueue.Dequeue().Element;

            // message contains an array of inputs, calculate what tick the final one is
            uint maxTick = inputMessage.startTickNumber + (uint)inputMessage.inputs.Length - 1;

            // if that tick is greater than or equal to the current tick we're on, then it
            // has inputs which are new
            if (maxTick >= _currentTickNumber)
            {
                // there may be some inputs in the array that we've already had,
                // so figure out where to start
                uint startIndex = _currentTickNumber > inputMessage.startTickNumber ? (_currentTickNumber - inputMessage.startTickNumber) : 0;

                // run through all relevant inputs, and step player forward
                for (int i = (int)startIndex; i < inputMessage.inputs.Length; ++i)
                {
                    PrePhysicsStep(_rigidbody, inputMessage.inputs[i]);

                    //PhysicsNetworkUpdater.Instance.OnReadyToSimulate();
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

            // sample and store inputs for this tick
            Inputs inputs;
            inputs.up = _isPressingUp;
            inputs.down = _isPressingDown;
            inputs.left = _isPressingLeft;
            inputs.right = _isPressingRight;
            inputs.jump = _isPressingJump;
            _clientInputBuffer[buffer_slot] = inputs;

            // store state for this tick, then use current state + input to step simulation
            ClientStoreCurrentStateAndStep(ref _clientStateBuffer[buffer_slot],_rigidbody,inputs, Time.fixedDeltaTime);
       
            InputMessage inputMessage = new InputMessage();
            var rtt = (NetworkManager.singleton.client.GetRTT() / 1000f);
            inputMessage.packetId = _clientPacketID;
            inputMessage.deliveryTime = _currentTime + rtt / 2 ;
            inputMessage.rtt = rtt;
            inputMessage.startTickNumber = _sendRedundantInputsToServer ? _clientLastRecivedStateTickNumber : _currentTickNumber;

            var inputList = new List<Inputs>();

            for (uint tick = inputMessage.startTickNumber; tick <= _currentTickNumber; tick++)
            {
                inputList.Add(_clientInputBuffer[tick % _clientBufferSize]);
            }

            inputMessage.inputs = inputList.ToArray();

            //Send Input Message To Server
            connectionToServer.Send(_InputMessageReceivedID, inputMessage);

            _clientPacketID++;

            _currentTickNumber++;
        }

        if (ClientHasStateMessage())
        {
            StateMessage stateMessage = _clientStateMessageQueue.Dequeue().Element;
            while (ClientHasStateMessage()) // make sure if there are any newer state messages available, we use those instead
            {
                stateMessage = _clientStateMessageQueue.Dequeue().Element;
            }

            _clientLastRecivedStateTickNumber = stateMessage.tickNumber;

            _serverGhostModel.transform.position = stateMessage.position;
            _serverGhostModel.transform.rotation = stateMessage.rotation;

            if (_enableCorrectionsInClient)
            {
                uint bufferSlot = stateMessage.tickNumber % _clientBufferSize;

                Vector3 positionError = stateMessage.position - _clientStateBuffer[bufferSlot].position;
                float rotationError = 1.0f - Quaternion.Dot(stateMessage.rotation, _clientStateBuffer[bufferSlot].rotation);

                if (positionError.sqrMagnitude > 0.0000001f || rotationError > 0.00001f)
                {
                    if (isLocalPlayer)
                    {
                        _correctionsMadeOnClient++;
                    }

                    // capture the current predicted pos for smoothing
                    Vector3 prevPosition = _rigidbody.position + _clientPositionError;
                    Quaternion prevRotation = _rigidbody.rotation * _clientRotationError;

                    // rewind & replay
                    _rigidbody.position = stateMessage.position;
                    _rigidbody.rotation = stateMessage.rotation;
                    _rigidbody.velocity = stateMessage.velocity;
                    _rigidbody.angularVelocity = stateMessage.angularVelocity;
              

                    uint rewindTickNumber = stateMessage.tickNumber;

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
            }
        }

        if (_enableCorrectionSmoothing)
        {
            _clientPositionError *= 0.9f;
            _clientRotationError = Quaternion.Slerp(_clientRotationError, Quaternion.identity, 0.1f);
        }
        else
        {
            _clientPositionError = Vector3.zero;
            _clientRotationError = Quaternion.identity;
        }

        _smoothedPlayerModel.transform.position = _rigidbody.position + _clientPositionError;
        _smoothedPlayerModel.transform.rotation = _rigidbody.rotation * _clientRotationError;
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

        PrePhysicsStep(rigidbody, inputs);
        PhysicsNetworkUpdater.Instance.UpdatePhysics(this);
        //Physics.Simulate(deltaTime);
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


    //Apply inputs to the Rigidbody, before triggering the physics simulation 
    private void PrePhysicsStep(Rigidbody rigidbody, Inputs inputs)
    {
        if (_cameraTransform != null)
        {
            if (inputs.up)
            {
                rigidbody.AddForce(_cameraTransform.forward * _movementImpulse, ForceMode.Impulse);
            }
            if (inputs.down)
            {
                rigidbody.AddForce(-_cameraTransform.forward * _movementImpulse, ForceMode.Impulse);
            }
            if (inputs.left)
            {
                rigidbody.AddForce(-_cameraTransform.right * _movementImpulse, ForceMode.Impulse);
            }
            if (inputs.right)
            {
                rigidbody.AddForce(_cameraTransform.right * _movementImpulse, ForceMode.Impulse);
            }
            if (rigidbody.transform.position.y <= _jumpThresholdY && inputs.jump)
            {
                rigidbody.AddForce(_cameraTransform.up * _movementImpulse, ForceMode.Impulse);
            }
        }
    }


    private void OnDestroy()
    {
        PhysicsNetworkUpdater.Instance._movementComponents.Remove(this);
    }

}

[System.Serializable]
public struct Inputs
{
    public bool up;
    public bool down;
    public bool left;
    public bool right;
    public bool jump;
}

class InputMessage : MessageBase
{
    public uint packetId;
    public float deliveryTime;
    public float rtt;
    public uint startTickNumber;
    public Inputs[] inputs;
}

[System.Serializable]
struct ClientState
{
    public Vector3 position;
    public Quaternion rotation;
}

public class StateMessage : MessageBase
{
    public uint packetId;
    public float deliveryTime;
    public uint tickNumber;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
}

