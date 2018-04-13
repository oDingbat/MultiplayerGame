using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class Server : MonoBehaviour {

	// Server Information
	private const int MAX_CONNECTION = 100;     // The max number of players allowed on the server
	private int port = 3333;                    // The port number
	private int hostId;                         // The Id of our host
	private int reliableChannel;                // Channel for sending reliable information
	private int unreliableChannel;              // Channel for sending unreliable information
	private int reliableFragmentedSequencedChannel;     // Channel for sending sequenced fragmented reliable information
	private int reliableSequencedChannel;		// Channel for sending sequenced reliable information
	private byte error;                         // Byte used to save errors returned by NetworkTransport.Receive
	private float tickRate = 64;                // The rate at which information is recieved and sent to and from the clients
	private string versionNumber = "0.1.13";		// The version number currently used by the server

	// Connection booleans
	private bool isStarted = false;
	
	// All clients
	public Dictionary<int, Player> players = new Dictionary<int, Player>();         // List of players
	public Dictionary<int, Entity> entities = new Dictionary<int, Entity>();
	int entityIdIterationCurrent = 0;

	public List<Node> nodes = new List<Node>();
	public LayerMask nodeMask;

	// Perimeter Calculation Variables
	public List<Node> nodesPerimeter = new List<Node>();       // The perimeter nodes
	public List<Node> nodesBlacklisted = new List<Node>();     // All nodes which are blacklisted from being part of the perimeter (ie: hanging nodes, encapsulated nodes)
	public List<Node> nodesPossible = new List<Node>();
	public float largestArea = 0;                              // The largest area currently found

	public Transform entitiesContainer;

	// Prefabs
	public GameObject prefab_Player;
	public GameObject prefab_Projectile;
	public GameObject prefab_Wall;
	public GameObject prefab_Gate;
	public GameObject prefab_CaptureRegion;

	// Mats
	public Material captureRegionMaterialDefault;

	private void InitializeServer() {
		// Initialize Server
		UnityEngine.Debug.Log("Attempting to initialize server...");

		NetworkTransport.Init();    // Initialize NetworkTransport
		ConnectionConfig newConnectionConfig = new ConnectionConfig();

		// Setup channels
		reliableChannel = newConnectionConfig.AddChannel(QosType.Reliable);
		unreliableChannel = newConnectionConfig.AddChannel(QosType.Unreliable);
		reliableFragmentedSequencedChannel = newConnectionConfig.AddChannel(QosType.ReliableFragmentedSequenced);
		reliableSequencedChannel = newConnectionConfig.AddChannel(QosType.ReliableSequenced);

		HostTopology topo = new HostTopology(newConnectionConfig, MAX_CONNECTION);      // Setup topology

		hostId = NetworkTransport.AddHost(topo, port);
		//webHostId = NetworkTransport.AddWebsocketHost(topo, port, null);

		isStarted = true;
		UnityEngine.Debug.Log("Server initialized successfully!");
	}

	private void Start () {
		InitializeServer();
		GetInitialEntities();
		StartCoroutine(TickUpdate());
	}
	
	private void GetInitialEntities () {
		// Find entities already created in the entities container
		if (GameObject.Find("[Entities]")) {
			entitiesContainer = GameObject.Find("[Entities]").transform;
			foreach (Transform entityTransform in entitiesContainer) {
				Entity entity = entityTransform.GetComponent<Entity>();
				if (entity != null) {
					entities.Add(entityIdIterationCurrent, entity);
					entity.entityId = entityIdIterationCurrent;
					entity.server = this;
					entity.networkPerspective = NetworkPerspective.Server;
					entityIdIterationCurrent++;

					if (entity is Node) {
						nodes.Add(entity as Node);
					}

				}
			}
		} else {
			entitiesContainer = new GameObject("[Entities]").transform;
		}

		// Create additional entities

	}

		// Update Methods
	IEnumerator TickUpdate () {
		while (true) {
			//UnityEngine.Debug.Log(players.Count);
			UpdateRecieve();
			UpdateSend();
			yield return new WaitForSeconds(1 / tickRate);
		}
	}

	private void UpdateSend () {
		Send_PlayerPosAndRot ();
	}

	private void UpdateRecieve() {
		if (isStarted == true) {            // Make sure the server is setup before attempting to receive information
			int recHostId;
			int connectionId;
			int channelId;
			byte[] recBuffer = new byte[32000];
			int dataSize;
			byte error;
			NetworkEventType recData = NetworkEventType.Nothing;
			do {    // Do While ensures that we process all of the sent messages each tick
				recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, recBuffer.Length, out dataSize, out error);
				switch (recData) {
					case NetworkEventType.ConnectEvent:
						UnityEngine.Debug.Log("Player " + connectionId + " has connected");
						OnConnection(connectionId);
						break;
					case NetworkEventType.DataEvent:
						ParseData(connectionId, channelId, recBuffer, dataSize);
						break;
					case NetworkEventType.DisconnectEvent:
						UnityEngine.Debug.Log("Player " + connectionId + " has disconnected");
						OnDisconnection(connectionId);
						break;
				}
			} while (recData != NetworkEventType.Nothing);
		}
	}

	private void ParseData (int connectionId, int channelId, byte[] recBuffer, int dataSize) {
		string data = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
		//UnityEngine.Debug.Log("Recieving from " + connectionId + " : " + data);

		string[] splitData = data.Split('|');

		if (splitData.Length > 0) {		// Make sure that the there is any split Data
			switch (splitData[0]) {

				case "MyInfo":
					Receive_MyInfo(connectionId, splitData);
					break;

				case "MyPosAndRot":
					Receive_MyPosAndRot(connectionId, splitData);
					break;

				case "FireProjectile":
					Receive_FireProjectile(connectionId, splitData);
					break;

				case "AttemptTether":
					Receive_AttemptTether(connectionId, splitData);
					break;

				case "AttemptChangeBuildType":
					Receive_AttemptChangeBuildType(connectionId, splitData);
					break;
			}
		}
	}

		// Connection/Disconnection Methods
	private void OnConnection (int connectionId) {
		// Add player to list of players
		Player newPlayer = new Player();
		newPlayer.connectionId = connectionId;
		newPlayer.playerName = "temp";
		newPlayer.playerColor = ColorHub.GetRandomPlayerColor();
		newPlayer.captureRegions = new List<CaptureRegion>();
		players.Add(connectionId, newPlayer);
		
		// Spawn the player
		newPlayer.playerGameObject = (GameObject)Instantiate(prefab_Player);
		newPlayer.playerController = newPlayer.playerGameObject.GetComponent<PlayerController>();
		newPlayer.playerController.networkPerspective = NetworkPerspective.Server;                 // Set the playerType to Server as to use the server specific code
		Color newColor = Color.black;
		ColorUtility.TryParseHtmlString("#" + newPlayer.playerColor, out newColor);
		newPlayer.playerController.playerSprite.GetComponent<SpriteRenderer>().color = newColor;
		newPlayer.playerController.tetherCircle.GetComponent<SpriteRenderer>().color = Color.Lerp(ColorHub.HexToColor(ColorHub.White), newColor, 0.5f);
		newPlayer.captureRegionMaterial = new Material(captureRegionMaterialDefault);
		newPlayer.captureRegionMaterial.color = Color.Lerp(ColorHub.HexToColor(ColorHub.White), newColor, 0.25f);
		newPlayer.playerController.connectionId = connectionId;
		newPlayer.playerController.server = this;

		// Add playerController to entities list
		entities.Add(entityIdIterationCurrent, newPlayer.playerController as Entity);		// Added the playerController to our entities dictionary
		newPlayer.playerController.entityId = entityIdIterationCurrent;			// Tell the playerController what it's entityId is
		entityIdIterationCurrent++;		// Iterate the entityIdNumber

		// When player joins serer, tell them their Id
		// Reqest player's info
		string msg = "AskInfo|" + connectionId + "|";
		foreach (KeyValuePair<int, Player> playerAndId in players) {
			Player player = playerAndId.Value;
			msg += player.playerName + "%" + player.connectionId + "%" + player.playerController.entityId + "%" + player.playerColor + "|";
		}
		msg = msg.Trim('|');

		// example: ASKNAME|3|DAVE%1|MICHAEL%2|TEMP%3
		
		Send(msg, reliableChannel, connectionId);
	}

	public void CreateEntity(Entity entity) {
		// Set basic entity values
		entity.entityId = entityIdIterationCurrent;
		entity.server = this;
		entity.networkPerspective = NetworkPerspective.Server;

		entities.Add(entityIdIterationCurrent, entity);
		entityIdIterationCurrent++;

		string newMessage = "CreateEntity|";

		// Get Entity Values
		int entityId = entity.entityId;
		string entityTypeName = entity.GetType().ToString();			// TODO: we could send this over as a single byte and then use that to figure out which type it is from an enum?
		int entityHealth = entity.healthCurrent;
		Vector2 entityPos = entity.transform.position;					// TODO: we can clamp the length of this to the neares 10th place or seomthing
		float entityScale = entity.transform.localScale.y;
		float entityRot = entity.transform.localEulerAngles.z;				// TODO: we can clamp this to the nearest degree

		string entitySpecificInfo = "_";

		if (entity is Node) {
			entitySpecificInfo = (entity as Node).capturedPlayerId.ToString();
		} else if (entity is Wall) {
			Wall entityAsWall = (entity as Wall);
			entitySpecificInfo = entityAsWall.wallType + "$" + entityAsWall.parentNodesEntityIds[0] + "$" + entityAsWall.parentNodesEntityIds[1];
		}

		// Entity initialization structure : EntityId|EntityTypeName|EntityHealth|EntityPosX|EntityPosY|EntityScale|EntityRot|EntitySpecificInfo
		newMessage += entityId + "|" + entityTypeName + "|" + entityHealth + "|" + entityPos.x + "|" + entityPos.y + "|" + entityScale + "|" + entityRot + "|" + entitySpecificInfo;

		UnityEngine.Debug.Log(newMessage);

		Send(newMessage, reliableChannel, players);
	}

	private void OnDisconnection (int connectionId) {
		// Remove this player from our client list
		Destroy(players[connectionId].playerGameObject);
		players.Remove(connectionId);

		// Tell everybody else that somebody has disconnected
		Send("PlayerDisconnected|" + connectionId, reliableChannel, players);
		DestroyPlayerItems(connectionId);
		
	}

	public void DestroyPlayerItems (int connectionId) {
		foreach (Node node in nodes) {
			if (node.capturedPlayerId == connectionId) {
				node.Die();
			}
		}
	}

	public void BuildWall (Node nodeA, Node nodeB, int buildType) {
		if (nodeA.connections.Any(x => x.node == nodeB) == false) {		// Check if any connections contain nodeB
			// Find out if this build completes a circuit
			// Ie: If we go through all of nodeA's connected nodes, do we eventually come to nodeB?
			
			Vector2 nodeMidpoint = (nodeA.transform.position + nodeB.transform.position) / 2;
			Vector2 nodeABDirection = nodeB.transform.position - nodeA.transform.position;

			GameObject newWall = null;
			Wall newWallObject = null;

			// Check if this wall completes the circuit
			
			if (buildType == 1) {
				newWall = (GameObject)Instantiate(prefab_Gate, nodeMidpoint, Quaternion.Euler(0, 0, Mathf.Atan2(nodeABDirection.y, nodeABDirection.x) * Mathf.Rad2Deg + 90));
				newWallObject = newWall.GetComponent<Wall>();
				newWallObject.wallType = "1";
			} else {
				nodesPerimeter.Clear();
				bool completesCircuit = CheckIfCircuitCompletes(nodeA, nodeB, null);
				if (completesCircuit == true) {
					newWall = (GameObject)Instantiate(prefab_Gate, nodeMidpoint, Quaternion.Euler(0, 0, Mathf.Atan2(nodeABDirection.y, nodeABDirection.x) * Mathf.Rad2Deg + 90));
					newWallObject = newWall.GetComponent<Wall>();
					newWallObject.wallType = "1";
				} else {
					newWall = (GameObject)Instantiate(prefab_Wall, nodeMidpoint, Quaternion.Euler(0, 0, Mathf.Atan2(nodeABDirection.y, nodeABDirection.x) * Mathf.Rad2Deg + 90));
					newWallObject = newWall.GetComponent<Wall>();
					newWallObject.wallType = "0";
				}
			}

			// Create Capture Region
			Stopwatch newStopwatch = new Stopwatch();

			newStopwatch.Start();
			if (nodeB.connections.Count > 0) {
				FindPerimeter(nodeA, nodeB);
			}
			newStopwatch.Stop();
			UnityEngine.Debug.Log("ElapsedTime - FindPerimeter: " + newStopwatch.ElapsedMilliseconds+ " milliseconds");

			if (nodesPerimeter.Count > 2) {
				CreateCaptureRegion(nodesPerimeter, nodeA.capturedPlayerId);
			}

			Node.Connection nodeConnectionA = new Node.Connection();
			nodeConnectionA.node = nodeB;
			nodeConnectionA.type = int.Parse(newWallObject.wallType);
			nodeConnectionA.wall = newWallObject;
			nodeA.connections.Add(nodeConnectionA);

			Node.Connection nodeConnectionB = new Node.Connection();
			nodeConnectionB.node = nodeA;
			nodeConnectionB.type = int.Parse(newWallObject.wallType);
			nodeConnectionB.wall = newWallObject;
			nodeB.connections.Add(nodeConnectionB);

			newWall.transform.localScale = new Vector3(1, Vector2.Distance(nodeA.transform.position, nodeB.transform.position), 1);
			
			newWallObject.SetTexture();
			newWallObject.parentNodesEntityIds = new int[] { nodeA.entityId, nodeB.entityId };

			nodeA.walls.Add(newWallObject);
			nodeB.walls.Add(newWallObject);

			
			// Add the new entity to the entities dictionary and tell all clients to add the new entity
			CreateEntity(newWallObject);
		}
	}

	public void DestroyWall (Wall wall) {

	}

	private bool CheckIfCircuitCompletes (Node nodeA, Node nodeB, List<Node> nodesChecked) {

		//nodesPerimeter.Add(nodeA);

		foreach (Node.Connection connectionNext in nodeA.connections) {
			Node nodeNext = connectionNext.node;
			int typeNext = connectionNext.type;
			if (typeNext != 1) {		// Make sure this connection is not a gate
				if (nodesChecked == null || nodesChecked.Contains(nodeNext) == false) {
					if (nodeNext == nodeB) {
						//nodesPerimeter.Add(nodeNext);
						return true;        // COMPLETES!
					} else {
						List<Node> nodesCheckedPlusNodeA = (nodesChecked == null ? new List<Node>() : nodesChecked);
						nodesCheckedPlusNodeA.Add(nodeA);
						if (CheckIfCircuitCompletes(nodeNext, nodeB, nodesCheckedPlusNodeA)) {      // Stack overflow // TODO: simply this and next line
							return true;
						}
					}
				}
			}
		}

		//nodesPerimeter.Remove(nodeA);
		return false;
	}
	
	private void FindPerimeter (Node nodeA, Node nodeB) {
		nodesPerimeter = new List<Node>();
		nodesBlacklisted = new List<Node>();
		nodesPossible = new List<Node>();
		largestArea = 0;
		
		FindPerimeter_A(nodeA, nodeB, new List<Node>());
	}

	private void FindPerimeter_A (Node nodeA, Node nodeB, List<Node> newNodesPerimeter) {
		newNodesPerimeter.Add(nodeA);

		List<Node> sortedNodes = new List<Node>();		// List of connectedNodes which are later sorted from greatest to least distance from nodeB

		foreach (Node.Connection connectionCurrent in nodeA.connections) {
			Node nodeNext = connectionCurrent.node;
			if (nodeNext == nodeB) {        // Success!
				List<Node> finalNodesPerimeter = new List<Node> (newNodesPerimeter);
				finalNodesPerimeter.Add(nodeB);

				string orderHelper = "(";
				for (int i = 0; i < finalNodesPerimeter.Count; i++) {
					orderHelper += finalNodesPerimeter[i].entityId + " ";
				}
				orderHelper += ")";

				//UnityEngine.Debug.Log("Success " + finalNodesPerimeter.Count + " " + orderHelper);
				TestPerimeter(finalNodesPerimeter);
			} else {                        // Fail! Test nodeNext's connections
				if (nodeNext.isInterior == false && newNodesPerimeter.Contains(nodeNext) == false && nodesBlacklisted.Contains(nodeNext) == false) {        // Don't path over somewhere we've been already (anti-stackoverflow)
					float nodeNextDistance = Vector2.Distance(nodeNext.transform.position, nodeB.transform.position);
					nodeNext.tempDistance = nodeNextDistance;
					sortedNodes.Add(nodeNext);
					if (nodesPossible.Contains(nodeNext) == false) {
						nodesPossible.Add(nodeNext);
					}
				}
			}
		}

		// Sort the nodes
		sortedNodes = sortedNodes.OrderByDescending(o => o.tempDistance).ToList();

		// Go through each sorted node
		for (int i = 0; i < sortedNodes.Count; i++) {      // TODO: foreach cleaner?
			Node nodeSorted = sortedNodes[i];
			if (nodesBlacklisted.Contains(nodeSorted) == false) {       // Make sure this node isn't blacklisted
				FindPerimeter_A(nodeSorted, nodeB, new List<Node>(newNodesPerimeter));
			}
		}
	}

	private void TestPerimeter (List<Node> perimeterNodes) {
		float newArea = GetArea(perimeterNodes);
		if (newArea > largestArea) {
			// Success
			largestArea = newArea;
			nodesPerimeter = perimeterNodes;

			// Take all possibleNodes that are inside of this polygon and blacklist them
			for (int i = 0; i < nodesPossible.Count; i++) {
				Node nodePossible = nodesPossible[i];
				if (perimeterNodes.Contains(nodePossible) == false && IsPointInPolygon(perimeterNodes, nodePossible.transform.position)) {
					nodesBlacklisted.Add(nodePossible);
					nodesPossible.Remove(nodePossible);
					UnityEngine.Debug.Log("BLACKLIST THAT BITCH!");
					i--;
				}
			}

		}
	}

	private float GetArea(List<Node> nodes) {
		int n = nodes.Count;
		float A = 0.0f;
		for (int p = n - 1, q = 0; q < n; p = q++) {
			Vector2 pval = nodes[p].transform.position;
			Vector2 qval = nodes[q].transform.position;
			A += pval.x * qval.y - qval.x * pval.y;
		}
		return Mathf.Abs(A * 0.5f);
	}

	private float GetArea(List<Vector2> points) {
		int n = points.Count;
		float A = 0.0f;
		for (int p = n - 1, q = 0; q < n; p = q++) {
			Vector2 pval = points[p];
			Vector2 qval = points[q];
			A += pval.x * qval.y - qval.x * pval.y;
		}
		return Mathf.Abs(A * 0.5f);
	}

	private void CreateCaptureRegion (List<Node> perimeterNodes, int playerId) {
		// Create Vector2 vertices from perimeter nodes
		List<Vector2> vertices2D = new List<Vector2>();
		foreach (Node node in perimeterNodes) {
			vertices2D.Add(node.transform.position);
		}

		// Remove any capture regions that this new capture region will encapsulate
		for (int j = 0; j < players[playerId].captureRegions.Count; j++) {				// Check every capture region the player has
			bool doesEncapsulate = true;
			CaptureRegion captureRegionCurrent = players[playerId].captureRegions[j];
			for (int c = 0; c < captureRegionCurrent.perimeterPoints.Count; c++) {
				if ((vertices2D.Contains(captureRegionCurrent.perimeterPoints[c]) == false && IsPointInPolygon(vertices2D, captureRegionCurrent.perimeterPoints[c]) == false) || GetArea(perimeterNodes) < GetArea(captureRegionCurrent.perimeterPoints)) {
					doesEncapsulate = false;
					break;
				}
			}

			if (doesEncapsulate == true) {
				// Remove captureRegion
				//UnityEngine.Debug.Log("Destroy Old Capture Region (" + GetArea(perimeterNodes) + ") - (" + GetArea(captureRegionCurrent.perimeterPoints) + ")");
				Destroy(captureRegionCurrent.gameObject);
				players[playerId].captureRegions.RemoveAt(j);
				j--;
			}
		}

		
		// Check to see if this new region is redundant (already inside of another region)
		bool newRegionRedundant = false;
		for (int j = 0; j < players[playerId].captureRegions.Count; j++) {              // Check every capture region the player has
			CaptureRegion captureRegionCurrent = players[playerId].captureRegions[j];
			bool currentRegionEncapsulates = true;
			
			// Check each captureRegion created by the player to see if it encapsulates the new captureRegion
			for (int c = 0; c < vertices2D.Count; c++) {
				if (captureRegionCurrent.perimeterPoints.Contains(vertices2D[c]) == false) {       // If the currentCaptureRegion doesn't contain a vertice
					if (IsPointInPolygon(captureRegionCurrent.perimeterPoints, vertices2D[c]) == false) {
						currentRegionEncapsulates = false;
						break;
					}
				}
			}

			if (currentRegionEncapsulates == true) {
				UnityEngine.Debug.Log("Cancel New Capture Region");
				newRegionRedundant = true;
				break;
			}
		}

		if (players[playerId].captureRegions != null && newRegionRedundant == false) {
			// Create new CaptureRegion
			CaptureRegion newCaptureRegion = GameObject.Instantiate(prefab_CaptureRegion, Vector3.zero, Quaternion.identity).GetComponent<CaptureRegion>();
			players[playerId].captureRegions.Add(newCaptureRegion);
			newCaptureRegion.perimeterPoints = vertices2D;
			newCaptureRegion.regionRenderer.material = players[playerId].captureRegionMaterial;

			// Use the triangulator to get indices for creating triangles
			Triangulator tr = new Triangulator(vertices2D.ToArray());
			int[] indices = tr.Triangulate();

			// Create the Vector3 vertices							// TODO: can we remove this step?
			Vector3[] vertices = new Vector3[vertices2D.Count];
			for (int i = 0; i < vertices.Length; i++) {
				vertices[i] = new Vector3(vertices2D[i].x, vertices2D[i].y, 0);
			}

			// Create the mesh
			Mesh newMesh = new Mesh();
			newMesh.vertices = vertices;
			newMesh.triangles = indices;
			newMesh.RecalculateNormals();
			newMesh.RecalculateBounds();

			// Apply the new mesh to the new CaptureRegion
			newCaptureRegion.meshFilter.mesh = newMesh;
			newCaptureRegion.InitializePolygonCollider();

			// Set all Nodes within the newCaptureRegion to interior unless they are part of the perimeter
			ContactFilter2D newContactFilter = new ContactFilter2D();
			newContactFilter.layerMask = nodeMask;
			newContactFilter.useLayerMask = true;
			Collider2D[] hitNodes = new Collider2D[9999];							// TODO: holy garbage batman
			Physics2D.OverlapCollider(newCaptureRegion.polygonCollider, newContactFilter, hitNodes);

			UnityEngine.Debug.Log(hitNodes.Length);

			hitNodes = hitNodes.Where(n => n != null).ToArray();

			foreach (Collider2D col in hitNodes) {
				Node colNode = col.transform.GetComponent<Node>();
				if (colNode != null && perimeterNodes.Contains(colNode) == false) {
					UnityEngine.Debug.Log("Yuppers!");
					colNode.isInterior = true;
				}
			}
		}
	}

	private bool IsPointInPolygon (List<Vector2> polygon, Vector2 testPoint) {
        bool result = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++) {
            if (polygon[i].y < testPoint.y && polygon[j].y >= testPoint.y || polygon[j].y < testPoint.y && polygon[i].y >= testPoint.y) {
                if (polygon[i].x + (testPoint.y - polygon[i].y) / (polygon[j].y - polygon[i].y) * (polygon[j].x - polygon[i].x) < testPoint.x) {
                    result = !result;
                }
            }
            j = i;
        }
        return result;
    }

	private bool IsPointInPolygon(List<Node> polygon, Vector2 testPoint) {
		bool result = false;
		int j = polygon.Count - 1;
		for (int i = 0; i < polygon.Count; i++) {
			if (polygon[i].transform.position.y < testPoint.y && polygon[j].transform.position.y >= testPoint.y || polygon[j].transform.position.y < testPoint.y && polygon[i].transform.position.y >= testPoint.y) {
				if (polygon[i].transform.position.x + (testPoint.y - polygon[i].transform.position.y) / (polygon[j].transform.position.y - polygon[i].transform.position.y) * (polygon[j].transform.position.x - polygon[i].transform.position.x) < testPoint.x) {
					result = !result;
				}
			}
			j = i;
		}
		return result;
	}

	// Send Methods
	private void Send_PlayerPosAndRot () {
		string message = "PlayerPositions|";

		foreach (KeyValuePair<int, Player> playerAndId in players) {
			Player player = playerAndId.Value;
			message += player.connectionId + "%" + player.playerGameObject.transform.position.x + "%" + player.playerGameObject.transform.position.y + "%" + player.playerController.playerSprite.eulerAngles.z + "|";
		}
		
		message = message.Trim('|');      // Trim the final bar

		Send(message, unreliableChannel, players);
	}

	public void Send_EntityTakeDamage (int entityId, int damage, int playerId) {
		string message = "EntityTakeDamage|" + entityId + "|" + damage + "|" + playerId;
		Send(message, reliableChannel, players);
	}

	public void Send_EntityTakeHeal(int entityId, int heal, int playerId) {
		string message = "EntityTakeHeal|" + entityId + "|" + heal + "|" + playerId;
		Send(message, reliableChannel, players);
	}

	public void Send_EntityDie(int entityId) {
		string message = "EntityDie|" + entityId;
		Send(message, reliableChannel, players);
	}

	public void Send_EntityRespawn(int entityId) {
		string message = "EntityRespawn|" + entityId;
		Send(message, reliableChannel, players);
	}

	public void Send_NodeCaptureChange (int nodeEntityId, int capturePlayerId) {
		// This method sends a message to all players that a node (nodeEntityId) has been captured by a player (capturePlayerId)
		string message = "NodeCaptureChange|" + nodeEntityId + "|" + capturePlayerId;
		Send(message, reliableChannel, players);
	}

	public void Send_InitializeEntities (int connectionId) {
		string newMessage = "InitializeEntities|";

		foreach (KeyValuePair<int, Entity> entityAndId in entities) {
			if ((entityAndId.Value is PlayerController) == false) {     // Don't send player entites
				
				Entity entity = entityAndId.Value;
			
				// Get Entity Values
				int		entityId = entity.entityId;
				string	entityTypeName = entity.GetType().ToString();
				int		entityHealth = entity.healthCurrent;
				Vector2 entityPos = entity.transform.position;
				float	entityScale = entity.transform.localScale.y;
				float	entityRot = entity.transform.localEulerAngles.z;
				string	entitySpecificInfo = "null";

				if (entity is Node) {
					entitySpecificInfo = (entity as Node).capturedPlayerId.ToString();
				} else if (entity is Wall) {
					Wall entityAsWall = (entity as Wall);
					entitySpecificInfo = entityAsWall.wallType + "$" + entityAsWall.parentNodesEntityIds[0] + "$" + entityAsWall.parentNodesEntityIds[1];
				}

				// Entity initialization structure : EntityId|EntityTypeName|EntityHealth|EntityPosX|EntityPosY|EntityScale|EntityRot|EntitySpecificInfo
				newMessage += entityId + "%" + entityTypeName + "%" + entityHealth + "%" + entityPos.x + "%" + entityPos.y + "%" + entityScale + "%" + entityRot + "%" + entitySpecificInfo + "|";
			}
		}

		newMessage = newMessage.Trim('|');

		Send(newMessage, reliableFragmentedSequencedChannel, connectionId);
	}

	public void Send_PlayerTethered (int connectionId, int nodeEntityId) {
		string newMessage = "PlayerTethered|" + connectionId + "|" + nodeEntityId;
		Send(newMessage, reliableChannel, players);
	}

	public void Entity_Remove (int entityId) {
		if (entities.ContainsKey(entityId)) {
			Destroy(entities[entityId].gameObject);
			entities.Remove(entityId);
			Send("EntityRemove|" + entityId, reliableChannel, players);
		}
	}

	// Receive Methods
	private void Receive_MyPosAndRot(int connectionId, string[] splitData) {
		if (VerifySplitData(connectionId, splitData, 4)) {
			// Verify split data parsability
			float splitData1, splitData2, splitData3;
			if (float.TryParse(splitData[1], out splitData1) && float.TryParse(splitData[2], out splitData2) && float.TryParse(splitData[3], out splitData3)) {
				// Get split data
				float posX = splitData1;
				float posY = splitData2;
				float rot = splitData3;

				// Position player
				players[connectionId].playerController.desiredPosition = new Vector3(posX, posY, 0);
				players[connectionId].playerController.desiredRotation = rot;
			}
		}
	}

	private void Receive_MyInfo (int connectionId, string[] splitData) {
		if (VerifySplitData(connectionId, splitData, 3)) {		// Make sure the split Data is equal to 3, otherwise, send an error
			// Get split data
			string playerName = splitData[1];
			string playerVersionNumber = splitData[2];

			// Link name to the connectionId
			Player currentPlayer = players[connectionId];
			currentPlayer.playerName = playerName;
			
			if (playerVersionNumber != versionNumber) {
				StartCoroutine(Send_WrongVersion(connectionId));
			} else {
				// Tell everybody that a new player has connected
				Send("PlayerConnected|" + playerName + "|" + connectionId + "|" + currentPlayer.playerController.entityId + "|" + currentPlayer.playerColor, reliableSequencedChannel, players);
				Send_InitializeEntities(connectionId);      // Send the player the initializeEntities info
			}
		} else {

		}
	}

	private IEnumerator Send_WrongVersion (int connectionId) {
		Send("WrongVersion", reliableChannel, connectionId);
		yield return new WaitForSeconds(0.5f);
		NetworkTransport.Disconnect(hostId, connectionId, out error);   // Kick player
	}

	private void Receive_FireProjectile(int connectionId, string[] splitData) {
		if (VerifySplitData(connectionId, splitData, 6)) {
			float splitData1, splitData2, splitData3, splitData4, splitData5;
			// Verify split data parsability
			if (float.TryParse(splitData[1], out splitData1) && float.TryParse(splitData[2], out splitData2) && float.TryParse(splitData[3], out splitData3) && float.TryParse(splitData[4], out splitData4) && float.TryParse(splitData[5], out splitData5)) {
				// Get split data
				Player parentPlayer = players[connectionId];
				Vector2 origin = new Vector2(splitData1, splitData2);
				Vector2 velocity = new Vector2(splitData3, splitData4);
				float curve = splitData5;

				// Create projectile
				GameObject newProjectile = (GameObject)Instantiate(prefab_Projectile, origin, Quaternion.Euler(0, 0, (Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg) + 90));
				newProjectile.transform.localScale = Vector3.one * (velocity.magnitude / 4);
				Projectile newProjectileClass = newProjectile.GetComponent<Projectile>();
				newProjectileClass.playerId = connectionId;
				newProjectileClass.velocity = velocity;
				newProjectileClass.curve = curve;
				newProjectileClass.parentEntity = parentPlayer.playerController as Entity;
				newProjectileClass.isServerSide = true;

				Send("CreateProjectile|" + parentPlayer.connectionId + "|" + origin.x + "|" + origin.y + "|" + velocity.x + "|" + velocity.y + "|" + splitData5, reliableChannel, players);
			}
		}
	}

	private void Receive_AttemptTether(int connectionId, string[] splitData) {
		if (VerifySplitData(connectionId, splitData, 1)) {
			players[connectionId].playerController.AttemptTether();
		}
	}

	private void Receive_AttemptChangeBuildType(int connectionId, string[] splitData) {
		if (VerifySplitData(connectionId, splitData, 1)) {
			players[connectionId].playerController.ChangeBuildType();

			string newMessage = "PlayerChangeBuildType|" + connectionId + "|" + players[connectionId].playerController.buildType;
			Send(newMessage, reliableChannel, players);
		}
	}

	// Both Send Methods
	private void Send (string message, int channelId, int connectionId) {
		Dictionary<int, Player> newDictionaryPlayers = new Dictionary<int, Player>();
		newDictionaryPlayers.Add(connectionId, players[connectionId]);
		Send(message, channelId, newDictionaryPlayers);
	}
	
	private void Send (string message, int channelId, Dictionary<int, Player> listPlayers) {
		byte[] msg = Encoding.Unicode.GetBytes(message);        // Turn string message into byte array
		foreach (KeyValuePair<int, Player> playerAndId in listPlayers) {
			Player player = playerAndId.Value;
			NetworkTransport.Send(hostId, player.connectionId, channelId, msg, message.Length * sizeof(char), out error);
		}
	}

	private bool VerifySplitData (int connectionId, string[] splitData, int desiredLength) {
		if (splitData.Length == desiredLength) {
			return true;
		} else {
			Send("Error: received message invalid " + string.Join("", splitData), unreliableChannel, connectionId);
			return false;
		}
	}
}
