using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Tester : MonoBehaviour {

	public Text text;
	public Transform pointA;
	public Transform pointB;
	public Transform pointC;

	void Update() {
		Vector2 v1 = (pointB.transform.position - pointA.transform.position).normalized;
		Vector2 v2 = (pointC.transform.position - pointB.transform.position).normalized;

		float dot = v1.x * v2.x + v1.y * v2.y;
		float det = v1.x * v2.y - v1.y * v2.x;

		float angle = Mathf.Atan2(det, dot);

		Debug.Log("dot (" + dot + ") det (" + det + ") angle (" + angle + ")");

	}

}
