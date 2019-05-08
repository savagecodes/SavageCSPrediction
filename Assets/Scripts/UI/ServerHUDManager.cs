using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class ServerHUDManager : NetworkBehaviour
{
    [SerializeField]
    public GameObject _serverHUDPreab;
    private GameObject _serverHudInstance;

    void Start()
    {
        if (isServer)
        {

            if (_serverHudInstance == null)
            {
                _serverHudInstance = Instantiate(_serverHUDPreab);
            }
        }
    }
    
    
}
