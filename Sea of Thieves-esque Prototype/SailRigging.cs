using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SailRigging : Interactable
{
	[Header("Parameters")]
	[SerializeField] float speed;

	[Header("Internals")]
	[SerializeField] Sail[] sails;
	[SerializeField] Transform[] standPositions;
	float sailFullnessRatio;

	public float SailRatio { get { return sailFullnessRatio; }}

	public override Vector3 GetPlayerStartPos(PlayerMove playerMove)
	{
		return GetPos(playerMove.transform.position);
	}

	public override Quaternion GetPlayerStartRotation (PlayerMove playerMove)
	{
		// Could define a look target, for now just average the sail positions
		Vector3 lookTarget = Vector3.zero;
		foreach(Sail sail in sails)
		{
			lookTarget += sail.transform.position;
		}
		lookTarget /= sails.Length;

		Vector3 forward = (lookTarget - GetPos(playerMove.transform.position)).normalized;
		return Quaternion.LookRotation(forward, transform.up);
	}

	public override void OnEndInteract(PlayerMove playerMove)
	{
		
	}
	public override void OnRecieveInput(PlayerMove playerMove)
	{
		InputAction moveAction = playerMove.playerInputActions.FindAction("Move");
		float deltaMove = moveAction.ReadValue<Vector2>().y;
		deltaMove *= speed * Time.deltaTime;
		deltaMove = -deltaMove; // Flip it since up is positive
		sailFullnessRatio = Mathf.Clamp01(sailFullnessRatio + deltaMove);

		foreach(Sail sail in sails)
		{
			sail.SailFullness = sailFullnessRatio;
		}
	}

	public override Vector3 GetPos(Vector3 playerPos)
	{
		List<Vector3> positions = new();
		for (int i = 0; i < standPositions.Length; i++)
		{
			positions.Add(standPositions[i].position);
		}

		float minDist = Mathf.Infinity;
		Vector3 bestPos = Vector3.zero;
		foreach (Vector3 pos in positions)
		{
			float dist = Vector3.Distance(pos, playerPos);
			if (dist < minDist)
			{
				bestPos = pos;
				minDist = dist;
			}
		}
		return bestPos;
	}
}
