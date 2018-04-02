using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpritePixelPerfector : MonoBehaviour {

	Vector3 initialOffset;
	float pixelsPerUnit = 64;

	void Start () {
		initialOffset = transform.localPosition;
	}

	void Update () {
		if (transform.parent != null) {
			Vector3 perfectPos = transform.parent.position + initialOffset;
			perfectPos = new Vector3(Mathf.Round(perfectPos.x * pixelsPerUnit) / pixelsPerUnit, Mathf.Round(perfectPos.y * pixelsPerUnit) / pixelsPerUnit, perfectPos.z);
			transform.position = perfectPos;
		}
		
	}

}
