using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
	[Serializable] public class Attack
	{
		public enum Direction { Up, Down, Forwards, Backwards}
		public string name = "";
		public float length = 1.0f;
		public float windupTime = 0.0f;
		public float cooldownTime = 0.0f;
		public int healthDamage = 1;
		public float postureDamage = 0.0f;
		public GameObject hitbox = null;
		public Direction direction = Direction.Forwards;
		public int priority = 0;
	}

	PlayerMove playerMove;
	PlayerHealth playerHealth;
	Rigidbody2D rb;
	[SerializeField] Animator weaponAnimator;
	// Maybe temp
	[SerializeField] Transform horizontalCritEnemyPos;
	[SerializeField] Transform verticalCritEnemyPos;

	[Header("Parameters")]
	[SerializeField] Transform parrySparksPos;
	[SerializeField] GameObject parrySparksPrefab;
	[SerializeField] List<Attack> attacks;
	[SerializeField] float attackKnockback;
	[Tooltip("Parry Length + Parry Stun Length is the total amount of time for which the parry takes place")]
	[SerializeField] float parryLength;
	[SerializeField] float parryStunLength;
	[Range(0.0f, 1.0f)]
	[Tooltip("0 means that parries may always interupt attacks, 1 is never")]
	[SerializeField] float attackToParryLeeway;
	[Range(0.0f, 1.0f)]
	[SerializeField] float attackToFollowUpLeeway;

	[Header("Internals")]
	[SerializeField] float m_attackTimer = 0.0f;
	[SerializeField] bool m_isParrying = false;
	[SerializeField] float m_parryTimer = 0.0f;
	[SerializeField] bool m_isBlocking = false;
	[SerializeField] List<int> m_attackQueue;
	EnemyBase m_critTarget = null;

	InputAction moveAction = null;
	InputAction attackAction = null;
	InputAction parryAction = null;

	public bool IsParrying { get { return m_isParrying; } }
	public bool IsBlocking { get { return m_isBlocking; } }
	public bool IsAttacking { get { return m_attackQueue.Count != 0; } }

	private void Awake ()
	{
		m_attackQueue = new();

		foreach(Attack a in attacks)
		{
			a.hitbox.SetActive(false);
		}

		// Read input actions
		InputActionMap playerInputsMap = InputSystem.actions.FindActionMap("Player");
		moveAction = playerInputsMap.FindAction("Move");
		attackAction = playerInputsMap.FindAction("Attack");
		parryAction = playerInputsMap.FindAction("Parry");
	}

	private void OnDisable ()
	{
		foreach(Attack a in attacks)
		{
			a.hitbox.SetActive(false);
		}
	}

	void Start()
	{
		rb = GetComponent<Rigidbody2D>();
		playerMove = GetComponent<PlayerMove>();
		playerHealth = GetComponent<PlayerHealth>();
    }

    void Update()
    {
		UpdateAttack();

		UpdateBlock();

		// Update crit target
		{
			if (m_critTarget != null && !m_critTarget.HealthComp.IsStaggered)
			{
				m_critTarget.SetIsCritTarget(false);
				m_critTarget = null;
			}

			float minDist = m_critTarget != null ? Vector2.Distance(m_critTarget.transform.position, playerMove.GetCenter()) : 1.5f;
			foreach (EnemyBase enemy in EnemyBase.enemyList)
			{
				if (!enemy.HealthComp.IsStaggered)
					continue;

				Vector2 toEnemy = (Vector2)enemy.transform.position - playerMove.GetCenter();
				float dist = toEnemy.magnitude;
				if (dist < minDist)
				{
					minDist = dist;
					m_critTarget = enemy;
				}
			}

			if (m_critTarget != null)
			{
				m_critTarget.SetIsCritTarget(true);
			}
		}

		if (parryAction.WasPressedThisFrame())
		{
			ParryAction();
		}

		if (attackAction.WasPressedThisFrame())
		{
			if (m_critTarget != null)
			{
				// Do crit attack
				m_critTarget.RecieveCritAttack();
				StartCoroutine(PerformCritAttack());
				return;
			}
			else
			{
				// Perform normal attack
				AttackAction();
			}
		}

		weaponAnimator.SetBool("IsBlocking", m_isBlocking);
    }

	void UpdateAttack()
	{
		// Process current attack
		Attack currAttack = GetCurrentAttack();
		if (currAttack != null)
		{
			if (m_attackTimer >= currAttack.length)
			{
				// End of attack
				EndCurrentAttack();
				currAttack = GetCurrentAttack();

				if (currAttack != null)
				{
					// Begining of follow up attack
					OnBeginAttack();
				}
			}
			else
			{
				// During attack
				bool isPastWindup = m_attackTimer > currAttack.windupTime;
				bool isBeforeCooldown = m_attackTimer < currAttack.length - currAttack.cooldownTime;
				currAttack.hitbox.SetActive(isPastWindup && isBeforeCooldown);
				
				m_attackTimer += Time.deltaTime;
			}
		}
	}

	void UpdateBlock()
	{
		if (!parryAction.IsPressed() && m_isBlocking)
		{
			m_isBlocking = false;
			playerMove.detectMoveInputs = true;
			playerMove.freezePositionX = false;
		}

		// Parry timer
		m_isParrying = m_parryTimer > parryStunLength;
		
		if (m_parryTimer > 0.0f)
		{
			// During parry
			m_parryTimer -= Time.deltaTime;
		}
	}

	void ParryAction()
	{
		Attack currAttack = GetCurrentAttack();
		if (currAttack == null || (m_attackTimer > currAttack.length * attackToParryLeeway))
		{
			// Begin parry
			while (m_attackQueue.Count != 0)
			{
				EndCurrentAttack();
			}

			m_isBlocking = true;

			playerMove.detectMoveInputs = false;
			playerMove.freezePositionX = true;

			m_parryTimer = parryLength + parryStunLength;
		}
	}

	void AttackAction()
	{
		Attack currAttack = GetCurrentAttack();
		if (m_attackQueue.Count < 2 && (currAttack == null || m_attackTimer > currAttack.length * attackToFollowUpLeeway))
		{
			List<Attack> validAttacks = new();
			foreach(Attack a in attacks)
			{
				if (IsAttackValid(a))
				{
					validAttacks.Add(a);
				}
			}

			if (validAttacks.Count != 0)
			{
				validAttacks.Sort((a,b) => b.priority.CompareTo(a.priority));

				int newAttackIndex = GetAttack(validAttacks[0].name);
				Debug.Assert(newAttackIndex != -1);
				m_attackQueue.Add(newAttackIndex);

				if (currAttack == null)
				{
					// We just added the first attack to the queue
					OnBeginAttack();
				}
			}
		}
	}

	bool IsAttackValid(Attack a)
	{
		// Ignore repeat attacks
		if (IsAttackQueued() && m_attackQueue[m_attackQueue.Count - 1] == GetAttack(a.name))
		{
			//return false;
		}

		Vector2 attackDir = moveAction.ReadValue<Vector2>();
		switch (a.name)
		{
			default:
				return false;
			case ("Down Swing"):
			{
				return attackDir.y < -0.5f && !playerMove.IsGrounded;
			}
			case ("Up Swing"):
			{
				return attackDir.y > 0.5f;
			}
			case ("Basic Swing"):
			{
				return attackDir.y < 0.5f && attackDir.y > -0.5f;
			}
			case ("Second Swing"):
			{
				return attackDir.y < 0.5f && attackDir.y > -0.5f && IsAttackQueued() && m_attackQueue[m_attackQueue.Count - 1] == GetAttack("Basic Swing");
			}
		}
	}

	void OnBeginAttack()
	{
		Attack currAttack = GetCurrentAttack();

		m_isBlocking = false;

		// Try to play the relevant attack animation
		string attackName = currAttack.name;
		attackName = attackName.Replace(" ", string.Empty);
		string stateName = "PlayerGun_" + attackName;
		
		if (weaponAnimator.HasState(0, Animator.StringToHash("Base Layer." + stateName)))
		{
			weaponAnimator.Play(stateName);
		}
	}

	// Called by attack hitbox
	public void OnAttackHit(Collider2D collision)
	{
		//Hit Something
		Attack currAttack = GetCurrentAttack();
		Debug.Assert(currAttack != null);

		switch (currAttack.direction)
		{
			case Attack.Direction.Forwards:
			{
				if (collision.CompareTag("Hazard") || collision.CompareTag("Enemy"))
				{
					rb.MovePosition(rb.position - attackKnockback * new Vector2(transform.right.x, transform.right.y));
				}
			} break;
			case Attack.Direction.Backwards:
			{
				if (collision.CompareTag("Hazard") || collision.CompareTag("Enemy"))
				{
					rb.MovePosition(rb.position + attackKnockback * new Vector2(transform.right.x, transform.right.y));
				}
			} break;
			case Attack.Direction.Down:
			{
				if (collision.CompareTag("Hazard"))
				{
					playerMove.OnPogo(false);
				}
				else if (collision.CompareTag("Enemy"))
				{
					playerMove.OnPogo(true);
				}
			} break;
			default:
			case Attack.Direction.Up:
				break;
		}

		if (collision.TryGetComponent<Health>(out Health enemyHealth))
		{
			enemyHealth.TakeDamage(new() { healthDamage = currAttack.healthDamage, postureDamage = currAttack.postureDamage, source = gameObject });
		}
	}

	void EndCurrentAttack()
	{
		Attack currAttack = GetCurrentAttack();

		if (currAttack != null)
		{
			m_attackTimer = 0.0f;
			currAttack.hitbox.SetActive(false);
			m_attackQueue.RemoveAt(0);
		}
	}

	IEnumerator PerformCritAttack()
	{
		enabled = false;

		playerMove.enabled = false;
		playerMove.rigidBody.linearVelocity = Vector2.zero;
		playerMove.boxCollider.enabled = false;

		playerHealth.IgnoreAllDamage = true;

		Vector2 currentTargetPos = m_critTarget.GetCritTargetPos();
		Vector2 offset = transform.position - horizontalCritEnemyPos.position; // this can be different per crit attack type

		Vector2 playerPos = currentTargetPos + offset;
		transform.position = playerPos;

		//weaponAnimator.Play("CritAttack", 0);

		yield return new WaitForSeconds(1.0f);

		m_critTarget.HealthComp.Kill();

		yield return new WaitForSeconds(0.5f);

		playerHealth.IgnoreAllDamage = false;

		playerMove.boxCollider.enabled = true;
		playerMove.enabled = true;

		enabled = true;
	}

	public void TriggerParry()
	{
		// Put parry effects here
		GameObject.Instantiate(parrySparksPrefab, parrySparksPos.position, Quaternion.identity);
	}

	// Helpers
	int GetAttack(string name)
	{
		for (int i = 0; i < attacks.Count; i++)
		{
			if (attacks[i].name == name)
			{
				return i;
			}
		}
		return -1;
	}

	bool IsAttackQueued()
	{
		return m_attackQueue.Count != 0;
	}

	public Attack GetCurrentAttack()
	{
		if (m_attackQueue.Count == 0)
			return null;
		return attacks[m_attackQueue[0]];
	}
}
