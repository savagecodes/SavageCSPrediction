using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class StateProcessor : MonoBehaviour
{
    private PredictedNetworkMovement ServerSyncer;

    private ServerState _currentState;
    
    public ServerState CurrentServerState
    {
        set { _currentState = value; }
    }

    private void Awake()
    {
        ServerSyncer = GetComponent<PredictedNetworkMovement>();
      //  ServerSyncer.OnServerStateExecutionRequest += ExecuteState;
    }

    public virtual ServerState GetCurrentState()
    {
        return _currentState;
    }

   /* public virtual ServerState GetCurrentState()
    {
        return default(ServerState);
    }*/

    public virtual void ExecuteState(ServerState state)
    {
        
    }
    
    
    
}
