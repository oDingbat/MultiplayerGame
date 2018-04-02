using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour {

	public LayerMask collisionMask;

	public Vector2 velocity;
	float deceleration = 2f;
	float curve = 0;

	public bool isBroken;
	public bool isStuck;
	public bool isDecelerated;

	public Color colorDecelerated;
	public SpriteRenderer spriteRenderer;
	int deleceratedTurnDirection;

	void Update() {
		UpdateMovement();
	}

	void UpdateMovement() {
		if (isBroken == false) {
			RaycastHit2D hit = Physics2D.Raycast(transform.position, velocity, velocity.magnitude * Time.deltaTime, collisionMask);

			if (hit) {
				transform.position = hit.point;
				isStuck = true;
				if (hit.transform.GetComponent<Entity>() != null) {
					Entity hitEntity = hit.transform.GetComponent<Entity>();
					hitEntity.TakeDamage(1);
					if (hitEntity is Node) {
						transform.parent = hitEntity.transform;
					}
				}
				StartCoroutine(BreakProjectile());
			}

			if (velocity.magnitude < 1) {
				isDecelerated = true;
				StartCoroutine(BreakProjectile());
			}
		}

		if (isStuck == false) {
			transform.position += (Vector3)(velocity * Time.deltaTime);
			velocity = velocity.normalized * (Mathf.Clamp(velocity.magnitude - (deceleration * Time.deltaTime), 0, Mathf.Infinity));
			velocity = Quaternion.Euler(0, 0, (float)curve * Time.deltaTime) * velocity;
		}
		
		if (isDecelerated) {
			spriteRenderer.color = Color.Lerp(spriteRenderer.color, colorDecelerated, 10 * Time.deltaTime);
			velocity = Quaternion.Euler(0, 0, (float)deleceratedTurnDirection * 100 * Time.deltaTime) * velocity;
		}

		transform.rotation = Quaternion.Euler(0, 0, (Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + -90));
	}

	IEnumerator BreakProjectile () {
		Debug.Log("Break");
		if (isBroken == false) {
			isBroken = true;
			if (isDecelerated == true) {
				deleceratedTurnDirection = (Random.Range(0f, 1f) > 0.5f ? -1 : 1);
				yield return new WaitForSeconds(0.25f);
			}
			Destroy(this);
		}
	}
	

}
