using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : Entity {

	public int connectionId;

	public LayerMask nodeMask;

	public Vector3 inputMovement;
	public Vector2 inputMouse;
	public Vector3 velocity;
	float speed = 6f;

	public Vector3 desiredPosition;
	public float desiredRotation;

	public Camera cam;
	public Camera mouseCamera;

	public Transform playerSprite;

	public Rigidbody2D rigidbody;

	// Mouse Info
	public Transform aimIndicator;
	Vector3 mousePosClickLeft;
	bool isAiming;
	float aimTime;
	Vector2 aimVector;
	Vector2 curveDirection;

	// Tethering Info
	public Node tetheredNode;
	public Transform tetherCircle;

	// Prefab Info
	public GameObject prefab_Projectile;

	void Start() {
		rigidbody = GetComponent<Rigidbody2D>();

		if (GameObject.Find("[Client]")) {
			client = GameObject.Find("[Client]").GetComponent<Client>();
		}

		if (GameObject.Find("[Server]")) {
			server = GameObject.Find("[Server]").GetComponent<Server>();
			rigidbody.isKinematic = true;
		}

		eventDie += OnDie;
		eventRespawn += OnRespawn;

		if (networkPerspective == NetworkPerspective.Client) {
			cam = GameObject.Find("[Cameras]").transform.Find("Camera Main").GetComponent<Camera>();
			mouseCamera = cam.transform.Find("Mouse Camera").GetComponent<Camera>();
			aimIndicator = GameObject.Find("AimIndicator").transform;
		}
	}

	void Update () {
		if (isDead == false) {
			switch (networkPerspective) {
				case (NetworkPerspective.Client): {
						UpdateMovement_Client();
					} break;
				case (NetworkPerspective.Peer): {
					UpdateMovement_Peer();
					} break;
				case (NetworkPerspective.Server): {
						UpdateMovement_Server();
					} break;
			}
		}

		UpdateTether();

		UpdateAll();
	}
	
	void UpdateMovement_Client () {
		inputMovement = Vector3.Lerp(inputMovement, new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")), 10 * Time.deltaTime);
		if (inputMovement.magnitude > 1) {
			inputMovement = inputMovement.normalized;
		}
		inputMouse = (mouseCamera.ScreenToWorldPoint(Input.mousePosition) + new Vector3(0, 0, 1));

		rigidbody.velocity = Vector3.Lerp(rigidbody.velocity, inputMovement * speed * Mathf.Abs(Mathf.Clamp(aimTime * 2.75f, 0.0f, 0.9f) - 1), 3f * Time.deltaTime);

		//transform.position += velocity * Time.deltaTime;
		//rigidbody.velocity = Vector2.Lerp(rigidbody.velocity, velocity, 10 * Time.deltaTime);

		cam.transform.position = Vector3.Lerp(cam.transform.position, transform.position + new Vector3(0, 0, -1), 7.5f * Time.deltaTime);
		
		if (Input.GetMouseButtonDown(0)) {
			mousePosClickLeft = Input.mousePosition;
			isAiming = true;
		}

		if (Input.GetMouseButton(0)) {
			aimTime += Time.deltaTime;
			aimIndicator.transform.position = ((Vector2)mouseCamera.ScreenToWorldPoint(mousePosClickLeft) + inputMouse) / 2;
			aimIndicator.localScale = new Vector3(1, Vector2.Distance(mouseCamera.ScreenToWorldPoint(mousePosClickLeft), inputMouse) * 2, 1);

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

		if (Input.GetKeyDown(KeyCode.Space)) {
			// Change arrow
		}

		if (Input.GetKey(KeyCode.F)) {
			Screen.fullScreen = !Screen.fullScreen;
		}

		Vector2 directionLerp = Vector2.Lerp(inputMovement, -aimVector, Mathf.Clamp01(aimTime * 25));
		playerSprite.rotation = Quaternion.Lerp(playerSprite.rotation, Quaternion.Euler(0, 0, Mathf.Atan2(directionLerp.y, directionLerp.x) * Mathf.Rad2Deg) * Quaternion.Euler(0, 0, 135), 20 * Time.deltaTime);
	}

	public void SetTether (int nodeEntityId) {
		if (nodeEntityId >= 0) {
			if (networkPerspective == NetworkPerspective.Server) {
				tetheredNode = server.entities[nodeEntityId] as Node;
				server.Send_PlayerTethered(connectionId, nodeEntityId);
			} else {
				tetheredNode = client.entities[nodeEntityId] as Node;
			}
		} else {
			if (networkPerspective == NetworkPerspective.Server) {
				server.Send_PlayerTethered(connectionId, -1);
			}
			tetheredNode = null;
		}

		// Do anim
	}

	void FireProjectile () {
		//GameObject newProjectile = (GameObject)Instantiate(prefab_Projectile, transform.position, Quaternion.Euler(0, 0, (Mathf.Atan2(aimVector.y, aimVector.x) * Mathf.Rad2Deg) + 90));
		//Projectile newProjectileClass = newProjectile.GetComponent<Projectile>();
		//newProjectileClass.velocity = -aimVector * Mathf.Clamp(aimTime, 0.125f, 0.75f) * 15f;
		Vector3 projectileVelocity = -aimVector * Mathf.Clamp(aimTime, 0.125f, 0.75f) * 15f;
		client.Send_Projectile(transform.position, projectileVelocity, 0f);
	}

	void UpdateMovement_Peer () {
		// Code for manipulating the player client side when this player is one of the client's peers
		//transform.position = Vector3.Lerp(transform.position, desiredPosition, 40 * Time.deltaTime);
		Vector2 desiredMovement = (desiredPosition - transform.position);
		rigidbody.velocity = Vector2.ClampMagnitude(Vector2.Lerp(rigidbody.velocity, desiredMovement * 10, 50 * Time.deltaTime), (desiredMovement / Time.deltaTime).magnitude);
		playerSprite.transform.rotation = Quaternion.Lerp(playerSprite.transform.rotation, Quaternion.Euler(0, 0, desiredRotation), 75 * Time.deltaTime);
	}

	void UpdateMovement_Server () {
		// Code for manipulating the player server side
		transform.position = Vector3.Lerp(transform.position, desiredPosition, 40 * Time.deltaTime);
		playerSprite.transform.rotation = Quaternion.Lerp(playerSprite.transform.rotation, Quaternion.Euler(0, 0, desiredRotation), 75 * Time.deltaTime);
	}

	void UpdateAll () {

	}

	void UpdateTether () {
		if (tetheredNode != null) {
			// Set up Tether Indicator
			tetherCircle.gameObject.SetActive(true);

			// Make sure tether isn't too long
			if (networkPerspective == NetworkPerspective.Server) {
				if (Vector2.Distance(tetheredNode.transform.position, transform.position) > 5) {
					SetTether(-1);
				}
			}

			// Tether Circle
			tetherCircle.transform.position = tetheredNode.transform.position;
			tetherCircle.transform.localRotation *= Quaternion.Euler(0, 0, 10 * Time.deltaTime);
		} else {
			tetherCircle.gameObject.SetActive(false);
		}
	}

	void OnDie () {
		playerSprite.gameObject.SetActive(false);

		if (networkPerspective == NetworkPerspective.Server) {
			SetTether(-1);
			server.DestroyPlayerItems(connectionId);
		}
	}
	
	void OnRespawn () {
		playerSprite.gameObject.SetActive(true);
	}

}
