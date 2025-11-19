using System.Collections.Generic;
using TMPro;
using UnityEngine.AI;
using UnityEngine;
using System.Collections;
using System;
using static UnityEngine.EventSystems.EventTrigger;
using Unity.VisualScripting;

[RequireComponent(typeof(NavMeshAgent))]
abstract public class EnemyBase : MonoBehaviour
{
    public enum EnemyType { Worker, Security, Janitor };
    public enum AlertState { Idle, Investigating, Alert, PostAlert, Stunned, Stopped };
    public enum IdleState { Stationary, Patrol, Wander };
    public enum PlayerVisibility { Clear, Blocked };
    protected NavMeshAgent navMeshAgent;
    protected EnemyManager manager;
    protected EnemySightConeDrawer enemySightConeDrawer;
    MeshRenderer meshRenderer;
    Animator anim;
    Animator alertAnim;

    [Header("Setup")]
    [SerializeField] protected LayerMask obstacleMask;
    [SerializeField] GameObject animatedBody;
    [SerializeField] GameObject alertPrefab;

    [Range(0, 10)]
    [SerializeField] protected int colourVariant;

    [SerializeField] Material[] colourVariants;
    [SerializeField] SkinnedMeshRenderer headMesh;
    [SerializeField] SkinnedMeshRenderer tailMesh;

    [Header("State Change Attributes")]
    [Tooltip("Represents how alert the enemy is to the player.")]
    [SerializeField] protected AlertState currentAlertState;
    [Tooltip("Represents whether the player can be seen this frame")]
    [SerializeField] protected PlayerVisibility playerSightline;
    protected float enemyAlertness = 0.0f;
    [SerializeField] protected float timeToDetect = 5.0f;
    [Tooltip("The field of view in degrees.")]
    [Range(0.0f, 360.0f)]
    [SerializeField] private float FOV = 90.0f;
    [Tooltip("The maximum distance at which the player can be seen.")]
    [SerializeField] protected float viewDistance = 0.0f;
    [Tooltip("The distance at which the player will always be seen, even when outside the enemy's field of view.")]
    [SerializeField] protected float alwaysDetectDistance = 0.0f;
    [Range(0.0f, 1.0f)]
    [SerializeField] protected float investigateThreshold = 0.5f;
    [Range(0.0f, 1.0f)]
    [SerializeField] protected float alertThreshold = 0.8f;

    // How long the enemy has been in the same state
    protected float stateTimer = 0.0f;

    protected Vector3 machineSpot = new Vector3(0, 0, 0);

    // The position enemies will go to when investigating. Set internally and by EnemyManager
    protected Vector3 investigatePosition;
    public Vector3 InvestigateTarget { set { investigatePosition = value; } }
    [NonSerialized] public bool isDistracted = false; // TEMP - so that enemies cant be distracted twice

    [Header("Idle State Attributes")]
    [SerializeField] protected IdleState idleBehavior;
    public Vector3 stationaryPosition;
    protected Quaternion stationaryRotation;
    [SerializeField] protected List<PatrolNode> patrolPoints;
    protected int patrolIndex = 0;
    protected bool patrolIsWaiting = false;
    protected float patrolWaitTime = 0f;
    [SerializeField] protected float wanderDist;

    [Header("Audio")]
    [SerializeField] protected SoundProfile _investigateSound;
    [SerializeField] protected SoundProfile _alertSound;
    [SerializeField] protected SoundProfile _footStepSound;

    [Header("State Speeds")]
    [Range(0.0f, 15.0f)]
    [SerializeField] protected float idleVelocity = 5.0f;
    [Range(0.0f, 15.0f)]
    [SerializeField] protected float investVelocity = 5.5f;
    [Range(0.0f, 15.0f)]
    [SerializeField] protected float alertVelocity = 6.0f;
    [Range(0.0f, 15.0f)]
    [SerializeField] protected float postAlertVelocity = 5.75f;

    [Range(0.0f, 15.0f)]
    [SerializeField] protected float stunnedTime = 10f;
    private float remainingStunTime = 0.0f;

