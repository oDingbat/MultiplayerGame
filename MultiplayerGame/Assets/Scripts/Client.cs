using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class Player {
	public string playerName;
	public GameObject avatar;
	public PlayerController playerController;
	public int connectionId;
}


public class Client : MonoBehaviour {

	private const int MAX_CONNECTION = 100;

	private int port = 5701;

	public string playerName;
	private int hostId;
	private int webHostId;

	private int reliableChannel;
	private int unreliableChannel;

	private int ourClientId;
	private int connectionId;
	private float connectionTime;

	private bool isConnected = false;
	private bool isStarted = false;
	private byte error;

	float startTime;
	int msgTotal;

	public GameObject playerPrefab;
	
	public Dictionary<int, Player> players = new Dictionary<int, Player>();

	public void Connect () {
		string newName = GameObject.Find("NameInput").GetComponent<InputField>().text;
		if (newName == "") {
			Debug.Log("You must enter a name");
			return;
		}

		playerName = newName;

		NetworkTransport.Init();
		ConnectionConfig cc = new ConnectionConfig();

		reliableChannel = cc.AddChannel(QosType.Reliable);
		unreliableChannel = cc.AddChannel(QosType.Unreliable);

		HostTopology topo = new HostTopology(cc, MAX_CONNECTION);

		hostId = NetworkTransport.AddHost(topo, 0);
		connectionId = NetworkTransport.Connect(hostId, "127.0.0.1", port, 0, out error);

		connectionTime = Time.time;		
		isConnected = true;
	}

	private void Start () {
		StartCoroutine(TickUpdate());
	}

	IEnumerator TickUpdate() {
		while (true) {
			UpdateRecieve();
			yield return new WaitForSeconds(1 / 64);
		}
	}

	void UpdateRecieve() {
		if (isConnected != false) {
			int recHostId;
			int connectionId;
			int channelId;
			byte[] recBuffer = new byte[1024];
			int bufferSize = 1024;
			int dataSize;
			byte error;

			NetworkEventType recData = NetworkEventType.Nothing;
			do {
				recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);

				switch (recData) {
					case NetworkEventType.DataEvent:
						ParseData(connectionId, channelId, recBuffer, dataSize);
						break;
				}
			} while (recData != NetworkEventType.Nothing);
		}
	}

	private void ParseData (int connectionId, int channelId, byte[] recBuffer, int dataSize) {
		string data = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
		Debug.Log("Recieving : " + data);

		string[] splitData = data.Split('|');

		switch (splitData[0]) {

			case "AskName":
				OnAskName(splitData);
				startTime = Time.time;
				break;

			case "PlayerConnected":
				SpawnPlayer(splitData[1], int.Parse(splitData[2]));
				break;

			case "PlayerDisconnected":
				PlayerDisconnected(int.Parse(splitData[1]));
				break;

			case "AskPosition":
				OnAskPosition(splitData);
				msgTotal++;
				Debug.Log("Msg total: " + ((Time.time - startTime) / msgTotal));
				break;
		}
	}

	private void OnAskName (string[] splitData) {
		// Set this client's ID
		ourClientId = int.Parse(splitData[1]);
		
		// Send our name to the server
		Send("NameIs|" + playerName, reliableChannel);

		// Create all of the other players
		for (int i = 2; i < splitData.Length - 1; i++) {
			string[] d = splitData[i].Split('%');
			SpawnPlayer(d[0], int.Parse(d[1]));
		}
	}

	private void OnAskPosition(string[] data) {
		if (isStarted == true) {
			// Update everyone else
			for (int i = 1; i < data.Length; i++) {
				string[] d = data[i].Split('%');
				if (int.Parse(d[0]) != ourClientId) {      // Prevent server from updating us
					if (players.ContainsKey(int.Parse(d[0])) == true) {			// Have we created this player yet?
						Vector3 position = new Vector3(float.Parse(d[1]), float.Parse(d[2]), 0);
						players[int.Parse(d[0])].playerController.desiredPosition = position;
					}
				}
			}

			// Send our own position
			Vector3 myPosition = players[ourClientId].avatar.transform.position;
			string m = "MyPosition|" + myPosition.x.ToString() + "|" + myPosition.y.ToString();
			Send(m, unreliableChannel);
		}
	}

	private void SpawnPlayer (string playerName, int cnnId) {
		GameObject newPlayerGameObject = (GameObject)Instantiate(playerPrefab);

		Player p = new Player();
		p.avatar = newPlayerGameObject;
		p.playerController = p.avatar.GetComponentInChildren<PlayerController>();
		p.playerName = playerName;
		p.connectionId = cnnId;
		p.avatar.GetComponentInChildren<TextMesh>().text = p.playerName;

		if (cnnId == ourClientId) { // Is this playerGameObject ours?
			p.playerController.isClient = true;
			GameObject.Find("Canvas").SetActive(false);
			isStarted = true;
		}

		players.Add(cnnId, p);
	}

	private void PlayerDisconnected (int cnnId) {
		Destroy(players[cnnId].avatar);
		players.Remove(cnnId);
	}

	private void Send(string message, int channelId) {
		byte[] msg = Encoding.Unicode.GetBytes(message);        // Turn string message into byte array
		NetworkTransport.Send(hostId, connectionId, channelId, msg, message.Length * sizeof(char), out error);
	}

}
