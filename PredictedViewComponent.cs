using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace SavageCodes.Networking.ClientSidePrediction
{
    [RequireComponent(typeof(PredictedNetworkMovement))]
    public class PredictedViewComponent : NetworkBehaviour
    {
        [Header("Required")] 
        [SerializeField] private GameObject _nonLocalClientModelPrefab;
        [SerializeField] private Vector3 _offset; 
        [SerializeField] private bool _updateNonLocalPlayerControllerPosition = true; 

        private GameObject _nonLocalClientInstance;
        private PredictedNetworkMovement _predictedMovementComponent;

        public GameObject NonLocalClientInstance => _nonLocalClientInstance;

        void Awake()
        {
            _predictedMovementComponent = GetComponent<PredictedNetworkMovement>();
        }

        void Start()
        {
            if(!isLocalPlayer && !isServer)
            {
                _nonLocalClientInstance = Instantiate(_nonLocalClientModelPrefab);
                
                _predictedMovementComponent.OnSmoothedPositionReady += x =>
                {
                    if (_updateNonLocalPlayerControllerPosition)
                    {
                        transform.position = x.position;
                        transform.rotation = x.rotation;
                    }

                    if (x.position != _nonLocalClientInstance.transform.position)
                    {
                        _nonLocalClientInstance.transform.position = x.position + _offset;
                        
                    }

                    if (x.rotation != _nonLocalClientInstance.transform.rotation &&
                        x.rotation != new Quaternion(0, 0, 0, 0))
                    {
                        _nonLocalClientInstance.transform.rotation = x.rotation;
                    }

                };
            }

        }

        private void OnDestroy()
        { 
            Destroy(_nonLocalClientInstance);
        }
    }
}
