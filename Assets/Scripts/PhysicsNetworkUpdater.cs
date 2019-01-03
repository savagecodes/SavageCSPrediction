using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class PhysicsNetworkUpdater : MonoBehaviour {

    private static PhysicsNetworkUpdater _instance;

    //remove this when we have time
    public GameObject ServerHUDPreab;
    public GameObject ServerHudInstance;

    public List<NetworkedMovement> _movementComponents = new List<NetworkedMovement>();

    private uint clientsReady = 0;

    public void OnReadyToSimulate()
    {
	    clientsReady++;

        if (clientsReady >= NetworkManager.singleton.numPlayers)
	    {
		    SimulatePhysics();
		    
		    foreach (var mc in _movementComponents)
		    {
			 
			    mc.OnPhysiscsUpdated();
		    }
		    
		    clientsReady = 0;
	    }
    }


    public static PhysicsNetworkUpdater Instance
    {
	    get { return _instance; }
    }


    // Use this for initialization
	void Awake ()
	{
		if (_instance != null) Destroy(_instance);
		_instance = this;
	}

	public void SimulatePhysics()
	{
		Physics.Simulate(Time.fixedDeltaTime);
	}
	
}
