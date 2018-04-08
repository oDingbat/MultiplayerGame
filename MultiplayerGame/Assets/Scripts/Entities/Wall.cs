using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wall : Entity {

	public Transform texture;
	Material personalMat;

	public string wallType;		// 0 = wall, 1 = gate

	void Start() {
		eventDie += OnDie;
	}

	public void SetTexture() {
		personalMat = new Material(texture.GetComponent<Renderer>().material);
		texture.GetComponent<Renderer>().material = personalMat;
		personalMat.SetTextureScale("_MainTex", new Vector2(1, transform.localScale.y));
	}

	void OnDie() {
		if (networkPerspective == NetworkPerspective.Server) {
			server.entities.Remove(entityId);
		} else {
			client.entities.Remove(entityId);
		}
		Destroy(this.transform.gameObject);
	}

}
