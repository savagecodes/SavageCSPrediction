using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class InputProcessor : MonoBehaviour
{

    protected PredictedNetworkMovement predictedNetworkMovement;

    public event Action OnInputExecuted = () => { };

    private Inputs _currentInputs;

    private void Awake()
    {
        predictedNetworkMovement = GetComponent<PredictedNetworkMovement>();
    }

    public virtual void ExecuteInputs(Inputs input)
    {
        OnInputExecuted();
        if (predictedNetworkMovement.isServer)
        {
            predictedNetworkMovement.OnServerStateUpdated();
        }
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

