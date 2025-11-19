using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerSmokeController : SmokeContainer
{
    [Header("Player Specific")]
    [SerializeField] GameObject dashContainerPrefab;

    public const int MinSize = 0;
    public const int MaxSize = 5;

    const int numSmokeballsPerHealthChunk = 5;

    [Header("Size Controls")]
    [SerializeField] private int m_startingSize;
    [SerializeField] private int m_currentSize;

    [Header("Smoke Movement")]
    [Range(0, 1)]
    [SerializeField] float forceAdjustmentRatio;

    public int CurrentSize { get { return m_currentSize; } }

    PlayerMovement playerMovement;

    [Header("Audio")]
    [SerializeField]
    SoundProfile onTakeDamage;
    private Color defaultColor;
    [SerializeField] Color flourColor = Color.white;
    bool inFlour = false;
    [SerializeField] float FlourRate = 1.0f;

    protected override void Awake()
    {
        base.Awake();
        m_currentSize = m_startingSize; // will get overwritten if data is loaded in

        playerMovement = GetComponent<PlayerMovement>();
        defaultColor = smokeMaterialInstance.color;
    }

    // Used by Player.cs
    public void SaveState()
    {
        GetComponent<Player>().playerData.size = m_currentSize;
    }

    // Used by Player.cs
    // Loading happens after Awake but before Start
    public void LoadState()
    {
        m_currentSize = GetComponent<Player>().playerData.size;
        if (UIManager.instance && UIManager.instance.HUD != null)
            UIManager.instance.HUD.SetHP(m_currentSize, false);

        InitializeSmoke();
    }

    protected override void Start()
    {
        base.Start();

        InitializeSmoke();
    }

    public void UnpauseHP()
    {
        if (UIManager.instance && UIManager.instance.HUD != null)
            UIManager.instance.HUD.SetHP(m_currentSize, false);
    }
    public void MachineDecrease()
    {
        if (UIManager.instance && UIManager.instance.HUD != null)
            UIManager.instance.HUD.HealthDecrease(m_currentSize, false);
    }

    void InitializeSmoke()
    {
        RemoveAll();

        // Create smoke balls for every target in our initial smoke form
        int numBallsToMake = numSmokeballsPerHealthChunk * m_currentSize;
        int count = 0;
        foreach (SmokeTarget target in currentForm)
        {
            if (count >= numBallsToMake)
            {
                break;
            }
            CreateSmoke(target);
            ++count;
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!inFlour && smokeMaterialInstance.color != defaultColor)
        {
            smokeMaterialInstance.color = Color.Lerp(smokeMaterialInstance.color, defaultColor, Time.deltaTime * FlourRate);
            Player.instance.playerMovement.GetSmokeContainer().SmokeMaterial.color = smokeMaterialInstance.color;
        }
    }


    // Using a function here for script execution order purposes
    public void OnPlayerPushed(Vector3 force)
    {
        Vector3 aquiredAccel = force / playerMovement._rigidbody.mass;
        aquiredAccel *= forceAdjustmentRatio;
        if (!Mathf.Approximately(aquiredAccel.sqrMagnitude, 0.0f))
        {
            foreach (SmokeBall smoke in m_smokeBalls)
            {
                if (smoke.followTarget.rb)
                {
                    smoke.followTarget.rb.AddForce(aquiredAccel, ForceMode.Acceleration);
                }
            }
        }
    }

    public override void MergeSmoke(List<SmokeBall> otherSmoke)
    {
        base.MergeSmoke(otherSmoke);

        // Increase the player's health if the smoke we gained brought us over the amount needed
        int correctSize = CalcSizeByNumBalls();
        if (correctSize > m_currentSize && correctSize <= MaxSize)
        {


            m_currentSize = correctSize;

            if (UIManager.instance && UIManager.instance.HUD)
            {
                UIManager.instance.HUD.HealthIncrease(m_currentSize);
            }

        }



        // Set up the new smoke so it behaves physicsy until it reaches us
        foreach (SmokeBall smoke in otherSmoke)
        {
            smoke.OnMerge();
        }


    }

    public int GetRemainingSize()
    {
        return m_currentSize - MinSize;
    }

    public float GetPercentSize()
    {
        return Mathf.Clamp01((float)(m_currentSize - MinSize) / (float)(MaxSize - MinSize));
    }

    public void SetSmokeForm(string name = "Default", float lerpTime = 1.0f)
    {
        Debug.LogWarning("SetSmokeForm called on the player, the player should not use smoke forms");
        if (name == "Default")
        {
            SetSmokeForm(0, lerpTime);
        }
        else
        {
            Transform form = smokeFormsParent.transform.Find(name);
            SetSmokeForm(form.GetSiblingIndex(), lerpTime);
        }

    }

    public void EnemyDrainHealth(int amount, GameObject drainPos)
    {
        if (playerMovement.CurrentState != PlayerMovement.PlayerState.Dead)
        {
            int numLost = LoseHealth(amount, false, false);

            AudioManager.instance.Play(onTakeDamage, gameObject);

            // Do this here instead of doing it through lose health so we can pass in our own stuff
            if (numLost > 0)
            {
                RemoveSmoke(numLost, 1.0f, drainPos);
                if (UIManager.instance && UIManager.instance.HUD)
                {
                    UIManager.instance.HUD.HealthDecrease(m_currentSize, true);
                }
            }
        }
    }

    // Returns the amount of smoke it has removed
    public int LoseHealth(int amount, bool isSafe = true, bool removeSmoke = false, bool flashHealthBar = true)
    {
        if (DebugGUI.instance && DebugGUI.instance.godMode)
        {
            return 0;
        }
        if (amount < 0)
        {
            Debug.Assert(true, "PlayerSmokeController.LoseHealth called with an improper value."); // Some saftey against trying to use this to gain health
            return 0;
        }
        else if (amount == 0)
        {
            return 0;
        }

        if (flashHealthBar)
        {
            UIManager.instance.HUD.FlashRed();

        }

        int newHealth = m_currentSize - amount;
        if (isSafe)
        {
            newHealth = Math.Max(newHealth, MinSize);
        }

        m_currentSize = newHealth;



        // Option for straight up removing smoke, smoke will usually go somewhere else instead of just being removed from the player
        int smokeToRemove = m_smokeBalls.Count - newHealth * numSmokeballsPerHealthChunk;
        if (removeSmoke)
        {
            RemoveSmoke(smokeToRemove);
        }

        if (!isSafe && newHealth < MinSize)
        {
            playerMovement.KillPlayer();
        }



        return smokeToRemove;
    }

    int CalcSizeByNumBalls()
    {
        return Mathf.CeilToInt((float)m_smokeBalls.Count / numSmokeballsPerHealthChunk);
    }

    public void OnTeleportPlayer()
    {
        foreach (SmokeBall smokeBall in m_smokeBalls)
        {
            Rigidbody rb = smokeBall.GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.None;
            smokeBall.transform.position = transform.position; // Just moving them to the center of the player for now

            IEnumerator SetInterpolateModeOnNextFrame()
            {
                yield return new WaitForFixedUpdate();
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            StartCoroutine(SetInterpolateModeOnNextFrame());
        }
    }

    public void OnDashMove(int healthCost)
    {
        int numToRemove = m_smokeBalls.Count; // Remove all
        for (int i = m_smokeBalls.Count - numToRemove; i < m_smokeBalls.Count; i++)
        {
            SmokeBall smokeBall = m_smokeBalls[i];
            smokeBall.SetTarget(null, 2.0f);
            smokeBall.StartCoroutine(smokeBall.Dissipate(delayTime: 0.5f));
            smokeBall.followTarget.rb.interpolation = RigidbodyInterpolation.Interpolate;
            smokeBall.GetComponent<Rigidbody>().AddForce(5.0f * (smokeBall.transform.position - smokeCenter), ForceMode.Impulse);
        }

        GameObject dashObject = Instantiate(dashContainerPrefab);
        EffectsSmokeContainer dashContainer = dashObject.GetComponent<EffectsSmokeContainer>();
        GiveSmoke(dashContainer, numToRemove);


        LoseHealth(healthCost, flashHealthBar: false);
        if (UIManager.instance && UIManager.instance.HUD)
        {
            UIManager.instance.HUD.HealthDecrease(m_currentSize, false);
        }

    }

    public void EndDash()
    {
        for (int i = 0; i < m_currentSize * numSmokeballsPerHealthChunk; i++)
        {
            CreateSmoke(currentForm[i]);
        }
    }

    public void EnterFlour()
    {
        smokeMaterialInstance.color = flourColor;
        Player.instance.playerMovement.GetSmokeContainer().SmokeMaterial.color = flourColor;
        inFlour = true;
    }
    public void ExitFlour()
    {
        inFlour = false;

    }
}
