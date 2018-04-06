using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public abstract class Entity : MonoBehaviour {

	public int entityId;
	public Server server;

	// Health information
	public int healthCurrent = 10;
	public int healthMax = 10;
	public bool isDead = false;

	// Events
	public event Action eventTakeDamage;
	public event Action eventTakeHeal;
	public event Action eventDie;
	public event Action eventRespawn;

	public void TakeDamage (int damage) {
		healthCurrent = (int)Mathf.Clamp(healthCurrent - damage, 0, healthMax);
		if (eventTakeDamage != null) {
			eventTakeDamage();
		}

		if (server != null) {
			server.Send_EntityTakeDamage(entityId, damage);

			if (healthCurrent == 0) {
				Die();
			}
		}
	}

	public void SetHealth (int health) {
		healthCurrent = health;

		if (healthCurrent == 0) {
			Die();
		}
	}

	public void TakeHeal (int heal) {
		healthCurrent = (int)Mathf.Clamp(healthCurrent + heal, 0, healthMax);
		if (eventTakeHeal != null) {
			eventTakeHeal();
		}

		if (server != null) {
			server.Send_EntityTakeHeal(entityId, heal);
		}
	}

	public void Die () {
		if (isDead == false) {
			isDead = true;
			if (eventDie != null) {
				eventDie();
			}

			if (server != null) {
				server.Send_EntityDie(entityId);

				if (this is PlayerController) {
					StartCoroutine(DelayedRespawn());
				}
			}
		}
	}

	public IEnumerator DelayedRespawn () {
		yield return new WaitForSeconds(3);
		if (isDead == true) {
			Respawn();
		}
	}

	public void Respawn() {
		if (isDead == true) {
			isDead = false;
			healthCurrent = healthMax;
			if (eventRespawn != null) {
				eventRespawn();
			}

			if (server != null) {
				server.Send_EntityRespawn(entityId);
			}
		}
	}

}
