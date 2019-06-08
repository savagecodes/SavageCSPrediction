using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SavageCodes.Networking.ClientSidePrediction
{
    public class ClientPredictionTypes
    {

    }

    [System.Serializable]
    public struct ServerState
    {
        public Vector3 position;
        public Quaternion rotation;
       /* public Vector3 velocity;
        public Vector3 angularVelocity;
        public float angularDrag;
        public float drag;*/
    }

    [System.Serializable]

    public struct Inputs
    {
        public bool jump;
        public bool run;
        public float XMoveInput;
        public float YMoveinput;

        public float cameralookX;
        public float cameralookY;
    }
}
