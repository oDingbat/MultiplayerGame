using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public abstract class Entity : MonoBehaviour {

	[Space(10)] [Header("Entity Information")]
	public int entityId;
	public Server server;
	public Client client;
	public int healthCurrent = 10;
	public int healthMax = 10;
	public bool isDead = false;
	public NetworkPerspective networkPerspective = NetworkPerspective.Client;

	// Events
	public event Action<int> eventTakeDamage;
	public event Action<int> eventTakeHeal;
	public event Action eventDie;
	public event Action eventRevive;
	public event Action eventRespawn;

	public void TakeDamage (int damage, int playerId) {
		healthCurrent = (int)Mathf.Clamp(healthCurrent - damage, 0, healthMax);
		if (eventTakeDamage != null) {
			eventTakeDamage(playerId);
		}
		
		if (server != null) {
			server.Send_EntityTakeDamage(entityId, damage, playerId);
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

	public void TakeHeal (int heal, int playerId) {
		healthCurrent = (int)Mathf.Clamp(healthCurrent + heal, 0, healthMax);
		if (eventTakeHeal != null) {
			eventTakeHeal(playerId);
		}

		if (server != null) {
			server.Send_EntityTakeHeal(entityId, heal, playerId);
		}
	}

	public void Die () {
		if (isDead == false) {
			healthCurrent = 0;
			isDead = true;
			if (eventDie != null) { eventDie(); }

			if (server != null) {
				server.Send_EntityDie(entityId);

				if (this is PlayerController) {			// TODO: maybe move to playerController so all entities dont try it?
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

	public void Revive () {
		if (isDead == true) {
			isDead = false;
			healthCurrent = healthMax;
			if (eventRevive != null) { eventRevive(); }
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
