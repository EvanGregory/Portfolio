using System.Collections;
using UnityEngine;

public class FlyingEnemy : EnemyBase
{
	FlyingNavigation navComp;

	Vector2 attackPos;
	[Header("Parameters")]
	[SerializeField] float swoopSpeed;

	protected override void Start ()
	{
		base.Start();

		attackComp.Delegate_ChooseAttack = ChooseAttack;
		attackComp.Delegate_OnBeginAttack = OnBeginAttack;
		attackComp.Delegate_OnEndAttack = OnEndAttack;
		attackComp.Delegate_OnHitboxActive = OnHitboxActive;
		attackComp.Delegate_OnHitboxDisabled = OnHitboxDisabled;

		navComp = GetComponent<FlyingNavigation>();
		navComp.enabled = false;
	}

	protected override void Update()
	{
		base.Update();

		if (IsAlert)
		{

			if (!attackComp.IsAttacking)
			{
				// Set facing
				Vector2 toPlayer = Player.transform.position - transform.position;
				bool isFacingLeft = toPlayer.x < 0.0f;
				transform.localRotation = Quaternion.AngleAxis(isFacingLeft ? 180.0f : 0.0f, Vector3.up);
			}

			// Update components
			if (!attackComp.IsAttacking)
				attackComp.attackTarget = Player.transform.position;

			Vector3 localPlayerPos = transform.InverseTransformPoint(Player.transform.position);
			localPlayerPos -= (Vector3)attackComp.ChosenAttack.idealPlayerOffset;
			navComp.m_targetPos = transform.TransformPoint(localPlayerPos);
		}
	}

	EnemyAttack.Attack ChooseAttack()
	{
		return attackComp.GetRandomDefaultAttack();
	}

	void OnBeginAttack ()
	{
		navComp.enabled = false;
		attackPos = Player.transform.position;
	}

	void OnHitboxActive ()
	{
		navComp.rb.bodyType = RigidbodyType2D.Dynamic;

		EnemyAttack.Attack attack = attackComp.ChosenAttack;
	}

	void OnHitboxDisabled ()
	{
		EnemyAttack.Attack attack = attackComp.ChosenAttack;

		navComp.rb.linearVelocity = Vector2.zero;
	}

	void OnEndAttack (EnemyAttack.Attack attackData)
	{
		navComp.enabled = true;
	}

	public void OnBeginStagger()
	{
		navComp.enabled = false;
		navComp.rb.gravityScale = 1.0f;
	}

	public void OnEndStagger()
	{
		navComp.rb.gravityScale = 0.0f;
		navComp.enabled = true;
	}

	protected override void OnAlertStatusChange()
	{
		navComp.enabled = IsAlert;
	}
}
