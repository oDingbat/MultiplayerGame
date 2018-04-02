using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ColorHub {

	public static string[] playerColors = new string[] {
		"FF404B",
		"1D1D1D",
		"F8FF3D",
		"FF3D6E",
		"3EBDFF",
		"96FF3D"
	};

	public static string GetRandomPlayerColor () {
		return playerColors[Random.Range(0, playerColors.Length)];
	}

}
