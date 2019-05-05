using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeInputProcessor : InputProcessor
{
    private SimpleCubeMovement _cubeMovement;
    private void Start()
    {
        _cubeMovement = GetComponent<SimpleCubeMovement>();
    }

    public override void ExecuteInputs(Inputs input)
    {
   
        base.ExecuteInputs(input);
    }

}
