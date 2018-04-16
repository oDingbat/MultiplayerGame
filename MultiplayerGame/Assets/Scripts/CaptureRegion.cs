using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaptureRegion : MonoBehaviour {

	public List<Vector2> perimeterPoints = new List<Vector2>();     // Ordererd list of perimeter points
	public List<Node> perimeterNodes = new List<Node>();
	public List<Node> interiorNodes = new List<Node>();
	public PolygonCollider2D polygonCollider;
	public MeshFilter meshFilter;
	public Renderer regionRenderer;
	public int playerId;
	public int captureRegionId;

	public void InitializePolygonCollider () {
		polygonCollider.points = perimeterPoints.ToArray();
	}

	public void Collapse () {
		// This method will collapse this capture region, but will rebuild any capture regions possible within this region
	}

}
