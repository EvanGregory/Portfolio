using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EnemyBase;
using UnityEngine.AI;

public class EnemySecurity : EnemyBase
{

	[SerializeField] float attackDist = 5.0f;
	[SerializeField] float pullStrength = 10.0f;

	Vector3 stationarySpeed;

	protected override void Awake()
	{
		base.Awake();
	}

	protected override void Start()
	{
		base.Start();
	}

	// --- Update ---

	protected override void OnUpdateIdle() 
	{
		switch (idleBehavior)
		{
		case IdleState.Stationary:
		{
			if (HasReachedDestination())
			{
				if (IsWorkingMachine())
				{
					enemySightConeDrawer.ClearSightCone();
					enemySightConeDrawer.enabled = false;
				}

				transform.rotation = Quaternion.Slerp(transform.rotation, stationaryRotation, .05f);

				Vector3 newDest = FollowTarget.UpdateSpringVector(ref stationarySpeed, transform.position, stationaryPosition, 1.0f, Time.deltaTime);
				navMeshAgent.nextPosition = newDest;
			}
		} 
		break;
		case IdleState.Patrol:
		{
			UpdatePatrol();
		} 
		break;
		case IdleState.Wander:
		{
			if (HasReachedDestination())
			{
				navMeshAgent.SetDestination(CalculateWanderPoint());
			}
		} 
		break;
		}

		// Check for a change in alert state
		base.OnUpdateIdle();
	}
	protected override void OnUpdateInvestigating() 
	{
		if (playerSightline == PlayerVisibility.Clear)
		{
			investigatePosition = Player.instance.transform.position;
		}
		else if (HasReachedDestination())
		{
			investigatePosition = CalculateWanderPoint();
		}

		navMeshAgent.SetDestination(investigatePosition);

		// Check for a change in alert state
		if (enemyAlertness >= alertThreshold)
		{
			ChangeAlertState(AlertState.Alert);
		}
		else if (stateTimer > 7.5)
		{
			ChangeAlertState(AlertState.Idle);
		}
	}
	protected override void OnUpdateAlert() 
	{
		if (playerSightline == PlayerVisibility.Clear)
		{
			// Alert all nearby enemies and update the last known player position
			EnemyManager.instance.NotifyNeighbors(transform.position, Player.instance.transform.position);
		}
		else if (HasReachedDestination())
		{
			// Once we have reached the last known player pos, and we don't see the player, go to post alert
			CurrentAlertState = AlertState.PostAlert;
		}

		// Travel to the last known player pos
		Vector3 targetPosition = EnemyManager.instance.LastKnownPlayerPos;
		navMeshAgent.SetDestination(targetPosition);
		
		float distToPlayer = Vector3.Distance(Player.instance.transform.position, transform.position);
		bool doPull = Player.instance.playerMovement.CurrentState != PlayerMovement.PlayerState.Hiding &&
			distToPlayer <= attackDist;
		if (doPull)
		{
			Vector3 targetPullPos = transform.position + 1.0f * transform.forward;
			Vector3 forceDir = (targetPullPos - Player.instance.transform.position);
			// do not pull in the y
			forceDir.y = 0;
			forceDir = forceDir.normalized;
			float magnitude = pullStrength;
			Player.instance.GetComponent<Rigidbody>().AddForce(forceDir * magnitude);
		}

		base.OnUpdateAlert();
	}
	protected override void OnUpdatePostAlert() 
	{
		if (HasReachedDestination())
		{
			navMeshAgent.SetDestination(CalculateWanderPoint());
		}

		base.OnUpdatePostAlert();
	}


	// --- State Changes ---

	protected override void OnChangeStateToIdle(AlertState previousState) 
	{
		switch (idleBehavior)
		{
		case IdleState.Stationary:
		{
			stationarySpeed = navMeshAgent.velocity;
			navMeshAgent.SetDestination(stationaryPosition);
		} 
		break;
		case IdleState.Patrol:
		{
			Debug.Assert(patrolPoints.Count > 0, "Enemy in patrol state has no patrol points!");
			navMeshAgent.SetDestination(patrolPoints[patrolIndex].transform.position);
		}
		break;
		case IdleState.Wander:
		{
			navMeshAgent.SetDestination(CalculateWanderPoint());
		} 
		break;
		}

		base.OnChangeStateToIdle(previousState);
	}

	protected override void OnChangeStateToInvestigating(AlertState previousState) 
	{
		base.OnChangeStateToInvestigating(previousState);
	}

	protected override void OnChangeStateToAlert(AlertState previousState) 
	{
		GameObject janitor = EnemyManager.instance.CallJanitor(transform.position);

		base.OnChangeStateToAlert(previousState);
	}

	protected override void OnChangeStateToPostAlert(AlertState previousState) 
	{
		base.OnChangeStateToPostAlert(previousState);
	}

}
