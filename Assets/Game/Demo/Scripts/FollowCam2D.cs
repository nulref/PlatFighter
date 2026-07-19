using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using System.Collections;

public class FollowCam2D : MonoBehaviour
{
	public Transform target;

	public float distance = 10.0f;
	[FormerlySerializedAs("extraHeight")]
	[Tooltip("Vertical camera offset from the target. Negative values keep the target higher in frame and reveal more below.")]
	public float verticalOffset = -5.0f;

	float origDist;

	void Start () 
	{
		origDist = distance;
	}

	void FixedUpdate () 
	{
		if (target)
		{
			Keyboard keyboard = Keyboard.current;
			if (keyboard != null && keyboard.leftCtrlKey.isPressed)
				distance = origDist * 5;
			else
				distance = origDist;

			Vector3 targetPos = target.position + Vector3.up * verticalOffset;
			targetPos.z = -distance;
			transform.position -= (transform.position - targetPos) * 0.25f;
			
		}
	}

	public void SetTarget(Transform inTarget)
	{
		target = inTarget;
	}
}

