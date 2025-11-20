using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ShipMove))]
public class ShipAI : MonoBehaviour
{
	enum MoveState { Chasing, Orbiting }
	
	[Header("General Parameters")]
	[SerializeField] float closeDist;
	[SerializeField] float orbitTurnSharpness;
	[SerializeField] float orbitMaxFullness = 1.0f;
	[SerializeField] float chaseTurnSharpness;
	[SerializeField] float chaseMaxFullness = 1.0f;
	
	[Header("Internals")]
	[SerializeField] ShipMove targetShip;
	[SerializeField] MoveState moveState;
	[SerializeField] Vector3 targetDir;

	ShipMove shipMove;

	private void Awake()
	{
		shipMove = GetComponent<ShipMove>();
	}

	private void Update()
	{
		if (targetShip)
		{
			moveState = MoveState.Chasing;
			if (Vector3.Distance(targetShip.transform.position, transform.position) < closeDist)
			{
				moveState = MoveState.Orbiting;
			}

			float turnSharpness = 1.0f;
			float maxFullness = 1.0f;
			switch (moveState)
			{
			case MoveState.Chasing:
				turnSharpness = chaseTurnSharpness;
				maxFullness = chaseMaxFullness;

				targetDir = Vector3.Normalize(targetShip.transform.position - transform.position);
			break;
			case MoveState.Orbiting:
				turnSharpness = orbitTurnSharpness;
				maxFullness = orbitMaxFullness;

				float angle = 45.0f;
				Vector3 toShip = targetShip.transform.position - transform.position;
				Vector3 toShipDir = Vector3.Normalize(new Vector3(toShip.x, 0.0f, toShip.z));
				if (Vector3.Cross(transform.forward, toShipDir).y > 0.0f)
				{
					angle = -angle;
				}
				targetDir = Quaternion.AngleAxis(angle, Vector3.up) * toShipDir;

			break;
			}
			UpdateMove(turnSharpness, maxFullness);
		}
	}

	void UpdateMove(float turnSharpness, float maxFullness)
	{
		float dot = Vector3.Dot(targetDir, transform.forward);
		float clampedDot = Mathf.Max(dot, 0.0f);
		float turnRatio = 1.0f - Mathf.Pow(clampedDot, turnSharpness);
		if (Vector3.Cross(transform.forward, targetDir).y < 0.0f)
		{
			turnRatio = -turnRatio;
		}

		shipMove.wheel.TurnRatio = turnRatio;

		float fullness = clampedDot;
		foreach(Sail sail in shipMove.sails)
		{
			sail.SailFullness = fullness * maxFullness;
		}
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.green;
		Vector3 startPos = transform.position + Vector3.up * 7.5f;
		Gizmos.DrawLine(startPos, startPos + targetDir * 20.0f);
	}
}
