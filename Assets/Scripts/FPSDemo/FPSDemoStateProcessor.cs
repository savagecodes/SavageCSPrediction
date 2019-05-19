using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SavageCodes.Networking.ClientSidePrediction;
public class FPSDemoStateProcessor : StateProcessor
{
    private Rigidbody _rb;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public override ServerState GetCurrentState()
    {
        var state = new ServerState();
        state.position = _rb.position;
        state.rotation = _rb.rotation;
        state.velocity = _rb.velocity;
        state.angularVelocity = _rb.angularVelocity;
        state.drag = _rb.drag;
        state.angularDrag = _rb.angularDrag;
        return state;

    }

    public override void ExecuteState(ServerState state)
    {
        base.ExecuteState(state);
        
        _rb.position = state.position;
        _rb.rotation = state.rotation;
        _rb.velocity = state.velocity;
        _rb.angularVelocity = state.angularVelocity;
        _rb.drag = state.drag;
        _rb.angularDrag = state.angularDrag;
    }
}
