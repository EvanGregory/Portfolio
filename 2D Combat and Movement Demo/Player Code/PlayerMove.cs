using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
	public enum State { Running, Jumping, Falling, Rolling }

    [NonSerialized] public Rigidbody2D rigidBody;
	[NonSerialized] public BoxCollider2D boxCollider;
	PlayerHealth healthComp;
	PlayerCombat combatComp;

	InputAction moveAction;
	InputAction jumpAction;
	InputAction rollAction;

	// Visuals
	Animator animatorComp;

	[Header("Parameters")]
	public PhysicsMaterial2D playerPhysicsMat;
	public PhysicsMaterial2D ragdollPhysicsMat;
	[Header("Movement")]
	[SerializeField] float runSpeed;
	[SerializeField] float jumpSpeed;
	[SerializeField] float jumpHeight;
	[SerializeField] float gravityScale = 1.0f;
	[SerializeField] float terminalVelocity;
	[Header("Roll")]
	[SerializeField] float rollDistance;
	[SerializeField] float rollSpeed;
	[SerializeField] float rollImmunityTime;
	[SerializeField] float rollCooldownTime;
	[Header("Pogo")]
	[SerializeField] float enemyPogoSpeed;
	[SerializeField] float hazardPogoSpeed;
	[Header("Knockdown")]
	[SerializeField] float knockdownForce;
	[SerializeField] float knockdownRecoveryTime;
	[Range(0.0f, 90.0f)] [SerializeField] float knockdownAngle;

	[Header("Internals")]
	[SerializeField] bool m_isGrounded;
	[SerializeField] bool m_isKnockedDown = false;
	[SerializeField] State m_state = State.Running;
	[SerializeField] Vector3 m_lastGroundedPos;

	public bool detectMoveInputs = true;
	public bool freezePositionX = false;

	float groundLength = 0.0f; // amount of ground the player is actually standing on
	float mJumpTimer = 0.0f;
	float mRollTimer = 0.0f;

	public const float enemyCollideOffset = 0.15f;

	public bool IsGrounded { get { return m_isGrounded; } }
	public bool IsKnockedDown { get { return m_isKnockedDown; } }
	public bool HasRollImmunity { get { return m_state == State.Rolling && mRollTimer < rollImmunityTime; } }

	private void Awake ()
	{
		// Read input actions
		InputActionMap playerInputsMap = InputSystem.actions.FindActionMap("Player");
		moveAction = playerInputsMap.FindAction("Move");
		jumpAction = playerInputsMap.FindAction("Jump");
		rollAction = playerInputsMap.FindAction("Roll");
	}

	private void OnEnable ()
	{
		if (rigidBody == null)
			rigidBody = GetComponent<Rigidbody2D>();

		m_state = State.Running;
		detectMoveInputs = true;
		freezePositionX = false;

		// we apply our own gravity and dampening
		rigidBody.linearDamping = 0.0f;
		rigidBody.gravityScale = 0.0f;
		rigidBody.sharedMaterial = playerPhysicsMat;
	}

	void OnDisable ()
	{
		animatorComp.SetBool("IsGrounded", true);
		animatorComp.SetBool("IsMoving", false);
	}

	void Start()
    {
		boxCollider = GetComponent<BoxCollider2D>();
		healthComp = GetComponent<PlayerHealth>();
		combatComp = GetComponent<PlayerCombat>();
		animatorComp = GetComponent<Animator>();
    }

	void FixedUpdate()
    {
		UpdateIsGrounded();

        Vector2 vel = rigidBody.linearVelocity;
		
		// Apply gravity
		vel += (gravityScale * Time.fixedDeltaTime * Physics2D.gravity);
		if (vel.y < terminalVelocity)
		{
			vel.y = terminalVelocity;
		}

		// Apply movement
		float moveInput = moveAction.ReadValue<Vector2>().x;
		bool jumpInput = jumpAction.IsPressed();
		bool rollInput = rollAction.IsPressed();

		if (!detectMoveInputs)
		{
			moveInput = 0.0f;
			jumpInput = false;
		}

		// Doing our own deadzone here
		if (moveInput > 0.23f)
			moveInput = 1.0f;
		else if (moveInput < -0.23f)
			moveInput = -1.0f;
		else
			moveInput = 0.0f;

		// Jump should be --- 
		// 1. Linear upward movement
		// 2. Letting go of jump quickly lowers vertical velocity for precise stoppage
		// 3. Gravity with a quick terminal velocity for smooth but fast drops
		if (jumpInput && m_state != State.Falling && mJumpTimer < jumpHeight / jumpSpeed)
		{
			if (m_state != State.Jumping)
			{
				// Begin jump
				m_state = State.Jumping;
				animatorComp.SetTrigger("Jump");
				m_isGrounded = false;
			}

			// Continue jump
			vel.y = jumpSpeed;
		}
		else if (m_state == State.Jumping)
		{
			// Stop the jump
			m_state = State.Falling;
			vel.y = 0.2f;
		}

		if (detectMoveInputs)
		{
			vel.x = moveInput * runSpeed;
		}

		if (freezePositionX)
		{
			vel.x = 0.0f;
		}

		// Roll
		if (m_state == State.Rolling)
		{
			if (mRollTimer < rollDistance / rollSpeed)
			{
				// During roll
			}
			else
			{
				// End roll
				combatComp.enabled = true;
				detectMoveInputs = true;

				m_state = State.Falling;
				mRollTimer = rollCooldownTime;
			}
		}
		else if (rollInput && !combatComp.IsAttacking && mRollTimer <= 0.0f)
		{
			// Begin roll
			combatComp.enabled = false;
			detectMoveInputs = false;

			m_state = State.Rolling;
			mRollTimer = 0.0f;

			vel = transform.right * rollSpeed;
		}

		// Turning
		bool isMoving = detectMoveInputs && !Mathf.Approximately(vel.x, 0.0f);
		if (isMoving)
		{
			transform.localRotation = Quaternion.AngleAxis(vel.x < 0.0f ? 180.0f : 0.0f, Vector3.up);
		}

		// Dont let the player walk through enemies
		if (m_state == State.Running)
		{
			foreach(EnemyBase enemy in EnemyBase.enemyList)
			{
				if (!boxCollider.OverlapPoint(enemy.transform.position))
					continue;
				GroundNavigation.BlockWalkingThroughPos(ref vel.x, transform.position.x, enemy.transform.position.x, enemyCollideOffset);
			}
		}

		rigidBody.linearVelocity = vel;

		// Animation
		animatorComp.SetBool("IsMoving", isMoving);
		animatorComp.SetBool("IsJumping", m_state == State.Jumping);
		animatorComp.SetBool("IsGrounded", m_isGrounded);
    }

	void UpdateIsGrounded()
	{
		int numBottomCollisions = 0;
		float centerY = GetCenter().y;

		ContactPoint2D[] contacts = new ContactPoint2D[4];
		int numContacts = rigidBody.GetContacts(contacts);
		int firstGroundIndex = -1;
		for (int i = 0; i < numContacts; i++)
		{
			if (contacts[i].point.y < centerY)
			{
				if (firstGroundIndex == -1)
					firstGroundIndex = i;
				else
					groundLength = Mathf.Abs(contacts[firstGroundIndex].point.x - contacts[i].point.x);

				numBottomCollisions++;
			}
		}

		m_isGrounded = numBottomCollisions > 1 && m_state != State.Jumping;

		if (m_isGrounded && m_state == State.Running && groundLength > boxCollider.bounds.size.x * 0.9f)
		{
			m_lastGroundedPos = transform.position;
		}
	}

	private void Update ()
	{
		// Update State
		if (m_state == State.Running && !m_isGrounded) // Walking off ledges
		{
			m_state = State.Falling;
		}
		else if (m_state == State.Falling && m_isGrounded) // Grounded and not jumping
		{
			m_state = State.Running;
		}

		if (m_state != State.Jumping)
		{
			mJumpTimer = 0.0f;
		}
		else
		{
			mJumpTimer += Time.deltaTime;
		}

		if (m_state == State.Rolling)
		{
			mRollTimer += Time.deltaTime;
		}
		else if (mRollTimer > 0.0f)
		{
			mRollTimer -= Time.deltaTime;
		}
	}

	public void RecieveKnockDown(GameObject source)
	{
		if (source == null)
			return;

		// Apply knockback force
		float xDiff = transform.position.x - source.transform.position.x;
		float angleRad = knockdownAngle * Mathf.Deg2Rad;
		Vector2 forceDir = new (Mathf.Cos(angleRad), Mathf.Sin(angleRad));
		if (xDiff < 0.0f)
		{
			forceDir.x = -forceDir.x;
		}
		Vector2 knockbackSpeed = forceDir * knockdownForce;

		StartCoroutine(HandleKnockdown(knockbackSpeed));
	}

	private IEnumerator HandleKnockdown(Vector2 pushSpeed)
	{
		m_isKnockedDown = true;

		enabled = false;
		rigidBody.linearDamping = 0.0f;
		rigidBody.gravityScale = 1.0f;
		rigidBody.sharedMaterial = ragdollPhysicsMat;
		rigidBody.linearVelocity = pushSpeed;
		m_state = State.Falling;
		m_isGrounded = false;

		combatComp.enabled = false;

		// wait for the stun time to conclude or for the player to cancel the stun with jump or roll
		for (float timer = knockdownRecoveryTime; rigidBody.linearVelocity.sqrMagnitude > 0.05f && !(timer <= 0.0f && (rollAction.IsPressed() || jumpAction.IsPressed())); timer -= Time.fixedDeltaTime)
		{
			yield return new WaitForFixedUpdate();
		}

		combatComp.enabled = true;
		enabled = true;
		m_isKnockedDown = false;
	}

	public void OnCollidedWithHazard()
	{
		healthComp.TakeDamage(new() { healthDamage = 1, knockdown = false });

		StartCoroutine(HandleLocalRespawn());
	}

	private IEnumerator HandleLocalRespawn()
	{
		animatorComp.SetTrigger("Die");
		this.enabled = false;
		UIManager.Instance.isFadedOut = true;

		yield return new WaitForSeconds(UIManager.Instance.fadeTime);

		rigidBody.linearVelocity = Vector2.zero;
		transform.position = m_lastGroundedPos;
		UIManager.Instance.isFadedOut = false;

		yield return new WaitForSeconds(UIManager.Instance.fadeTime);

		this.enabled = true;
	}

	public void OnPogo(bool isEnemy)
	{
		rigidBody.linearVelocityY = isEnemy ? enemyPogoSpeed : hazardPogoSpeed;
	}

	public Vector2 GetCenter()
	{
		return new Vector2(transform.position.x, transform.position.y) + boxCollider.offset;
	}
}
