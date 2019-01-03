﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CorrectiosHUD : MonoBehaviour {

    public Text correctionsHUD;
    public Image playerColorImage;
    NetworkedMovement _movementComponent;

    public void SetMovementComponent(NetworkedMovement mc)
    {
        _movementComponent = mc;
    }

    public void SetColor(Color c)
    {
        playerColorImage.color = c;
    }
	
	// Update is called once per frame
	void Update () {
        if (_movementComponent == null || !_movementComponent.isLocalPlayer) return;

        correctionsHUD.text = "Corrections Made : " + _movementComponent._correctionsMadeOnClient;
     
	}
}
