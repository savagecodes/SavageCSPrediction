using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeDemoStateProcessor : StateProcessor
{
    private Rigidbody _rb;

    private void Awake()
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
}
