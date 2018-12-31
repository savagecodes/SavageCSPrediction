﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkedMovement : NetworkBehaviour {

    const short InputMessageReceived = 1002;
    const short StateMessageReceived = 1003;

    public float CurrentTime;

    public Transform local_player_camera_transform;
    public GameObject proxy_player;
    public float player_movement_impulse;
    public float player_jump_y_threshold;

    public GameObject smoothed_client_player;

    private float client_timer;
    private uint client_tick_number;
    private uint client_last_received_state_tick;
    private const int c_client_buffer_size = 1024;
    private ClientState[] client_state_buffer; // client stores predicted moves here
    private Inputs[] client_input_buffer; // client stores predicted inputs here
    private BinaryHeap<StateMessage> client_state_msgs;
    private Vector3 client_pos_error;
    private Quaternion client_rot_error;

    public bool client_enable_corrections = true;
    public bool client_correction_smoothing = true;
    public bool client_send_redundant_inputs = true;

    // server specific
    public uint server_snapshot_rate;
    private uint server_tick_number;
    private uint server_tick_accumulator;
    private BinaryHeap<InputMessage> server_input_msgs;

    private void Awake()
    {
        Application.targetFrameRate = 60;

    }

    // Use this for initialization
    void Start () {

        this.client_timer = 0.0f;
        this.client_tick_number = 0;
        this.client_last_received_state_tick = 0;
        this.client_state_buffer = new ClientState[c_client_buffer_size];
        this.client_input_buffer = new Inputs[c_client_buffer_size];
        this.client_state_msgs = new BinaryHeap<StateMessage>();
        this.client_pos_error = Vector3.zero;
        this.client_rot_error = Quaternion.identity;

        this.server_tick_number = 0;
        this.server_tick_accumulator = 0;
        this.server_input_msgs = new BinaryHeap<InputMessage>();

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
            base.connectionToClient.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128);
        }

        NetworkServer.RegisterHandler(InputMessageReceived, OnInputMessageReceived);



    }

    void OnInputMessageReceived(NetworkMessage netMsg)
    {
        var message = netMsg.ReadMessage<InputMessage>();

        this.server_input_msgs.Enqueue(new HeapElement<InputMessage>(message,message.start_tick_number));
    }

    void OnStateMessageReceived(NetworkMessage netMsg)
    {
        var message = netMsg.ReadMessage<StateMessage>();
        client_state_msgs.Enqueue(new HeapElement<StateMessage>(message, message.tick_number));
    }

    void ServerUpdate()
    {
        CurrentTime += Time.deltaTime;
        float dt = Time.fixedDeltaTime;
        uint server_tick_number = this.server_tick_number;
        uint server_tick_accumulator = this.server_tick_accumulator;
        Rigidbody server_rigidbody = GetComponent<Rigidbody>();

        while (this.server_input_msgs.Count > 0 && CurrentTime >= this.server_input_msgs.Peek().Element.delivery_time)
        {
            InputMessage input_msg = this.server_input_msgs.Dequeue().Element;

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
                    this.PrePhysicsStep(server_rigidbody, input_msg.inputs[i]);
                    Physics.Simulate(dt);

                    ++server_tick_number;
                    ++server_tick_accumulator;
                    if (server_tick_accumulator >= this.server_snapshot_rate)
                    {
                        server_tick_accumulator = 0;
                        
                     StateMessage state_msg = new StateMessage();
                     state_msg.delivery_time = CurrentTime + server_input_msgs.Peek().Element.rtt / 2;
            
                     state_msg.tick_number = server_tick_number;
                     state_msg.position = server_rigidbody.position;
                     state_msg.rotation = server_rigidbody.rotation;
                     state_msg.velocity = server_rigidbody.velocity;
                     state_msg.angular_velocity = server_rigidbody.angularVelocity;

                     //SendMesageToClient
                     base.connectionToClient.Send(StateMessageReceived, state_msg);
                    }
                }


                this.smoothed_client_player.transform.position = server_rigidbody.position;
                this.smoothed_client_player.transform.rotation = server_rigidbody.rotation;
            }
        }

        this.server_tick_number = server_tick_number;
        this.server_tick_accumulator = server_tick_accumulator;
    }

    void ClientUpdate()
    {

        CurrentTime += Time.deltaTime;

        Rigidbody client_rigidbody = GetComponent<Rigidbody>();
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
            inputs.up = Input.GetKey(KeyCode.W);
            inputs.down = Input.GetKey(KeyCode.S);
            inputs.left = Input.GetKey(KeyCode.A);
            inputs.right = Input.GetKey(KeyCode.D);
            inputs.jump = Input.GetKey(KeyCode.Space);
            this.client_input_buffer[buffer_slot] = inputs;

            // store state for this tick, then use current state + input to step simulation
            this.ClientStoreCurrentStateAndStep(
                ref this.client_state_buffer[buffer_slot],
                client_rigidbody,
                inputs,
                dt);
       
            InputMessage input_msg = new InputMessage();
            var rtt = (NetworkManager.singleton.client.GetRTT() / 1000f);
            input_msg.delivery_time = CurrentTime + rtt / 2 ;
            input_msg.rtt = rtt;
            input_msg.start_tick_number = this.client_send_redundant_inputs ? this.client_last_received_state_tick : client_tick_number;
            var inputs_List = new List<Inputs>();

            for (uint tick = input_msg.start_tick_number; tick <= client_tick_number; ++tick)
            {
                inputs_List.Add(this.client_input_buffer[tick % c_client_buffer_size]);
            }

            input_msg.inputs = inputs_List.ToArray();

            //Sern Input Message To Server
            base.connectionToServer.Send(InputMessageReceived, input_msg);
            
            ++client_tick_number;
        }

       // Debug.Log(ClientHasStateMessage());

        if (this.ClientHasStateMessage())
        {
            StateMessage state_msg = this.client_state_msgs.Dequeue().Element;
            while (this.ClientHasStateMessage()) // make sure if there are any newer state messages available, we use those instead
            {
                state_msg = this.client_state_msgs.Dequeue().Element;
            }

            this.client_last_received_state_tick = state_msg.tick_number;

            this.proxy_player.transform.position = state_msg.position;
            this.proxy_player.transform.rotation = state_msg.rotation;

            if (this.client_enable_corrections)
            {
                uint buffer_slot = state_msg.tick_number % c_client_buffer_size;
                Vector3 position_error = state_msg.position - this.client_state_buffer[buffer_slot].position;
                float rotation_error = 1.0f - Quaternion.Dot(state_msg.rotation, this.client_state_buffer[buffer_slot].rotation);

                if (position_error.sqrMagnitude > 0.0000001f ||
                    rotation_error > 0.00001f)
                {
                    Debug.Log("Correcting");
                    // capture the current predicted pos for smoothing
                    Vector3 prev_pos = client_rigidbody.position + this.client_pos_error;
                    Quaternion prev_rot = client_rigidbody.rotation * this.client_rot_error;

                    // rewind & replay
                    client_rigidbody.position = state_msg.position;
                    client_rigidbody.rotation = state_msg.rotation;
                    client_rigidbody.velocity = state_msg.velocity;
                    client_rigidbody.angularVelocity = state_msg.angular_velocity;
              

                    uint rewind_tick_number = state_msg.tick_number;
                    while (rewind_tick_number < client_tick_number)
                    {
                        buffer_slot = rewind_tick_number % c_client_buffer_size;
                        this.ClientStoreCurrentStateAndStep(
                            ref this.client_state_buffer[buffer_slot],
                            client_rigidbody,
                            this.client_input_buffer[buffer_slot],
                            dt);

                        ++rewind_tick_number;
                    }

                    // if more than 2ms apart, just snap
                    if ((prev_pos - client_rigidbody.position).sqrMagnitude >= 4.0f)
                    {
                        this.client_pos_error = Vector3.zero;
                        this.client_rot_error = Quaternion.identity;
                    }
                    else
                    {
                        this.client_pos_error = prev_pos - client_rigidbody.position;
                        this.client_rot_error = Quaternion.Inverse(client_rigidbody.rotation) * prev_rot;
                    }
                }
            }
        }

        this.client_timer = client_timer;
        this.client_tick_number = client_tick_number;

        if (this.client_correction_smoothing)
        {
            this.client_pos_error *= 0.9f;
            this.client_rot_error = Quaternion.Slerp(this.client_rot_error, Quaternion.identity, 0.1f);
        }
        else
        {
            this.client_pos_error = Vector3.zero;
            this.client_rot_error = Quaternion.identity;
        }

        this.smoothed_client_player.transform.position = client_rigidbody.position + this.client_pos_error;
        this.smoothed_client_player.transform.rotation = client_rigidbody.rotation * this.client_rot_error;
    }
	
	// Update is called once per frame
	void Update () {
        if (isServer) ServerUpdate();
        else ClientUpdate();
	}

    private void PrePhysicsStep(Rigidbody rigidbody, Inputs inputs)
    {
        if (this.local_player_camera_transform != null)
        {
            if (inputs.up)
            {
                rigidbody.AddForce(this.local_player_camera_transform.forward * this.player_movement_impulse, ForceMode.Impulse);
            }
            if (inputs.down)
            {
                rigidbody.AddForce(-this.local_player_camera_transform.forward * this.player_movement_impulse, ForceMode.Impulse);
            }
            if (inputs.left)
            {
                rigidbody.AddForce(-this.local_player_camera_transform.right * this.player_movement_impulse, ForceMode.Impulse);
            }
            if (inputs.right)
            {
                rigidbody.AddForce(this.local_player_camera_transform.right * this.player_movement_impulse, ForceMode.Impulse);
            }
            if (rigidbody.transform.position.y <= this.player_jump_y_threshold && inputs.jump)
            {
                rigidbody.AddForce(this.local_player_camera_transform.up * this.player_movement_impulse, ForceMode.Impulse);
            }
        }
    }

    private bool ClientHasStateMessage()
    {
        if (this.client_state_msgs.Peek() == null) return false;

       // Debug.Log(CurrentTime + " vs " + this.client_state_msgs.Peek().Element.delivery_time);

        return this.client_state_msgs.Count > 0 && CurrentTime >= this.client_state_msgs.Peek().Element.delivery_time;
    }

    private void ClientStoreCurrentStateAndStep(ref ClientState current_state, Rigidbody rigidbody, Inputs inputs, float dt)
    {
        current_state.position = rigidbody.position;
        current_state.rotation = rigidbody.rotation;

        this.PrePhysicsStep(rigidbody, inputs);
        Physics.Simulate(dt);
    }


}

[System.Serializable]
struct Inputs
{
    public bool up;
    public bool down;
    public bool left;
    public bool right;
    public bool jump;
}

class InputMessage : MessageBase
{
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
    public float delivery_time;
    public uint tick_number;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angular_velocity;
}

