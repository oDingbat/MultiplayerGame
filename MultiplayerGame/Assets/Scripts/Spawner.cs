using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : Entity {

	[Space(10)] [Header("Spawner Information")]
	public GameObject prefab_Mob;
	public SpriteRenderer spriteEdge;

}
