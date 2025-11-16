using UnityEngine;
using System.Collections;

/* An empty version of an Enemy I can copy later
public class EnemyTemplate : EnemyBase
{
	protected override void Start ()
	{
		base.Start();

		attackComp.Delegate_ChooseAttack = ChooseAttack;
		attackComp.Delegate_OnBeginAttack = OnBeginAttack;
		attackComp.Delegate_OnEndAttack = OnEndAttack;
		attackComp.Delegate_OnHitboxActive = OnHitboxActive;
		attackComp.Delegate_OnHitboxDisabled = OnHitboxDisabled;
	}

	protected override void Update()
	{
		base.Update();
	}

	EnemyAttack.Attack ChooseAttack()
	{
		// No bothering to weigh attack odds or anything here
		return attackComp.GetRandomDefaultAttack();
	}

	void OnBeginAttack ()
	{

	}

	void OnHitboxActive ()
	{
		EnemyAttack.Attack attack = attackComp.ChosenAttack;
	}

	void OnHitboxDisabled ()
	{
		EnemyAttack.Attack attack = attackComp.ChosenAttack;
	}

	void OnEndAttack (EnemyAttack.Attack attackData)
	{

	}

	public void OnBeginParryStun() {}

	public void OnEndParryStun() {}
}
*/