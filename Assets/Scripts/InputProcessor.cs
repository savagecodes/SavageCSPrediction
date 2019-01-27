using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class InputProcessor : MonoBehaviour
{

    protected ServerPredictionSyncer serverPredictionSyncer;

    private Inputs _currentInputs;

    private void Awake()
    {
        serverPredictionSyncer = GetComponent<ServerPredictionSyncer>();
        serverPredictionSyncer.OnInputExecutionRequest += ExecuteInputs;
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

