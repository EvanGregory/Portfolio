using UnityEngine;
using System.Collections;

public class EnemyHealth : Health
{
	EnemyBase enemyBase;

	private void Start ()
	{
		enemyBase = GetComponent<EnemyBase>();
	}

	public override void OnCollidedWithHazard ()
	{
		Kill();
	}

	public void Kill()
	{
		Destroy(gameObject);
	}

	public void OnBeginStagger()
	{
		HitPoints = 1;
		this.enabled = false;
	}

	public void OnEndStagger()
	{
		this.enabled = true;
	}

	// EnemyBase Messsage
	public void OnRecieveCritAttack()
	{
		if (staggerCoroutine != null)
		{
			StopCoroutine(staggerCoroutine);
		}
		this.enabled = false;
	}

	protected override void OnHealthChange (int difference)
	{
		if (difference == 0)
			return;

		if (HitPoints == 0)
		{
			if (IsStaggered)
			{
				Kill();
			}
			else
			{
				BeginStagger();
			}
		}
	}

	protected override void OnPostureChange ()
	{
		base.OnPostureChange();
	}

	// EnemyAttack Messsage
	public void OnAttackParried(EnemyAttack.Attack attack)
	{
		Damage damage = new() { postureDamage = 0.6f }; // TODO: how much posture damage to take when we are parried?
		TakeDamage(damage);
	}
}
