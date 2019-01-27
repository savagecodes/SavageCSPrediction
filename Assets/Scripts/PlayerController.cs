﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(NetworkedMovement))]
public class PlayerController : NetworkBehaviour {

    NetworkedMovement _movementComponent;
    [SyncVar]
    Color _playerColor;
    public GameObject correctionsHudPrefab;
    public CorrectiosHUD HUD;
    public MeshRenderer meshRenderer;

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
        meshRenderer = GetComponent<MeshRenderer>();


        if (isLocalPlayer)
        {
            
            var chud = Instantiate(correctionsHudPrefab);
            HUD = chud.GetComponent<CorrectiosHUD>();
            HUD.SetMovementComponent(GetComponent<NetworkedMovement>());

        }

        if (isServer)
        {
            _playerColor = new Color(Random.Range(0f, 1f), Random.Range(0, 1f), Random.Range(0, 1f));

            meshRenderer.material.color = _playerColor;

            if (PhysicsNetworkUpdater.Instance.ServerHudInstance == null)
            {
                PhysicsNetworkUpdater.Instance.ServerHudInstance = Instantiate(PhysicsNetworkUpdater.Instance.ServerHUDPreab);
            }
        }

    }

    public void SetColorPlayer(Color c)
    {
        _playerColor = c;
        if (isLocalPlayer) HUD.SetColor(c);
        _movementComponent.SmoothedPlayerModel.GetComponent<MeshRenderer>().material.color = _playerColor;
    }
	
	void Update ()
    {
        if (_movementComponent == null) return;

        if (_movementComponent.SmoothedPlayerModel.GetComponent<MeshRenderer>().material.color != _playerColor) SetColorPlayer(_playerColor);

        if (!isLocalPlayer) return;

        if(HUD.playerColorImage.color != _playerColor) SetColorPlayer(_playerColor);

       Inputs CurrentInputs = new Inputs();

       CurrentInputs.up = Input.GetKey(UP);
       CurrentInputs.down = Input.GetKey(DOWN);
       CurrentInputs.left = Input.GetKey(LEFT);
       CurrentInputs.right = Input.GetKey(RIGHT);
       CurrentInputs.jump = Input.GetKey(JUMP);

       _movementComponent.CurrentInputState = CurrentInputs;
    }

    public override void OnNetworkDestroy()
    {
        base.OnNetworkDestroy();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if(!isServer) Destroy(HUD.gameObject);
    }
}
