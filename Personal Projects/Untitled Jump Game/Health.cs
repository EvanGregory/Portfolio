using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
	public class Damage
	{
		public int healthDamage = 0;
		public float postureDamage = 0.0f;
		public GameObject source = null;
		public bool knockdown = false;
		public bool dodgeable = true;
	}

	[Header("Parameters")]
	[SerializeField] protected int maxHealth;
	[SerializeField] protected float maxPosture = 1.0f;
	[SerializeField] protected float postureRechargeTimeFull = 1.0f;
	[SerializeField] protected float postureRechargeTimeDamaged = 1.0f;
	[SerializeField] protected float postureRechargeTimeAlmostDead = 1.0f;
	[SerializeField] protected float postureChargeDelay;
	[SerializeField] protected float staggerTime;

	[Header("Internals")]
	[SerializeField] protected int m_hitPoints;
	[SerializeField] [Range(0, 1)] protected float m_posture;
	[SerializeField] bool m_isStaggered = false;
	protected Coroutine staggerCoroutine = null;
	public bool ignoreHealthDamage = false;
	public bool ignorePostureDamage = false;

	public bool IgnoreAllDamage { set { ignoreHealthDamage = value; ignorePostureDamage = value; } }

	public bool IsStaggered { get { return m_isStaggered; } }

#if UNITY_EDITOR
	[Header("Debug")]
	[SerializeField] bool infiniteHealth = false;
	[SerializeField] bool infinitePosture = false;
#endif

	protected float m_postureRechargeTimer = -1.0f;
	
	protected bool IsRechargingPosture { get { return m_postureRechargeTimer <= 0.0f; } }

	public int HitPoints { get { return m_hitPoints; } 
		protected set 
		{
#if UNITY_EDITOR 
			if (infiniteHealth)
				return;
#endif
			if (ignoreHealthDamage)
				return;

			int prevHealth = m_hitPoints; 
			m_hitPoints = Mathf.Clamp(value, 0, maxHealth); 
			OnHealthChange(m_hitPoints - prevHealth); 
		} 
	}

	public float Posture { get { return m_posture; } 
		protected set 
		{ 
#if UNITY_EDITOR 
			if (infinitePosture)
				return;
#endif
			if (ignorePostureDamage)
				return;

			m_posture = Mathf.Clamp(value, 0.0f, maxPosture); 
			OnPostureChange();
		}
	}

	public float HealthRatio { get { return (float)HitPoints / (float)maxHealth; } }
	public float PostureRatio { get { return Posture / maxPosture; } }

	protected virtual void Update()
	{
		UpdatePosture();
	}

	protected void UpdatePosture()
	{
		if (IsRechargingPosture)
		{
			// Increment posture
			float ratio = HealthRatio;
			if (ratio > 0.8f)
			{
				Posture += Time.deltaTime / postureRechargeTimeFull;
			}
			else if (ratio > 0.2f)
			{
				Posture += Time.deltaTime / postureRechargeTimeDamaged;
			}
			else
			{
				Posture += Time.deltaTime / postureRechargeTimeAlmostDead;
			}
		}
		else
		{
			m_postureRechargeTimer -= Time.deltaTime;
		}
	}

	public virtual void TakeDamage(Damage damage)
	{
		HitPoints -= damage.healthDamage;

		if (!ignorePostureDamage && damage.postureDamage > 0.0f)
		{
			m_postureRechargeTimer = postureChargeDelay;
		}

		Posture -= damage.postureDamage;
	}

	public void BeginStagger()
	{
		if (m_isStaggered)
			return;

		staggerCoroutine = StartCoroutine(PerformStagger());
	}

	protected IEnumerator PerformStagger()
	{
		m_isStaggered = true;
		SendMessage("OnBeginStagger");

		yield return new WaitForSeconds(staggerTime);

		SendMessage("OnEndStagger");
		staggerCoroutine = null;
		m_isStaggered = false;
	}

	public virtual void OnCollidedWithHazard() { }
	protected virtual void OnHealthChange(int difference) {}
	protected virtual void OnPostureChange() 
	{ 
		if (Posture <= 0.0f)
		{
			BeginStagger();
		}
	}
}
