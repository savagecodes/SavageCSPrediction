using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class InputProcessor : MonoBehaviour
{

    protected PredicetdNetworkMovement predicetdNetworkMovement;

    public event Action OnInputExecuted = () => { };

    private Inputs _currentInputs;

    private void Awake()
    {
        predicetdNetworkMovement = GetComponent<PredicetdNetworkMovement>();
    }

    public virtual void ExecuteInputs(Inputs input)
    {
        OnInputExecuted();
        if (predicetdNetworkMovement.isServer)
        {
            predicetdNetworkMovement.OnServerStateUpdated();
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

