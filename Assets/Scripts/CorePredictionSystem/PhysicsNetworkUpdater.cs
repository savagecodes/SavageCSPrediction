using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

//TODO: this class need a huge clean up and maybe optimizations
public class PhysicsNetworkUpdater : MonoBehaviour
{
    private static PhysicsNetworkUpdater _instance;
    Dictionary<GameObject, Tuple<Scene, PhysicsScene>> _PhysicsScenes =
        new Dictionary<GameObject, Tuple<Scene, PhysicsScene>>();

    [FormerlySerializedAs("StaticWorld")] [SerializeField]
    public GameObject _staticWorld;
    public static PhysicsNetworkUpdater Instance => _instance;


    void Awake()
    {
        if (_instance != null) Destroy(_instance);
        _instance = this;
    }


    public void CreatePhysicsSceneForGO(GameObject GO)
    {
        CreateSceneParameters csp = new CreateSceneParameters(LocalPhysicsMode.Physics3D);
        Scene PhysicsScene =
            SceneManager.CreateScene("PSFor: " + GO.name + " NetID " + GO.GetComponent<PredictedNetworkMovement>().netId.Value,
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
        var world = Instantiate(_staticWorld);
        var renderers = world.GetComponentsInChildren<MeshRenderer>();
        var meshes = world.GetComponentsInChildren<MeshFilter>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Destroy(renderers[i]);
            Destroy(meshes[i]);
        }

        return world;
    }

    public void UpdatePhysics(PredictedNetworkMovement NM)
    {
        _PhysicsScenes[NM.gameObject].Item2.Simulate(Time.fixedDeltaTime);
    }

}
