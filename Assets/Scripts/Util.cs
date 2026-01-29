using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Util 
{
    public static Vector3Data ConvertToGameStateData(this Vector3 v)
    {
        return new Vector3Data(v);
    }
    
}
