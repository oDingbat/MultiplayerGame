using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gate : Entity {

	Transform texture;
	Material personalMat;

	void Start() {
		eventDie += OnDie;
		texture.transform.Find("Texture");

		personalMat = new Material(texture.GetComponent<Renderer>().material);

		texture.GetComponent<Renderer>().material = personalMat;
	}

	public void SetTexture () {
		personalMat.SetTextureScale(0, new Vector2(1, transform.localScale.y));
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
