using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
	[Serializable] public class Attack
	{
		public enum ResponseType { Standard, Heavy, Aerial } // feel free to change the logic behind these
		public enum TriggerType { Default, Situational, Never }
		public string name = "";
		public float windupTime = 0.0f;
		public float activeDamageTime = 1.0f;
		public float postAttackTime = 0.0f;
		public int healthDamage = 1;
		public float postureDamage = 0.0f;
		public GameObject hitbox = null;
		public Collider2D attackBounds;
		public AnimateRigidbody.PositionCurves movementCurves = null;
		public Vector2 idealPlayerOffset = Vector2.zero;
		public ResponseType responseType = ResponseType.Standard;
		public TriggerType triggerType = TriggerType.Default;
		[NonSerialized] public int m_index;

		public bool IsBlockable { get { return responseType == ResponseType.Standard || responseType == ResponseType.Aerial; } }
		public bool IsParryable { get { return true; } }
		public bool IsDirectional { get { return responseType != ResponseType.Aerial; } }
	}

	[Header("Parameters")]
	[SerializeField] List<Attack> attacks;
	public float attackCooldownTime;
	public float staggerTime;

	[Header("Internals")]
	[SerializeField] float cooldownTimer = 0.0f;
	[SerializeField] IEnumerator attackCoroutine = null;
	Attack m_chosenAttack = null;
	AnimateRigidbody animateRigidbody;
	[SerializeField] bool m_isAttacking = false;

	#if UNITY_EDITOR
	[Header("Debug")]
	[SerializeField] string currentAttackName;
	[SerializeField] int debugAttackIndex;
	#endif

	public Attack ChosenAttack { 
		get { 
			if (m_chosenAttack == null) 
				ChosenAttack = Delegate_ChooseAttack();
			return m_chosenAttack; 
		}
		set { 
			m_chosenAttack = value;
			if (ChosenAttack != null)
			{
				currentAttackName = ChosenAttack.name;
			}
			else
			{
				currentAttackName = "None";
			}
		} 
	}

	public Vector3 attackTarget;

	// Function hook headers
	public delegate Attack ChooseAttack();
	public delegate void OnBeginAttack();
	public delegate void OnEndAttack(Attack attackData);
	public delegate void OnHitboxActive();
	public delegate void OnHitboxDisabled();
	public delegate Attack TryFollowUp();

	// These delegates can be set to run code at certain moments
	public ChooseAttack Delegate_ChooseAttack;
	public OnBeginAttack Delegate_OnBeginAttack;
	public OnEndAttack Delegate_OnEndAttack;
	public OnHitboxActive Delegate_OnHitboxActive;
	public OnHitboxDisabled Delegate_OnHitboxDisabled;
	public TryFollowUp Delegate_TryFollowUp;

	public bool CanAttack { get { return cooldownTimer <= 0.0f; } }
	public bool IsAttacking { get { return m_isAttacking; } }

	void Awake()
	{
		for(int i = 0; i < attacks.Count; i++)
		{
			Attack attack = attacks[i];
			attack.hitbox.SetActive(false);
			attack.m_index = i;
		}
	}

	void Start()
	{
		animateRigidbody = GetComponent<AnimateRigidbody>();
	}

	// Search for an attack in attacks by name
	public Attack GetAttack(string name)
	{
		foreach(Attack attack in attacks)
		{
			if (attack.name == name)
				return attack;
		}
		Debug.LogWarning("GetAttack(" + name + ") returned null!");
		return null;
	}

	public Attack GetValidSituationalAttack()
	{
		List<Attack> validAttacks = new();
		foreach (Attack a in attacks)
		{
			if (a.triggerType == Attack.TriggerType.Situational && IsAttackValid(a))
			{
				validAttacks.Add(a);
			}
		}
		if (validAttacks.Count == 0)
			return null;
		return validAttacks[UnityEngine.Random.Range(0, validAttacks.Count)];
	}

	public Attack GetRandomDefaultAttack()
	{
		List<Attack> validAttacks = new();
		foreach (Attack a in attacks)
		{
			if (a.triggerType == Attack.TriggerType.Default)
			{
				validAttacks.Add(a);
			}
		}
		if (validAttacks.Count == 0)
			return null;
		return validAttacks[UnityEngine.Random.Range(0, validAttacks.Count)];
	}

	// Returns whether the attackTarget is currently going to be hit by a given attack
	public bool IsAttackValid(Attack a)
	{
		if (a == null)
			return false;
		return a.attackBounds.OverlapPoint(attackTarget);
	}

	// Interrupts the current attack to perform a different attack immediately
	public void ForceBeginAttack(Attack a)
	{
		Debug.Assert(a != null);

		if (attackCoroutine != null)
		{
			StopCoroutine(attackCoroutine);
			EndCurrentAttack();
		}

		attackCoroutine = PerformAttack(a);
		StartCoroutine(attackCoroutine);
	}

	// ----------------------------------

    void Update()
    {
		// Choose an attack
		// Make sure that ChosenAttack is set to a valid state at start of frame
		if (ChosenAttack.triggerType == EnemyAttack.Attack.TriggerType.Situational)
		{
			ChosenAttack = Delegate_ChooseAttack();
		}

		// Check if there are any situational attacks that we could perform at the moment
		if (ChosenAttack.triggerType == Attack.TriggerType.Default)
		{
			Attack attackOverride = GetValidSituationalAttack();
			if (attackOverride != null)
			{
				ChosenAttack = attackOverride;
			}
		}


		// Begin attack
		if (CanAttack)
		{
			if (IsAttackValid(ChosenAttack))
			{
				ForceBeginAttack(ChosenAttack);
			}
		}
		else
		{
			cooldownTimer -= Time.deltaTime;
		}
    }

	// Template for what an attack needs to do
	IEnumerator PerformAttack(Attack attackData)
	{
		m_isAttacking = true;
		cooldownTimer = attackCooldownTime;
		ChosenAttack = attackData;
		this.enabled = false;

		// Windup
		Delegate_OnBeginAttack();
		yield return new WaitForSeconds(attackData.windupTime);

		// Actually attack
		attackData.hitbox.SetActive(true);
		if (animateRigidbody != null && attackData.movementCurves != null)
		{
			animateRigidbody.PlayAnimation(attackData.movementCurves);
		}
		Delegate_OnHitboxActive();

		yield return new WaitForSeconds(attackData.activeDamageTime);

		// Attack finished
		attackData.hitbox.SetActive(false);
		Delegate_OnHitboxDisabled();

		yield return new WaitForSeconds(attackData.postAttackTime);

		Attack followUpAttack = Delegate_TryFollowUp != null ? Delegate_TryFollowUp() : null;

		EndCurrentAttack();

		if (followUpAttack != null)
		{
			attackCoroutine = PerformAttack(followUpAttack);
			StartCoroutine(attackCoroutine);
		}
	}

	void EndCurrentAttack()
	{
		m_isAttacking = false;

		Attack attack = ChosenAttack;

		ChosenAttack?.hitbox.SetActive(false);
		ChosenAttack = null;

		if (animateRigidbody)
		{
			animateRigidbody.isPaused = true;
		}

		Delegate_OnEndAttack(attack);

		this.enabled = true;
		attackCoroutine = null;
	}

	public void OnAttackHit(Collider2D colliderHit)
	{
		if (colliderHit.CompareTag("Player"))
		{
			PlayerCombat playerCombat = colliderHit.GetComponent<PlayerCombat>();
			bool isPlayerFacingCorrectly = (!ChosenAttack.IsDirectional || Vector3.Dot(ChosenAttack.hitbox.transform.right, playerCombat.transform.right) < 0.0f);
			if (playerCombat.IsParrying && isPlayerFacingCorrectly)
			{
				Attack followUpAttack = Delegate_TryFollowUp != null ? Delegate_TryFollowUp() : null;
				if (followUpAttack != null)
				{
					ForceBeginAttack(followUpAttack);
				}
				else
				{
					StopCoroutine(attackCoroutine);
					EndCurrentAttack();
				}

				SendMessage("OnAttackParried", ChosenAttack);
				playerCombat.TriggerParry();
			}
			else
			{
				Health.Damage damageObj = new() { healthDamage = ChosenAttack.healthDamage, postureDamage = ChosenAttack.postureDamage, source = gameObject, knockdown = true };
				if (ChosenAttack.IsBlockable && playerCombat.IsBlocking && isPlayerFacingCorrectly)
				{
					damageObj.healthDamage = 0;
					damageObj.knockdown = false;
					// Stop movement
					animateRigidbody.isPaused = true;
				}
				colliderHit.GetComponent<PlayerHealth>().TakeDamage(damageObj);
			}
		}
	}

	public void OnBeginStagger()
	{
		if (attackCoroutine != null)
		{
			StopCoroutine(attackCoroutine);
			attackCoroutine = null;
		}
		this.enabled = false;
	}

	public void OnEndStagger()
	{
		this.enabled = true;
	}

	public void OnRecieveCritAttack()
	{
		if (attackCoroutine != null)
		{
			StopCoroutine(attackCoroutine);
			attackCoroutine = null;
		}
		this.enabled = false;
	}

	// Debug
	void OnDrawGizmosSelected ()
	{
		if (debugAttackIndex < 0 || debugAttackIndex >= attacks.Count)
			return;

		Attack attack = attacks[debugAttackIndex];

		Gizmos.color = Color.softRed;
		Gizmos.DrawWireSphere(transform.TransformPoint((Vector3)attack.idealPlayerOffset), 0.1f);
	}
}
