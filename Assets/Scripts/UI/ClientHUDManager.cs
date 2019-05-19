﻿using System.Collections;
using System.Collections.Generic;
using SavageCodes.Networking.ClientSidePrediction;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class ClientHUDManager : NetworkBehaviour
{
    [SerializeField]
    private GameObject _correctionsHudPrefab;
    private CorrectiosHUD _clientHud;
    
    private void Start()
    {
        if (isLocalPlayer)
        {
            var cHud = Instantiate(_correctionsHudPrefab);
            _clientHud = cHud.GetComponent<CorrectiosHUD>();
            _clientHud.SetMovementComponent(GetComponent<PredictedNetworkMovement>());

        }
    }
    
    public override void OnNetworkDestroy()
    {
        base.OnNetworkDestroy();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!isServer && _clientHud != null)
        {
            Destroy(_clientHud.gameObject);
        }
    }
}
