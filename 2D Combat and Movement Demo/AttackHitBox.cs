using UnityEngine;

public class AttackHitBox : MonoBehaviour
{
	public bool canHitPlayer = false;
	public bool canHitEnemy = false;
	public bool canHitHazards = false;
	// Set negative for infinite hits
	public int numHits = 1;

	int hitCount;

	private void OnEnable ()
	{
		hitCount = 0;
	}

	private void OnTriggerEnter2D (Collider2D collision)
	{
		if (numHits < 0 || hitCount >= numHits)
			return;

		if ((canHitHazards && collision.CompareTag("Hazard"))
			|| (canHitEnemy && collision.CompareTag("Enemy"))
			|| (canHitPlayer && collision.CompareTag("Player")))
		{
			++hitCount;
			SendMessageUpwards("OnAttackHit", collision);
		}
	}
}
