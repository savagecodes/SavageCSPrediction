using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityStandardAssets.Vehicles.Aeroplane;

public class AircraftPlayerController : NetworkBehaviour
{
    PredicetdNetworkMovement _movementComponent;
    [SyncVar]
    Color _playerColor;
    public GameObject correctionsHudPrefab;
    public CorrectiosHUD HUD;
    public MeshRenderer meshRenderer;
    
    // reference to the aeroplane that we're controlling
    private AeroplaneController m_Aeroplane;

    private Rigidbody _rb;
    
    private void Awake()
    {
        // Set up the reference to the aeroplane controller.
        m_Aeroplane = GetComponent<AeroplaneController>();
        _rb = GetComponent<Rigidbody>();
    }



	void Start () {

        _movementComponent = GetComponent<PredicetdNetworkMovement>();
        meshRenderer = GetComponentInChildren<MeshRenderer>();


        if (isLocalPlayer)
        {
            
            var chud = Instantiate(correctionsHudPrefab);
            HUD = chud.GetComponent<CorrectiosHUD>();
            HUD.SetMovementComponent(GetComponent<PredicetdNetworkMovement>());

        }

        if (isServer)
        {
            _playerColor = new Color(Random.Range(0f, 1f), Random.Range(0, 1f), Random.Range(0, 1f));

            meshRenderer.material.color = _playerColor;

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
        _movementComponent.SmoothedPlayerModel.GetComponentInChildren<MeshRenderer>().material.color = _playerColor;
    }
	
	void Update ()
    {
        if (_movementComponent == null) return;

        if (_movementComponent.SmoothedPlayerModel.GetComponentInChildren<MeshRenderer>().material.color != _playerColor) SetColorPlayer(_playerColor);

        if (!isLocalPlayer) return;

        if(HUD.playerColorImage.color != _playerColor) SetColorPlayer(_playerColor);

        Inputs CurrentInputs = new Inputs();

      CurrentInputs.horizontal = Input.GetAxis("Horizontal");
      CurrentInputs.vertical = Input.GetAxis("Vertical");
      CurrentInputs.jump = Input.GetButton("Fire1");
      CurrentInputs.throttle = Input.GetButton("Fire1") ? -1 : 1;
      CurrentInputs.drag = _rb.drag;
      CurrentInputs.angularDrag = _rb.angularDrag;

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
