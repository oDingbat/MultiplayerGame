using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wall : Entity {

	void Start () {
		eventDie += OnDie;
	}

	void OnDie () {
		if (networkPerspective == NetworkPerspective.Server) {
			server.entities.Remove(entityId);
		} else {
			client.entities.Remove(entityId);
		}
		Destroy(this.transform.gameObject);
	}

}
