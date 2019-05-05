using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

public class FPSDemoInputProcessor : InputProcessor
{
    private FirstPersonController _FPSController;
        
    private void Start()
    {
        _FPSController = GetComponent<FirstPersonController>();
  
    }

    public override void ExecuteInputs(Inputs input)
    {
        base.ExecuteInputs(input);
        
        PreStepPhysics(input);
    }

    public void PreStepPhysics(Inputs input)
    {
        _FPSController.SetRotationInput(new Vector2(input.cameralookX,input.cameralookY));
        _FPSController.IsRunning(input.run);
        if (input.jump)
        {
            _FPSController.Jump();
        }
        _FPSController.ProcessMovement(new Vector2(input.XMoveInput,input.YMoveinput));
          
    }
}
