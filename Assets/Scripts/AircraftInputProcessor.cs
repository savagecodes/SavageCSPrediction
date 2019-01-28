using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Vehicles.Aeroplane;

public class AircraftInputProcessor : InputProcessor
{
    // reference to the aeroplane that we're controlling
    private AeroplaneController m_Aeroplane;
    private Rigidbody _rb;
    public override void Awake() 
    {
        base.Awake();
        m_Aeroplane = GetComponent<AeroplaneController>();
        _rb = GetComponent<Rigidbody>();
    }

    public override void ExecuteInputs(Inputs input)
    {
        //_rb.drag = input.drag;
        //_rb.angularDrag = input.angularDrag;
        
        m_Aeroplane.Move(input.horizontal, input.vertical, 0, input.throttle, input.jump);
    }
}
