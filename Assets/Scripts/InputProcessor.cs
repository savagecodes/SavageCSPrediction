using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class InputProcessor : MonoBehaviour
{

    protected PredicetdNetworkMovement predicetdNetworkMovement;

    private Inputs _currentInputs;

    public virtual void Awake()
    {
        predicetdNetworkMovement = GetComponent<PredicetdNetworkMovement>();
        predicetdNetworkMovement.OnInputExecutionRequest += ExecuteInputs;
    }

    public virtual void ExecuteInputs(Inputs input)
    {
        
    }

    public void SetInputs(Inputs inputs)
    {
        _currentInputs = inputs;
    }
    
    public Inputs GetCurrentInputs()
    {
        return _currentInputs;
    }

}

