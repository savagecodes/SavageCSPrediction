using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(Rigidbody))]
public class NetworkedMovement : NetworkBehaviour {

    short InputMessageReceived = 1002;
    short StateMessageReceived = 1003;

    float CurrentTime;

    public Transform local_player_camera_transform;
    public GameObject proxy_player;
    public GameObject smoothed_client_player;

    public float player_movement_impulse;
    public float player_jump_y_threshold;

    Rigidbody _rigidbody;

    private float client_timer;
    private uint client_tick_number;
    private uint client_last_received_state_tick;
    private const int c_client_buffer_size = 1024;
    private ClientState[] client_state_buffer; // client stores predicted moves here
    private Inputs[] client_input_buffer; // client stores predicted inputs here
    private BinaryHeap<StateMessage> client_state_msgs;
    private HashSet<uint> client_state_msgs_IDs = new HashSet<uint>();
    private Vector3 client_pos_error;
    private Quaternion client_rot_error;
    uint clientPacketID;

    public bool client_enable_corrections = true;
    public bool client_correction_smoothing = true;
    public bool client_send_redundant_inputs = true;

    // server specific
    public uint server_snapshot_rate;
    private uint server_tick_number;
    private uint server_tick_accumulator;
    private BinaryHeap<InputMessage> server_input_msgs;
    HashSet<uint> server_input_msgs_IDs = new HashSet<uint>();
    uint serverPacketID;

    //inputs
    bool isPressingUp;
    bool isPressingDown;
    bool isPressingLeft;
    bool isPressingRight;
    bool isPressingJump;

    public bool IsPressingUp { set { isPressingUp = value; } }
    public bool IsPressingDown { set { isPressingDown = value; } }
    public bool IsPressingLeft { set { isPressingLeft = value; } }
    public bool IsPressingRight { set { isPressingRight = value; } }
    public bool IsPressingJump { set { isPressingJump = value; } }

