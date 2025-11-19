using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// Controls the creation and destruction of the SmokeBall prefab it is attached to
public class SmokeBall : MonoBehaviour
{
    public static GameObject bucket;

    public SmokeContainer container;

    private bool isFaded = false;
    public float innerRadius = 0.5f;
    public float outerRadius = 1.0f;

    // Lerp timer stuff
    bool isLerping = false;
    float currentTime = 0.0f;
    float totalTime = 0.0f;

    float initialInnerRadius = 0.0f;
    float initialOuterRadius = 0.0f;
    float targetInnerRadius = 0.0f;
    float targetOuterRadius = 0.0f;

    SphereCollider sphereCollider;
    [NonSerialized] public FollowTarget followTarget;
    MeshRenderer meshRenderer;
    ParticleSystem _particleSystem;

    // Start is called before the first frame update
    void Awake()
    {
        LinkSmokeBucket();

        sphereCollider = GetComponent<SphereCollider>();
        followTarget = GetComponent<FollowTarget>();
        meshRenderer = GetComponent<MeshRenderer>();
        _particleSystem = GetComponent<ParticleSystem>();
    }

    private void Start()
    {
        if (container)
        {
            LinkToContainer(container);
        }
    }

    private void Update()
    {
        if (isLerping)
        {
            float ratio = Mathf.Clamp01(currentTime / totalTime);

            if (Mathf.Approximately(ratio, 1.0f))
            {
                innerRadius = targetInnerRadius;
                outerRadius = targetOuterRadius;
                isLerping = false;
                currentTime = 0.0f;
            }
            else
            {
                innerRadius = Mathf.Lerp(initialInnerRadius, targetInnerRadius, ratio);
                outerRadius = Mathf.Lerp(initialOuterRadius, targetOuterRadius, ratio);
            }

            currentTime += Time.deltaTime;

            if (sphereCollider)
            {
                sphereCollider.radius = innerRadius;
            }

            // Scale used to resize the rendered sphere
            transform.localScale = 2.0f * outerRadius * Vector3.one;
        }
    }

    public void SetTarget(SmokeTarget targetObj, float lerpTime = 1.0f)
    {
        if (followTarget && followTarget.target != targetObj)
        {
            if (targetObj != null)
            {
                followTarget.target = targetObj.gameObject;
                LerpSize(targetObj.innerRadius, targetObj.outerRadius, lerpTime);

                if (targetObj.overrideDirectFollow)
                {
                    followTarget.CurrentFollowMode = FollowTarget.FollowMode.DirectFollow;
                }
            }
            else
            {
                followTarget.target = null;
                LerpSize(0.0f, 0.0f, lerpTime);
            }

        }
    }

    // Set target for when we don't need to lerp the size to the new target
    public void SetDummyTarget(GameObject targetObj)
    {
        if (followTarget)
        {
            followTarget.target = targetObj;
        }
    }

    public void LinkToContainer(SmokeContainer newContainer)
    {
        List<Material> materials = new();
        meshRenderer.GetMaterials(materials);
        materials[0] = newContainer.SmokeMaterial;
        meshRenderer.SetMaterials(materials);

        container = newContainer;
    }

    public void OnMerge(float mergeTime = 1.0f)
    {
        followTarget.CurrentFollowMode = FollowTarget.FollowMode.Lerp;
        followTarget.lerpTime = mergeTime;
    }

    public static SmokeBall Create(GameObject smokeBallPrefab, SmokeContainer owner, SmokeTarget target, Vector3 position, float innerRadius = 0.0f, float outerRadius = 0.0f)
    {
        // The first ball instantiated may have to create it's own bucket
        LinkSmokeBucket();

        GameObject smokeBall = Instantiate(smokeBallPrefab, position, Quaternion.identity, bucket.transform);
        SmokeBall smokeComp = smokeBall.GetComponent<SmokeBall>();

        smokeComp.container = owner;
        smokeComp.innerRadius = innerRadius;
        smokeComp.outerRadius = outerRadius;
        smokeComp.SetTarget(target);
        return smokeComp;
    }

    public IEnumerator Dissipate(Action<SmokeBall> callback = null, float delayTime = 0.0f, GameObject target = null)
    {
        ParticleSystem.EmissionModule particleEmitter = _particleSystem.emission;
        particleEmitter.enabled = false;
        if (followTarget)
        {
            followTarget.target = target;
        }

        // wait for how ever long particles can stay alive to stop popping
        float waitTime = _particleSystem.main.startLifetime.constantMax + delayTime;

        LerpSize(0.0f, 0.0f, waitTime);

        yield return new WaitForSeconds(waitTime);

        callback?.Invoke(this);
    }

    public IEnumerator FadeThroughWalls()
    {
        if (isFaded)
        {
            yield break;
        }

        isFaded = true;

        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody)
        {
            rigidbody.excludeLayers = 1; // Exclude the first layer, default
            yield return new WaitForSeconds(1);
            rigidbody.excludeLayers = 0; // Set exclude layers to nothing
        }

        isFaded = false;
    }

    private static void LinkSmokeBucket()
    {
        if (bucket == null)
        {
            bucket = GameObject.Find("Smoke Bucket");
            if (bucket == null)
            {
                bucket = new GameObject("Smoke Bucket");
                DontDestroyOnLoad(bucket);
            }
        }
    }

    private void LerpSize(float inner, float outer, float lerpTime = 1.0f)
    {
        isLerping = true;
        currentTime = 0.0f;
        totalTime = lerpTime;

        initialInnerRadius = innerRadius;
        initialOuterRadius = outerRadius;
        targetInnerRadius = inner;
        targetOuterRadius = outer;
    }

    public void HideSmoke(float lerpTime = 1.0f)
    {
        LerpSize(0.0f, 0.0f, lerpTime);
        ParticleSystem.EmissionModule particleEmitter = _particleSystem.emission;
        particleEmitter.enabled = false;
    }

    public void ShowSmoke(float lerpTime = 1.0f)
    {
        ParticleSystem.EmissionModule particleEmitter = _particleSystem.emission;
        particleEmitter.enabled = true;
        // Lerp size to the thing we are already following
        SmokeTarget target = followTarget.target.GetComponent<SmokeTarget>();
        LerpSize(target.innerRadius, target.outerRadius, lerpTime);
    }
}