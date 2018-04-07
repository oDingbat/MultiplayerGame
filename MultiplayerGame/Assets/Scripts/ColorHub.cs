using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ColorHub {

	public static string[] playerColors = new string[] {
		"FF3D66",
		"B33DFF",
		"3EBDFF",
		"96FF3D",
		"8353FF",
		"3BFF96",
		"FFCD3B",
		"FF883B"
	};

	public static string Black = "202020";
	public static string HotPink = "FF1E58";
	public static string Gray = "B9B9B9";
	public static string White = "FFFFFF";

	public static Color HexToColor (string inputHex) {
		Color newColor = Color.magenta;
		ColorUtility.TryParseHtmlString("#" + inputHex, out newColor);
		return newColor;
	}

	public static string GetRandomPlayerColor () {
		return playerColors[Random.Range(0, playerColors.Length)];
	}

}
