using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(Rigidbody))]
public class NetworkedMovement : NetworkBehaviour {

    private short _InputMessageReceivedID = 1002;
    private short _StateMessageReceivedID = 1003;

    private float _currentTime;

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

    private float _clientTimer;
    private uint _clientTickNumber;
    private uint _clientLastRecivedStateTickNumber;
    private const int _clientBufferSize = 1024;
    private ClientState[] _clientStateBuffer; // client stores predicted moves here
    private Inputs[] _clientInputBuffer; // client stores predicted inputs here
    private BinaryHeap<StateMessage> _clientStateMessageQueue;
    private HashSet<uint> _clientStateMessageIDs = new HashSet<uint>();
    private Vector3 _clientPositionError;
    private Quaternion _clientRotationError;
    private uint _clientPacketID;

    [Header("Client Replication Settings")]
    [SerializeField]
    private bool _bEnableCorrectionsInClient = true;
    [SerializeField]
    private bool _bEnableCorrectionSmoothing = true;
    [SerializeField]
    private bool _bSendRedundantInputsToServer = true;

    // server specific
    [Header("Server Replication Settings")]
    [SerializeField]
    private uint _serverSnapshotRate;
    private uint _serverTickNumber;
    private uint _serverTickAccumulator;
    private BinaryHeap<InputMessage> _serverInputMessageQueue;
    private HashSet<uint> _serverInputMessagesIDs = new HashSet<uint>();
    private uint serverPacketID;

    //inputs
    private bool _isPressingUp;
    private bool _isPressingDown;
    private bool _isPressingLeft;
    private bool _isPressingRight;
    private bool _isPressingJump;

    //client no local player
    private Vector3 _nonLocalClientTargetPosition;
    private Quaternion _nonLocalClientTargetRotation;
    [SerializeField]
    private float nonLocalSyncInterval = 0.1f;
    bool _firstSyncMessageRecived;
    bool _firstSynced;

    #region Getters

    public bool IsPressingUp { set { _isPressingUp = value; } }
    public bool IsPressingDown { set { _isPressingDown = value; } }
    public bool IsPressingLeft { set { _isPressingLeft = value; } }
    public bool IsPressingRight { set { _isPressingRight = value; } }
    public bool IsPressingJump { set { _isPressingJump = value; } }

    public int Corrections { get { return _correctionsMadeOnClient; } }

    #endregion

