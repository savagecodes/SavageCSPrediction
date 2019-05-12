using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class StateProcessor : MonoBehaviour
{
    private PredictedNetworkMovement ServerSyncer;

    private ServerState _currentState;
    
    private void Awake()
    {
        ServerSyncer = GetComponent<PredictedNetworkMovement>();
    }

    public virtual bool IsValidateState(ref ServerState receivedState, ref ServerState bufferedState)
    {
        return true;
    }

    public virtual ServerState GetCurrentState()
    {
        return _currentState;
    }

    public virtual void ExecuteState(ServerState state)
    {
        //nothing to do here
    }
      
}
