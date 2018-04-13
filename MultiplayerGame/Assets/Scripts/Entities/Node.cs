using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node : Entity {

	[Space(10)] [Header("Node Information")]
	public int capturedPlayerId = -1;           // ConnectionId of the player that has this node captured (-1 = not captured)
	public Color capturedColor;

	public SpriteRenderer spriteRenderer_Body;
	public SpriteRenderer spriteRenderer_Heart;
	public SpriteRenderer spriteRenderer_Skin;
	public Collider2D collider;

	public List<Connection> connections;
	public List<Wall> walls;

	public float tempDistance;
	public bool isInterior;

	[System.Serializable]
	public struct Connection {
		public int type;        // What type of wall connects these two
		public Node node;       // The node this connection is with
		public Wall wall;		// The wall bridging the connection
	}

	void Start() {
		eventTakeDamage += OnTakeDamage;
		eventDie += OnDie;
		eventTakeHeal += OnTakeHeal;
		collider = GetComponent<Collider2D>();

		if (networkPerspective == NetworkPerspective.Server) {
			TriggerNodeCaptureChange(-1);
		}
	}

	void OnTakeDamage(int playerId) {
		if (isDead == true) {
			Revive();
			TriggerNodeCaptureChange(playerId);
		}
	}

	void OnTakeHeal(int playerId) {
		
	}

	void OnDie() {
		TriggerNodeCaptureChange(-1);

		// Kill all walls attached to this node
		if (networkPerspective == NetworkPerspective.Server) {
			foreach (Wall wall in walls) {
				wall.Die();
			}
		}

		walls.Clear();
		connections.Clear();
	}

	public void TriggerNodeCaptureChange (int newCapturePlayerId) {
		// Change color
		if (newCapturePlayerId < 0) { // If we are not captured

			capturedColor = ColorHub.HexToColor(ColorHub.Black);
			capturedPlayerId = newCapturePlayerId;
		} else {
			capturedPlayerId = newCapturePlayerId;
			if (networkPerspective == NetworkPerspective.Server) {
				if (server.players.ContainsKey(capturedPlayerId)) {
					ColorUtility.TryParseHtmlString("#" + server.players[capturedPlayerId].playerColor, out capturedColor);
				}
			} else {
				if (client.players.ContainsKey(capturedPlayerId)) {
					ColorUtility.TryParseHtmlString("#" + client.players[capturedPlayerId].playerColor, out capturedColor);
				}
			}
		}
	}

	void Update () {
		if (isDead == true) {
			spriteRenderer_Heart.color = Color.Lerp(spriteRenderer_Heart.color, ColorHub.HexToColor(ColorHub.White), 15 * Time.deltaTime);
			spriteRenderer_Body.color = Color.Lerp(spriteRenderer_Body.color, ColorHub.HexToColor(ColorHub.Gray), 15 * Time.deltaTime);
		} else {
			spriteRenderer_Heart.color = Color.Lerp(spriteRenderer_Heart.color, capturedColor, 15 * Time.deltaTime);
			spriteRenderer_Body.color = Color.Lerp(spriteRenderer_Body.color, ColorHub.HexToColor(ColorHub.Black), 15 * Time.deltaTime);
		}
	}

}
