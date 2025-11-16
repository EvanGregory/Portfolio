using System;
using UnityEngine;

public class FlyingNavigation : MonoBehaviour
{
	public enum UpdateMode { Automatic, Manual }
	[NonSerialized] public Rigidbody2D rb;

	[Header("Parameters")]
	public Vector3 m_targetPos;
	public float moveSpeed;
	public UpdateMode updateMode;
	public bool controlFacing;

	bool m_isMoving;
	// Internal facing, only effects the transform rotation if controlFacing is true
	bool m_isFacingLeft; 

	public bool IsMoving { get { return m_isMoving; } }

    private void Awake ()
	{
		rb = GetComponent<Rigidbody2D>();
	}

	private void OnEnable ()
	{
		rb.bodyType = RigidbodyType2D.Dynamic;
	}

	private void OnDisable ()
	{
		rb.linearVelocity = Vector2.zero;
		rb.bodyType = RigidbodyType2D.Static;
	}

	// Should be called once every FixedUpdate if updateMode is set to Manual
	public void ForceUpdate()
	{
		Vector2 toTarget = m_targetPos - transform.position;
		float remainingDist = toTarget.magnitude;
		Vector2 dir = toTarget.normalized;

		m_isFacingLeft = dir.x < 0.0f;

		float speed = Mathf.Min(moveSpeed, remainingDist / Time.fixedDeltaTime);
		rb.linearVelocity = speed * dir;

		if (controlFacing)
		{
			transform.localRotation = Quaternion.AngleAxis(m_isFacingLeft ? 180.0f : 0.0f, Vector3.up);
		}
	}

	private void FixedUpdate ()
	{
		if (updateMode == UpdateMode.Automatic)
		{
			ForceUpdate();
		}
	}
}
