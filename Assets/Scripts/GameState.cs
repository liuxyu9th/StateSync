using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Vector3Data
{
    public float x, y, z;

    public Vector3Data(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

public enum OperationType
{
    UP = 0,
    Down = 1,
    Left = 2,
    Right = 3,
}
public class GameState
{
    public int myId;
    public Dictionary<int, Vector3Data> PlayerPos = new Dictionary<int, Vector3Data>();
}
