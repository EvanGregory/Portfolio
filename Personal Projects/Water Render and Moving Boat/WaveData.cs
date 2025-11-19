using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveData : MonoBehaviour
{
    [Serializable] public struct Wave
    {
        public Vector2 direction;
        public float speed;
    }

    [Header("Parameters")]
    [Range(0.01f, 3.0f)]
    public float amplitude;
    [Range(0.01f, 20.0f)]
    public float period;
    [Range(0.0f, 1.0f)]
    public float changeInAmplitude = 1.0f;
    [Range(1.0f, 3.0f)]
    public float changeInPeriod = 1.0f;

    [Header("Randomizer")]
    public bool generateRandom = false;
    public int numRandomWaves = 4;
    public float initialRandomSpeed = 2.0f;
    public float speedIncrease = 2.0f;

    [Header("Current Data")]
    public List<Wave> waves;

    public Material waterMaterial;
    Material waterMatInstance;

	private void Awake()
	{
        waterMatInstance = new(waterMaterial);
        waterMatInstance.name = "Water Material Instance";
	}

	void OnEnable()
    {
        if (generateRandom)
        {
            GenerateRandomData();
        }

        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        foreach(MeshRenderer renderer in meshRenderers)
        {
            renderer.material = waterMatInstance;
        }

        if (waves == null || waves.Count == 0)
        {
            waterMatInstance.SetInteger("_NumWaves", waves.Count);
        }
        else
        {
            List<Vector4> waveDirs = new();
            List<float> waveSpeeds = new();
            foreach (Wave wave in waves)
            {
                Vector2 waveDirection = wave.direction.normalized;
                waveDirs.Add(new Vector4(waveDirection.x, waveDirection.y, 0.0f, 0.0f));
                waveSpeeds.Add(wave.speed);
            }

            Material mat = waterMatInstance;
            mat.SetInteger("_NumWaves", waves.Count);
            mat.SetVectorArray("_WaveDirs", waveDirs);
            mat.SetFloatArray("_WaveSpeeds", waveSpeeds);
            mat.SetFloat("_WaveAmplitude", amplitude);
            mat.SetFloat("_WavePeriod", period);
            mat.SetFloat("_WaveDeltaAmplitude", changeInAmplitude);
            mat.SetFloat("_WaveDeltaPeriod", changeInPeriod);
        }
    }

    void GenerateRandomData()
    {
        waves.Clear();

        float currentSpeed = initialRandomSpeed;
        for (int i = 0; i < numRandomWaves; i++)
        {
			Wave wave = new()
			{
				direction = UnityEngine.Random.insideUnitCircle.normalized,
				speed = currentSpeed
			};
			waves.Add(wave);

            currentSpeed += UnityEngine.Random.Range(speedIncrease * 0.75f, speedIncrease * 1.25f);
        }
    }

    public float CalcWaterHeight(Vector3 position)
    {
        if (waves == null || waves.Count == 0)
        {
            return transform.position.y;
        }

        // Keep these values in sync with the ones in the water shader
        const float magicShift = 0.42f;
        const float eConst = 2.7182818284f;

        Vector2 xzPos = new(position.x, position.z);

        float waveValue = 0.0f;
        Vector2 prevDeriv = Vector2.zero;
        float currentPeriod = period;
        float currentAmplitude = amplitude;
        foreach (Wave wave in waves)
        {
            float temp = Vector2.Dot(xzPos + new Vector2(Mathf.Clamp01(prevDeriv.x / 5.0f), Mathf.Clamp01(prevDeriv.y / 5.0f)), wave.direction) * currentPeriod + Time.timeSinceLevelLoad * wave.speed;
            float sineVal = Mathf.Sin(temp) * currentAmplitude;
            float ePower = Mathf.Pow(eConst, sineVal - 1.0f);
            waveValue += ePower;

            float cosVal = Mathf.Cos(temp) * currentAmplitude;
            float slope = ePower * cosVal;
            prevDeriv = wave.direction * slope;

            currentAmplitude = currentAmplitude * changeInAmplitude;
            currentPeriod = currentPeriod * changeInPeriod;
        }

        return transform.position.y + (waveValue - magicShift * waves.Count);
    }
}
