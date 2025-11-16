using UnityEngine;
using System.Collections;

[RequireComponent(typeof(GroundNavigation))]
public class SwordEnemy : EnemyBase
{
	[Header("Parameters")]
	[SerializeField] float maxSwingDist;

	bool isAttackingLeft = false;

	GroundNavigation navComp;

	protected override void Start()
	{
		base.Start();

		navComp = GetComponent<GroundNavigation>();
		navComp.updateMode = GroundNavigation.UpdateMode.Automatic;
		navComp.enabled = false;

		attackComp.Delegate_ChooseAttack = ChooseAttack;
		attackComp.Delegate_OnBeginAttack = OnBeginAttack;
		attackComp.Delegate_OnEndAttack = OnEndAttack;
		attackComp.Delegate_OnHitboxActive = OnHitboxActive;
		attackComp.Delegate_OnHitboxDisabled = OnHitboxDisabled;
		attackComp.Delegate_TryFollowUp = TryFollowUpAttack;
	}

	protected override void Update()
	{
		base.Update();

		if (IsAlert)
		{
			// Update components
			if (!attackComp.IsAttacking)
				attackComp.attackTarget = Player.transform.position;

			Vector3 localPlayerPos = transform.InverseTransformPoint(Player.transform.position);
			localPlayerPos -= (Vector3)attackComp.ChosenAttack.idealPlayerOffset;
			navComp.m_targetPos = transform.TransformPoint(localPlayerPos);

			if (!attackComp.IsAttacking)
			{
				// Set facing
				Vector2 toPlayer = Player.transform.position - transform.position;
				bool isFacingLeft = toPlayer.x < 0.0f;
				transform.localRotation = Quaternion.AngleAxis(isFacingLeft ? 180.0f : 0.0f, Vector3.up);
			}
		}
	}

	EnemyAttack.Attack ChooseAttack()
	{
		// No bothering to weigh attack odds or anything here
		return attackComp.GetRandomDefaultAttack();
	}

	void OnBeginAttack()
	{
		navComp.enabled = false;
		isAttackingLeft = Player.transform.position.x - transform.position.x < 0.0f;
	}

	void OnHitboxActive()
	{
		navComp.rb.bodyType = RigidbodyType2D.Dynamic;
		EnemyAttack.Attack attack = attackComp.ChosenAttack;
	}

	void OnHitboxDisabled()
	{
		navComp.rb.linearVelocity = Vector2.zero;
	}

	void OnEndAttack(EnemyAttack.Attack attackData)
	{
		navComp.enabled = true;
	}

	EnemyAttack.Attack TryFollowUpAttack()
	{
		EnemyAttack.Attack attack = attackComp.ChosenAttack;

		bool playerIsLeft = Player.transform.position.x - transform.position.x < 0.0f;
		bool playerInSameDirection = !(playerIsLeft ^ isAttackingLeft);
		if (attack.name == "Forward Slash" && playerInSameDirection)
		{
			attackComp.attackTarget = Player.transform.position;
			return attackComp.GetAttack("Follow Up");
		}
		return null;
	}

	public void OnBeginStagger()
	{
		navComp.enabled = false;
	}

	public void OnEndStagger()
	{
		navComp.enabled = true;
	}

	protected override void OnAlertStatusChange()
	{
		navComp.enabled = IsAlert;
	}
}
