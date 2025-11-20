using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipMove : MonoBehaviour
{
    [Header("Ship Parts")]
    public Wheel wheel;
    public Sail[] sails;
	public Capstan capstan;
    [SerializeField] GameObject buoyancyVoxelGrid;
    [SerializeField] GameObject damagePointsParent;
	[SerializeField] GameObject damageDecalPrefab;
    [Header("Parameters")]
    [SerializeField] float turnForce;
    [SerializeField] float linearForce;
    [SerializeField] float buoyancyForce;
    [SerializeField] float gravity;
	[Header("Drag")]
    [SerializeField] float verticalDrag;
    [SerializeField] float forwardBackDrag;
    [SerializeField] float sidewaysDrag;
	[SerializeField] float anchorDragMult;
    [Header("Internals")]
    [SerializeField] float forwardSpeedRatio;
    [Tooltip("-1 is turning all the way left, 1 is all the way right")] [Range(-1,1)]
    [SerializeField] float turnRatio;
    float maxDrag;

    FollowWaves[] buoyancyVoxelComps;

    Rigidbody rigidBody;

	public Rigidbody RB { get { return rigidBody; } }

	void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        maxDrag = rigidBody.drag;

        buoyancyVoxelComps = buoyancyVoxelGrid.transform.GetComponentsInChildren<FollowWaves>();
    }

    // Late update so the other ship components update first
    void LateUpdate()
    {
        turnRatio = wheel.TurnRatio;

        forwardSpeedRatio = Sail.CalcAverageFullness(sails);

        UpdateMovement();
        UpdateBuoyancy();

        // Apply drag
		float anchorMultiplier = capstan.IsRaised ? 1.0f : anchorDragMult;
        Vector3 xzVel = new(rigidBody.velocity.x, 0.0f, rigidBody.velocity.z);
        float forwardVel = Vector3.Dot(transform.forward, xzVel);
        float rightVel = Vector3.Dot(transform.right, xzVel);
        rigidBody.AddForce(transform.forward * (-1.0f * forwardVel * forwardBackDrag * anchorMultiplier));
        rigidBody.AddForce(transform.right * (-1.0f * rightVel * sidewaysDrag));
        rigidBody.AddForce(Vector3.up * (-1.0f * rigidBody.velocity.y * verticalDrag));
    }

    void UpdateMovement()
    {
        // Turn the velocity to follow the ship's direction
        //Vector3 xzVel = new(rigidBody.velocity.x, 0.0f, rigidBody.velocity.z);
        //rigidBody.velocity = transform.forward * (xzVel.magnitude) + Vector3.up * rigidBody.velocity.y;

        // Add forces 
        // Doing impulses since idk if it will use the proper deltaTime
		if (capstan.IsRaised)
		{
			float forwardForce = Mathf.Lerp(0.0f, linearForce, forwardSpeedRatio) * Time.deltaTime;
			Vector3 xzForward = new(transform.forward.x, 0.0f, transform.forward.z);
			xzForward = xzForward.normalized;
			rigidBody.AddForce(xzForward * forwardForce, ForceMode.Impulse);
		}
		
		float anchorSpinMult = 1.0f;
		if (!capstan.IsRaised)
		{
			// anchorSpinMult grows as the spin progresses
			anchorSpinMult = Mathf.Lerp(1.0f, 6.0f, capstan.SpinPercent);
			// as the anchor slows down the boat, anchorSpinMult decreases
			anchorSpinMult *= Mathf.InverseLerp(0.0f, 8.0f, rigidBody.velocity.magnitude);
		}

        float turnLerp = turnRatio;
        turnLerp += 1.0f;
        turnLerp *= 0.5f;
        float torqueForce = Mathf.Lerp(-turnForce, turnForce, turnLerp) * Time.deltaTime;
		torqueForce *= anchorSpinMult;
        rigidBody.AddTorque(transform.up * torqueForce, ForceMode.Impulse);
    }

    void UpdateBuoyancy()
    {
        float totalDispersion = 0.0f;
        foreach(FollowWaves voxelComp in buoyancyVoxelComps)
        {
            float magnitude = voxelComp.Dispersion * buoyancyForce / buoyancyVoxelComps.Length;
            rigidBody.AddForceAtPosition(Vector3.up * magnitude, voxelComp.transform.position);

            totalDispersion += voxelComp.Dispersion;
        }

        rigidBody.drag = 0.0f;//Mathf.Lerp(0.0f, maxDrag, totalDispersion / buoyancyVoxelComps.Length);

        // Gravity
        rigidBody.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
    }

	public void OnCannonBallCollision(Collision collision)
	{
		ContactPoint contact = collision.GetContact(0);

		// Create decal
		Quaternion rotation = Quaternion.LookRotation(contact.normal, collision.transform.up);
		rotation *= Quaternion.AngleAxis(90.0f, Vector3.right);
		GameObject.Instantiate(damageDecalPrefab, contact.point, rotation, damagePointsParent.transform);

		// Effects

	}
}
