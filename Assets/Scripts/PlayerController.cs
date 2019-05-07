using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(PredicetdNetworkMovement))]
public class PlayerController : NetworkBehaviour {

    PredicetdNetworkMovement _movementComponent;
    [SyncVar]
    Color _playerColor;
    public GameObject correctionsHudPrefab;
    public CorrectiosHUD HUD;
    //public MeshRenderer meshRenderer;

    [Header("Input Mapping")]
    [SerializeField]
    KeyCode UP;
    [SerializeField]
    KeyCode DOWN;
    [SerializeField]
    KeyCode LEFT;
    [SerializeField]
    KeyCode RIGHT;
    [SerializeField]
    KeyCode JUMP;
    
    [SerializeField]
    KeyCode RUN;

	void Start () {

        _movementComponent = GetComponent<PredicetdNetworkMovement>();


        if (isLocalPlayer)
        {
            
            var chud = Instantiate(correctionsHudPrefab);
            HUD = chud.GetComponent<CorrectiosHUD>();
            HUD.SetMovementComponent(GetComponent<PredicetdNetworkMovement>());

        }

        if (isServer)
        {
            _playerColor = new Color(Random.Range(0f, 1f), Random.Range(0, 1f), Random.Range(0, 1f));

            if (PhysicsNetworkUpdater.Instance.ServerHudInstance == null)
            {
                PhysicsNetworkUpdater.Instance.ServerHudInstance = Instantiate(PhysicsNetworkUpdater.Instance.ServerHUDPreab);
            }
        }

    }

    public void SetColorPlayer(Color c)
    {
        _playerColor = c;
        if (isLocalPlayer) HUD.SetColor(c);
    }
	
	void Update ()
    {
        if (_movementComponent == null) return;

        if (!isLocalPlayer) return;

       Inputs CurrentInputs = new Inputs();

      CurrentInputs.XMoveInput = Input.GetAxis("Horizontal");
      CurrentInputs.YMoveinput = Input.GetAxis("Vertical");
      
      CurrentInputs.cameralookX = Input.GetAxis("Mouse X");
      CurrentInputs.cameralookY = Input.GetAxis("Mouse Y");

       CurrentInputs.run = Input.GetKey(RUN);

       CurrentInputs.jump = Input.GetKeyDown(JUMP);

       _movementComponent.InputProcessorComponent.SetInputs(CurrentInputs);
    }

    public override void OnNetworkDestroy()
    {
        base.OnNetworkDestroy();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if(!isServer) Destroy(HUD.gameObject);
    }
}
