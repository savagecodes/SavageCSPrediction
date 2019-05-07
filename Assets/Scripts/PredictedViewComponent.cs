using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(PredicetdNetworkMovement))]
public class PredictedViewComponent : NetworkBehaviour
{
   [Header("Required")] 
   [SerializeField] 
   private GameObject _localClientModelPrefab;
   [SerializeField] 
   private GameObject _nonLocalClientModelPrefab;
   [SerializeField] 
   private GameObject _serverModelPrefab; //for debugging
   
   
   
   private GameObject _localClientModelInstance;
   private GameObject _nonLocalClientInstance;
   private GameObject _serverModelInstance; //for debugging
   
   private PredicetdNetworkMovement _predictedMovementComponent;

   void Awake()
   {
      _predictedMovementComponent = GetComponent<PredicetdNetworkMovement>();
   }

   void Start()
   {
       if (isServer)
       {
           _serverModelInstance = Instantiate(_serverModelPrefab);
       }
       else if (isLocalPlayer)
       {
           _localClientModelInstance = Instantiate(_localClientModelPrefab);
           _predictedMovementComponent.OnSmoothedPositionReady += x =>
           {
               _localClientModelInstance.transform.position = x.position;
               _localClientModelInstance.transform.rotation= x.rotation;            
           };
       }
       else
       {
           _nonLocalClientInstance = Instantiate(_nonLocalClientModelPrefab);
           
           _predictedMovementComponent.OnSmoothedPositionReady += x =>
           {
            if(x.position != _nonLocalClientInstance.transform.position)
               _nonLocalClientInstance.transform.position = x.position;
            if(x.rotation != _nonLocalClientInstance.transform.rotation && x.rotation != new Quaternion(0, 0, 0, 0))
               _nonLocalClientInstance.transform.rotation= x.rotation;
               
           };
       }    
             
   }

   void Update()
   {
       if (isServer)
       {
           _serverModelInstance.transform.position = transform.position;
           _serverModelInstance.transform.rotation = transform.rotation;
       }
   }
   
}
