using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class SavageNetworkDiscovery : NetworkDiscovery
{
    private bool _showDebugGUI;

    public bool ShowDebugGui
    {
      get { return _showDebugGUI; }
      set { _showDebugGUI = value; }
    }

    private Dictionary<string, string> _matchFounded = new Dictionary<string, string>();
    
    public override void OnReceivedBroadcast(string fromAddress, string data)
    {
        Debug.Log("Received broadcast from: " + fromAddress+ " with the data: " + data);
        
        if (!_matchFounded.ContainsKey(fromAddress))
        {
            _matchFounded.Add(fromAddress,data); 
        }
    }

    private void OnGUI()
    {
      if (!_showDebugGUI)
        return;
      //TODO: Clean this
      //This  is stolen from the decompiled version
      //of the parent class
      //as a temporary resource
      
      int num1 = 10 + offsetX;
      int num2 = 40 + offsetY;

      int num3 = num2 + 24;

      int num4 = num3 + 24;
   
      foreach (var match in _matchFounded)
      {
    
        if (GUI.Button(new Rect((float) num1, (float) (num4 + 20), 200f, 20f), "Game at " + match.Key))
        {
          string[] strArray = match.Value.Split(':');
          if (strArray.Length == 3 && strArray[0] == "NetworkManager" && ((UnityEngine.Object) SavageNetwork.Instance != (UnityEngine.Object) null && SavageNetwork.Instance.client == null))
          {
            SavageNetwork.Instance.networkAddress = strArray[1];
            SavageNetwork.Instance.networkPort = Convert.ToInt32(strArray[2]);
            SavageNetwork.Instance.StartClient();
          }
        }
        
        num4 += 24;
      }
    }
      
}
