using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour {

	public LayerMask collisionMask;

	public Vector2 velocity;
	float deceleration = 2f;
	public float curve = 0;

	public bool isBroken;
	public bool isStuck;
	public bool isDecelerated;
	public int playerId;				// The id of the player who fired this projectile (connectionId)

	public Color colorDecelerated;
	public SpriteRenderer spriteRenderer;
	int deleceratedTurnDirection;

	public bool isServerSide = false;

	public Entity parentEntity;			// The entity this projectile came from

	void Update() {
		UpdateMovement();
	}

	void UpdateMovement() {
		if (isBroken == false) {
			RaycastHit2D hit = Physics2D.Raycast(transform.position, velocity, velocity.magnitude * Time.deltaTime, collisionMask);

			if (hit) {
				transform.position = hit.point + (velocity.normalized * 0.05f);

				if (hit.transform.GetComponent<Entity>() != null) {
					Entity hitEntity = hit.transform.GetComponent<Entity>();
					if (parentEntity == null || parentEntity != hitEntity) {        // Make sure players don't damage themselves
						if (isServerSide == true) {
							if (hitEntity is Node) {
								if ((hitEntity as Node).capturedPlayerId == playerId) {
									hitEntity.TakeHeal(3, playerId);
								} else {
									hitEntity.TakeDamage(1, playerId);
								}
							} else {
								hitEntity.TakeDamage(1, playerId);
							}
						}
						if (hitEntity is Node) {
							transform.parent = hitEntity.transform;
							isStuck = true;
						}
						StartCoroutine(BreakProjectile());
					}
				}
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
