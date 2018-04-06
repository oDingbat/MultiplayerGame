using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : Entity {

	public Client client;
	public int connectionId;

	public Vector3 inputMovement;
	public Vector2 inputMouse;
	public Vector3 velocity;
	float speed = 6f;

	public Vector3 desiredPosition;
	public float desiredRotation;

	public Camera cam;
	public Camera mouseCamera;

	public Transform playerSprite;

	public PlayerType playerType = PlayerType.Peer;						// Keeps track of what kind of playerController this is (Client = The client's playerController, Peer = not client's playerController but another peer on that server, Server = all playerControllers on the server side)
	public enum PlayerType { Client, Peer, Server }

	// Mouse Info
	public Transform aimIndicator;
	Vector3 mousePosClickLeft;
	bool isAiming;
	float aimTime;
	Vector2 aimVector;
	Vector2 curveDirection;

	// Prefab Info
	public GameObject prefab_Projectile;

	void Start() {
		if (GameObject.Find("[Client]")) {
			client = GameObject.Find("[Client]").GetComponent<Client>();
		}

		if (GameObject.Find("[Server]")) {
			server = GameObject.Find("[Server]").GetComponent<Server>();
		}

		eventDie += OnDie;
		eventRespawn += OnRespawn;

		if (playerType == PlayerType.Client) {
			cam = GameObject.Find("[Cameras]").transform.Find("Camera Main").GetComponent<Camera>();
			mouseCamera = cam.transform.Find("Mouse Camera").GetComponent<Camera>();
			aimIndicator = GameObject.Find("AimIndicator").transform;
		}
	}

	void Update () {
		if (isDead == false) {
			switch (playerType) {
				case (PlayerType.Client): {
						UpdateMovement_Client();
					} break;
				case (PlayerType.Peer): {
					UpdateMovement_Peer();
					} break;
				case (PlayerType.Server): {
						UpdateMovement_Server();
					} break;
			}
		}

		UpdateAll();
	}
	
	void UpdateMovement_Client () {
		inputMovement = Vector3.Lerp(inputMovement, new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")), 10 * Time.deltaTime);
		if (inputMovement.magnitude > 1) {
			inputMovement = inputMovement.normalized;
		}
		inputMouse = (mouseCamera.ScreenToWorldPoint(Input.mousePosition) + new Vector3(0, 0, 1));

		velocity = Vector3.Lerp(velocity, inputMovement * speed * 20 * Mathf.Abs(Mathf.Clamp(aimTime * 2.75f, 0.0f, 0.9f) - 1), 3f * Time.deltaTime);

		transform.position += velocity * Time.deltaTime;

		cam.transform.position = Vector3.Lerp(cam.transform.position, transform.position + new Vector3(0, 0, -1), 7.5f * Time.deltaTime);
		
		if (Input.GetMouseButtonDown(0)) {
			mousePosClickLeft = Input.mousePosition;
			isAiming = true;
		}

		if (Input.GetMouseButton(0)) {
			aimTime += Time.deltaTime;
			aimIndicator.transform.position = ((Vector2)mouseCamera.ScreenToWorldPoint(mousePosClickLeft) + inputMouse) / 2;
			aimIndicator.localScale = new Vector3(1, Vector2.Distance(mouseCamera.ScreenToWorldPoint(mousePosClickLeft), inputMouse) * 2, 1);

			FireProjectile();

			aimVector = (inputMouse - (Vector2)mouseCamera.ScreenToWorldPoint(mousePosClickLeft)).normalized;
			aimIndicator.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aimVector.y, aimVector.x) * Mathf.Rad2Deg + 90);
		}
		
		if (Input.GetMouseButtonUp(0)) {
			velocity += new Vector3(aimVector.x, aimVector.y, 0) * Mathf.Clamp01(aimTime) * 3.75f;

			FireProjectile();

			isAiming = false;
			aimTime = 0;
			aimIndicator.localScale = Vector3.zero;
			aimIndicator.transform.position = Vector3.zero;
		}

		Vector2 directionLerp = Vector2.Lerp(inputMovement, -aimVector, Mathf.Clamp01(aimTime * 25));
		playerSprite.rotation = Quaternion.Lerp(playerSprite.rotation, Quaternion.Euler(0, 0, Mathf.Atan2(directionLerp.y, directionLerp.x) * Mathf.Rad2Deg) * Quaternion.Euler(0, 0, 135), 20 * Time.deltaTime);
	}

	void FireProjectile () {
		//GameObject newProjectile = (GameObject)Instantiate(prefab_Projectile, transform.position, Quaternion.Euler(0, 0, (Mathf.Atan2(aimVector.y, aimVector.x) * Mathf.Rad2Deg) + 90));
		//Projectile newProjectileClass = newProjectile.GetComponent<Projectile>();
		//newProjectileClass.velocity = -aimVector * Mathf.Clamp(aimTime, 0.125f, 0.75f) * 15f;
		client.Send_Projectile(transform.position, -aimVector * Mathf.Clamp(aimTime * 50, 0.125f, 0.75f) * 15f);
	}

	void UpdateMovement_Peer () {
		// Code for manipulating the player client side when this player is one of the client's peers
		transform.position = Vector3.Lerp(transform.position, desiredPosition, 40 * Time.deltaTime);
		playerSprite.transform.rotation = Quaternion.Lerp(playerSprite.transform.rotation, Quaternion.Euler(0, 0, desiredRotation), 75 * Time.deltaTime);
	}

	void UpdateMovement_Server () {
		// Code for manipulating the player server side
		transform.position = Vector3.Lerp(transform.position, desiredPosition, 40 * Time.deltaTime);
		playerSprite.transform.rotation = Quaternion.Lerp(playerSprite.transform.rotation, Quaternion.Euler(0, 0, desiredRotation), 75 * Time.deltaTime);
	}

	void UpdateAll () {

	}

	void OnDie () {
		playerSprite.gameObject.SetActive(false);
	}
	
	void OnRespawn () {
		playerSprite.gameObject.SetActive(true);
	}

}
