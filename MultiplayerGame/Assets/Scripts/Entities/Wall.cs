using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wall : Entity {

	public Transform texture;
	Material personalMat;

	public string wallType;     // 0 = wall, 1 = gate
	public int[] parentNodesEntityIds;

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
			(server.entities[parentNodesEntityIds[0]] as Node).connections.Remove((server.entities[parentNodesEntityIds[0]] as Node).connections.Find(x => x.node == (server.entities[parentNodesEntityIds[1]] as Node)));
			(server.entities[parentNodesEntityIds[1]] as Node).connections.Remove((server.entities[parentNodesEntityIds[1]] as Node).connections.Find(x => x.node == (server.entities[parentNodesEntityIds[0]] as Node)));
			server.entities.Remove(entityId);
		} else {
			(client.entities[parentNodesEntityIds[0]] as Node).connections.Remove((client.entities[parentNodesEntityIds[0]] as Node).connections.Find(x => x.node == (client.entities[parentNodesEntityIds[1]] as Node)));
			(client.entities[parentNodesEntityIds[1]] as Node).connections.Remove((client.entities[parentNodesEntityIds[1]] as Node).connections.Find(x => x.node == (client.entities[parentNodesEntityIds[0]] as Node)));
			client.entities.Remove(entityId);
		}
		Destroy(this.transform.gameObject);
	}

}