    // Getters and setters
    public AlertState CurrentAlertState { get { return currentAlertState; } set { ChangeAlertState(value); } }
    public IdleState CurrentIdleState { get { return idleBehavior; } set { idleBehavior = value; } }
    public PlayerVisibility PlayerSightline { get { return playerSightline; } }
    public float EnemyFOV { get { return FOV; } }
    public float EnemySightDistance { get { return viewDistance; } }
    public LayerMask EnemyObstacleMask { get { return obstacleMask; } }


    protected virtual void Awake()
    {
        enemySightConeDrawer = GetComponent<EnemySightConeDrawer>();
        meshRenderer = GetComponent<MeshRenderer>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.SetDestination(transform.position);
        anim = animatedBody.GetComponent<Animator>();

        if (headMesh && tailMesh && colourVariant >= 0 && colourVariant < colourVariants.Length)
        {
            headMesh.material = colourVariants[colourVariant];
            tailMesh.material = colourVariants[colourVariant];
        }


        GameObject alertObjInstance = Instantiate(alertPrefab, gameObject.transform);
        alertAnim = alertObjInstance.GetComponentInChildren<Animator>();

        stationaryPosition = transform.position;
        stationaryRotation = transform.rotation;
    }

    protected virtual void Start()
    {
        // Setup sight cone
        if (enemySightConeDrawer != null)
        {
            enemySightConeDrawer.setSightConeArgs(FOV, viewDistance, obstacleMask);
        }
    }

    protected virtual void FixedUpdate()
    {
        // Update playerSightline
        if (CalcSeesPlayer())
        {
            playerSightline = PlayerVisibility.Clear;
        }
        else
        {
            playerSightline = PlayerVisibility.Blocked;
        }
    }

    // -Utility Functions-
    protected bool HasReachedDestination()
    {
        if (navMeshAgent.pathPending)
            return false;
        return navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance + 0.1f;
    }

    // Does a sweep of raycasts, returning true when the raycast hits the target layer
    private bool SweepCast(Vector3 searchDir, float fov, float numSteps, LayerMask collisionLayers, LayerMask targetLayers, out GameObject objectHit)
    {
        int combinedLayers = collisionLayers.value | targetLayers.value;
        Vector3 castDir = Quaternion.AngleAxis(-fov / 2.0f, Vector3.up) * searchDir;
        float stepSize = fov / numSteps;
        for (int i = 0; i < numSteps; i++)
        {
            if (Physics.Raycast(transform.position, castDir, out RaycastHit info, viewDistance, combinedLayers))
            {
                if (((1 << info.transform.gameObject.layer) & targetLayers.value) != 0)
                {
                    objectHit = info.transform.gameObject;
                    return true;
                }
            }

            castDir = Quaternion.AngleAxis(stepSize, Vector3.up) * castDir;
        }
        objectHit = null;
        return false;
    }

    private bool CalcSeesPlayer()
    {
        if (Player.instance.playerMovement.CurrentState == PlayerMovement.PlayerState.Hiding
            || Player.instance.playerMovement.CurrentState == PlayerMovement.PlayerState.Dead
            || Player.instance.playerMovement.CurrentState == PlayerMovement.PlayerState.Dash
            || (DebugGUI.instance && DebugGUI.instance.invisible) || Player.instance.playerMovement.IsInvisible)
        {
            return false;
        }

        Vector3 toPlayer = Player.instance.transform.position - transform.position;
        float distToPlayer = toPlayer.magnitude;
        Vector3 toPlayerDir = toPlayer.normalized;

        if (distToPlayer <= alwaysDetectDistance)
        {
            return true;
        }

        // Dont do raycasts when working machines
        if (IsWorkingMachine())
        {
            return false;
        }

        if (distToPlayer <= viewDistance)
        {
            // Do a quick raycast directly at the player to avoid the costly sweep 
            float fovDot = Mathf.Cos(FOV / 2.0f * Mathf.Deg2Rad);
            if (Vector3.Dot(toPlayerDir, transform.forward) >= fovDot && Physics.Raycast(transform.position, toPlayerDir, out RaycastHit info, distToPlayer, obstacleMask | LayerMask.GetMask("Player")))
            {
                if (info.transform.gameObject.CompareTag("Player"))
                {
                    return true;
                }
            }

            // Do a sweep of raycasts to roughly see if we can detect any smoke
            int numSteps = Mathf.FloorToInt(distToPlayer) * 4;
            if (SweepCast(transform.forward, FOV, numSteps, obstacleMask.value, LayerMask.GetMask("Smoke"), out GameObject objectHit))
            {
                // Kinda awkward way to check if its player smoke
                if (objectHit.GetComponent<SmokeBall>().container is PlayerSmokeController)
                {
                    return true;
                }
            }
        }
        return false;
    }

