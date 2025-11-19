using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Since we are fake parenting ship model to its ship,
// the existance this component lets game objects know if they are a part of a ship
public class ShipModel : MonoBehaviour
{
	ShipMove shipComp;

	public ShipMove Ship { get { return shipComp; } }

    void Awake()
    {
        shipComp = GetComponentInParent<ShipMove>();
		if (!shipComp)
		{
			Debug.LogWarning("Invalid ShipModel component!");
			Destroy(this);
		}
    }
}
