using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class StateProcessor : MonoBehaviour
{
    private ServerPredictionSyncer ServerSyncer;

    private ServerState _currentState;
    
    public ServerState CurrentServerState
    {
        set { _currentState = value; }
    }

    private void Awake()
    {
        ServerSyncer = GetComponent<ServerPredictionSyncer>();
    }

    public virtual ServerState GetServerState()
    {
        return _currentState;
    }
    
    
}