    protected Vector3 CalculateWanderPoint()
    {
        // generate a random position within certain range of enemy's current position
        Vector3 wanderPoint;
        // if it's not accessible, we need to keep generating new ones until it is
        NavMeshPath path = new();
        do
        {
            wanderPoint = new(UnityEngine.Random.Range(transform.position.x - wanderDist, transform.position.x + wanderDist), transform.position.y,
                                             UnityEngine.Random.Range(transform.position.z - wanderDist, transform.position.z + wanderDist));
        }
        while (!navMeshAgent.CalculatePath(wanderPoint, path));

        //then travel to it
        return wanderPoint;
    }

    // Called by other enemies or the enemy manager to alert about the player
    public void AlertToPlayer()
    {
        switch (currentAlertState)
        {
            case AlertState.Idle:
            case AlertState.Investigating:
                {
                    CurrentAlertState = AlertState.Alert;
                    enemyAlertness = 1.0f;
                }
                break;
            case AlertState.Alert:
                {
                    // Reset enemy alertness so they stay alert longer
                    SetDestination(EnemyManager.instance.LastKnownPlayerPos);
                    enemyAlertness = 1.0f;
                }
                break;
            case AlertState.PostAlert:
                {
                    CurrentAlertState = AlertState.Alert;
					enemyAlertness = 1.0f;
                }
                break;
            case AlertState.Stunned:
                {

                }
                break;
            case AlertState.Stopped:
                {

                }
                break;
        }
    }

    public void SetDestination(Vector3 pos)
    {
        navMeshAgent.SetDestination(pos);
    }

    public void SetStationaryPoint(Transform transform, bool isMachineSpot = false)
    {
        stationaryPosition = transform.position;
        stationaryRotation = transform.rotation;

        SetDestination(stationaryPosition);
        CurrentIdleState = EnemyBase.IdleState.Stationary;
        if (isMachineSpot)
        {
            machineSpot = stationaryPosition;
        }

    }

    public void Stun()
    {
        StartCoroutine(GetStunned());
    }

    // - Behind the scenes stuff -
    private void Update()
    {
        stateTimer += Time.deltaTime;

        if (anim)
        {
            bool isWalking = !(HasReachedDestination() || currentAlertState == AlertState.Stunned);
            anim.SetBool("Walking", isWalking);
        }

        OnUpdateAlertness();

        switch (currentAlertState)
        {
            case AlertState.Idle:
                {
                    OnUpdateIdle();
                }
                break;
            case AlertState.Investigating:
                {
                    OnUpdateInvestigating();
                }
                break;
            case AlertState.Alert:
                {
                    OnUpdateAlert();
                }
                break;
            case AlertState.PostAlert:
                {
                    OnUpdatePostAlert();
                }
                break;
            case AlertState.Stunned:
                {
                }
                break;
            case AlertState.Stopped:
                {

                }
                break;
        }

    }

