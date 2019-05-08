using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(PredictedNetworkMovement))]
public class PlayerController : NetworkBehaviour {

    PredictedNetworkMovement _movementComponent;
//TODO: Remove this from player Controller
//Make a hud Controller tha handles all this UI related
    public GameObject correctionsHudPrefab;
    public CorrectiosHUD HUD;

    [Header("Input Mapping")] 
    [SerializeField]
    private string _moveXAxis;
    [SerializeField]
    private string _moveYAxis;
    [SerializeField]
    private string _lookXAxis;
    [SerializeField]
    private string _lookYAxis;
    [SerializeField]
    KeyCode JUMP;
    [SerializeField]
    KeyCode RUN;

    private void Start () {

        _movementComponent = GetComponent<PredictedNetworkMovement>();

        if (isLocalPlayer)
        {
            var chud = Instantiate(correctionsHudPrefab);
            HUD = chud.GetComponent<CorrectiosHUD>();
            HUD.SetMovementComponent(GetComponent<PredictedNetworkMovement>());

        }

        if (isServer)
        {

            if (PhysicsNetworkUpdater.Instance.ServerHudInstance == null)
            {
                PhysicsNetworkUpdater.Instance.ServerHudInstance = Instantiate(PhysicsNetworkUpdater.Instance.ServerHUDPreab);
            }
        }

    }
	
    private void Update ()
    {
        if (_movementComponent == null) return;

        if (!isLocalPlayer) return;

        ProcessInputs();
    }

    private void ProcessInputs()
    {
        Inputs currentInputs = new Inputs
        {
            XMoveInput = Input.GetAxis(_moveXAxis),
            YMoveinput = Input.GetAxis(_moveYAxis),
            cameralookX = Input.GetAxis(_lookXAxis),
            cameralookY = Input.GetAxis(_lookYAxis),
            run = Input.GetKey(RUN),
            jump = Input.GetKeyDown(JUMP)
        };

        _movementComponent.InputProcessorComponent.SetInputs(currentInputs);
    }

    public override void OnNetworkDestroy()
    {
        base.OnNetworkDestroy();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if(!isServer) Destroy(HUD.gameObject);
    }
}
