using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ServerHUD : MonoBehaviour {

    public Text hudText;

	
	// Update is called once per frame
	void Update ()
    {
        if (hudText == null) return;
        hudText.text = "<b> SERVER </b> --> " + NetworkManager.singleton.numPlayers + " CLients Connected";
	}
}
