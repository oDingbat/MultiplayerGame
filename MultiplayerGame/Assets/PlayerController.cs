using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour {

	public Vector3 velocity;
	public Vector3 desiredPosition;
	public bool isClient;

	void Update () {
		if (isClient == true) {
			velocity = Vector3.Lerp(velocity, new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0), 10 * Time.deltaTime);
			transform.position += velocity * Time.deltaTime * 5;
		} else {
			transform.position = Vector3.Lerp(transform.position, desiredPosition, 10 * Time.deltaTime);
		}
	}
}
