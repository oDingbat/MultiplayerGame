﻿using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class Client : MonoBehaviour {

	// Server Information
	private const int MAX_CONNECTION = 100;     // The max number of players allowed on the server
	public string ipAddressServer = "";
	private int port = 3333;                    // The port number
	private int ourClientId;                    // The Id of our client (Used to manipulate the Players but avoid manipulating our own player incorrectly) (Not same as connectionId)
	private int hostId;                         // The Id of our host
	private int connectionId;                   // The Id of our connection
	private int reliableChannel;                // Channel for sending reliable information
	private int unreliableChannel;              // Channel for sending unreliable information
	private int reliableFragmentedChannel;	// Channel for sending fragmented unreliable information
	private byte error;                         // Byte used to save errors returned by NetworkTransport.Receive
	private float tickRate = 64;                // The rate at which information is sent and recieved to and from the server
	private string versionNumber = "0.1.6";     // The version number currently used by the server

	// Connection booleans
	private bool isConnected = false;           // Are we currently connected to the Server?
	private bool isAttemptingConnection;		// Are we currently attempting to connect to the server?
	private bool isLoaded = false;              // Is the server fully loaded and ready to be played?

	[Space(10)] [Header("Player Variables")]
	public string playerName;                   // The name of our player

	[Space(10)] [Header("UI")]
	public InputField inputField_PlayerName;    // Input field for typing the player's desired name
	public InputField inputField_IPAddress;		// input field for the desired ip address to connect to
	public InputField inputField_Chat;          // Input field for typing in the chat
	public RectTransform ui_LoginScreen;        // RT for login screen
	public Text text_Chat;                      // Text field for displaying game chat
	public Text text_Error;						// Text field for displaying errors while trying to connect to a server

	[Space(10)] [Header("Prefabs")]
	public GameObject prefab_Player;            // The prefab for player GameObjects
	public GameObject prefab_Projectile;        // The prefab for regular projectiles

	[Space(10)] [Header("Entity Prefabs")]
	public GameObject entityPrefab_Node;		// The prefab for Nodes

	public Dictionary<int, Player> players = new Dictionary<int, Player>();         // A dictionary of Players where the int key is that player's clientId
	public Dictionary<int, Entity> entities = new Dictionary<int, Entity>();

	public void Connect() {
		// This method connects the client to the server
		if (isAttemptingConnection == false && isConnected == false) {     // Make sure we're not already connected and we're not already attempting to connect to server
			Debug.Log("Attempting to connect to server...");
			playerName = inputField_PlayerName.text;
			ipAddressServer = "174.138.46.138";								// TODO: probably shouldn't hardcode this?

			NetworkTransport.Init();        // Initialize NetworkTransport
			ConnectionConfig cc = new ConnectionConfig();
			reliableChannel = cc.AddChannel(QosType.Reliable);          // Adds the reliable channel
			unreliableChannel = cc.AddChannel(QosType.Unreliable);      // Adds the unreliable channel
			reliableFragmentedChannel = cc.AddChannel(QosType.ReliableFragmented);
			HostTopology topo = new HostTopology(cc, MAX_CONNECTION);       // Setup topology
			hostId = NetworkTransport.AddHost(topo, 0);                                         // Gets the Id for the host

			Debug.Log("Connecting with Ip: " + ipAddressServer + " port: " + port);

			connectionId = NetworkTransport.Connect(hostId, ipAddressServer, port, 0, out error);   // Gets the Id for the connection (not the same as ourClientId)

			isAttemptingConnection = true;
		}
	}

	private void Start() {
		//Connect();
		StartCoroutine(TickUpdate());
	}

	// Update Methods
	private IEnumerator TickUpdate() {
		// This method is used to receive and send information back and forth between the connected server. It's tick rate depends on the variable tickRate
		while (true) {
			if (isAttemptingConnection || isConnected) {
				UpdateReceive();
			}
			if (isConnected) {      // Make sure we're connected first
				UpdateSend();
			}
			yield return new WaitForSeconds(1 / tickRate);
		}
	}

	private void UpdateSend() {
		if (isLoaded) {         // Is the server fully loaded?
			Send_PosAndRot();
		}
	}

	private void UpdateReceive() {
		// This method handles receiving information from the server
		int recHostId;
		int connectionId;
		int channelId;
		byte[] recBuffer = new byte[8000];
		int dataSize;
		byte error;
		NetworkEventType recData = NetworkEventType.Nothing;
		do {        // Do While ensures that we process all of the sent messages each tick
			recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, recBuffer.Length, out dataSize, out error);
			//Debug.Log(recData);

			switch (recData) {
				case NetworkEventType.ConnectEvent:
					Debug.Log("Successfully connected to server!");
					isConnected = true;
					break;
				case NetworkEventType.DataEvent:
					ParseData(connectionId, channelId, recBuffer, dataSize);
					break;
				case NetworkEventType.DisconnectEvent:
					Debug.Log("Disconnected");
					isConnected = false;
					isAttemptingConnection = false;
					break;
			}
		} while (recData != NetworkEventType.Nothing);
	}

	private void ParseData(int connectionId, int channelId, byte[] recBuffer, int dataSize) {
		//Debug.Log(dataSize);
		string data = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
		//Debug.Log("Recieving : " + data);
		
		string[] splitData = data.Split('|');

		if (splitData.Length > 0) {		// Check to make sure the split data even has any information
			switch (splitData[0]) {

				case "AskInfo":
					Receive_AskInfo(splitData);
					break;

				case "InitializeEntities":
					Receive_InitializeEntities(splitData);
					break;

				case "WrongVersion":
					Receive_WrongVersion();
					break;

				case "PlayerConnected":
					Receive_PlayerConnected(splitData);
					break;

				case "PlayerDisconnected":
					Receive_PlayerDisconnected(splitData);
					break;

				case "PlayerPositions":
					Receive_PlayerPositions(splitData);
					break;

				case "CreateProjectile":
					Receive_CreateProjectile(splitData);
					break;

				case "EntityTakeDamage":
					Receive_EntityTakeDamage(splitData);
					break;

				case "EntityTakeHeal":
					Receive_EntityTakeHeal(splitData);
					break;

				case "EntityDie":
					Receive_EntityDie(splitData);
					break;

				case "EntityRespawn":
					Receive_EntityRespawn(splitData);
					break;

				case "EntityRemove":
					Receive_EntityRemove(splitData);
					break;
			}
		}
	}

	// Connection/Disconnection Methods
	private void SpawnPlayer(string playerName, int connectionId, int entityId, string playerColor) {
		Player newPlayer = new Player();
		
		// Spawn Player
		newPlayer.playerName = playerName;
		newPlayer.connectionId = connectionId;
		newPlayer.playerGameObject = (GameObject)Instantiate(prefab_Player);
		newPlayer.playerGameObject.GetComponentInChildren<TextMesh>().text = newPlayer.playerName;
		newPlayer.playerController = newPlayer.playerGameObject.GetComponentInChildren<PlayerController>();
		newPlayer.playerController.entityId = entityId;
		Color colorParse = Color.black;
		ColorUtility.TryParseHtmlString("#" + playerColor, out colorParse);
		newPlayer.playerController.playerSprite.GetComponent<SpriteRenderer>().color = colorParse;

		// Add player to entities
		if (entities.ContainsKey(entityId)) {
			entities[entityId] = newPlayer.playerController;
		} else {
			entities.Add(entityId, newPlayer.playerController as Entity);
		}
		
		if (connectionId == ourClientId) { // Is this playerGameObject ours?
			newPlayer.playerController.playerType = PlayerController.PlayerType.Client;
			newPlayer.playerGameObject.layer = LayerMask.NameToLayer("ClientPlayer");
			ui_LoginScreen.gameObject.SetActive(false);
			isLoaded = true;
			// Currently this is the point at which the client's player becomes active and the GUI closes, we should probably change that! D:
		}

		if (players.ContainsKey(connectionId)) {
			players[connectionId] = newPlayer;
		} else {
			players.Add(connectionId, newPlayer);
		}
	}

	// Send Methods
	private void Send_PosAndRot() {
		// Send our position to the server
		Vector2 myPosition = players[ourClientId].playerGameObject.transform.position;
		float myRotation = players[ourClientId].playerController.playerSprite.eulerAngles.z;
		//string m = "MyPosAndRot|" + myPosition.x.ToString() + "|" + myPosition.y.ToString() + "|" + myRotation.ToString();
		string m = "MyPosAndRot|" + myPosition.x.ToString() + "|" + myPosition.y.ToString() + "|" + myRotation.ToString();
		Send(m, unreliableChannel);
	}

	public void Send_Projectile (Vector2 origin, Vector2 velocity) {
		string newMessage = "FireProjectile|" + origin.x.ToString() + "|" + origin.y.ToString() + "|" + velocity.x.ToString() + "|" + velocity.y.ToString();
		Send(newMessage, reliableChannel);
	}

	// Receive Methods
	private void Receive_PlayerConnected (string[] splitData) {
		SpawnPlayer(splitData[1], int.Parse(splitData[2]), int.Parse(splitData[3]), splitData[4]);
	}

	private void Receive_WrongVersion () {
		Debug.Log("Server says we have the wrong version number.");
		text_Error.text = "Error: outdated version number (v"+ versionNumber + ")";
	}

	private void Receive_PlayerRespawned(string[] splitData) {
		int deadPlayerConnectionId = int.Parse(splitData[1]);
		Debug.Log("Player Respawned");
		//if (deadPlayerConnectionId == ourClientId) {
		players[deadPlayerConnectionId].playerController.Respawn();
		//}
	}

	private void Receive_PlayerDisconnected(string[] splitData) {
		int connectionId = int.Parse(splitData[1]);
		Destroy(players[connectionId].playerGameObject);
		entities.Remove(players[connectionId].playerController.entityId);
		players.Remove(connectionId);
	}

	private void Receive_AskInfo (string[] splitData) {
		// Set this client's ID
		ourClientId = int.Parse(splitData[1]);

		// Send our name and version number to the server
		string newMessage = "MyInfo|" + playerName + "|" + versionNumber;
		Send(newMessage, reliableChannel);

		// Create all of the other players
		for (int i = 2; i < splitData.Length - 1; i++) {
			string[] d = splitData[i].Split('%');
			SpawnPlayer(d[0], int.Parse(d[1]), int.Parse(d[2]), d[3]);
		}
	}

	private void Receive_InitializeEntities (string[] splitData) {
		Debug.Log(splitData.Length);
		for (int i = 1; i < splitData.Length; i++) {
			string[] splitDataBits = splitData[i].Split('%');
			// Get data bits
			int entityId = int.Parse(splitDataBits[0]);
			string entityType = splitDataBits[1];
			int entityHealth = int.Parse(splitDataBits[2]);
			float posX = float.Parse(splitDataBits[3]);
			float posY = float.Parse(splitDataBits[4]);

			if (!entities.ContainsKey(entityId)) {     // Make sure we don't already have this entity created
				switch (entityType) {
					case "Node":
						GameObject newNodeGameObject = (GameObject)Instantiate(entityPrefab_Node, new Vector3(posX, posY), Quaternion.identity);
						Entity newNodeEntity = newNodeGameObject.GetComponent<Entity>();
						newNodeEntity.SetHealth(entityHealth);
						entities.Add(entityId, newNodeEntity);
						break;
				}
			}
		}
	}

	private void Receive_PlayerPositions (string[] splitData) {
		for (int i = 1; i < splitData.Length; i++) {
			string[] splitDataBits = splitData[i].Split('%');
			if (players.ContainsKey(int.Parse(splitDataBits[0]))) {     // Does a player with this connectionId exist?
				if (int.Parse(splitDataBits[0]) != ourClientId) {
					players[int.Parse(splitDataBits[0])].playerController.desiredPosition = new Vector3(float.Parse(splitDataBits[1]), float.Parse(splitDataBits[2]), 0);
					players[int.Parse(splitDataBits[0])].playerController.desiredRotation = float.Parse(splitDataBits[3]);
				}
			}
		}
	}

	private void Receive_CreateProjectile (string[] splitData) {
		Vector2 origin = new Vector2(float.Parse(splitData[2]), float.Parse(splitData[3]));
		Vector2 velocity = new Vector2(float.Parse(splitData[4]), float.Parse(splitData[5]));

		GameObject newProjectile = (GameObject)Instantiate(prefab_Projectile, origin, Quaternion.Euler(0, 0, (Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg) + 90));
		Projectile newProjectileClass = newProjectile.GetComponent<Projectile>();
		newProjectileClass.velocity = velocity;
		newProjectileClass.parentEntity = players[int.Parse(splitData[1])].playerController as Entity;
	}

	private void Receive_EntityTakeDamage(string[] splitData) {
		int entityId = int.Parse(splitData[1]);
		if (entities.ContainsKey(entityId)) {
			int damage = int.Parse(splitData[2]);
			entities[entityId].TakeDamage(damage);
		}
	}

	private void Receive_EntityTakeHeal(string[] splitData) {
		int entityId = int.Parse(splitData[1]);
		if (entities.ContainsKey(entityId)) {
			int heal = int.Parse(splitData[2]);
			entities[entityId].TakeHeal(heal);
		}
	}

	private void Receive_EntityDie(string[] splitData) {
		int entityId = int.Parse(splitData[1]);
		if (entities.ContainsKey(entityId)) {
			entities[entityId].Die();
		}
	}

	private void Receive_EntityRespawn(string[] splitData) {
		int entityId = int.Parse(splitData[1]);
		if (entities.ContainsKey(entityId)) {
			entities[entityId].Respawn();
		}
	}

	private void Receive_EntityRemove (string[] splitData) {
		int entityId = int.Parse(splitData[1]);
		Destroy(entities[entityId].gameObject);
		entities.Remove(entityId);
	}

	// Send Method
	private void Send(string message, int channelId) {
		byte[] msg = Encoding.Unicode.GetBytes(message);        // Turn string message into byte array
		NetworkTransport.Send(hostId, connectionId, channelId, msg, message.Length * sizeof(char), out error);
	}
	
}
