using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputExecuter : MonoBehaviour
{

    protected NetworkedMovement networkedMovement;

    private void Awake()
    {
        networkedMovement = GetComponent<NetworkedMovement>();
        networkedMovement.OnInputExecutionRequest += ExecuteInputs;
    }

    public virtual void ExecuteInputs(Inputs input)
    {
        
    }

}


[System.Serializable]
public struct Inputs
{
    public float horizontal;
    public float vertical; 
    public bool jump;
}

