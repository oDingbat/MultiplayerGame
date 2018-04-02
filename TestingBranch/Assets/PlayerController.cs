using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour {

	public Vector2 inputCurrent;
	public Vector2 velocityCurrent;
	public Vector2 velocityDesired;

	void Update () {
		inputCurrent = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
		velocityDesired = Vector2.Lerp(velocityDesired, inputCurrent * 10f, 10 * Time.deltaTime);
		GetComponent<Rigidbody2D>().velocity = velocityDesired;
	}

}
