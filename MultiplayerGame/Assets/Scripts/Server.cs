using System;
using System.Collections;
using System.Collections.Generic;
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
	private string versionNumber = "0.1.7";		// The version number currently used by the server

	// Connection booleans
	private bool isStarted = false;
	
	// All clients
	public Dictionary<int, Player> players = new Dictionary<int, Player>();         // List of players
	public Dictionary<int, Entity> entities = new Dictionary<int, Entity>();
	int entityIdIterationCurrent = 0;

	public List<Node> nodes = new List<Node>();

	public Transform entitiesContainer;

	// Prefabs
	public GameObject prefab_Player;
	public GameObject prefab_Projectile;

	private void InitializeServer() {
		// Initialize Server
		Debug.Log("Attempting to initialize server...");

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
		Debug.Log("Server initialized successfully!");
	}

	private void Start () {
		InitializeServer();
		GetInitialEntities();
		StartCoroutine(TickUpdate());
	}
	
	private void GetInitialEntities () {
		if (GameObject.Find("[Entities]")) {
			entitiesContainer = GameObject.Find("[Entities]").transform;
			foreach (Transform entityTransform in entitiesContainer) {
				Entity entity = entityTransform.GetComponent<Entity>();
				if (entity != null) {
					Debug.Log("Creating Entity: " + entityIdIterationCurrent);
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
	}

		// Update Methods
	IEnumerator TickUpdate () {
		while (true) {
			//Debug.Log(players.Count);
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
						Debug.Log("Player " + connectionId + " has connected");
						OnConnection(connectionId);
						break;
					case NetworkEventType.DataEvent:
						ParseData(connectionId, channelId, recBuffer, dataSize);
						break;
					case NetworkEventType.DisconnectEvent:
						Debug.Log("Player " + connectionId + " has disconnected");
						OnDisconnection(connectionId);
						break;
				}
			} while (recData != NetworkEventType.Nothing);
		}
	}

	private void ParseData (int connectionId, int channelId, byte[] recBuffer, int dataSize) {
		string data = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
		//Debug.Log("Recieving from " + connectionId + " : " + data);

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
		players.Add(connectionId, newPlayer);
		
		// Spawn the player
		newPlayer.playerGameObject = (GameObject)Instantiate(prefab_Player);
		newPlayer.playerController = newPlayer.playerGameObject.GetComponent<PlayerController>();
		newPlayer.playerController.networkPerspective = NetworkPerspective.Server;                 // Set the playerType to Server as to use the server specific code
		Color newColor = Color.black;
		ColorUtility.TryParseHtmlString("#" + newPlayer.playerColor, out newColor);
		newPlayer.playerController.playerSprite.GetComponent<SpriteRenderer>().color = newColor;
		newPlayer.playerController.tetherCircle.GetComponent<SpriteRenderer>().color = Color.Lerp(ColorHub.HexToColor(ColorHub.White), newColor, 0.5f);
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

	public void CreateEntity (Entity entity) {
		entities.Add(entityIdIterationCurrent, entity);
		Send("CreateEntity|" + entityIdIterationCurrent, reliableChannel, players);
		entityIdIterationCurrent++;
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
			if ((entityAndId.Value is PlayerController) == false) {		// Don't send player entites
				int entityId = entityAndId.Key;
				Entity entity = entityAndId.Value;
				string entityTypeName = entityAndId.Value.GetType().ToString();
				int entityHealth = entityAndId.Value.healthCurrent;
				string entitySpecificInfo = "null";
				if (entity is Node) {
					entitySpecificInfo = (entity as Node).capturedPlayerId.ToString();
				}
				newMessage += entityId + "%" + entityTypeName + "%" + entitySpecificInfo + "%" + entityHealth + "%" + entity.transform.position.x + "%" + entity.transform.position.y + "|";
			}
		}

		newMessage = newMessage.Trim('|');

		Send(newMessage, reliableFragmentedSequencedChannel, connectionId);
	}

	public void Send_PlayerTethered (int connectionId, int nodeEntityId) {
		string message = "PlayerTethered|" + connectionId + "|" + nodeEntityId;
		Send(message, reliableChannel, players);
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
