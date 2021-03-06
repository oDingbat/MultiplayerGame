﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Player {
	public string				playerName;
	public GameObject			playerGameObject;
	public PlayerController		playerController;
	public string				playerColor;
	public int					connectionId;
	public List<CaptureRegion> captureRegions;
	public Material				captureRegionMaterial;

	public Player () {

	}
}
