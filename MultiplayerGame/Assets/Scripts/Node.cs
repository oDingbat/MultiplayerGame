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
		Debug.Log("Healed");
		if (networkPerspective == NetworkPerspective.Server) {
			if (server.players[playerId].playerController.tetheredNode != this) {
				server.players[playerId].playerController.tetheredNode = this;
				server.players[playerId].playerController.SetTether(entityId);
			}
		}
	}

	void OnDie() {
		TriggerNodeCaptureChange(-1);
		Debug.Log("Ded");
	}

	public void TriggerNodeCaptureChange (int newCapturePlayerId) {
		// Change color
		if (newCapturePlayerId < 0) { // If we are not captured
			if (networkPerspective == NetworkPerspective.Server && capturedPlayerId >= 0) {
				if (server.players[capturedPlayerId] != null) {
					server.players[capturedPlayerId].playerController.SetTether(-1);
				}
			}
			capturedColor = ColorHub.HexToColor(ColorHub.Black);
			capturedPlayerId = newCapturePlayerId;
		} else {
			capturedPlayerId = newCapturePlayerId;
			if (networkPerspective == NetworkPerspective.Server) {
				if (server.players.ContainsKey(capturedPlayerId)) {
					ColorUtility.TryParseHtmlString("#" + server.players[capturedPlayerId].playerColor, out capturedColor);
					server.players[newCapturePlayerId].playerController.SetTether(entityId);
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
