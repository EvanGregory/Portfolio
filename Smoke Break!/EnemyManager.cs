using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.AI;

public class EnemyManager : Singleton<EnemyManager>
{
    public EnemyBase.AlertState WorldAlertState { get{ return m_highestAlert; }}
    public List<EnemyBase> enemyList;
    public List<EnemyWorker> workersList;
    public List<EnemySecurity> securityList;
    public List<EnemySpawnDoor> spawnDoors;

    public JanitorIndicator indicator;

    [SerializeField] float alertNeighborRadius;
    [SerializeField] Vector3 m_lastKnownPlayerPos;

    public Vector3 LastKnownPlayerPos { get{ return m_lastKnownPlayerPos; }}

    public EnemyJanitor janitor = null;

    [SerializeField] GameObject workerPrefab;
    [SerializeField] GameObject securityPrefab;
    [SerializeField] GameObject janitorPrefab;

    [SerializeField] GameObject indicatorPrefab;
    public GameObject WorkerPrefab { get { return workerPrefab; } }
    public GameObject SecurityPrefab { get { return securityPrefab; } }
    public GameObject JanitorPrefab { get { return janitorPrefab; } }

    /* Alert states are organized from lowest to highest:
     * Idle or Post Alert = 0
     * Investigating = 1
     * Alert = 2
     * A highest alert of 3 (post alert) is impossible
     */ 
    [SerializeField] EnemyBase.AlertState m_highestAlert = EnemyBase.AlertState.Idle;

    [Tooltip("The sound profile the Janitor will play when entering a door.")]
    [SerializeField] private SoundProfile onJanitorCalled;

    void Start()
    {

        foreach (GameObject enemy in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            EnemyBase comp = enemy.GetComponent<EnemyBase>();
            if (comp)
            {
                enemyList.Add(comp);

                EnemySecurity asSecurity = comp as EnemySecurity;
                EnemyWorker asWorker = comp as EnemyWorker;
                EnemyJanitor asJanitor = comp as EnemyJanitor;
                if (asSecurity)
                {
                    securityList.Add(asSecurity);
                }
                else if (asWorker)
                {
                    workersList.Add(asWorker);
                }
                else if (asJanitor)
                {
                    janitor = asJanitor;
                }
            }
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        int highestAlert = 0;
        foreach (EnemyBase enemyComp in enemyList)
        {
            // Determine the hightest alert state
            int magnitude = (int)enemyComp.CurrentAlertState;
            if (magnitude == 3)
            {
                magnitude = 0;
            }

            if (magnitude > highestAlert)
            {
                highestAlert = magnitude;
            }
        }

        m_highestAlert = (EnemyBase.AlertState)highestAlert;
    }

    // Finds the closest door the the called position and spawns an angry janitor at that door
    // Returns the janitor GameObject
    public GameObject CallJanitor(Vector3 callPos)
    {
        if (!indicator)
        {
            indicator = Instantiate(indicatorPrefab, UIManager.instance.HUD.transform).GetComponent<JanitorIndicator>();
            indicator.gameObject.SetActive(false);
        }
        // Find the closest door to callPos
        EnemySpawnDoor closestDoor = null;
        float closestDist = float.MaxValue;
        foreach(EnemySpawnDoor door in spawnDoors)
        {
            if (door)
            {
                /* If rooms are getting tracked and the door 
                is in the wrong room ignore this door */

                if (RoomTracker.instance && !RoomTracker.instance.IsInRoom(door.transform.root.gameObject))
                {
                    continue;
                }

                float dist = Vector3.Distance(door.transform.position, callPos);
                if (dist < closestDist)
                {
                    closestDoor = door;
                    closestDist = dist;
                }
            }
            door.Lock();
        }

        if (janitor)
        {
            indicator.gameObject.SetActive(true);
            janitor.AlertToPlayer();
            return janitor.gameObject;
        }

        if (closestDoor)
        {
            janitor = closestDoor.SpawnEnemy(EnemyBase.EnemyType.Janitor, true).GetComponent<EnemyJanitor>();
            enemyList.Add(janitor);
            
            AudioManager.instance.Play(onJanitorCalled, janitor.gameObject);

            indicator.gameObject.SetActive(true);

            return janitor.gameObject;
        }
        else
        {
            Debug.LogAssertion("EnemyManager.CallDoor called but no doors exist");
            return null;
        }
    }

    // Called by the janitor when they leave the scene (despawned)
    public void JanitorIsRemovedFromScene()
    {
        enemyList.Remove(janitor);
        janitor = null;
        UnlockDoors();
        indicator.gameObject.SetActive(false);
    }

    public void UnlockDoors()
    {
        foreach (EnemySpawnDoor door in spawnDoors)
        {
            door.Unlock();
        }
    }

    public float DistanceFromJanitor()
    {
        return Vector3.Distance(janitor.transform.position, Player.instance.transform.position);
    }

    public EnemySecurity GetClosestSecurity(Vector3 callPos)
    {
        float closestDist = float.MaxValue;
        EnemySecurity closestSecurity = null;
        foreach(EnemySecurity comp in securityList)
		{ 
			float dist = (callPos - transform.position).sqrMagnitude;
			if (dist < closestDist)
			{
				closestDist = dist;
				closestSecurity = comp;
			}
		}
        return closestSecurity;
    }

    public void NotifyNeighbors(Vector3 alerting_pos, Vector3 playerPos)
    {
        m_lastKnownPlayerPos = playerPos;
        foreach (EnemyBase enemy in enemyList)
        {
            Vector3 enemyPos = enemy.transform.position;
            if (enemy is EnemyJanitor || Vector3.Distance(enemyPos, alerting_pos) < alertNeighborRadius)
            {
                enemy.AlertToPlayer();
            }
        }
    }

    // Pause or unpause enemies
    public void CutscenePauseEnemies(bool pause)
    {
        foreach(EnemyBase enemy in enemyList)
        {
            if (enemy)
            {
                if (pause)
                {
                    enemy.PauseAI();
                }
                else
                {
                    enemy.ResumeAI();
                }
            }

        }
    }

    public void StunEnemiesInRadius(Vector3 callPos, float radius){
        foreach(EnemyBase enemy in enemyList)
		{ 
			float dist = (callPos - enemy.transform.position).sqrMagnitude;
			if (dist <= radius)
			{
				enemy.Stun();
			}
		}
    }
}
