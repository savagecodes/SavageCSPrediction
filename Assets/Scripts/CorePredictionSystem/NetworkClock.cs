using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkClock : NetworkBehaviour
{
    private int _latency;
    private int _roundTripTime;
    private int _timeDelta;
    
    private short _timeReceivedFromClientID = 2002;
    private short _timeReceivedFromServerID = 2003;

    #region Getters

    public int Latency => _latency;

    public int RoundTripTime => _roundTripTime;

    public int TimeDelta => _timeDelta;
    
    public int CurrentTimeInInt => (int) ((isServer ? DateTime.UtcNow : GetSyncedTime()) - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds;

    #endregion

    void Start()
    {
        _timeReceivedFromClientID += System.Convert.ToInt16(netId.Value);
        _timeReceivedFromServerID += System.Convert.ToInt16(netId.Value);
        
        if (isLocalPlayer)
        {
            StartCoroutine(SendTimeStamp());
        }

        if (isServer)
        {
            NetworkServer.RegisterHandler(_timeReceivedFromClientID, OnTimeReceivedFromClient);
        }
        else
        {
            NetworkManager.singleton.client.RegisterHandler(_timeReceivedFromServerID, OnTimeReceivedFromServer);
        }
        
    }

    public DateTime GetSyncedTime()
    {
        DateTime dateNow = DateTime.UtcNow;
       return dateNow.AddMilliseconds(_timeDelta).ToLocalTime();
    }


    void OnTimeReceivedFromClient(NetworkMessage netMsg)
    {
        var timeMessage =  netMsg.ReadMessage<TimeMessage>();
        
        timeMessage.serverTimeStamp = (long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds;
        
        NetworkServer.SendToClientOfPlayer(this.gameObject, _timeReceivedFromServerID, timeMessage);
    }

    void OnTimeReceivedFromServer(NetworkMessage netMsg)
    {
        var timeMessage =  netMsg.ReadMessage<TimeMessage>();
        
        CalculateTimeDelta(timeMessage);
        //SyncClock(timeMessage);
    }
    
    IEnumerator SendTimeStamp()
    {
        while (true)
        {
            connectionToServer.Send(_timeReceivedFromClientID, CreateTimePacket());
            yield return new WaitForSeconds(5f);
        }   
    }

    TimeMessage CreateTimePacket()
    {
        var timePacket = new TimeMessage
        {
            clientTimeStamp = (long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds
        };
        
        return timePacket;
    }

    void CalculateTimeDelta(TimeMessage timeMessage)
    {
        _roundTripTime = (int)((long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds - timeMessage.clientTimeStamp);
        _latency = _roundTripTime / 2; 
        int serverDelta = (int)(timeMessage.serverTimeStamp - (long)(DateTime.UtcNow - new DateTime (1970, 1, 1, 0, 0, 0)).TotalMilliseconds);
        _timeDelta = serverDelta + _latency;
    }

    void OnGUI(){

        if (isServer)
        {
            GUI.Label (new Rect(10, 250, 400, 30), "Server Time:"+ System.DateTime.Now.TimeOfDay);
            return;
        }
        if(!isLocalPlayer) 
            return;
        GUI.Label (new Rect(10, 250, 400, 30), "Server Time:"+ GetSyncedTime().TimeOfDay);
        GUI.Label (new Rect(10, 270, 400, 30), "Latency:"+ _latency.ToString()+"ms");
        GUI.Label (new Rect(10, 290, 400, 30), "Time Delta:"+ _timeDelta.ToString()+"ms");
    }
}

public class TimeMessage : MessageBase
{
    public long clientTimeStamp;
    public long serverTimeStamp;
}
