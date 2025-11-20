using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
	public enum State { Wait, Walk, Turn, Jump, Shoot, Dash, None };

	[SerializeField] PhysicsMaterial2D playerPhysicsMat;

	public LayerMask GroundMask;
	private BoxCollider2D _collider;
	public SplineBeltManager splineBelt;
	Rigidbody2D rb;
	Animator anim;
	[SerializeField] public List<State> actionQueue;
	State currentState = State.None;

	[Header("Parameters")]
	[SerializeField] float moveSpeed;
	[SerializeField] float moveAccel;
	[SerializeField] [Range(30.0f, 90.0f)] float jumpAngle;
	[SerializeField] float jumpSpeed;
	[SerializeField] float dashSpeed;

	private float _walkStateSoundTimer = 0f;
	
	[Header("Internals")]
	public bool isInAir;

	public bool prevFrameIsInAir = false;
	static InputAction mouse;
	[NonSerialized]
	public bool initialized = false; // if has been initialized by the editor manager

	[Header("Audio")]
	private AudioSource _audioSource;
	[SerializeField] private float _walkSoundInterval = 0.5f;
	[SerializeField] private AudioClip _landSound;
	[SerializeField] private AudioClip _jumpSound;
	[SerializeField] private AudioClip _walkSound;
	[SerializeField] private AudioClip _shootSound;
	[SerializeField] private AudioClip _turnSound;
	
	
	// not scuffed in the slightest
	private void Awake()
	{
		{
			PhysicsMaterial2D matInstance = new PhysicsMaterial2D();
			matInstance.friction = playerPhysicsMat.friction;
			matInstance.frictionCombine = playerPhysicsMat.frictionCombine;
			matInstance.bounciness = playerPhysicsMat.bounciness;
			matInstance.bounceCombine = playerPhysicsMat.bounceCombine;
			playerPhysicsMat = matInstance;
			
			_audioSource = GetComponent<AudioSource>();
		}

		_collider = GetComponent<BoxCollider2D>();
		if (LevelManager.Instance)
			LevelManager.Instance.playerMove = this;
	}

	// not scuffed in the slightest
	void Start()
    {
	    if (LevelManager.Instance)
		    LevelManager.Instance.playerMove = this;
	    mouse = GetComponent<PlayerInput>().actions.FindAction("Mouse");
		rb = GetComponent<Rigidbody2D>();
		anim = GetComponent<Animator>();
    }

	public void OnInteract()
	{
		// Unity's documentation is wrong so I have to do this check
		if (!this.enabled || !initialized)
			return;
		
		if (actionQueue.Count > 0)
		{
			BeginNextState();

			actionQueue.Add(actionQueue[0]);
			actionQueue.RemoveAt(0);

			splineBelt.CycleBeltItems();
		}
	}

	public void BeginNextState()
	{
		currentState = actionQueue[0];
		switch (currentState)
		{
			case State.Wait:
			{
				anim.SetTrigger("Idle");
			} break;
			case State.Turn:
			{
				transform.Rotate(Vector3.up, 180.0f);
				anim.SetTrigger("Idle");
				_audioSource.PlayOneShot(_turnSound);
			} break;
			case State.Walk:
			{
				anim.SetTrigger("Walk");
			} break;
			case State.Jump:
			{
				if (!isInAir)
				{
					anim.SetTrigger("Jump");
					float angle = Mathf.Deg2Rad * jumpAngle;
					Vector2 jumpVel = new Vector2(transform.right.x * Mathf.Cos(angle), transform.up.y * Mathf.Sin(angle));
					jumpVel *= jumpSpeed;
					rb.linearVelocity = jumpVel;
					_audioSource.PlayOneShot(_jumpSound);
				}
			} break;	
			case State.Shoot:
			{
				anim.SetTrigger("Idle");
				PlayerGun gunComp = GetComponent<PlayerGun>();
				gunComp.Shoot();
				_audioSource.PlayOneShot(_shootSound);
			} break;
			case State.Dash:
			{
				Vector3 toMouse = GetWorldMousePos() - transform.position;
				rb.linearVelocity = toMouse.normalized * dashSpeed;
				anim.SetTrigger("Idle");
			} break;
		}
	}

	void FixedUpdate ()
	{
		if (!initialized) return;
		
		if (actionQueue.Count > 0)
		{
			switch (currentState)
			{
				case State.Wait:
				{
					
				} break;
				case State.Turn:
				{

				} break;
				case State.Walk:
				{
					float deltaSpeed = transform.right.x * moveAccel * Time.fixedDeltaTime;
					rb.linearVelocityX = Mathf.Clamp(rb.linearVelocityX + deltaSpeed, -moveSpeed, moveSpeed);
					_walkStateSoundTimer += Time.fixedDeltaTime;
					if (_walkStateSoundTimer >= _walkSoundInterval)
					{
						_walkStateSoundTimer -= _walkSoundInterval;
						_audioSource.PlayOneShot(_walkSound);
					}
				} break;
				case State.Jump:
				{

				} break;
				case State.Shoot:
				{
					
				} break;
				case State.Dash:
				{

				} break;
			}
		}
		prevFrameIsInAir = isInAir;
		RaycastHit2D info = Physics2D.BoxCast(transform.TransformPoint(_collider.offset + new Vector2(0f, -_collider.size.y / 2f + 0.2f)), new Vector2(_collider.size.x - 0.1f, 0.1f), 0, -transform.up, 0.3f, GroundMask);
		isInAir = !info;

		if (!isInAir && prevFrameIsInAir)
		{
			_audioSource.PlayOneShot(_landSound);
		}
		
		playerPhysicsMat.friction = isInAir ? 0.0f : 0.8f;
		_collider.enabled = false;
		_collider.enabled = true;
	}

	public static Vector3 GetWorldMousePos()
	{
		Vector2 screenMousePos = mouse.ReadValue<Vector2>();
		Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(new Vector3(screenMousePos.x, screenMousePos.y, 0.0f));
		worldMousePos.z = 0.0f;
		return worldMousePos;
	}
}
