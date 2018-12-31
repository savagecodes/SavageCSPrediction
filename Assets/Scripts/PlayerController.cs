using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(NetworkedMovement))]
public class PlayerController : NetworkBehaviour {

    NetworkedMovement _movementComponent;

    [Header("Input Mapping")]
    [SerializeField]
    KeyCode UP;
    [SerializeField]
    KeyCode DOWN;
    [SerializeField]
    KeyCode LEFT;
    [SerializeField]
    KeyCode RIGHT;
    [SerializeField]
    KeyCode JUMP;

	void Start () {

        _movementComponent = GetComponent<NetworkedMovement>();
	}
	
	void Update ()
    {
        if (!isLocalPlayer) return;

        _movementComponent.IsPressingUp = Input.GetKey(UP);
        _movementComponent.IsPressingDown = Input.GetKey(DOWN);
        _movementComponent.IsPressingLeft = Input.GetKey(LEFT);
        _movementComponent.IsPressingRight = Input.GetKey(RIGHT);
        _movementComponent.IsPressingJump = Input.GetKey(JUMP);
	}
}
