using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class PhysicsNetworkUpdater : MonoBehaviour {

    private static PhysicsNetworkUpdater _instance;

    public Dictionary<uint, List<PhysicsUpdateRequest>> _requestByTickNumber = new Dictionary<uint, List<PhysicsUpdateRequest>>();

    public List<NetworkedMovement> _movementComponents = new List<NetworkedMovement>();

    private uint clientsReady = 1;

    public void OnReadyToSimulate()
    {
	    clientsReady++;

	    if (clientsReady >= NetworkServer.connections.Count)
	    {
		    SimulatePhysics();
		    
		    foreach (var mc in _movementComponents)
		    {
			 
			    mc.OnPhysiscsUpdated();
		    }
		    
		    clientsReady = 1;
	    }
    }

    private uint _mainServerTick;

    public void RegisterPhysicsUpdateRequest(PhysicsUpdateRequest request)
    {
	    if (!_requestByTickNumber.ContainsKey(request.targetServerTick))
	    {
		    var requestList = new List<PhysicsUpdateRequest>();
		    requestList.Add(request);
		    _requestByTickNumber.Add(request.targetServerTick,requestList);
		    return;
	    }
	    
	    _requestByTickNumber[request.targetServerTick].Add(request);
	    
    }
    
    public uint MainServerTick
    {
	    get { return _mainServerTick; }
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

	void Start()
	{
		//SimpleUpdateManager.instance.RegisterAfterPhysicsUpdater(UpdaterIDs.LEVEL_UPDATER,CustomUpdate);
	}

	public void SimulatePhysics()
	{
		Physics.Simulate(Time.fixedDeltaTime);
	}
	
	// Update is called once per frame
	void CustomUpdate ()
	{
		_mainServerTick++;

		while (_requestByTickNumber.Count > 0)
		{
			var result = _requestByTickNumber.OrderByDescending(x => x.Key);
			
			var requestList = result.First();
			_requestByTickNumber.Remove(requestList.Key);
			
			for (int i = 0; i < requestList.Value.Count; i++)
			{
				//requestList.Value[i].movementComponent.PrePhysicsStep(requestList.Value[i].movementComponent.Rigidbody, requestList.Value[i].input);
				Physics.Simulate(Time.fixedDeltaTime);
				//requestList.Value[i].movementComponent.OnPhysiscsUpdated(requestList.Value[i].currentTick);
			}
		}
	}

	private void OnDestroy()
	{
		//SimpleUpdateManager.instance.DeRegisterAfterPhysicsUpdater(UpdaterIDs.LEVEL_UPDATER,CustomUpdate);
	}
}

public struct PhysicsUpdateRequest
{
	public uint targetServerTick;
	public uint currentTick;
	public NetworkedMovement movementComponent;
	public Inputs input;

}
