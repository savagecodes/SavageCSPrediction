using System.Collections;
using System.Collections.Generic;
using SavageCodes.Networking.ClientSidePrediction;
using UnityEngine;
using UnityEngine.Networking;
[RequireComponent(typeof(PredictedNetworkMovement))]
public class PlayerController : NetworkBehaviour {

    PredictedNetworkMovement _movementComponent;

    [Header("Input Mapping")] 
    [SerializeField]
    private string _moveXAxis;
    [SerializeField]
    private string _moveYAxis;
    [SerializeField]
    private string _lookXAxis;
    [SerializeField]
    private string _lookYAxis;
    [SerializeField]
    KeyCode JUMP;
    [SerializeField]
    KeyCode RUN;

    private void Start () 
    {
        _movementComponent = GetComponent<PredictedNetworkMovement>();
    }
	
    private void Update ()
    {
        if (_movementComponent == null || !isLocalPlayer) return;

        ProcessInputs();
    }

    private void ProcessInputs()
    {
        Inputs currentInputs = new Inputs
        {
            XMoveInput = Input.GetAxis(_moveXAxis),
            YMoveinput = Input.GetAxis(_moveYAxis),
            cameralookX = Input.GetAxis(_lookXAxis),
            cameralookY = Input.GetAxis(_lookYAxis),
            run = Input.GetKey(RUN),
            jump = Input.GetKeyDown(JUMP)
        };

        _movementComponent.InputProcessorComponent.SetInputs(currentInputs);
    }

}
