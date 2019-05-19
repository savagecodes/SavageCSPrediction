using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace SavageCodes.Networking.ClientSidePrediction
{
    public class PredictedRigidBodyHelper : NetworkBehaviour
    {
        private Rigidbody _rigidbody;
        private PredictedNetworkMovement _predictedMovement;

        void Start()
        {
            _predictedMovement = GetComponent<PredictedNetworkMovement>();
            _rigidbody = GetComponent<Rigidbody>();

            if (!isServer && !isLocalPlayer)
            {
                _rigidbody.isKinematic = true;
                return;
            }

            PhysicsNetworkUpdater.Instance.CreatePhysicsSceneForGO(this.gameObject);

            _predictedMovement.InputProcessorComponent.OnInputExecuted += OnInputProcessedAndExecuted;
        }

        public void OnInputProcessedAndExecuted()
        {
            PhysicsNetworkUpdater.Instance.UpdatePhysics(_predictedMovement);
        }

        void OnDestroy()
        {
            PhysicsNetworkUpdater.Instance.DestroyPhysicsSceneOfGO(this.gameObject);
        }
    }
}
