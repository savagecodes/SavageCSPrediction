using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;

public class SavageNetwork : NetworkManager
{
    private static SavageNetwork _instance;
    public static SavageNetwork Instance => _instance;
    
    [Header("Required Components")]
    [SerializeField]
    private SavageNetworkDiscovery _discovery;

    private bool _showSearchMatchGUI = true;

    private void Awake()
    {
        networkPort = 7777;
        networkAddress = GetLocalIPAddress();
        
        NetworkTransport.Init();
        
        if (_instance != null)
        {
            Destroy(_instance.gameObject);
        }
        
        _instance = this;
    }
    
    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
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
        if (_discovery == null)
        {
            throw new Exception("Discover System Missing");
        }
        _discovery.isServer = true;
        _discovery.Initialize();
        _discovery.StartAsServer();
        _showSearchMatchGUI = false;
  
        Debug.Log("Server has started");
    }
    
    public override void OnStopServer()
    {
        _discovery.StopBroadcast();
        Debug.Log("Server has stopped");
    }

    public override void OnStartClient(NetworkClient client)
    {
        base.OnStartClient(client);
        Debug.Log("Client has started");
        _discovery.ShowDebugGui = false;
        _discovery.StopBroadcast();
    }

    public void StartSearchAvailableLocalMatch()
    {
        if (_discovery == null)
        {
            throw new Exception("Discover System Missing");
        }
           
        _showSearchMatchGUI = false;      
        _discovery.Initialize();
        _discovery.StartAsClient();
        _discovery.ShowDebugGui = true;
    }

    private void OnGUI()
    {
        if(!_showSearchMatchGUI) 
            return;
        
        if (GUI.Button(new Rect(10, 350, 200f, 20f), "Search Match in LAN"))
        {
            StartSearchAvailableLocalMatch();
        }
    }

}
