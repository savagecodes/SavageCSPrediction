using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class SavageNetwork : NetworkManager
{
    private static SavageNetwork _instance;
    public static SavageNetwork Instance => _instance;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(_instance);
            _instance = this;
        }
    }

    // Set custom connection parameters early, so they are not too late to be enforced
    void Start()
    {
        customConfig = true;
        connectionConfig.PacketSize = 1470;
        connectionConfig.MaxCombinedReliableMessageCount = 40;
        connectionConfig.MaxCombinedReliableMessageSize = 800;
        connectionConfig.MaxSentMessageQueueSize = 2048;
        connectionConfig.IsAcksLong = true;
        globalConfig.ThreadAwakeTimeout = 1;
    }

    public override void OnStartServer()
    {
        Debug.Log("Server has started");
    }
    
    public override void OnStopServer()
    {
        Debug.Log("Server has stopped");
    }
   
}
