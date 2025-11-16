using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGun : MonoBehaviour
{
	[Header("Parameters")]
	[SerializeField] [Range(0, 90)] float maxLookAngle = 80.0f;
	[SerializeField] [Range(0, 90)] float minLookAngle = 80.0f;

	[Header("Internals")]
	[SerializeField] Transform gunPivot;
	[SerializeField] Transform gunModel;
	[SerializeField] Transform gunBarrel;

	[SerializeField] private GameObject gunLine;
	[SerializeField] private ParticleSystem gunSparks;

	[SerializeField] private bool bPrevFrameLeft = false;
	
	[NonSerialized]
	public bool initialized = false;

	float m_rotationSpeed = 0.0f;
	
	private void Awake()
	{
		if (LevelManager.Instance)
			LevelManager.Instance.playerGun = this;
	}

	private void Start()
	{
		if (LevelManager.Instance)
			LevelManager.Instance.playerGun = this;
	}

	void Update()
    {
	    if (!initialized) 
			return;
	    
		bool isFacingLeft = transform.right.x < 0.0f;

		Vector3 worldMousePos = PlayerMove.GetWorldMousePos();
		Vector3 toMouse = worldMousePos - gunBarrel.position;
		Vector3 pivotToMouse = worldMousePos - gunPivot.position;

		if ((pivotToMouse.sqrMagnitude < (gunPivot.position - gunBarrel.position).sqrMagnitude) || (pivotToMouse.x < 0.0f != isFacingLeft)) // The mouse is between the player and the tip of the gun
		{
			toMouse = isFacingLeft ? Vector3.left : Vector3.right;
		}

		float targetAngle = Mathf.Rad2Deg * Mathf.Atan2(toMouse.y, toMouse.x);
		
		if (isFacingLeft)
		{
			gunModel.localRotation = Quaternion.Euler(180.0f, 0, 0);
			if (targetAngle >= 0.0f) 
				targetAngle = Mathf.Max(targetAngle, 180.0f - maxLookAngle);
			else 
				targetAngle = Mathf.Min(targetAngle, -180.0f + minLookAngle);
		}
		else
		{
			gunModel.localRotation = Quaternion.Euler(0, 0, 0);
			targetAngle = Mathf.Clamp(targetAngle, -minLookAngle, maxLookAngle);
		}
		
		
		float angle = Mathf.SmoothDampAngle(gunPivot.rotation.eulerAngles.z, targetAngle, ref m_rotationSpeed, 0.1f);
		gunPivot.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

		bPrevFrameLeft = isFacingLeft;
    }

	public Vector3 GetGunDir()
	{
		return gunPivot.transform.right;
	}

	public void Shoot()
	{
		bool isFacingLeft = transform.right.x < 0.0f;

		Vector3 worldMousePos = PlayerMove.GetWorldMousePos();
		Vector3 pivotToMouse = worldMousePos - gunPivot.position;

		if ((pivotToMouse.sqrMagnitude < (gunPivot.position - gunBarrel.position).sqrMagnitude) || (pivotToMouse.x < 0.0f != isFacingLeft)) // The mouse is between the player and the tip of the gun
		{
			return;
		}

		Vector3 toMouse = (worldMousePos - gunBarrel.position).normalized;

		LayerMask mask = LayerMask.GetMask("Default", "wall");
		RaycastHit2D info = Physics2D.Raycast(gunBarrel.position, toMouse, Mathf.Infinity, mask);
		//Instantiate(gunSparks, info.point, transform.rotation);

		GunTrail trail = Instantiate(gunLine, Vector3.zero, Quaternion.identity).GetComponent<GunTrail>();
		if (info)
		{
			Target target = info.transform.GetComponent<Target>();
			if (target)
			{
				target.OnHit();
			}

			trail.Set(gunBarrel.position, info.point);
		}
		else
		{
			trail.Set(gunBarrel.position, gunBarrel.position + toMouse * 100.0f);
		}
		
		
		
		float knockBack = 40.0f;
		if (gunBarrel.right.x < 0.0f)
			knockBack = -knockBack;

		gunPivot.rotation *= Quaternion.Euler(0,0,knockBack);
	}
}
