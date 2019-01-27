using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClientPredictionTypes
{
 
}

[System.Serializable]
public struct ServerState
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
    public float angularDrag;
    public float drag;
}

[System.Serializable]
public struct ClientState
{
    public Vector3 position;
    public Quaternion rotation;
}

[System.Serializable]
public struct Inputs
{
    public float horizontal;
    public float vertical; 
    public bool jump;
}
