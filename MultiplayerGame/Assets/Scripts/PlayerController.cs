using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : Entity {

	public int connectionId;

	[Space(10)] [Header("Layer Masks")]
	public LayerMask nodeMask;

	[Space(10)] [Header("Movement Information")]
	public Vector3 inputMovement;
	public Vector2 inputMouse;
	float speed = 6f;

	public Vector3 desiredPosition;
	public float desiredRotation;

	[Space(10)] [Header("Cameras")]
	public Camera cam;
	public Camera mouseCamera;

	[Space(10)] [Header("Player Objects")]
	public Transform playerSprite;
	public Rigidbody2D rigidbody;

	[Space(10)] [Header("Mouse Information")]
	public Transform aimIndicator;
	Vector3 mousePosClickLeft;
	bool isAiming;
	float aimTime;
	Vector2 aimVector;
	Vector2 curveDirection;

	[Space(10)] [Header("Tethering Information")]
	public Node tetheredNode;
	public Transform tetherLine;
	public Transform tetherCircle;

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
					UpdateBuilding();
					} break;
			}
		}

		UpdateTether();

		UpdateAll();
	}
	
	void UpdateMovement_Client () {
		// Handle Movement input from the client
		inputMovement = Vector3.Lerp(inputMovement, new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")), 10 * Time.deltaTime);
		if (inputMovement.magnitude > 1) {
			inputMovement = inputMovement.normalized;
		}
		inputMouse = (mouseCamera.ScreenToWorldPoint(Input.mousePosition) + new Vector3(0, 0, 1));

		rigidbody.velocity = Vector3.Lerp(rigidbody.velocity, inputMovement * speed * Mathf.Abs(Mathf.Clamp(aimTime * 2.75f, 0.0f, 0.9f) - 1), 3f * Time.deltaTime);

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
			rigidbody.velocity += new Vector2(aimVector.x, aimVector.y) * Mathf.Clamp01(aimTime) * 3.75f;

			FireProjectile();

			isAiming = false;
			aimTime = 0;
			aimIndicator.localScale = Vector3.zero;
			aimIndicator.transform.position = Vector3.zero;
		}

		// Handle Arrow mode
		if (Input.GetKeyDown(KeyCode.Space)) {
			client.Send_AttemptTether();
		}

		if (Input.GetKey(KeyCode.F)) {
			Screen.fullScreen = !Screen.fullScreen;
		}

		Vector2 directionLerp = Vector2.Lerp(inputMovement, -aimVector, Mathf.Clamp01(aimTime * 25));
		playerSprite.rotation = Quaternion.Lerp(playerSprite.rotation, Quaternion.Euler(0, 0, Mathf.Atan2(directionLerp.y, directionLerp.x) * Mathf.Rad2Deg) * Quaternion.Euler(0, 0, 135), 20 * Time.deltaTime);
	}

	public void AttemptTether() {
		if (isDead == false) {
			int closestNodeId = -1;
			float closestNodeDistance = Mathf.Infinity;

			// Cycle through all nodes on the server to see if any are close enough				// TODO: We should be saving each players captured nodes and only cylce through those! Much faster.
			foreach (Node nodeCurrent in server.nodes) {
				if (nodeCurrent.capturedPlayerId == connectionId) {
					float distanceCurrent = Vector2.Distance(nodeCurrent.transform.position, transform.position);
					if (distanceCurrent < 0.75f && distanceCurrent < closestNodeDistance) {
						closestNodeDistance = distanceCurrent;
						closestNodeId = nodeCurrent.entityId;
					}
				}
			}

			if (closestNodeId == -1 || (tetheredNode != null && closestNodeId == tetheredNode.entityId)) {
				Debug.Log("derper");
				SetTether(-1);
			} else {
				SetTether(closestNodeId);
			}
		}
	}

	public void SetTether (int nodeEntityId) {
		// If server, tell the clients
		if (networkPerspective == NetworkPerspective.Server) {
			server.Send_PlayerTethered(connectionId, nodeEntityId);
		}
	
		if (nodeEntityId >= 0) {
			if (networkPerspective == NetworkPerspective.Server) {
				tetheredNode = server.entities[nodeEntityId] as Node;
			} else {
				tetheredNode = client.entities[nodeEntityId] as Node;
			}
		} else {
			tetheredNode = null;
		}

		// Do anim
	}

	void FireProjectile () {
		Vector3 projectileVelocity = -aimVector * Mathf.Clamp(aimTime, 0.125f, 0.75f) * 15f;        // TODO: This should be calculated server side to avoid hacking
		client.Send_Projectile(transform.position, projectileVelocity, 0f);
	}

	void UpdateMovement_Peer () {
		// Code for manipulating the player client side when this player is one of the client's peers
		Vector2 desiredMovement = (desiredPosition - transform.position);
		rigidbody.velocity = Vector2.ClampMagnitude(Vector2.Lerp(rigidbody.velocity, desiredMovement * 10, 50 * Time.deltaTime), (desiredMovement / Time.deltaTime).magnitude);
		playerSprite.transform.rotation = Quaternion.Lerp(playerSprite.transform.rotation, Quaternion.Euler(0, 0, desiredRotation), 75 * Time.deltaTime);
	}

	void UpdateMovement_Server () {
		// Code for manipulating the player server side
		transform.position = Vector3.Lerp(transform.position, desiredPosition, 40 * Time.deltaTime);
		playerSprite.transform.rotation = Quaternion.Lerp(playerSprite.transform.rotation, Quaternion.Euler(0, 0, desiredRotation), 75 * Time.deltaTime);
	}

	void UpdateBuilding () {
		if (tetheredNode != null) {
			RaycastHit2D hit = Physics2D.Raycast(transform.position, tetheredNode.transform.position - transform.position, Mathf.Infinity, nodeMask);
			if (hit.transform != tetheredNode.transform) {      // If we hit a node that is not the node we are tethered to
				Node hitNode = hit.transform.gameObject.GetComponent<Node>();
				if (hitNode != null) {
					if (hitNode.capturedPlayerId == connectionId) {
						server.BuildWall(tetheredNode, hitNode);
					}					

					SetTether(-1);
				}
			}
		}
	}

	void UpdateAll () {

	}

	void UpdateTether () {
		if (tetheredNode != null) {
			// Set up Tether Indicator
			tetherLine.gameObject.SetActive(true);
			tetherCircle.gameObject.SetActive(true);

			// Make sure tether isn't too long
			if (networkPerspective == NetworkPerspective.Server) {
				if (Vector2.Distance(tetheredNode.transform.position, transform.position) > 5) {
					SetTether(-1);
				}
			}

			// Tether Line
			tetherLine.transform.position = (tetheredNode.transform.position + transform.position) / 2;
			tetherLine.localScale = new Vector3(1, Vector2.Distance(tetheredNode.transform.position, transform.position) * 2, 1);
			Vector2 tetherVector = (transform.position - tetheredNode.transform.position).normalized;
			tetherLine.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(tetherVector.y, tetherVector.x) * Mathf.Rad2Deg + 90);

			// Tether Circle
			tetherCircle.transform.position = tetheredNode.transform.position;
			tetherCircle.transform.localRotation *= Quaternion.Euler(0, 0, 10 * Time.deltaTime);
		} else {
			tetherLine.gameObject.SetActive(false);
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
