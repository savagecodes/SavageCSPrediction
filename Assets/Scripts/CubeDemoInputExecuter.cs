using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeDemoInputExecuter : InputExecuter
{
    
    private Rigidbody _rb;
    [SerializeField] 
    private float _movementImpulse;
    [SerializeField] 
    private float _jumpThresholdY;

    [SerializeField] 
    private Transform _cameraTransform;
    
    
    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public override void ExecuteInputs(Inputs input)
    {
        base.ExecuteInputs(input);
        
        PreStepPhysics(input);
    }

    public void PreStepPhysics(Inputs input)
    {
        _rb.AddForce(_cameraTransform.forward * _movementImpulse * input.vertical, ForceMode.Impulse);
        _rb.AddForce(_cameraTransform.right * _movementImpulse * input.horizontal, ForceMode.Impulse);
        
     if (_rb.transform.position.y <= _jumpThresholdY && input.jump)
     {
         _rb.AddForce(_cameraTransform.up * _movementImpulse, ForceMode.Impulse);
     }
     
    }
}
