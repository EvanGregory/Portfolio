using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sail : MonoBehaviour
{
    [Header("Parameters")]
    [Tooltip("The 'size' of the sail. Compared with the size of other sails to determine the average sail fullness")]
    [SerializeField] float size;
    float fullness = 0;

    [Header("Internals")]
    [SerializeField] Transform model;
    [SerializeField] Transform raisedTransform;
    [SerializeField] Transform loweredTransform;

    public float SailFullness { set { fullness = value; }}

    void Update()
    {
        model.localPosition = Vector3.Lerp(raisedTransform.localPosition, loweredTransform.localPosition, fullness);
        model.localScale = Vector3.Lerp(raisedTransform.localScale, loweredTransform.localScale, fullness);
    }

    public static float CalcAverageFullness(Sail[] sails)
    {
        if (sails.Length == 0)
        {
            return 0.0f;
        }

        float totalRatio = 0.0f;
        float totalSize = 0.0f;
        foreach (Sail sail in sails)
        {
            totalRatio += sail.fullness * sail.size;
            totalSize += sail.size;
        }
        return totalRatio / totalSize;
    }
}