    // Use this for initialization
    void Start () {

        _rigidbody = GetComponent<Rigidbody>();
        InputMessageReceived += System.Convert.ToInt16(netId.Value);
        StateMessageReceived += System.Convert.ToInt16(netId.Value);

        if (!isServer && !isLocalPlayer)
        {
            _rigidbody.isKinematic = true;
            this.enabled = false;
            var networkTransform = gameObject.AddComponent<NetworkTransform>();
            networkTransform.transformSyncMode = NetworkTransform.TransformSyncMode.SyncTransform;
            return;
        }

        client_timer = 0.0f;
        client_tick_number = 0;
        client_last_received_state_tick = 0;
        client_state_buffer = new ClientState[c_client_buffer_size];
        client_input_buffer = new Inputs[c_client_buffer_size];
        client_state_msgs = new BinaryHeap<StateMessage>();
        client_pos_error = Vector3.zero;
        client_rot_error = Quaternion.identity;

        server_tick_number = 0;
        server_tick_accumulator = 0;
        server_input_msgs = new BinaryHeap<InputMessage>();

        if (isServer)
        {
            proxy_player.transform.SetParent(null);
            smoothed_client_player.transform.SetParent(null);
            proxy_player.GetComponent<MeshRenderer>().enabled = false;
            smoothed_client_player.GetComponent<MeshRenderer>().enabled = false;
        }
        else
        {
            proxy_player.transform.SetParent(null);
            smoothed_client_player.transform.SetParent(null);
        }

        if (!isServer)
        {
            NetworkManager.singleton.client.RegisterHandler(StateMessageReceived, OnStateMessageReceived);
            base.connectionToServer.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128);
        }
        else
        {
            PhysicsNetworkUpdater.Instance._movementComponents.Add(this);
            base.connectionToClient.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128);
        }

        NetworkServer.RegisterHandler(InputMessageReceived, OnInputMessageReceived);

    }

    void OnInputMessageReceived(NetworkMessage netMsg)
    {
        var message = netMsg.ReadMessage<InputMessage>();

        if (server_input_msgs_IDs.Contains(message.packetId)) return;

        server_input_msgs_IDs.Add(message.packetId);

        server_input_msgs.Enqueue(new HeapElement<InputMessage>(message,message.packetId));

    }

    void OnStateMessageReceived(NetworkMessage netMsg)
    {
        var message = netMsg.ReadMessage<StateMessage>();

        if (client_state_msgs_IDs.Contains(message.packetId)) return;
        client_state_msgs_IDs.Add(message.packetId);

        client_state_msgs.Enqueue(new HeapElement<StateMessage>(message, message.packetId));
    }
    
    public void OnPhysiscsUpdated()
    {
       
       // ++server_tick_accumulator;
                    
        // Debug.Log("NetID : "+netId.Value+" | Server Tick : " + server_tick_number + " | Normalized Tick : " + (serverTickAtStart + realServerTick));
                    
        ++server_tick_accumulator;
        if (server_tick_accumulator >= server_snapshot_rate)
        {
            server_tick_accumulator = 0;
                        
            StateMessage state_msg = new StateMessage();
            state_msg.packetId = serverPacketID;
            state_msg.delivery_time = CurrentTime + server_input_msgs.Peek().Element.rtt / 2;
            state_msg.tick_number = server_tick_number;
            state_msg.position = _rigidbody.position;
            state_msg.rotation = _rigidbody.rotation;
            state_msg.velocity = _rigidbody.velocity;
            state_msg.angular_velocity = _rigidbody.angularVelocity;

            //SendMesageToClient
            //base.connectionToClient.Send(StateMessageReceived, state_msg);
            NetworkServer.SendToClientOfPlayer(this.gameObject, StateMessageReceived, state_msg);
            serverPacketID++;
                    
        }
        
        smoothed_client_player.transform.position = _rigidbody.position;
        smoothed_client_player.transform.rotation = _rigidbody.rotation;
    }


    void ServerUpdate()
    {
        CurrentTime += Time.deltaTime;
        float dt = Time.fixedDeltaTime;
        uint server_tick_number = this.server_tick_number;
        uint server_tick_accumulator = this.server_tick_accumulator;

        while (server_input_msgs.Count > 0 && CurrentTime >= server_input_msgs.Peek().Element.delivery_time)
        {
            InputMessage input_msg = server_input_msgs.Dequeue().Element;

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
                    // Physics.Simulate(dt);

                    PhysicsNetworkUpdater.Instance.OnReadyToSimulate();

                    ++server_tick_number;
                    
                    OnPhysiscsUpdated();
                   
                }


                //smoothed_client_player.transform.position = _rigidbody.position;
                //smoothed_client_player.transform.rotation = _rigidbody.rotation;
            }
        }

       this.server_tick_number = server_tick_number;
       //this.server_tick_accumulator = server_tick_accumulator;
    }

    void ClientUpdate()
    {

        CurrentTime += Time.deltaTime;

        float dt = Time.fixedDeltaTime;
        float client_timer = this.client_timer;
        uint client_tick_number = this.client_tick_number;

        client_timer += Time.deltaTime;

        while (client_timer >= dt)
        {
            client_timer -= dt;

            uint buffer_slot = client_tick_number % c_client_buffer_size;

            // sample and store inputs for this tick
            Inputs inputs;
            inputs.up = isPressingUp;
            inputs.down = isPressingDown;
            inputs.left = isPressingLeft;
            inputs.right = isPressingRight;
            inputs.jump = isPressingJump;
            client_input_buffer[buffer_slot] = inputs;

            // store state for this tick, then use current state + input to step simulation
            ClientStoreCurrentStateAndStep(
                ref client_state_buffer[buffer_slot],
                _rigidbody,
                inputs,
                dt);
       
            InputMessage input_msg = new InputMessage();
            var rtt = (NetworkManager.singleton.client.GetRTT() / 1000f);
            input_msg.packetId = clientPacketID;
            input_msg.delivery_time = CurrentTime + rtt / 2 ;
            input_msg.rtt = rtt;
            input_msg.start_tick_number = client_send_redundant_inputs ? client_last_received_state_tick : client_tick_number;
            var inputs_List = new List<Inputs>();

            for (uint tick = input_msg.start_tick_number; tick <= client_tick_number; ++tick)
            {
                inputs_List.Add(client_input_buffer[tick % c_client_buffer_size]);
            }

            input_msg.inputs = inputs_List.ToArray();

            //Sern Input Message To Server
            base.connectionToServer.Send(InputMessageReceived, input_msg);

            clientPacketID ++;

            ++client_tick_number;
        }

        if (ClientHasStateMessage())
        {
            StateMessage state_msg = client_state_msgs.Dequeue().Element;
            while (ClientHasStateMessage()) // make sure if there are any newer state messages available, we use those instead
            {
                state_msg = client_state_msgs.Dequeue().Element;
            }

            client_last_received_state_tick = state_msg.tick_number;

            proxy_player.transform.position = state_msg.position;
            proxy_player.transform.rotation = state_msg.rotation;

            if (client_enable_corrections)
            {
                uint buffer_slot = state_msg.tick_number % c_client_buffer_size;
                Vector3 position_error = state_msg.position - client_state_buffer[buffer_slot].position;
                float rotation_error = 1.0f - Quaternion.Dot(state_msg.rotation, client_state_buffer[buffer_slot].rotation);

                if (position_error.sqrMagnitude > 0.0000001f ||
                    rotation_error > 0.00001f)
                {
                    Debug.Log("Correcting");

                    // capture the current predicted pos for smoothing
                    Vector3 prev_pos = _rigidbody.position + client_pos_error;
                    Quaternion prev_rot = _rigidbody.rotation * client_rot_error;

                    // rewind & replay
                    _rigidbody.position = state_msg.position;
                    _rigidbody.rotation = state_msg.rotation;
                    _rigidbody.velocity = state_msg.velocity;
                    _rigidbody.angularVelocity = state_msg.angular_velocity;
              

                    uint rewind_tick_number = state_msg.tick_number;
                    while (rewind_tick_number < client_tick_number)
                    {
                        buffer_slot = rewind_tick_number % c_client_buffer_size;
                        ClientStoreCurrentStateAndStep(
                            ref client_state_buffer[buffer_slot],
                            _rigidbody,
                            client_input_buffer[buffer_slot],
                            dt);

                        ++rewind_tick_number;
                    }

                    // if more than 2mts apart, just snap
                    if ((prev_pos - _rigidbody.position).sqrMagnitude >= 4.0f)
                    {
                        client_pos_error = Vector3.zero;
                        client_rot_error = Quaternion.identity;
                    }
                    else
                    {
                        client_pos_error = prev_pos - _rigidbody.position;
                        client_rot_error = Quaternion.Inverse(_rigidbody.rotation) * prev_rot;
                    }
                }
            }
        }

        this.client_timer = client_timer;
        this.client_tick_number = client_tick_number;

        if (client_correction_smoothing)
        {
            client_pos_error *= 0.9f;
            client_rot_error = Quaternion.Slerp(client_rot_error, Quaternion.identity, 0.1f);
        }
        else
        {
            client_pos_error = Vector3.zero;
            client_rot_error = Quaternion.identity;
        }

        smoothed_client_player.transform.position = _rigidbody.position + client_pos_error;
        smoothed_client_player.transform.rotation = _rigidbody.rotation * client_rot_error;
    }
	
	void Update () {

        if (isServer) ServerUpdate();
        else ClientUpdate();
	}

    private void PrePhysicsStep(Rigidbody rigidbody, Inputs inputs)
    {
        if (local_player_camera_transform != null)
        {
            if (inputs.up)
            {
                rigidbody.AddForce(local_player_camera_transform.forward * player_movement_impulse, ForceMode.Impulse);
            }
            if (inputs.down)
            {
                rigidbody.AddForce(-local_player_camera_transform.forward * player_movement_impulse, ForceMode.Impulse);
            }
            if (inputs.left)
            {
                rigidbody.AddForce(-local_player_camera_transform.right * player_movement_impulse, ForceMode.Impulse);
            }
            if (inputs.right)
            {
                rigidbody.AddForce(local_player_camera_transform.right * player_movement_impulse, ForceMode.Impulse);
            }
            if (rigidbody.transform.position.y <= player_jump_y_threshold && inputs.jump)
            {
                rigidbody.AddForce(local_player_camera_transform.up * player_movement_impulse, ForceMode.Impulse);
            }
        }
    }

    private bool ClientHasStateMessage()
    {
        if (client_state_msgs.Peek() == null) return false;

       // Debug.Log(CurrentTime + " vs " + client_state_msgs.Peek().Element.delivery_time);

        return client_state_msgs.Count > 0 && CurrentTime >= client_state_msgs.Peek().Element.delivery_time;
    }

    private void ClientStoreCurrentStateAndStep(ref ClientState current_state, Rigidbody rigidbody, Inputs inputs, float dt)
    {
        current_state.position = rigidbody.position;
        current_state.rotation = rigidbody.rotation;

        PrePhysicsStep(rigidbody, inputs);
        Physics.Simulate(dt);
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

