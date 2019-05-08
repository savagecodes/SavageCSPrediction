using System.Collections;
using System.Collections.Generic;
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
            var chud = Instantiate(_correctionsHudPrefab);
            _clientHud = chud.GetComponent<CorrectiosHUD>();
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
