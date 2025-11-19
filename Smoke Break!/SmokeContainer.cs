using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class SmokeContainer : MonoBehaviour, IPersistant
{
    [SerializeField] protected Material smokeMaterialClass;
    [SerializeField] protected GameObject smokeBallPrefab;

    [SerializeField] int maxSmokeBalls = 40; 
    public int MaxSmokeBalls { get { return maxSmokeBalls; }}

    protected Material smokeMaterialInstance;
    
    [Header("Generic Smoke Variables")]
	protected List<SmokeBall> m_smokeBalls;

    // List to store dying smokeBalls while they dissipate
    protected List<SmokeBall> m_removedBalls;

    // Smokeballs that are not technically a part of the player but should still be drawn by the player
    [SerializeField] protected List<SmokeBall> otherVisibleBalls;

    [SerializeField] protected bool m_fadeWhenFar = true;
    [SerializeField] protected float m_farDistance;
    protected Vector3 smokeCenter = Vector3.zero;
    public Vector3 SmokeCenter { get { return smokeCenter; }}

    [Header("Smoke Forms")]
    [Tooltip("The different shapes the smoke can be in.")]
    [SerializeField] protected GameObject smokeFormsParent; // Set the list based off of this object set in editor
    protected List<GameObject> smokeForms;
    [Tooltip("The index in the above list. Do not change this directly during play.")]
    [SerializeField] protected int currentFormIndex = 0;
    protected List<SmokeTarget> currentForm;

    protected SmokeTarget overflowTarget;

    [Header("Noise")]
    [Range(0.01f, 2.0f)]
    [SerializeField] float noiseScrollSpeed = 0.5f;
    [Range(0.0f, 2.0f)]
    [SerializeField] float noiseImpact = 0.5f;
    [SerializeField] Vector2 windDir = Vector2.one.normalized;
    Vector2 noiseOffset = Vector2.zero;

    public Material SmokeMaterial { get { return smokeMaterialInstance; }}

    // Setting this value may fail if the new value lies outside the number of smoke forms
    public int CurrentSmokeForm { get { return currentFormIndex; } set { SetSmokeForm(value); }}

    // Gets a copy of the list of smoke balls
    public SmokeBall[] SmokeBalls { get { return m_smokeBalls.ToArray(); } }

	protected virtual void Awake()
	{
        Debug.Assert(smokeMaterialClass, "Smoke container created without a material class! Instantiate smoke containers through a prefab.");
        if (smokeMaterialClass && !smokeMaterialInstance)
        {
            smokeMaterialInstance = new(smokeMaterialClass);
        }

        // Create a child to store excess smoke
        GameObject overflow = new("Smoke Overflow");
        overflow.transform.position = transform.position;
        overflow.transform.parent = gameObject.transform;

        overflowTarget = overflow.AddComponent<SmokeTarget>();
        overflowTarget.innerRadius = 0.0f;
        overflowTarget.outerRadius = 0.0f;

        // Set up smoke form array
        smokeForms = new();
        if (smokeFormsParent)
        {
            foreach (Transform childTransform in smokeFormsParent.transform) // Gets all first level children
            {
                smokeForms.Add(childTransform.gameObject);
            }
        }
		else
		{
			smokeForms.Add(gameObject);
		}

        // Set up these arrays to be of a max size
        List<Vector4> vertexPositions = new();
        for (int i = 0; i < maxSmokeBalls; i++)
        {
            vertexPositions.Add(Vector4.zero);
        }
            
        List<float> innerRadii = new();
        for (int i = 0; i < maxSmokeBalls; i++)
        {
            innerRadii.Add(0.0f);
        }

        List<float> outerRadii = new();
        for (int i = 0; i < maxSmokeBalls; i++)
        {
            outerRadii.Add(0.0f);
        }

        smokeMaterialInstance.SetVectorArray("_BallPositions", vertexPositions);
        smokeMaterialInstance.SetFloatArray("_BallInnerRadii", innerRadii);
        smokeMaterialInstance.SetFloatArray("_BallOuterRadii", outerRadii);

        m_smokeBalls = new();
        m_removedBalls = new();
	}

	protected virtual void Start()
    {
        foreach (GameObject smokeForm in smokeForms)
        {
            smokeForm.SetActive(false);
        }

        smokeForms[currentFormIndex].SetActive(true);
        currentForm = new(smokeForms[currentFormIndex].GetComponentsInChildren<SmokeTarget>());
    }

	protected virtual void Update()
	{
        UpdateCenterPos();

        if (m_fadeWhenFar)
        {
            foreach(SmokeBall smokeBall in m_smokeBalls)
            {
                if (Vector3.Distance(smokeCenter, smokeBall.transform.position) >= m_farDistance)
                {
                    StartCoroutine(smokeBall.FadeThroughWalls());
                }
            }
        }
	}

    private void LateUpdate()
	{
        // Don't need to push anything else if there is no smoke
        int numBalls = m_smokeBalls.Count + m_removedBalls.Count;
        if (numBalls == 0) 
        {
            smokeMaterialInstance.SetInt("_NumSmokeBalls", 0);
            return;
        }

        List<Vector4> vertexPositions = new();
        List<float> innerRadii = new();
        List<float> outerRadii = new();

        foreach (SmokeBall smokeComp in m_smokeBalls)
        {
            vertexPositions.Add(smokeComp.transform.position);
            innerRadii.Add(smokeComp.innerRadius);
            outerRadii.Add(smokeComp.outerRadius);
        }
        foreach (SmokeBall smokeComp in m_removedBalls)
        {
			if (vertexPositions.Count > maxSmokeBalls)
			{
				// Material uniforms are fixed size arrays, so don't go over the max
				return;
			}
            vertexPositions.Add(smokeComp.transform.position);
            innerRadii.Add(smokeComp.innerRadius);
            outerRadii.Add(smokeComp.outerRadius);
        }

        smokeMaterialInstance.SetInt("_NumSmokeBalls", numBalls);
        smokeMaterialInstance.SetVectorArray("_BallPositions", vertexPositions);
        smokeMaterialInstance.SetFloatArray("_BallInnerRadii", innerRadii);
        smokeMaterialInstance.SetFloatArray("_BallOuterRadii", outerRadii);
        smokeMaterialInstance.SetVector("_CenterPos", smokeCenter);
        smokeMaterialInstance.SetVector("_NoiseOffset", noiseOffset);
        smokeMaterialInstance.SetFloat("_NoiseImpact", noiseImpact);

        noiseOffset += windDir * (Time.deltaTime * noiseScrollSpeed);
		// Mod 2 since the texture mirrors
        noiseOffset.x %= 2.0f;
        noiseOffset.y %= 2.0f;
	}

    void UpdateCenterPos()
    {
        Vector3 pos = transform.position;
        if (m_smokeBalls.Count > 0)
        {
            pos = Vector3.zero;
            foreach (SmokeBall smokeBall in m_smokeBalls)
            {
                pos += smokeBall.transform.position;
            }
            pos /= m_smokeBalls.Count;
        }
        smokeCenter = pos;
    }

	public int GetNumSmoke()
    {
        return m_smokeBalls.Count;
    }

    public void SetSmokeForm(int index, float lerpTime = 1.0f)
    {
        if (index >= smokeForms.Count || index < 0)
        {
            Debug.LogAssertion("SetSmokeForm called with an invalid index.");
            return;
        }

        // Currently making a new list every time for simplicity
        smokeForms[currentFormIndex].SetActive(false);
        smokeForms[index].SetActive(true);
        currentFormIndex = index;
        currentForm = new(smokeForms[currentFormIndex].GetComponentsInChildren<SmokeTarget>());

        int i = 0;
        foreach (SmokeTarget target in currentForm)
        {
            if (m_smokeBalls.Count <= i)
            {
                break;
            }

            m_smokeBalls[i].SetTarget(target, lerpTime);

            i++;
        }

        // Set the remaining balls to the overflow
        for (; i < m_smokeBalls.Count; i++)
        {
            m_smokeBalls[i].SetTarget(overflowTarget, lerpTime);
        }
    }

    // Add's another container's smoke to this container
    public virtual void MergeSmoke(List<SmokeBall> otherSmoke)
    {
        foreach (SmokeBall smokeBall in otherSmoke)
        {
            if (currentForm.Count > m_smokeBalls.Count)
            {
                smokeBall.SetTarget(currentForm[m_smokeBalls.Count]);
            }
            else
            {
                smokeBall.SetTarget(overflowTarget);
            }
            m_smokeBalls.Add(smokeBall);

            smokeBall.LinkToContainer(this);
        }
    }

    // Remove smoke and give it to another container
    public int GiveSmoke(SmokeContainer reciever, int num)
    {
        int amountToGive = Mathf.Min(num, reciever.MaxSmokeBalls - reciever.GetNumSmoke());
        amountToGive = Mathf.Min(amountToGive, GetNumSmoke());
        if (amountToGive <= 0)
        {
            return amountToGive;
        }

        List<SmokeBall> smokeRemoved;

        if (amountToGive >= m_smokeBalls.Count)
        {
            // Take all of them
			amountToGive = m_smokeBalls.Count;
            smokeRemoved = new(m_smokeBalls);
            m_smokeBalls.Clear();
        }
        else
        {
            // Take smoke from the end of the list
            int startIndex = m_smokeBalls.Count - amountToGive;
            smokeRemoved = m_smokeBalls.GetRange(startIndex, amountToGive);
            m_smokeBalls.RemoveRange(startIndex, amountToGive);
        }

        reciever.MergeSmoke(smokeRemoved);
		return amountToGive;
    }

    public SmokeBall CreateSmoke(SmokeTarget target, float innerRadius = 0.0f, float outerRadius = 0.0f)
    {
		return CreateSmoke(target, target.transform.position, innerRadius, outerRadius);
    }

	public SmokeBall CreateSmoke(SmokeTarget target, Vector3 position, float innerRadius = 0.0f, float outerRadius = 0.0f)
	{
		SmokeBall smokeBall = SmokeBall.Create(smokeBallPrefab, this, target, position, innerRadius, outerRadius);
        m_smokeBalls.Add(smokeBall);
		return smokeBall;
	}

    public void CreateSmoke(SmokeTarget target, int num)
    {
        for (int i = 0; i < num; i++)
        {
            CreateSmoke(target);
        }
    }

    // For now, just removes the last smoke in the list,
    // might make it compare and remove the farthest ones later
    public void RemoveSmoke(int num = 1, float delay = 0.0f, GameObject target = null)
    {
        for (int i = 0; i < num; i++)
        {
            if (m_smokeBalls.Count == 0)
                return;
            
            int endIndex = m_smokeBalls.Count - 1;
            SmokeBall ball = m_smokeBalls[endIndex];
 
            m_removedBalls.Add(ball);
            m_smokeBalls.RemoveAt(endIndex);

            void FinishRemoveSmoke(SmokeBall smokeBall)
            {
                m_removedBalls.Remove(smokeBall);
                Destroy(smokeBall.gameObject);
            }
            StartCoroutine(ball.Dissipate(FinishRemoveSmoke, delay, target));
        }
    }

    public void RemoveAll(GameObject target = null, float delay = 0.0f)
    {
        RemoveSmoke(m_smokeBalls.Count, delay);
    }

    public void OnRespawn()
    {
    }

	protected virtual void OnDestroy()
	{
		foreach (SmokeBall smokeBall in m_smokeBalls)
        {
            if (smokeBall && !smokeBall.gameObject.IsDestroyed())
            {
                Destroy(smokeBall.gameObject);
            }
        }

        foreach (SmokeBall smokeBall in m_removedBalls)
        {
            if (smokeBall && !smokeBall.gameObject.IsDestroyed())
            {
                Destroy(smokeBall.gameObject);
            }
        }
	}

	// These two are wrappers we need for some cutscenes
	public void ShowSmoke(float time)
	{
		SetSmokeVisibility(true, time);
	}

	public void HideSmoke(float time)
	{
		SetSmokeVisibility(false, time);
	}

	public void SetSmokeVisibility(bool isVisible, float lerpTime = 1.0f)
	{
		if (isVisible)
		{
			foreach(SmokeBall ball in m_smokeBalls)
			{
				ball.ShowSmoke(lerpTime);
			}
		}
		else
		{
			foreach(SmokeBall ball in m_smokeBalls)
			{
				ball.HideSmoke(lerpTime);
			}
		}
	}
}
