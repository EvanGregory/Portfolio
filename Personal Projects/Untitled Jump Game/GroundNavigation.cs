using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.EventSystems.EventTrigger;

[RequireComponent(typeof(Rigidbody2D))]
public class GroundNavigation : MonoBehaviour
{
	// Navigation is updated every frame or called explicitly
	public enum UpdateMode { Automatic, Manual }
	public enum NavMode { FollowTarget, RunAway }
	[NonSerialized] public Rigidbody2D rb;

	[Header("Parameters")]
	public Vector3 m_targetPos;
	public float moveSpeed;
	public UpdateMode updateMode;
	public NavMode navMode;
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
		switch (navMode)
		{
			case NavMode.FollowTarget:
			{
				FollowTarget(m_targetPos);
			} break;
			case NavMode.RunAway:
				// TODO: add this and maybe some other movement modes
				break;
		}

		if (controlFacing)
		{
			transform.localRotation = Quaternion.AngleAxis(m_isFacingLeft ? 180.0f : 0.0f, Vector3.up);
		}
	}

	void FollowTarget(Vector3 targetPos)
	{
		float diff = targetPos.x - transform.position.x;
		float dir = Mathf.Sign(diff);

		m_isFacingLeft = diff < 0.0f;

		Tilemap tiles = GlobalGrid.Instance.TerrainTilemap;
		Vector3Int pos = tiles.WorldToCell(transform.position);
		Vector3Int offsetPos = pos + new Vector3Int((int)dir, 0, 0 );
		bool canKeepMoving = !tiles.HasTile(offsetPos) && tiles.HasTile(offsetPos + Vector3Int.down);

		m_isMoving = canKeepMoving;

		float speed = Mathf.Min(moveSpeed, Mathf.Abs(diff / Time.fixedDeltaTime));
		if (!canKeepMoving)
		{
			float cellOffset = GlobalGrid.CellToWorld(pos).x - transform.position.x;
			dir = Mathf.Sign(cellOffset);

			float speedToTileCenter = Mathf.Abs(cellOffset / Time.fixedDeltaTime);
			speed = Mathf.Min(moveSpeed, speedToTileCenter);
		}
		rb.linearVelocityX = speed * dir;
	}

	void FixedUpdate()
    {
		if (updateMode == UpdateMode.Automatic)
		{
			ForceUpdate();
		}
    }

	// Blocks the object at xPos from walking through blockerXPos
	// Return an updated xVelocity that will not walk though the blocker
	public static void BlockWalkingThroughPos(ref float xVelocity, float xPos, float blockerXPos, float offset)
	{
		float xDiff = blockerXPos - xPos;

		// Moving away from blocker
		if (xDiff * xVelocity <= 0.0f)
			return;

		// Too far away for us to need to decrease velocity at all
		if (Mathf.Abs(xDiff) >= offset + Mathf.Abs(xVelocity) * Time.fixedDeltaTime)
			return;

		// Shift xDiff closer to 0 by offset
		if (xDiff < 0.0f)
			xDiff += offset;
		else
			xDiff -= offset;
		float newVel = xDiff / Time.fixedDeltaTime;

		// Safety check, we only ever want to decrease speed
		if (Mathf.Abs(newVel) > Mathf.Abs(xVelocity))
			return;

		xVelocity = newVel;
	}
}