    protected void ChangeAlertState(AlertState newState)
    {
        if (newState == currentAlertState)
        {
            return;
        }

        if (currentAlertState == AlertState.Alert)
        {
            if (anim)
            {
                anim.SetBool("Chase", false);
            }
        }

        switch (newState)
        {
            case AlertState.Idle:
                {
                    AnimateAlert("Reset");
                    enemyAlertness = 0.0f;
                    meshRenderer.material.color = Color.white;
                    enemySightConeDrawer.enabled = true;
                    OnChangeStateToIdle(currentAlertState);
                }
                break;
            case AlertState.Investigating:
                {
                    AnimateAlert("Search");
                    meshRenderer.material.color = Color.yellow;
                    enemySightConeDrawer.enabled = true;
                    OnChangeStateToInvestigating(currentAlertState);
                }
                break;
            case AlertState.Alert:
                {
                    AnimateAlert("Alert");
                    enemyAlertness = 1.0f;
                    meshRenderer.material.color = Color.red;
                    enemySightConeDrawer.enabled = true;
                    OnChangeStateToAlert(currentAlertState);
                }
                break;
            case AlertState.PostAlert:
                {
                    AnimateAlert("Alert");
                    meshRenderer.material.color = Color.yellow;
                    enemySightConeDrawer.enabled = true;
                    anim.SetBool("Chase", false);
                    OnChangeStateToPostAlert(currentAlertState);
                }
                break;
            case AlertState.Stunned:
                {
                    AnimateAlert("Stun");
                    //Hide SIghtCone
                    enemySightConeDrawer.ClearSightCone();
                    enemySightConeDrawer.enabled = false;
                    meshRenderer.material.color = Color.gray;
                    anim.SetBool("Stunned", true);
                    OnChangeStateToStun(currentAlertState);
                }
                break;
        }

        currentAlertState = newState;
        stateTimer = 0.0f;
    }

    protected virtual void OnUpdateAlertness()
    {
        enemyAlertness += CalculateChangeInAlertness() * Time.deltaTime;
        enemyAlertness = Mathf.Clamp01(enemyAlertness);
    }

    protected virtual void OnUpdateIdle()
    {
        if (enemyAlertness >= investigateThreshold)
        {
            ChangeAlertState(AlertState.Investigating);
        }
    }
    protected virtual void OnUpdateInvestigating()
    {
        if (enemyAlertness >= alertThreshold)
        {
            ChangeAlertState(AlertState.Alert);
        }
        else if (enemyAlertness < investigateThreshold)
        {
            ChangeAlertState(AlertState.Idle);
        }
    }
    protected virtual void OnUpdateAlert()
    {
        if (enemyAlertness < alertThreshold)
        {
            ChangeAlertState(AlertState.PostAlert);
        }
    }
    protected virtual void OnUpdatePostAlert()
    {
        if (Mathf.Approximately(enemyAlertness, 0.0f))
        {
            enemyAlertness = 0.0f;
            ChangeAlertState(AlertState.Idle);
        }
        else if (enemyAlertness >= alertThreshold)
        {
            ChangeAlertState(AlertState.Alert);
        }
    }
    protected virtual void OnChangeStateToIdle(AlertState previousState)
    {
        navMeshAgent.speed = idleVelocity;
        patrolIsWaiting = false;
    }
    protected virtual void OnChangeStateToInvestigating(AlertState previousState)
    {
        navMeshAgent.speed = investVelocity;
        if (AudioManager.instance)
        {
            AudioManager.instance.Play(_investigateSound, gameObject);
        }
    }
    protected virtual void OnChangeStateToAlert(AlertState previousState)
    {
        navMeshAgent.speed = alertVelocity;
        anim.SetBool("Chase", true);
        if (AudioManager.instance)
        {
            AudioManager.instance.Play(_alertSound, gameObject);
        }
    }
    protected virtual void OnChangeStateToPostAlert(AlertState previousState)
    {
        navMeshAgent.speed = postAlertVelocity;
    }
    protected virtual void OnChangeStateToStun(AlertState previousState)
    {
        if (anim)
        {
            anim.SetBool("Chase", false);
        }
    }

    // Returns the change in alertness per second based on how well the enemy can see the player
    protected virtual float CalculateChangeInAlertness()
    {
        float deltaAlertness = 0.0f;
        switch (playerSightline)
        {
            case PlayerVisibility.Clear:
                {
                    deltaAlertness = 1.0f / timeToDetect;
                }
                break;
            case PlayerVisibility.Blocked:
                {
                    deltaAlertness = -1.0f / timeToDetect;
                }
                break;
        }
        return deltaAlertness;
    }