    // Use this for initialization
    void Start () {

        _rigidbody = GetComponent<Rigidbody>();
        _InputMessageReceivedID += System.Convert.ToInt16(netId.Value);
        _StateMessageReceivedID += System.Convert.ToInt16(netId.Value);

        if (!isServer && !isLocalPlayer)
        {
            _rigidbody.isKinematic = true;
            return;
        }

        _clientTimer = 0.0f;
        _clientTickNumber = 0;
        _clientLastRecivedStateTickNumber = 0;
        _clientStateBuffer = new ClientState[_clientBufferSize];
        _clientInputBuffer = new Inputs[_clientBufferSize];
        _clientStateMessageQueue = new BinaryHeap<StateMessage>();
        _clientPositionError = Vector3.zero;
        _clientRotationError = Quaternion.identity;

        _serverTickNumber = 0;
        _serverTickAccumulator = 0;
        _serverInputMessageQueue = new BinaryHeap<InputMessage>();

        if (isServer)
        {
            _serverGhostModel.transform.SetParent(null);
            _smoothedPlayerModel.transform.SetParent(null);
            _serverGhostModel.GetComponent<MeshRenderer>().enabled = false;
            _smoothedPlayerModel.GetComponent<MeshRenderer>().enabled = false;
            StartCoroutine(SyncNonLocalClientTransform());
        }
        else
        {
            _serverGhostModel.transform.SetParent(null);
            _smoothedPlayerModel.transform.SetParent(null);
        }

        if (!isServer)
        {
            NetworkManager.singleton.client.RegisterHandler(_StateMessageReceivedID, OnStateMessageReceived);
            base.connectionToServer.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128);
        }
        else
        {
            PhysicsNetworkUpdater.Instance._movementComponents.Add(this);
            base.connectionToClient.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128);
        }

        NetworkServer.RegisterHandler(_InputMessageReceivedID, OnInputMessageReceived);

        

    }

    IEnumerator SyncNonLocalClientTransform()
    {
        var wait = new WaitForSeconds(nonLocalSyncInterval);

        while (true)
        {
            RpcTransformUpdate(transform.position,transform.rotation);
            yield return wait;
        }
    }

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


    public void OnPhysiscsUpdated()
    {            
        ++_serverTickAccumulator;
        if (_serverTickAccumulator >= _serverSnapshotRate)
        {
            _serverTickAccumulator = 0;
                        
            StateMessage state_msg = new StateMessage();
            state_msg.packetId = serverPacketID;
            state_msg.delivery_time = _currentTime + _serverInputMessageQueue.Peek().Element.rtt / 2;
            state_msg.tick_number = _serverTickNumber;
            state_msg.position = _rigidbody.position;
            state_msg.rotation = _rigidbody.rotation;
            state_msg.velocity = _rigidbody.velocity;
            state_msg.angular_velocity = _rigidbody.angularVelocity;

            //SendMesageToClient
            NetworkServer.SendToClientOfPlayer(this.gameObject, _StateMessageReceivedID, state_msg);
            serverPacketID++;
                    
        }
        
        _smoothedPlayerModel.transform.position = _rigidbody.position;
        _smoothedPlayerModel.transform.rotation = _rigidbody.rotation;
    }


    void ServerUpdate()
    {
        _currentTime += Time.deltaTime;

        uint server_tick_number = this._serverTickNumber;
        //uint server_tick_accumulator = this.server_tick_accumulator;

        while (_serverInputMessageQueue.Count > 0 && _currentTime >= _serverInputMessageQueue.Peek().Element.delivery_time)
        {
            InputMessage input_msg = _serverInputMessageQueue.Dequeue().Element;

            // message contains an array of inputs, calculate what tick the final one is
            uint max_tick = input_msg.start_tick_number + (uint)input_msg.inputs.Length - 1;

            // if that tick is greater than or equal to the current tick we're on, then it
            // has inputs which are new
            if (max_tick >= server_tick_number)
            {
                // there may be some inputs in the array that we've already had,
                // so figure out where to start
                uint start_i = server_tick_number > input_msg.start_tick_number ? (server_tick_number - input_msg.start_tick_number) : 0;

                // run through all relevant inputs, and step player forward
                for (int i = (int)start_i; i < input_msg.inputs.Length; ++i)
                {
                    PrePhysicsStep(_rigidbody, input_msg.inputs[i]);

                    PhysicsNetworkUpdater.Instance.OnReadyToSimulate();

                    ++server_tick_number;
                   
                }

            }
        }

        this._serverTickNumber = server_tick_number;

    }

    void ClientUpdate()
    {

        _currentTime += Time.deltaTime;

        float client_timer = this._clientTimer;
        uint client_tick_number = this._clientTickNumber;

        client_timer += Time.deltaTime;

        while (client_timer >= Time.fixedDeltaTime)
        {
            client_timer -= Time.fixedDeltaTime;

            uint buffer_slot = client_tick_number % _clientBufferSize;

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
       
            InputMessage input_msg = new InputMessage();
            var rtt = (NetworkManager.singleton.client.GetRTT() / 1000f);
            input_msg.packetId = _clientPacketID;
            input_msg.delivery_time = _currentTime + rtt / 2 ;
            input_msg.rtt = rtt;
            input_msg.start_tick_number = _bSendRedundantInputsToServer ? _clientLastRecivedStateTickNumber : client_tick_number;
            var inputs_List = new List<Inputs>();

            for (uint tick = input_msg.start_tick_number; tick <= client_tick_number; ++tick)
            {
                inputs_List.Add(_clientInputBuffer[tick % _clientBufferSize]);
            }

            input_msg.inputs = inputs_List.ToArray();

            //Sern Input Message To Server
            base.connectionToServer.Send(_InputMessageReceivedID, input_msg);

            _clientPacketID ++;

            ++client_tick_number;
        }

        if (ClientHasStateMessage())
        {
            StateMessage state_msg = _clientStateMessageQueue.Dequeue().Element;
            while (ClientHasStateMessage()) // make sure if there are any newer state messages available, we use those instead
            {
                state_msg = _clientStateMessageQueue.Dequeue().Element;
            }

            _clientLastRecivedStateTickNumber = state_msg.tick_number;

            _serverGhostModel.transform.position = state_msg.position;
            _serverGhostModel.transform.rotation = state_msg.rotation;

            if (_bEnableCorrectionsInClient)
            {
                uint buffer_slot = state_msg.tick_number % _clientBufferSize;
                Vector3 position_error = state_msg.position - _clientStateBuffer[buffer_slot].position;
                float rotation_error = 1.0f - Quaternion.Dot(state_msg.rotation, _clientStateBuffer[buffer_slot].rotation);

                if (position_error.sqrMagnitude > 0.0000001f ||
                    rotation_error > 0.00001f)
                {
                    if (isLocalPlayer)
                    {
                        _correctionsMadeOnClient++;
                    }

                    // capture the current predicted pos for smoothing
                    Vector3 prev_pos = _rigidbody.position + _clientPositionError;
                    Quaternion prev_rot = _rigidbody.rotation * _clientRotationError;

                    // rewind & replay
                    _rigidbody.position = state_msg.position;
                    _rigidbody.rotation = state_msg.rotation;
                    _rigidbody.velocity = state_msg.velocity;
                    _rigidbody.angularVelocity = state_msg.angular_velocity;
              

                    uint rewind_tick_number = state_msg.tick_number;
                    while (rewind_tick_number < client_tick_number)
                    {
                        buffer_slot = rewind_tick_number % _clientBufferSize;
                        ClientStoreCurrentStateAndStep(
                            ref _clientStateBuffer[buffer_slot],
                            _rigidbody,
                            _clientInputBuffer[buffer_slot],
                            Time.fixedDeltaTime);

                        ++rewind_tick_number;
                    }

                    // if more than 2mts apart, just snap
                    if ((prev_pos - _rigidbody.position).sqrMagnitude >= 4.0f)
                    {
                        _clientPositionError = Vector3.zero;
                        _clientRotationError = Quaternion.identity;
                    }
                    else
                    {
                        _clientPositionError = prev_pos - _rigidbody.position;
                        _clientRotationError = Quaternion.Inverse(_rigidbody.rotation) * prev_rot;
                    }
                }
            }
        }

        this._clientTimer = client_timer;
        this._clientTickNumber = client_tick_number;

        if (_bEnableCorrectionSmoothing)
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
	
	void Update () {

        if (isServer) ServerUpdate();
        else if (isLocalPlayer) ClientUpdate();
        else InterpolateTransform();

	}

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

    private bool ClientHasStateMessage()
    {
        if (_clientStateMessageQueue.Peek() == null)
        {
            return false;
        }

        return _clientStateMessageQueue.Count > 0 && _currentTime >= _clientStateMessageQueue.Peek().Element.delivery_time;
    }

    private void ClientStoreCurrentStateAndStep(ref ClientState current_state, Rigidbody rigidbody, Inputs inputs, float deltaTime)
    {
        current_state.position = rigidbody.position;
        current_state.rotation = rigidbody.rotation;

        PrePhysicsStep(rigidbody, inputs);
        Physics.Simulate(deltaTime);
    }

    [ClientRpc]
    void RpcTransformUpdate(Vector3 position , Quaternion rotation)
    {
        _nonLocalClientTargetPosition = position;
        _nonLocalClientTargetRotation = rotation;
        _firstSyncMessageRecived = true;
    }

    //Only has to be called in Clients not local player

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
            transform.position = Vector3.Lerp(transform.position, _nonLocalClientTargetPosition, 4f * Time.deltaTime);
        
        if(transform.rotation != _nonLocalClientTargetRotation && _nonLocalClientTargetRotation != new Quaternion(0,0,0,0))
        transform.rotation = Quaternion.Lerp(transform.rotation, _nonLocalClientTargetRotation, 14f * Time.deltaTime);
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
    public float delivery_time;
    public float rtt;
    public uint start_tick_number;
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
    public float delivery_time;
    public uint tick_number;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angular_velocity;
}

