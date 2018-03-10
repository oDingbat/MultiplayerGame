using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ServerClient {
	public int connectionId;
	public string playerName;
	public Vector3 position;
}

public class Server : MonoBehaviour {

	private const int MAX_CONNECTION = 100;

	private int port = 5701;

	private int hostId;
	private int webHostId;

	private int reliableChannel;
	private int unreliableChannel;

	private bool isStarted = false;
	private byte error;

	private float lastMovementUpdate;
	private float movementUpdateRate = 64;

	private List<ServerClient> clients = new List<ServerClient>();

	private void Start () {
		NetworkTransport.Init();
		ConnectionConfig cc = new ConnectionConfig();

		reliableChannel = cc.AddChannel(QosType.Reliable);
		unreliableChannel = cc.AddChannel(QosType.Unreliable);

		HostTopology topo = new HostTopology(cc, MAX_CONNECTION);

		hostId = NetworkTransport.AddHost(topo, port, null);
		webHostId = NetworkTransport.AddWebsocketHost(topo, port, null);

		isStarted = true;

		StartCoroutine(TickUpdate());
	}

	IEnumerator TickUpdate () {
		while (true) {
			lastMovementUpdate = Time.time;
			string m = "AskPosition|";
			foreach (ServerClient sc in clients) {
				if (sc.playerName != "") {
					m += sc.connectionId.ToString() + "%" + sc.position.x.ToString() + "%" + sc.position.y.ToString() + "|";
				}
			}
			m = m.Trim('|');
			Send(m, unreliableChannel, clients);

			yield return new WaitForSeconds(0.05f);
		}
	}

	private void Update() {
		if (isStarted == true) {
			int recHostId;
			int connectionId;
			int channelId;
			byte[] recBuffer = new byte[1024];
			int bufferSize = 1024;
			int dataSize;
			byte error;

			NetworkEventType recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);

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
		}
	}

	private void ParseData (int connectionId, int channelId, byte[] recBuffer, int dataSize) {
		string data = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
		//Debug.Log("Recieving from " + connectionId + " : " + data);

		string[] splitData = data.Split('|');

		switch (splitData[0]) {

			case "NameIs":
				OnNameIs(connectionId, splitData[1]);
				break;

			case "MyPosition":
				OnMyPosition(connectionId, float.Parse(splitData[1]), float.Parse(splitData[2]));
				break;
		}
	}

	void OnConnection (int cnnId) {
		// Add player to list of players
		ServerClient c = new ServerClient();
		c.connectionId = cnnId;
		c.playerName = "TEMP";
		clients.Add(c);

		// When player joins serer, tell them their Id
		// Reqest player name, return name of other players in game
		string msg = "AskName|" + cnnId + "|";
		foreach (ServerClient sc in clients) {
			msg += sc.playerName + "%" + sc.connectionId + "|";
		}
		msg = msg.Trim('|');

		// example: ASKNAME|3|DAVE%1|MICHAEL%2|TEMP%3

		Send(msg, reliableChannel, cnnId);
	}

	void OnDisconnection (int cnnId) {
		// Remove this player from our client list
		clients.Remove(clients.Find(x => x.connectionId == cnnId));

		// Tell everybody else that somebody has disconnected
		Send("PlayerDisconnected|" + cnnId, reliableChannel, clients);
	}

	private void OnMyPosition (int cnnId, float x, float y) {
		clients.Find(c => c.connectionId == cnnId).position = new Vector3(x, y, 0);
	}

	private void OnNameIs (int cnnId, string playerName) {
		// Link name to the connectionId
		clients.Find(x => x.connectionId == cnnId).playerName = playerName;

		// Tell everybody that a new player has connected
		Send("PlayerConnected|" + playerName + "|" + cnnId, reliableChannel, clients);
	}

	private void Send (string message, int channelId, int cnnId) {
		List<ServerClient> c = new List<ServerClient>();
		c.Add(clients.Find(x => x.connectionId == cnnId));
		Send(message, channelId, c);
	}
	
	private void Send (string message, int channelId, List<ServerClient> c) {
		//Debug.Log("Sending : " + message);
		byte[] msg = Encoding.Unicode.GetBytes(message);        // Turn string message into byte array
		foreach (ServerClient sc in c) {
			NetworkTransport.Send(hostId, sc.connectionId, channelId, msg, message.Length * sizeof(char), out error);
		}
	}

}