    // -Debug-
    private void OnDrawGizmos()
    {
        if (!(Application.isPlaying && Application.isEditor))
        {
            return;
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, navMeshAgent.destination);
        //Gizmos.color = Color.white;
        //Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation * Quaternion.AngleAxis(90.0f, Vector3.forward), Vector3.one);
        //Gizmos.DrawFrustum(Vector3.zero, FOV, 10.0f, 0.0f, 0.0f);

        Color lineColor = Color.gray;
        switch (playerSightline)
        {
            case PlayerVisibility.Blocked:
                lineColor = Color.red;
                break;
            case PlayerVisibility.Clear:
                if (currentAlertState == AlertState.Investigating || currentAlertState == AlertState.Idle)
                {
                    lineColor = Color.yellow;
                }
                else
                {
                    lineColor = Color.green;
                }
                break;
        }
        lineColor.a = 0.5f;
        Gizmos.color = lineColor;
        Gizmos.DrawLine(transform.position, Player.instance.transform.position);
    }

    protected IEnumerator GetStunned()
    {
        navMeshAgent.speed = 0.0f;
        ChangeAlertState(AlertState.Stunned);
        remainingStunTime = stunnedTime;
        // currentAlertState = AlertState.Stunned;
        // meshRenderer.material.color = Color.gray;

        while (remainingStunTime > 0.0f)
        {
            remainingStunTime -= Time.deltaTime;
            yield return null;
        }
        enemySightConeDrawer.enabled = true;
        enemyAlertness = investigateThreshold;
        anim.SetBool("Stunned", false);

        ChangeAlertState(AlertState.Investigating);
    }

    public IEnumerator BackToIdle()
    {
        yield return new WaitForSeconds(1.0f);
        ChangeAlertState(AlertState.Idle);
    }

    public void PauseAI()
    {
        if (anim) anim.speed = 0.0f;
        CurrentAlertState = AlertState.Stopped;
        navMeshAgent.isStopped = true;
    }

    public void ResumeAI(AlertState newAlertState = AlertState.Idle)
    {
        if (anim) anim.speed = 1.0f;
        navMeshAgent.isStopped = false;
        CurrentAlertState = newAlertState;
    }

    //Clears patrol points then goes to first patrol point of next path
    public void SetPatrolPath(GameObject path)
    {
        patrolPoints.Clear();
        if (path == null) return;
        foreach (Transform child in path.transform)
        {
            PatrolNode patrolNode = child.GetComponent<PatrolNode>();
            if (patrolNode != null)
            {
                patrolPoints.Add(patrolNode);
            }
        }
        //next update, patrolIndex++ so start at first of next path
        patrolIndex = patrolPoints.Count;
    }

    protected void UpdatePatrol()
    {
        Debug.Assert(patrolPoints.Count > 0, "Enemy in patrol state has no patrol points!");
        if (patrolIsWaiting && stateTimer >= patrolWaitTime) // When waiting in patrol
        {
            patrolIndex++;
            if (patrolIndex >= patrolPoints.Count)
            {
                patrolIndex = 0;
            }

            navMeshAgent.SetDestination(patrolPoints[patrolIndex].transform.position);
            patrolIsWaiting = false;
            patrolWaitTime = patrolPoints[patrolIndex].WaitTime;
        }
        else if (!patrolIsWaiting && HasReachedDestination()) // When first reaching the next patrol point
        {
            patrolIsWaiting = true;
            stateTimer = 0.0f;
        }
    }

    protected bool IsWorkingMachine()
    {
        return (transform.position - machineSpot).sqrMagnitude < 2.0f && currentAlertState == AlertState.Idle && isDistracted;
    }

    public void AnimateAlert(String trigger)
    {
        if (alertAnim)
        {
            alertAnim.SetTrigger(trigger);
        }
    }
}