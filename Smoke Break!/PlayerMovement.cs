using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;


//using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerMovement : MonoBehaviour
{
    //public
    public enum PlayerState { Move, Dash, Hiding, Dead, Paused, HoldingDash }
    public PlayerState CurrentState;
    public bool IsInvisible { get; private set; }
    public bool isMoving;

    public Vector3 MovementDirection { get { return _rigidbody.velocity.normalized; } }

    //serialized private
    [Header("Movement Parameters")]
    [SerializeField] float _maxWalkSpeed;
    [SerializeField] float _maxDashSpeed;
    [Tooltip("Amount of acceleration to apply to Applewood until max walk speed hit.")]
    [SerializeField] float _walkAccel;
    [Tooltip("After done accelerating max speed won't be hard limited")]
    [SerializeField] float _maxSpeedExtendTime;
    [SerializeField] float _turnConstant;

    [Header("Dash Parameters")]
    [Tooltip("The ratio the player speed is slowed by while charging a dash")]
    [SerializeField] float _dashSlowdown = 0.5f;

    [Tooltip("Amount of acceleration to apply to Applewood until max dash speed hit")]
    [SerializeField] float _dashAccel;
    [Tooltip("Health cost of dashing")]
    [SerializeField] int _dashCost;

    [Tooltip("Radius in which enemies are stunned by the dash")]
    [SerializeField] float _dashStunRadius = 3.0f;

    [Tooltip("Total time acceleration is applied")]
    [SerializeField] float _dashAccelTime;

    [Tooltip("How long the dash needs to be held to successfully trigger")]
    [SerializeField] float _dashHoldTime = 1.0f;

    [Tooltip("Time all movement is stopped before dash starts accelerating")]
    [SerializeField] float _dashStartupTime;

    [SerializeField] float _squishConst = .75f;

    [SerializeField] DashUI dashUI;

    [Header("Audio")]
    [SerializeField] SoundProfile movementSoundSP;
    [SerializeField] SoundProfile dashLightSP;
    [SerializeField] SoundProfile dashHeavySP;
    [SerializeField] SoundProfile dashChargeSP;
    [SerializeField] SoundProfile onDeathSP;

    [Header("Internals")]
    public GameObject stunObject;

    // Pointers
    PlayerSmokeController _smokeController;
    [SerializeField] SmokeContainer _headSmokeContainer;
    [NonSerialized] public Rigidbody _rigidbody;
    //[SerializeField] private Animator FlourHide;
    InputActionMap _playerInputMap;

    // Hidden Internals
    private Vector3 _targetLookDirection;
    private Vector3 _moveDirection;
    private float _angularVelocity = 0.0f;
    private AudioSource _standardMovementSound;

    [NonSerialized] public bool inVent = false;
    [NonSerialized] public bool inTransitVent = false;
    [NonSerialized] public VentNode currNode = null;

    private float dashTimer = 0.0f;

    private bool isInitialized = false;

    bool IsControllable { get { return CurrentState == PlayerState.Move || CurrentState == PlayerState.HoldingDash; } }

    private void Awake()
    {
        if (gameObject.IsDestroyed())
        {
            return;
        }
        // Get Components.
        _smokeController = GetComponent<PlayerSmokeController>();
        _rigidbody = GetComponent<Rigidbody>();

        _targetLookDirection = transform.forward;
        _rigidbody.maxLinearVelocity = _maxWalkSpeed;
        CurrentState = PlayerState.Move;
        IsInvisible = false;

        PlayerInput playerInput = GetComponent<PlayerInput>();
        playerInput.enabled = true;
        _playerInputMap = playerInput.actions.FindActionMap("Player");
    }

    void Start()
    {
        TryStartupMovement();
    }

    // we can call this manually instead of in Start when this will be disabled on start (bc of cutscenes) 
    // Carter added this bc he needed to hide the dash UI
    void TryStartupMovement()
    {
        if (isInitialized)
        {
            return;
        }
        _standardMovementSound = AudioManager.instance.Play(movementSoundSP, gameObject);
        _standardMovementSound.Pause();

        // Make the dash icon invisible to start with
        dashUI.Disappear();
        isInitialized = true;
    }

    // --- Public ---
    public void KillPlayer()
    {
        CurrentState = PlayerState.Dead;
        _headSmokeContainer.SetSmokeVisibility(false);
        AudioManager.instance.Play(onDeathSP, gameObject);
        if (UIManager.instance != null) // For camera demo scenes where UI manager does not exist
        {
            UIManager.instance.OnDeath();
        }
    }

    public void SaveState()
    {
        Player player = GetComponent<Player>();
        // Respawner sets the position, so the player doesnt
        //player.playerData.position = transform.position;
    }

    public void LoadState()
    {
        Player player = GetComponent<Player>();
        // Nothing to load right now
    }

    public void StartCutscene()
    {
        DisablePlayerMovement(false);
    }

    public void EndCutscene()
    {
        EnablePlayerMovement();
    }

    /*
     * Should be used instead of transform.position =
     * to set the position to avoid the interpolation
     * not respecting the newPosition
     */
    public void SetPosition(Vector3 newPosition, bool isTeleport = false, bool smoothCamera = true)
    {
        _rigidbody.interpolation = RigidbodyInterpolation.None;
        Vector3 oldPos = transform.position;
        transform.position = newPosition;
        IEnumerator SetInterpolateModeOnNextFrame()
        {
            yield return new WaitForFixedUpdate();
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }
        StartCoroutine(SetInterpolateModeOnNextFrame());

        if (isTeleport)
        {
            _smokeController.OnTeleportPlayer();
            if (!smoothCamera)
            {
                //CinemachineVirtualCamera cvc = LevelManager.instance.FindPersistant("Camera").GetComponentInChildren<CinemachineVirtualCamera>();
                //O
                CameraController cc = LevelManager.instance.GetPersistantComponent<CameraController>();
                cc.PrepForTeleport();
                CinemachineCore.Instance.OnTargetObjectWarped(transform, oldPos - newPosition);
            }
        }
    }

    // --- Update ---
    void Update()
    {

        bool isControllable = IsControllable;

        if (CurrentState == PlayerState.HoldingDash)
        {
            dashTimer += Time.deltaTime;
            if (dashTimer <= _dashHoldTime)
            {
                transform.localScale = new Vector3(transform.localScale.x + _squishConst * Time.deltaTime, transform.localScale.y, transform.localScale.z - _squishConst * Time.deltaTime);
            }

            if (dashUI && _smokeController.GetRemainingSize() >= _dashCost)
            {
                dashUI.SetFill(Mathf.Clamp01(dashTimer / _dashHoldTime));
            }

        }

        if (isControllable)
        {
            UpdateDirection();
        }

        if (_playerInputMap != null)
        {
            InputAction dashAction = _playerInputMap.FindAction("Dash");

            if (null != dashAction)
            {

                //if I dash and I can
                if (dashAction.WasPressedThisFrame() && isControllable && CurrentState != PlayerState.HoldingDash)
                {
                    if (_smokeController.GetRemainingSize() >= _dashCost)
                    {
                        dashUI.StartDash();
                    }
                    CurrentState = PlayerState.HoldingDash;
                    _rigidbody.maxLinearVelocity *= _dashSlowdown;
                    // Squish
                    //transform.localScale = new Vector3(transform.localScale.x + 0.4f*dashTimer, transform.localScale.y, transform.localScale.z - 0.4f*dashTimer);
                }
                else if (dashAction.WasReleasedThisFrame() && CurrentState == PlayerState.HoldingDash)
                {
                    Dash();
                }

                if (DebugGUI.instance && DebugGUI.instance.hideHUD)
                {
                    dashUI.enabled = false;
                }
                else
                {
                    dashUI.enabled = true;
                }


            }

            if (CurrentState == PlayerState.Hiding && inVent)
            {
                InputAction moveAction = _playerInputMap.FindAction("Move");
                if (null != moveAction && currNode)
                {
                    Vector2 moveDir = moveAction.ReadValue<Vector2>();
                    if (moveDir.x > 0)
                    {
                        currNode.TeleportPlayer(KeyCode.D);//don't change these values
                    }
                    else if (moveDir.x < 0)
                    {
                        currNode.TeleportPlayer(KeyCode.A);
                    }
                    else if (moveDir.y > 0)
                    {
                        currNode.TeleportPlayer(KeyCode.W);
                    }
                    else if (moveDir.y < 0)
                    {
                        currNode.TeleportPlayer(KeyCode.S);
                    }
                }
            }
        }
    }

    void FixedUpdate()
    {
        // Apply movement forces based on state
        switch (CurrentState)
        {
            case PlayerState.Move:
                {
                    float acceleration = _walkAccel;
                    if (DebugGUI.instance && DebugGUI.instance.speedy)
                    {
                        acceleration *= 2.0f;
                        _rigidbody.maxLinearVelocity = Mathf.Max(_maxWalkSpeed * 2.0f, _maxDashSpeed);
                    }
                    _rigidbody.AddForce(_moveDirection * acceleration, ForceMode.Acceleration);
                }
                break;
            case PlayerState.Dash:
                {
                    Vector3 direction = _moveDirection;
                    if (Mathf.Approximately(_moveDirection.sqrMagnitude, 0.0f))
                    {
                        direction = _targetLookDirection;
                    }

                    float acceleration = _dashAccel;
                    if (DebugGUI.instance && DebugGUI.instance.speedy)
                    {
                        acceleration *= 2.0f;
                    }
                    //magnitude *= Mathf.Floor(dashTimer / _dashHoldTime); //if we wanted holding the dash for longer to go farther, though this method is uncapped
                    _rigidbody.AddForce(direction * acceleration, ForceMode.Acceleration);
                }
                break;
            case PlayerState.HoldingDash:
                {
                    float acceleration = _walkAccel * _dashSlowdown;
                    if (DebugGUI.instance && DebugGUI.instance.speedy)
                    {
                        acceleration *= 2.0f;
                    }
                    _rigidbody.AddForce(_moveDirection * acceleration, ForceMode.Acceleration);
                }
                break;
        }

        if (IsControllable)
        {
            UpdateTurn();
        }

        if (CurrentState != PlayerState.Dash)
        {
            _smokeController.OnPlayerPushed(_rigidbody.GetAccumulatedForce());
        }
    }

    // Set up the parameters and then use the spring function.
    // Our current angle is the angle from world forward to character forward,
    // and the ideal angle is from world forward to target forward.
    // Only works assuming that all 3 vectors (both forwards and target) lie along the xz plane.
    private void UpdateTurn()
    {
        float currentAngle = Mathf.Acos(Vector3.Dot(Vector3.forward, Vector3.Normalize(transform.forward)));
        if (Vector3.Cross(Vector3.forward, transform.forward).y < 0)
        {
            currentAngle = -currentAngle;
        }

        float idealAngle = Mathf.Acos(Vector3.Dot(Vector3.forward, _targetLookDirection));
        if (Vector3.Cross(Vector3.forward, _targetLookDirection).y < 0)
        {
            idealAngle = -idealAngle;
        }

        float turnConstant = _turnConstant;
        if (CurrentState == PlayerState.HoldingDash)
        {
            turnConstant *= 2.0f;
        }

        float newAngle = FollowTarget.UpdateSpringRotation(ref _angularVelocity, currentAngle, idealAngle, turnConstant, Time.fixedDeltaTime);

        if (float.IsNaN(newAngle))
        {
			newAngle = 0.0f;
			_angularVelocity = 0.0f;
        }

		newAngle *= Mathf.Rad2Deg;
        _rigidbody.MoveRotation(Quaternion.AngleAxis(newAngle, Vector3.up));
    }

    //Sets vectors to later tell applewood where to move + look.
    private void UpdateDirection()
    {
        Transform cameraTransform = UnityEngine.Camera.main.transform;
        Vector3 forward = cameraTransform.forward + cameraTransform.up; // Use both up and forward to account for vertical camera perspective
        Vector3 right = UnityEngine.Camera.main.transform.right;

        // flatten forward onto the 2D plane of movement
        forward = new Vector3(forward.x, 0.0f, forward.z).normalized;
        right = new Vector3(right.x, 0.0f, right.z).normalized;

        Vector2 moveDir = new(0.0f, 0.0f);

        if (_playerInputMap != null)
        {
            InputAction moveAction = _playerInputMap.FindAction("Move");
            if (moveAction != null)
            {
                if (!Debug.isDebugBuild || (DebugGUI.instance && !DebugGUI.instance.freeCamMove))
                {
                    moveDir = moveAction.ReadValue<Vector2>();
                }
            }
        }

        _moveDirection = forward * moveDir.y + right * moveDir.x;

        isMoving = false;
        if (Mathf.Approximately(_moveDirection.sqrMagnitude, 0.0f))
        {
            _targetLookDirection = transform.forward;
        }
        else
        {
            isMoving = true;
            //_moveDirection = _moveDirection.normalized; // Try removing this line for controller?
            _targetLookDirection = _moveDirection.normalized;
        }

        if (_standardMovementSound)
        {
            if (!PauseScreen.GameIsPaused)
            {
                if (isMoving)
                {
                    if (!_standardMovementSound.isPlaying) _standardMovementSound.UnPause();
                }
                else
                {
                    if (_standardMovementSound.isPlaying) _standardMovementSound.Pause();
                }
            }
        }
    }

    // --- Private ---
    private void Dash()
    {
        if (_smokeController.GetRemainingSize() >= _dashCost && dashTimer >= _dashHoldTime)
        {
            dashUI.DashSuccess();
            StartCoroutine(DashPressed());
            stunObject.SetActive(true);
        }
        else
        {
            if (UIManager.instance && UIManager.instance.HUD != null && _smokeController.GetRemainingSize() < _dashCost)
                UIManager.instance.HUD.CreatePopupText("All out!");
            Unsquish();
        }
    }

    IEnumerator DashPressed()
    {
        IsInvisible = true;
        CurrentState = PlayerState.Dash;
        _rigidbody.maxLinearVelocity = 0.0f;
        _headSmokeContainer.SetSmokeVisibility(false, 0.35f);

        yield return new WaitForSeconds(_dashStartupTime);

        // Stun
        if (EnemyManager.instance)
        {
            EnemyManager.instance.StunEnemiesInRadius(transform.position, _dashStunRadius * _dashStunRadius);
        }

        // Unsquish
        transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

        _rigidbody.maxLinearVelocity = _maxDashSpeed;

        if (AudioManager.instance)
        {
            if (_smokeController.GetPercentSize() < 0.5f)
            {
                AudioManager.instance.Play(dashLightSP, gameObject);
            }
            else
            {
                AudioManager.instance.Play(dashHeavySP, gameObject);
            }
        }

        _smokeController.OnDashMove(_dashCost);

        yield return new WaitForSeconds(_dashAccelTime);
        if (UIManager.instance && UIManager.instance.HUD != null)
            UIManager.instance.HUD.CreatePopupText("-1!");
        if (CurrentState != PlayerState.Dash)
        {
            // Something went wrong, stop the dash
            yield break;
        }
        CurrentState = PlayerState.Move;

        yield return new WaitForSeconds(_maxSpeedExtendTime);
        _rigidbody.maxLinearVelocity = _maxWalkSpeed;

        IsInvisible = false;
        _smokeController.EndDash();
        dashTimer = 0.0f;
        stunObject.SetActive(false);
        _headSmokeContainer.SetSmokeVisibility(true, 0.35f);
    }

    public void DisablePlayerMovement(bool hideSmoke = true)
    {
        TryStartupMovement();
        enabled = false;
        if (CurrentState == PlayerState.HoldingDash)
        {
            // Reset all the stuff that dash changes
            Unsquish();
        }
        CurrentState = PlayerState.Hiding;
        _standardMovementSound.Pause();


        // Disable collisions + rigidbody
        _rigidbody.useGravity = false;
        _rigidbody.velocity = Vector3.zero;

        EnableColliders(false);

        if (hideSmoke)
        {
            _smokeController.SetSmokeVisibility(false);
        }
        isMoving = false;
    }

    public void EnablePlayerMovement()
    {
        enabled = true;
        CurrentState = PlayerState.Move;
        _smokeController.SetSmokeVisibility(true);

        // Enable collisions + rigidbody
        _rigidbody.useGravity = true;

        EnableColliders(true);
    }

    public void IgnoreInteract(Collider modelCollider)
    {
        int smokeLayer = LayerMask.NameToLayer("Smoke");
        // int playerLayer = LayerMask.NameToLayer("Player");
        int interactLayer = LayerMask.NameToLayer("Interactable");

        Physics.IgnoreLayerCollision(smokeLayer, interactLayer, true);
        // Physics.IgnoreLayerCollision(playerLayer, interactLayer, true);

        Physics.IgnoreCollision(GetComponent<Collider>(), modelCollider, true);
    }

    public void RememberInteract(Collider modelCollider)
    {
        int smokeLayer = LayerMask.NameToLayer("Smoke");
        int interactLayer = LayerMask.NameToLayer("Interactable");

        Physics.IgnoreLayerCollision(smokeLayer, interactLayer, false);
        // Physics.IgnoreLayerCollision(playerLayer, interactLayer, false);
        Physics.IgnoreCollision(GetComponent<Collider>(), modelCollider, false);
    }

    public void OnVentInteraction(bool isEntering, VentNode node, bool isFirstOrLast = false)
    {
        if (isEntering)
        {
            DisablePlayerMovement();

            inVent = true;
            if (CurrentState == PlayerMovement.PlayerState.HoldingDash) { Unsquish(); }
            CurrentState = PlayerMovement.PlayerState.Hiding;
            EnableColliders(false);
            _rigidbody.useGravity = false;
            _rigidbody.velocity = Vector3.zero;
            if (node is TransitionVent)
            {
                inTransitVent = true;
            }

            //IgnoreInteract(node.modelCollider);
            currNode = node;
            SetPosition(node.insideLoc.position, true);
        }
        else
        {
            EnablePlayerMovement();
            inVent = false;
            if (node is TransitionVent)
            {
                inTransitVent = false;
            }
            CurrentState = PlayerMovement.PlayerState.Move;
            EnableColliders(true);
            _rigidbody.useGravity = true;
            //RememberInteract(node.modelCollider);
            currNode = null;
            SetPosition(node.exitLoc.position, true);
        }
    }
    public void SetRenderVisible()
    {
        _headSmokeContainer.SetSmokeVisibility(true);
    }

    private void Unsquish()
    {
        transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        dashTimer = 0.0f;
        CurrentState = PlayerState.Move;
        _rigidbody.maxLinearVelocity = _maxWalkSpeed;
        dashUI.DashFailed();
    }

    private void EnableColliders(bool enabled)
    {
        Collider[] colliders = gameObject.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
        {
            c.enabled = enabled;
        }
    }
    public SmokeContainer GetSmokeContainer()
    {
        return _headSmokeContainer;
    }

}