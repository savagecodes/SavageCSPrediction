using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class PhysicsNetworkUpdater : MonoBehaviour {

    private static PhysicsNetworkUpdater _instance;

    //remove this when we have time
    public GameObject ServerHUDPreab;
    public GameObject ServerHudInstance;
    public GameObject StaticWorld;
    Dictionary<GameObject, PhysicsScene> _PhysicsScenes = new Dictionary<GameObject, PhysicsScene>();
    

    public List<NetworkedMovement> _movementComponents = new List<NetworkedMovement>();

    private uint clientsReady = 0;

    public void CreatePhysicsSceneForGO(GameObject GO)
    {
        CreateSceneParameters csp = new CreateSceneParameters(LocalPhysicsMode.Physics3D);
        Scene PhysicsScene = SceneManager.CreateScene("PSFor: "+ GO.name + " NetID " + GO.GetComponent<NetworkedMovement>().netId.Value , csp);

        _PhysicsScenes.Add(GO, PhysicsScene.GetPhysicsScene());

        SceneManager.MoveGameObjectToScene(GetStaticWorldNoRenderer(), PhysicsScene);
        SceneManager.MoveGameObjectToScene(GO, PhysicsScene);
    }

    GameObject GetStaticWorldNoRenderer()
    {
        var world = Instantiate(StaticWorld);
        var renderes = world.GetComponentsInChildren<MeshRenderer>();
        var meshes = world.GetComponentsInChildren<MeshFilter>();

        for (int i = 0; i < renderes.Length; i++)
        {
            Destroy(renderes[i]);
            Destroy(meshes[i]);
        }

        return world;
    }

    public void UpdatePhysics(NetworkedMovement NM)
    {
        //if (_PhysicsScenes[NM.gameObject].IsValid())
        //{
            _PhysicsScenes[NM.gameObject].Simulate(Time.fixedDeltaTime);
            NM.OnPhysiscsUpdated();          
        //}
    }

    public void OnReadyToSimulate()
    {
	    clientsReady++;

        if (clientsReady >= NetworkManager.singleton.numPlayers)
	    {
		    SimulatePhysics();
		    
		    foreach (var mc in _movementComponents)
		    {
			 
			    //mc.OnPhysiscsUpdated();
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
