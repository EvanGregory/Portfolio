using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AnimateRigidbody : MonoBehaviour
{
	[Serializable]
	public class PositionCurves
	{ 
		public AnimationCurve xCurve;
		public float xPlaybackSpeed = 1.0f;
		public AnimationCurve yCurve;
		public float yPlaybackSpeed = 1.0f;
		public bool xExists { get { return xCurve != null && xCurve.length != 0; } }
		public bool yExists { get { return yCurve != null && yCurve.length != 0;} }
	}

	Rigidbody2D rb;
	[SerializeField] PositionCurves curves;
	
	float time = 1.0f;
	float totalTime = 0.0f;

	public bool isPaused = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

	public void PlayAnimation(PositionCurves newCurves)
	{
		isPaused = false;
		rb.bodyType = RigidbodyType2D.Dynamic;
		time = 0.0f;
		curves = newCurves;
		float xMax = curves.xExists ? curves.xCurve.keys[curves.xCurve.length-1].time : 0.0f;
		float yMax = curves.yExists ? curves.yCurve.keys[curves.yCurve.length-1].time : 0.0f;
		totalTime = Mathf.Max(xMax, yMax);
	}

    void FixedUpdate()
    {
		if (isPaused || time >= totalTime)
		{
			return;
		}

		float nextTime = time + Time.fixedDeltaTime;

		float xDiff = curves.xExists ? curves.xCurve.Evaluate(nextTime * curves.xPlaybackSpeed) - curves.xCurve.Evaluate(time * curves.xPlaybackSpeed) : 0.0f;
		float yDiff = curves.yExists ? curves.yCurve.Evaluate(nextTime * curves.yPlaybackSpeed) - curves.yCurve.Evaluate(time * curves.yPlaybackSpeed) : 0.0f;
		Vector2 offset = new(xDiff, yDiff);
		Vector2 speed = offset / Time.fixedDeltaTime;
		Vector2 worldSpeed = transform.TransformVector(speed);
		rb.linearVelocity = worldSpeed;

		time = nextTime;
    }
}
