using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Ladder : Interactable
{
	[Header("Parameters")]
	[SerializeField] float climbSpeed;
	[SerializeField] float beginClimbTime;
	[Header("Internal pointers")]
	[SerializeField] Transform topPos;
	[SerializeField] Transform bottomPos;
	[SerializeField] Transform topDismount;
	[SerializeField] Transform bottomDismount;

	Vector3 GetUpDir() 
	{
		return (topPos.position - bottomPos.position).normalized;
	}

	public override void OnRecieveInput(PlayerMove playerMove)
	{
		InputAction moveAction = playerMove.playerInputActions.FindAction("Move");
		float vertMove = moveAction.ReadValue<Vector2>().y;
		Vector3 up = GetUpDir();
		playerMove.transform.position += (vertMove * climbSpeed * Time.deltaTime) * up;

		// Check if we've reached to top or bottom to dismount
		float ratio = GetClimbRatio(playerMove.transform.position);
		if (ratio > 1.0f)
		{
			// Dismount up
			playerMove.OnInteract();
			playerMove.transform.position = topDismount.position;
		}
		else if (ratio < 0.0f)
		{
			// Dismount down
			playerMove.OnInteract();
			playerMove.transform.position = bottomDismount.position;
		}
	}

	public override Vector3 GetPlayerStartPos(PlayerMove playerMove)
	{
		return GetPos(playerMove.transform.position);
	}

	public override Quaternion GetPlayerStartRotation (PlayerMove playerMove)
	{
		return transform.rotation;
	}

	public override void OnEndInteract(PlayerMove playerMove)
	{
		
	}

	public override Vector3 GetPos(Vector3 playerPos)
	{
		float ratio = GetClimbRatio(playerPos);
		return Vector3.Lerp(bottomPos.position, topPos.position, Mathf.Clamp01(ratio));
	}

	// Returns the fraction of how far up the ladder pos is.
	// A value < 0 means pos is below the ladder and a value > 1 is above the ladder
	float GetClimbRatio(Vector3 pos)
	{
		Vector3 up = GetUpDir();
		Vector3 bottomToPlayer = pos - bottomPos.position;
		float length = Vector3.Dot(bottomToPlayer, up);
		float totalLength = Vector3.Distance(bottomPos.position, topPos.position);
		return length / totalLength;
	}
}
