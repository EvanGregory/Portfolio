using System;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerMove : MonoBehaviour
{
    Interactable currentInteract = null;
    Interactable bestInteractTarget = null;

    public enum MoveState { Grounded, Swimming, Airborne }

    CharacterController controller;
    WaveData waveData;
    Camera playerCamera;
    FollowParent followParent;

    [NonSerialized] public InputActionAsset playerInputActions;
    InputAction jumpAction;
    InputAction lookAction;
    InputAction moveAction;
    InputAction interactAction;

    [Header("Movement")]
    public float forwardMoveSpeed;
    public float backwardMoveSpeed;
    public float acceleration;
    public float jumpSpeed;
    public float gravity;
    public float horizontalDrag;
    public float maxFallSpeed = 100.0f;

    [Header("Look")]
    [Range(0, 10)]
    public float horizontalLookSpeed;
    [Range(0, 10)]
    public float verticalLookSpeed;

    [Header("Interaction")]
    public float interactionDist;
    [Tooltip("Degrees")]
    public float maxInteractAngle;

    [Header("Internals")]
    [SerializeField] Vector3 speed = Vector3.zero;
    [SerializeField] MoveState moveState;

    Quaternion rotation;

    public bool IsGrounded { get { return controller.isGrounded; } }
    public MoveState CurrentMoveState { get { return moveState; } }

    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();

        waveData = GameObject.FindWithTag("Ocean").GetComponent<WaveData>();
        followParent = GetComponent<FollowParent>();
        playerCamera = Camera.main;
        playerCamera.depthTextureMode = DepthTextureMode.Depth;

        // Input actions
        playerInputActions = GetComponent<PlayerInput>().actions;
        jumpAction = playerInputActions.FindAction("Jump");
        moveAction = playerInputActions.FindAction("Move");
        lookAction = playerInputActions.FindAction("Look");
        interactAction = playerInputActions.FindAction("Interact");

        rotation = transform.localRotation;

        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        UpdateLook();
        if (currentInteract)
        {
			Vector3 targetPos = currentInteract.GetPlayerStartPos(this);
			if (Vector3.SqrMagnitude(targetPos - transform.position) < 0.0005f)
			{
				transform.position = targetPos;
				currentInteract.OnRecieveInput(this);
				foreach (Interactable currInter in Interactable.interactables)
				{
				    currInter.SetDisplay(false);
				}
			}
			else
			{
				transform.position = Utility.UpdateSpringVector(ref speed, transform.position, targetPos, 10.0f, Time.deltaTime);

				Quaternion idealRot = currentInteract.GetPlayerStartRotation(this);

				// Cant use the nice framerate independent way, since the math doesn't work with quaternions
				// and I don't want to bother storing the necessary data
				SetRotation(Quaternion.Slerp(transform.rotation, idealRot, 0.275f));
			}
        }
        else
        {
            UpdateMovement();

            // Display interact text
            List<Interactable> closeInteractables = FindCloseInteractables();
			bestInteractTarget = FindBestInteractable(closeInteractables);
			foreach (Interactable currInter in Interactable.interactables)
            {
                bool doDisplay = closeInteractables.Contains(currInter);
                bool isTarget = currInter == bestInteractTarget;
                
                currInter.SetDisplay(doDisplay);
                currInter.SetIsInteractTarget(isTarget);

                if (doDisplay)
                {
                    currInter.SetTextPos(playerCamera.transform.position);
                }
            }
        }
    }

    void UpdateMovement()
    {
        Physics.SyncTransforms();

        // Calculate the current state
        float waterHeight = waveData.CalcWaterHeight(transform.position);

        // Horizontal movement
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        Vector3 moveDir = new(moveInput.x, 0.0f, moveInput.y);
		bool isMoveInput = moveDir.sqrMagnitude > 0.003f;
        moveDir = moveDir.normalized;
        moveDir = transform.TransformDirection(moveDir);

		if (isMoveInput)
		{
			Vector3 addedVel = moveDir * (acceleration * Time.deltaTime);
			Vector3 finalVel = speed + addedVel;
			if (moveState == MoveState.Airborne)
			{
				Vector2 flatSpeed = new(speed.x, speed.z);
				float a = addedVel.sqrMagnitude;
				float b = Vector3.Dot(speed, addedVel);
				float c = flatSpeed.sqrMagnitude - forwardMoveSpeed * forwardMoveSpeed;
				if (Utility.Quadratic(out float answer1, out float answer2, a, b, c))
				{
					// When the change in player speed would make them go over the speed cap, only move them up to the cap
					if (answer1 <= 1.0f && answer1 >= 0.0f)
					{
						speed = Vector3.Lerp(speed, finalVel, answer1);
					}
					else if (answer2 <= 1.0f && answer2 >= 0.0f)
					{
						speed = Vector3.Lerp(speed, finalVel, answer2);
					}
					// if it does not cross the speed cap threshold
					else 
					{
						if (flatSpeed.sqrMagnitude > forwardMoveSpeed * forwardMoveSpeed)
						{
							// When over the cap
							// Use the smaller of the old and new speed
							if (flatSpeed.sqrMagnitude > finalVel.sqrMagnitude)
							{
								speed = finalVel;
							}
						}
						else
						{
							// under the cap, always use the new speed
							speed = finalVel;
						}
					}
				}
				// if it does not cross the speed cap threshold
				else 
				{
					if (flatSpeed.sqrMagnitude > forwardMoveSpeed * forwardMoveSpeed)
					{
						// When over the cap
						// Use the smaller of the old and new speed
						if (flatSpeed.sqrMagnitude > finalVel.sqrMagnitude)
						{
							speed = finalVel;
						}
					}
					else
					{
						// under the cap, always use the new speed
						speed = finalVel;
					}
				}
			}
			else
			{
				speed = finalVel;
			}
		}


        // Vertical movement
        speed.y = Mathf.Max(speed.y - gravity * Time.deltaTime, -maxFallSpeed);

        if (moveState != MoveState.Airborne)
        {
            if (jumpAction.IsPressed())
            {
                speed.y = jumpSpeed;
            }
			else
			{
				// need to set this negative to the character controller will know the player is grounded
				speed.y = -1.0f;
			}
        }

        // Speed Drag and Cap
        {
            Vector3 horizontalSpeed = new(speed.x, 0.0f, speed.z);
            Vector3 horizontalSpeedDir = horizontalSpeed.normalized;

			float mag = horizontalSpeed.magnitude;

			// Apply by move state
			switch (moveState)
			{
				case MoveState.Grounded:
				case MoveState.Swimming:
				{
					// Apply drag
					
					horizontalSpeed += (mag * horizontalDrag * -horizontalSpeedDir) * Time.deltaTime;
					
					// Apply cap
					float forwardScalar = Vector3.Dot(transform.forward, horizontalSpeedDir);
					forwardScalar += 1.0f;
					forwardScalar /= 2.0f;
					float maxSpeed = Mathf.Lerp(backwardMoveSpeed, forwardMoveSpeed, forwardScalar);
					float speedScalar = Mathf.Min(horizontalSpeed.magnitude, maxSpeed);
					horizontalSpeed = horizontalSpeedDir * speedScalar;
				} break;
				case MoveState.Airborne:
				{
					// Apply drag
					horizontalSpeed += (mag * horizontalDrag / 2.0f * -horizontalSpeedDir) * Time.deltaTime;
				} break;
			}

            speed.x = horizontalSpeed.x;
            speed.z = horizontalSpeed.z;
        }

        controller.Move(speed * Time.deltaTime);

        if (moveState == MoveState.Swimming && !(followParent && followParent.IsFollowing()))
        {
            Vector3 newPos = transform.position;
            newPos.y = waterHeight;
            transform.position = newPos;
        }

		// Update state for next frame
		switch (moveState)
        {
            case MoveState.Grounded:
			{
                if (!IsGrounded || jumpAction.IsPressed())
                {
                    moveState = MoveState.Airborne;
					
					if (speed.y < 0.0f)
					{
						speed.y = 0.0f;
					}
                }
                else if (transform.position.y < waterHeight)
                {
                    moveState = MoveState.Swimming;
                }
			} break;
            case MoveState.Swimming:
			{
				if (IsGrounded)
				{
				    moveState = MoveState.Grounded;
				}
                if (jumpAction.IsPressed())
                {
                    moveState = MoveState.Airborne;
                }
			} break;
            case MoveState.Airborne:
			{
				if (IsGrounded)
				{
				    moveState = MoveState.Grounded;
				}
                else if (transform.position.y < waterHeight)
                {
                    moveState = MoveState.Swimming;
                }
			} break;
        }
    }

    void UpdateLook()
    {
        float prevYRot = transform.rotation.eulerAngles.y;
        Vector2 lookVec = lookAction.ReadValue<Vector2>();
        float horizontalAngle = rotation.eulerAngles.y;
        horizontalAngle += lookVec.x * horizontalLookSpeed * Time.deltaTime;

        if (transform.parent != null)
        {
            horizontalAngle += prevYRot - rotation.eulerAngles.y;
        }

        float deltaLookAngle = lookVec.y * -verticalLookSpeed * Time.deltaTime;
        float verticalAngle = playerCamera.transform.localRotation.eulerAngles.x + deltaLookAngle;

        SetRotation(horizontalAngle, verticalAngle);
    }

    // Called automatically by PlayerInput
    public void OnInteract()
    {
        if (currentInteract == null)
        {
            if (bestInteractTarget && bestInteractTarget.OnBeginInteract(this))
            {
                currentInteract = bestInteractTarget;

                if (followParent && currentInteract.useFollowTransform)
                {
                    followParent.OnEnter(currentInteract.GetParentingTransform());
                }

                ResetPhysics();

                // Disable all interact text
                foreach(Interactable currInter in Interactable.interactables)
                {
                    currInter.SetDisplay(false);
                }
            }
        }
        else
        {
            currentInteract.OnEndInteract(this);
            
            if (followParent && currentInteract.useFollowTransform)
            {
                followParent.OnExit(currentInteract.GetParentingTransform());
            }

            currentInteract = null;
        }
    }

	// Called by FollowParent
	public void OnBeginFollow(Transform transform)
	{
		Debug.Log("Begin Follow");
		ShipModel shipModel = transform.GetComponentInParent<ShipModel>();
		if (shipModel)
		{
			speed -= shipModel.Ship.RB.velocity;
		}
	}

	public void OnEndFollow(Transform transform)
	{
		Debug.Log("End Follow");
		ShipModel shipModel = transform.GetComponentInParent<ShipModel>();
		if (shipModel)
		{
			speed += shipModel.Ship.RB.velocity;
		}
	}

    // Only uses the x and y eulers of the rotation, since the player can't roll
    public void SetRotation(float horizontalRotation, float verticalRotation)
    {
        SetYaw(horizontalRotation);

        SetPitch(verticalRotation);
    }

	public void SetPitch(float verticalRotation)
	{
		float vertAngle = verticalRotation;
        if (vertAngle > 85.0f && vertAngle < 180.0f)
        {
            vertAngle = 85.0f;
        }
        if (vertAngle < 275.0f && vertAngle >= 180.0f)
        {
            vertAngle = 275.0f;
        }
        playerCamera.transform.localRotation = Quaternion.AngleAxis(vertAngle, Vector3.right);
	}

	public void SetYaw(float horizontalRotation)
	{
		rotation = Quaternion.AngleAxis(horizontalRotation, Vector3.up);
        transform.rotation = rotation;
	}

	public Quaternion GetRotation()
	{
		return transform.rotation;
	}

    // Only uses the x and y eulers of the rotation, since the player can't roll
    public void SetRotation(Quaternion newRotation)
    {
        SetRotation(newRotation.eulerAngles.y, newRotation.eulerAngles.x);
    }

    Interactable FindBestInteractable()
    {
        return FindBestInteractable(FindCloseInteractables());
    }

    // Finds the interactable in the list that the player is closest to looking at.
    // Can return null
    Interactable FindBestInteractable(List<Interactable> interactablesToSearch)
    {
        Interactable bestInter = null;
        float bestDot = Mathf.Cos(maxInteractAngle);
        foreach(Interactable currInter in interactablesToSearch)
        {
            Vector3 interPos = currInter.GetPos(playerCamera.transform.position);
            Vector3 playerToInter = interPos - playerCamera.transform.position;
            float dot = Vector3.Dot(playerToInter.normalized, playerCamera.transform.forward);
            if (dot > bestDot)
            {
                bestInter = currInter;
                bestDot = dot;
            }
        }
        return bestInter;
    }

    List<Interactable> FindCloseInteractables()
    {
        List<Interactable> foundInteractables = new();
        foreach(Interactable currInter in Interactable.interactables)
        {
            if (Vector3.Distance(currInter.GetPos(playerCamera.transform.position), playerCamera.transform.position) < interactionDist)
            {
                foundInteractables.Add(currInter);
            }
        }
        return foundInteractables;
    }

    void ResetPhysics()
    {
        speed = Vector3.zero;
        moveState = MoveState.Grounded;
    }
}
