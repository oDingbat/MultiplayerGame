using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node : Entity {

	public Color colorNeutral;
	public Color colorDead;

	public SpriteRenderer spriteRenderer;
	public Collider2D collider;

	void Start () {
		eventTakeDamage += OnTakeDamage;
		eventDie += OnDie;
		spriteRenderer = GetComponent<SpriteRenderer>();
		collider = GetComponent<Collider2D>();
	}

	void OnTakeDamage() {

	}

	void OnDie () {
		collider.enabled = false;
	}

	void Update () {
		if (isDead == true) {
			spriteRenderer.color = Color.Lerp(spriteRenderer.color, colorDead, 15 * Time.deltaTime);
		}
	}

}
