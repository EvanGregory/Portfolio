using System.Collections.Generic;
using UnityEngine;

public class EnemyBase : MonoBehaviour
{
	[Header("Parameters")]
	[SerializeField] float detectionRange;
	[SerializeField] Color critColor; // temp

	[Header("Internals")]
	[SerializeField] protected bool m_isAlert = false;
	Color defaultColor; // temp

	protected EnemyAttack attackComp;
	protected EnemyHealth healthComp;
	protected Rigidbody2D rigidBody;
	protected SpriteRenderer spriteRenderer;

	static GameObject m_player;

	public EnemyAttack AttackComp { get { return attackComp;} }
	public EnemyHealth HealthComp { get { return healthComp;} }

	protected static GameObject Player { 
		get { 
			if (m_player == null) 
				m_player = GameObject.FindGameObjectWithTag("Player"); 
			return m_player; 
		}
	}

	public bool IsAlert { 
		get { return m_isAlert; } 
		set { m_isAlert = value; attackComp.enabled = value; OnAlertStatusChange(); } 
	}

	public static List<EnemyBase> enemyList = new();

	void OnEnable()
	{
		enemyList.Add(this);
	}

	void OnDisable()
	{
		enemyList.Remove(this);
	}

	protected virtual void Start ()
	{
		rigidBody = GetComponent<Rigidbody2D>();

		spriteRenderer = GetComponent<SpriteRenderer>();
		defaultColor = spriteRenderer.color;

		healthComp = GetComponent<EnemyHealth>();

		attackComp = GetComponent<EnemyAttack>();
		attackComp.enabled = m_isAlert;

	}

	protected virtual void Update ()
	{
		if (!m_isAlert)
		{
			// Check for alerting to the player
			Vector3 toPlayer = Player.transform.position - transform.position;
			if (toPlayer.sqrMagnitude <= detectionRange * detectionRange)
			{
				IsAlert = true;
			}
		}

		if (rigidBody.bodyType == RigidbodyType2D.Dynamic && attackComp.IsAttacking && Mathf.Abs(transform.position.y - Player.transform.position.y) < 0.5f)
		{
			float xVel = rigidBody.linearVelocityX;
			GroundNavigation.BlockWalkingThroughPos(ref xVel, transform.position.x, Player.transform.position.x, 0.15f);
			rigidBody.linearVelocityX = xVel;
		}
	}

	public void RecieveCritAttack()
	{
		enabled = false;
		SendMessage("OnRecieveCritAttack");
	}

	public void SetIsCritTarget(bool value)
	{
		if (value)
		{
			spriteRenderer.color = critColor;
		}
		else
		{
			spriteRenderer.color = defaultColor;
		}
	}

	// Maybe override this for big enemies
	public virtual Vector2 GetCritTargetPos() 
	{
		return transform.position;
	}

	protected virtual void OnAlertStatusChange() { }

	private void OnDrawGizmosSelected ()
	{
		Gizmos.color = Color.blue;
		Gizmos.DrawWireSphere(transform.position, detectionRange);
	}
}
