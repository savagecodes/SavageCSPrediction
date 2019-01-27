using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class PhysicsNetworkUpdater : MonoBehaviour
{

    private static PhysicsNetworkUpdater _instance;

    //remove this when we have time
    public GameObject ServerHUDPreab;
    public GameObject ServerHudInstance;
    public GameObject StaticWorld;

    Dictionary<GameObject, Tuple<Scene, PhysicsScene>> _PhysicsScenes =
        new Dictionary<GameObject, Tuple<Scene, PhysicsScene>>();


    void Awake()
    {
        if (_instance != null) Destroy(_instance);
        _instance = this;
    }


    public void CreatePhysicsSceneForGO(GameObject GO)
    {
        CreateSceneParameters csp = new CreateSceneParameters(LocalPhysicsMode.Physics3D);
        Scene PhysicsScene =
            SceneManager.CreateScene("PSFor: " + GO.name + " NetID " + GO.GetComponent<PredicetdNetworkMovement>().netId.Value,
                csp);

        _PhysicsScenes.Add(GO, Tuple.Create(PhysicsScene, PhysicsScene.GetPhysicsScene()));

        SceneManager.MoveGameObjectToScene(GetStaticWorldNoRenderer(), PhysicsScene);
        SceneManager.MoveGameObjectToScene(GO, PhysicsScene);
    }

    public void DestroyPhysicsSceneOfGO(GameObject GO)
    {
        SceneManager.UnloadSceneAsync(_PhysicsScenes[GO].Item1);
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

    public void UpdatePhysics(PredicetdNetworkMovement NM)
    {
        _PhysicsScenes[NM.gameObject].Item2.Simulate(Time.fixedDeltaTime);
       if(NM.isServer) NM.OnPhysiscsUpdated();

    }


    public static PhysicsNetworkUpdater Instance
    {
        get { return _instance; }
    }
}
