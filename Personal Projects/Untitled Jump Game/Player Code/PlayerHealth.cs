using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


public class PlayerHealth : Health
{
	PlayerMove moveComp;
	PlayerCombat combatComp;

	void Start ()
	{
		moveComp = GetComponent<PlayerMove>();
		combatComp = GetComponent<PlayerCombat>();
	}

	public override void TakeDamage (Damage damage)
	{
		if (damage.dodgeable && moveComp.HasRollImmunity)
			return;

		if (combatComp.IsParrying)
			damage.postureDamage = 0.0f;
		// else if (combatComp.IsBlocking)

		base.TakeDamage(damage);

		if (damage.knockdown)
		{
			moveComp.RecieveKnockDown(damage.source);
		}
	}

	protected override void Update ()
	{
		if (moveComp.IsKnockedDown)
		{
			// Stop posture regen while knocked down
			m_postureRechargeTimer = postureChargeDelay;
		}

		base.Update();
	}

	public override void OnCollidedWithHazard ()
	{
		moveComp.OnCollidedWithHazard();
	}

	protected override void OnHealthChange (int difference)
	{
		if (difference == 0)
			return;

		if (m_hitPoints == 0)
		{
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}
	}

	protected override void OnPostureChange ()
	{
		base.OnPostureChange();

		if (UIManager.Instance)
		{
			UIManager.Instance.PlayerPostureRatio = m_posture;
		}
	}
}
