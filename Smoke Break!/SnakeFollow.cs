using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnakeFollow : MonoBehaviour
{
	[SerializeField] Transform followTransform;
	[SerializeField] GameObject[] bones;

	[SerializeField] float minDist = 0.5f;
	[SerializeField] float maxDist = 2.5f;
	[SerializeField] float decayRate;
	//[SerializeField] 
	float boneDist = 0.0f;

	List<Vector3> positions;

	private void Start()
	{
		positions = new(35);
	}

	private void Update ()
	{
		if (bones.Length == 0)
		{
			return;
		}

		// Insert our current position into the queue
		Vector3 posToFollow = followTransform.position;
		if (positions.Count == 0 || positions[0] != posToFollow)
		{
			positions.Insert(0, posToFollow);

			// Increase boneDist up to maxDist by the distance traveled this frame
			if (positions.Count > 1)
			{
				float increase = (positions[1] - positions[0]).magnitude / bones.Length;
				float ratio = Mathf.InverseLerp(minDist, maxDist, boneDist);
				boneDist += (1.0f - ratio) * increase;
			}
		}
		{
			// Decay boneDist down to minDist by a fixed amount
			float ratio = Mathf.InverseLerp(minDist, maxDist, boneDist);
			boneDist -= ratio * (decayRate * Time.deltaTime);
		}
		boneDist = Mathf.Clamp(boneDist, minDist, maxDist);

		// Travel through the positions array until we go back enough distance for each bone position
		int i = 1;
		Vector3 lastPos = positions[0];
		bones[0].transform.position = lastPos;
		float totalDist = 0.0f;
		int boneIndex = 1;
		for (; i < positions.Count; ++i)
		{
			float dist = Vector3.Magnitude(positions[i] - lastPos);

			totalDist += dist;

			float targetDist = boneIndex * boneDist;
			if (totalDist > targetDist)
			{
				float posRatio = (totalDist - targetDist) / dist;
				posRatio = 1.0f - posRatio;
				bones[boneIndex].transform.position = Vector3.Lerp(positions[i - 1], positions[i], posRatio);
				++boneIndex;
				if (boneIndex >= bones.Length)
				{
					break;
				}
			}
			lastPos = positions[i];
		}

		if (i < positions.Count - 1)
		{
			positions.RemoveRange(i, positions.Count - i);
		}
	}

	private void OnDrawGizmosSelected ()
	{
		Gizmos.color = Color.green;
		for (int i = 0; i < bones.Length - 1; i++)
		{
			Vector3 startPos = bones[i].transform.position;
			Vector3 endPos = bones[i + 1].transform.position;
			Gizmos.DrawSphere(startPos, 0.1f);
			Gizmos.DrawLine(startPos, endPos);
		}
	}
}
