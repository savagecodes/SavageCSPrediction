using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSDemoStateProcessor : StateProcessor
{
    public override ServerState GetServerState()
    {
        var state = new ServerState();
        state.position = transform.position;
        state.rotation = transform.rotation;
        return state;

    }
}
