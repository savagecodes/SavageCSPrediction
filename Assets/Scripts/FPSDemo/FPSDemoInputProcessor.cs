using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

public class FPSDemoInputProcessor : InputProcessor
{
    private RigidbodyFirstPersonController _FPSController;
        
    private void Start()
    {
        _FPSController = GetComponent<RigidbodyFirstPersonController>();
  
    }

    public override void ExecuteInputs(Inputs input)
    {
        base.ExecuteInputs(input);
        
        PreStepPhysics(input);
    }

    public void PreStepPhysics(Inputs input)
    {
        _FPSController.Rotate(new Vector2(input.cameralookX,input.cameralookY));
        _FPSController.IsRunning(input.run);
        if (input.jump)
        {
            _FPSController.Jump();
        }
        _FPSController.Move(new Vector2(input.XMoveInput,input.YMoveinput));
          
    }
}
