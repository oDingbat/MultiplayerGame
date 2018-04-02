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
	private byte error;                         // Byte used to save errors returned by NetworkTransport.Receive
	private float tickRate = 64;                // The rate at which information is recieved and sent to and from the clients
	private string versionNumber = "0.1.4";		// The version number currently used by the server

	// Connection booleans
	private bool isStarted = false;
	
	// All clients
	public List<Player> players = new List<Player>();			// List of players

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

		HostTopology topo = new HostTopology(newConnectionConfig, MAX_CONNECTION);      // Setup topology

		hostId = NetworkTransport.AddHost(topo, port);
		//webHostId = NetworkTransport.AddWebsocketHost(topo, port, null);

		isStarted = true;
		Debug.Log("Server initialized successfully!");
	}

	private void Start () {
		InitializeServer();
		StartCoroutine(TickUpdate());
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
			byte[] recBuffer = new byte[1024];
			int bufferSize = 1024;
			int dataSize;
			byte error;
			NetworkEventType recData = NetworkEventType.Nothing;
			do {    // Do While ensures that we process all of the sent messages each tick
				recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);
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
		Debug.Log("Recieving from " + connectionId + " : " + data);

		string[] splitData = data.Split('|');

		switch (splitData[0]) {

			case "MyName":
				Receive_MyName(connectionId, splitData[1], splitData[2]);
				break;

			case "MyPosAndRot":
				Receive_MyPosAndRot(connectionId, float.Parse(splitData[1]), float.Parse(splitData[2]), float.Parse(splitData[3]));
				break;

			case "FireProjectile":
				Receive_FireProjectile(connectionId, splitData);
				break;
		}
	}

		// Connection/Disconnection Methods
	private void OnConnection (int connectionId) {
		// Add player to list of players
		Player newPlayer = new Player();
		newPlayer.connectionId = connectionId;
		newPlayer.playerName = "temp";
		newPlayer.playerColor = ColorHub.GetRandomPlayerColor();
		players.Add(newPlayer);

		Debug.Log(newPlayer.playerColor);

		// Spawn the player
		newPlayer.playerGameObject = (GameObject)Instantiate(prefab_Player);
		newPlayer.playerController = newPlayer.playerGameObject.GetComponent<PlayerController>();
		newPlayer.playerController.playerType = PlayerController.PlayerType.Server;                 // Set the playerType to Server as to use the server specific code
		Color newColor = Color.black;
		ColorUtility.TryParseHtmlString("#" + newPlayer.playerColor, out newColor);
		newPlayer.playerController.playerSprite.GetComponent<SpriteRenderer>().color = newColor;

		// When player joins serer, tell them their Id
		// Reqest player name, return name of other players in game
		string msg = "AskName|" + connectionId + "|";
		foreach (Player player in players) {

			msg += player.playerName + "%" + player.connectionId + "%" + player.playerColor + "|";
		}
		msg = msg.Trim('|');

		// example: ASKNAME|3|DAVE%1|MICHAEL%2|TEMP%3
		
		Send(msg, reliableChannel, connectionId);
	}

	private void OnDisconnection (int connectionId) {
		// Remove this player from our client list
		Destroy(players.Find(x => x.connectionId == connectionId).playerGameObject);
		players.Remove(players.Find(x => x.connectionId == connectionId));

		// Tell everybody else that somebody has disconnected
		Send("PlayerDisconnected|" + connectionId, reliableChannel, players);
	}

		// Send Methods
	private void Send_PlayerPosAndRot () {
		string message = "PlayerPositions|";

		foreach (Player player in players) {
			message += player.connectionId + "%" + player.playerGameObject.transform.position.x + "%" + player.playerGameObject.transform.position.y + "%" + player.playerController.playerSprite.eulerAngles.z + "|";
		}
		
		message = message.Trim('|');      // Trim the final bar

		Send(message, unreliableChannel, players);
	}

		// Receive Methods
	private void Receive_MyPosAndRot(int connectionId, float posX, float posY, float rot) {
		players.Find(x => x.connectionId == connectionId).playerController.desiredPosition = new Vector3(posX, posY, 0);
		Debug.Log(rot);
		players.Find(x => x.connectionId == connectionId).playerController.desiredRotation = rot;
	}

	private void Receive_MyName (int connectionId, string playerName, string playersVersionNumber) {
		// Link name to the connectionId
		Player currentPlayer = players.Find(x => x.connectionId == connectionId);
		currentPlayer.playerName = playerName;

		if (playersVersionNumber != versionNumber) {
			NetworkTransport.Disconnect(hostId, connectionId, out error);   // Kick player
		} else {
			// Tell everybody that a new player has connected
			Send("PlayerConnected|" + playerName + "|" + connectionId + "|" + currentPlayer.playerColor, reliableChannel, players);
		}
	}

	private void Receive_FireProjectile(int connectionId, string[] splitData) {
		Vector2 origin = new Vector2(float.Parse(splitData[1]), float.Parse(splitData[2]));
		Vector2 velocity = new Vector2(float.Parse(splitData[3]), float.Parse(splitData[4]));

		GameObject newProjectile = (GameObject)Instantiate(prefab_Projectile, origin, Quaternion.Euler(0, 0, (Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg) + 90));
		Projectile newProjectileClass = newProjectile.GetComponent<Projectile>();
		newProjectileClass.velocity = velocity;

		Send("CreateProjectile|" + origin.x + "|" + origin.y + "|" + velocity.x + "|" + velocity.y, reliableChannel, players);
	}

	// Both Send Methods
	private void Send (string message, int channelId, int connectionId) {
		List<Player> newListPlayers = new List<Player>();
		newListPlayers.Add(players.Find(p => p.connectionId == connectionId));
		Send(message, channelId, newListPlayers);
	}
	
	private void Send (string message, int channelId, List<Player> listPlayers) {
		Debug.Log("Sending : " + message);
		byte[] msg = Encoding.Unicode.GetBytes(message);        // Turn string message into byte array
		foreach (Player player in listPlayers) {
			NetworkTransport.Send(hostId, player.connectionId, channelId, msg, message.Length * sizeof(char), out error);
		}
	}

}
