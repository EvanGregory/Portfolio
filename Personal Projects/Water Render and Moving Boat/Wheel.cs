using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Wheel : Interactable
{
	[Header("Parameters")]
	[Tooltip("The speed the wheel rotates in degrees per second")]
	[SerializeField] float rotationSpeed;
	[Tooltip("How far the wheel rotates in either direction in degrees.")]
	[SerializeField] float maxRotationAngle;

	[Header("Internals")]
	[SerializeField] Transform playerStandPos;
	[SerializeField] Transform wheelModel;
	[SerializeField] float wheelRotation = 0.0f;

	public float TurnRatio { get { return wheelRotation / maxRotationAngle; } set { SetRotation(value * maxRotationAngle); }}

	public override Vector3 GetPlayerStartPos(PlayerMove playerMove)
	{
		return playerStandPos.position;
	}

	public override Quaternion GetPlayerStartRotation (PlayerMove playerMove)
	{
		return playerStandPos.rotation;
	}

	public override void OnEndInteract(PlayerMove playerMove)
	{
		
	}

	public override void OnRecieveInput(PlayerMove playerMove)
	{
		InputAction moveAction = playerMove.playerInputActions.FindAction("Move");
		float deltaMove = moveAction.ReadValue<Vector2>().x;
		SetRotation(wheelRotation + deltaMove * rotationSpeed * Time.deltaTime);
	}

	void SetRotation(float angle)
	{
		wheelRotation = Mathf.Clamp(angle, -maxRotationAngle, maxRotationAngle);

		// Update the rotation of the wheel transform
		Quaternion rot = Quaternion.AngleAxis(-wheelRotation, Vector3.forward);
		wheelModel.localRotation = rot;
	}
}
