using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class Interactable : MonoBehaviour
{
	public static List<Interactable> interactables;

	public string label;

	[SerializeField] protected TextMeshPro text;
	[SerializeField] private Color defaultColor = Color.white;
	[SerializeField] private Color selectedColor = Color.red;

	public bool useFollowTransform = true;

	protected MeshRenderer textRenderer;

	protected virtual void Awake()
	{
		text.text = label;
		textRenderer = text.GetComponent<MeshRenderer>();
		textRenderer.enabled = false;
	}

	private void OnEnable()
	{
		if (interactables == null)
		{
			interactables = new();
		}
		interactables.Add(this);
	}

	private void OnDisable()
	{
		if (interactables != null)
		{
			interactables.Remove(this);
		}

		SetDisplay(false);
	}

	public void SetDisplay(bool isDisplayed)
	{
		textRenderer.enabled = isDisplayed;
	}

	public void SetIsInteractTarget(bool isTargeted)
	{
		if (isTargeted)
		{
			text.color = selectedColor;
		}
		else
		{
			text.color = defaultColor;
		}
		
	}

	// Gets the position of the interactable
	// Possibly variable due to long interactables like ladders
	public virtual Vector3 GetPos(Vector3 playerPos)
	{
		return transform.position;
	}

	public virtual void SetTextPos(Vector3 playerPos)
	{
		text.transform.position = GetPos(playerPos);
	}

	public virtual Transform GetParentingTransform()
	{
		return transform.root;
	}

	// Returns the target default point of the player when they start interacting
	// Used to lerp the player into position
	public abstract Vector3 GetPlayerStartPos(PlayerMove playerMove);

	// Returns the target default rotation of the player when they start interacting
	// Used to lerp the player to the correct rotation
	public abstract Quaternion GetPlayerStartRotation(PlayerMove playerMove);

	// Called every Update by the player while this object is being interacted with
	public abstract void OnRecieveInput(PlayerMove playerMove);

	// Called once when the player starts interacting with this object
	// Returns false if the interact object wishes to refuse a prolonged interaction
	public virtual bool OnBeginInteract(PlayerMove playerMove)
	{
		return true;
	}

	// Called once when the player stops interacting with this object
	public abstract void OnEndInteract(PlayerMove playerMove);
}
