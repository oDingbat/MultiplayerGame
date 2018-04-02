using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour {

	public List<Node> nodes;
	public int mapWidth = 10, mapHeight = 10;
	public float mapScaleMultiplier =  10;
	public float nodePositionRandomness = 1f;

	public GameObject prefabNode;

	void Start () {
		GenerateLevel();
	}

	void GenerateLevel () {
		Debug.Log("Generating Level");
		for (int a = 0; a < mapWidth; a++) {
			for (int b = 0; b < mapHeight; b++) {
				Vector3 newPosition = new Vector3((a - mapWidth / 2) * mapScaleMultiplier, (b - mapHeight / 2) * mapScaleMultiplier) + (Quaternion.Euler(0, 0, Random.Range(0f, 360f)) * new Vector3(Random.Range(0, nodePositionRandomness), 0, 0));
				GameObject newNodeGO = (GameObject)Instantiate(prefabNode, newPosition, Quaternion.identity);
				Node newNode = newNodeGO.GetComponent<Node>();
				nodes.Add(newNode);
			}
		}
	}

}
