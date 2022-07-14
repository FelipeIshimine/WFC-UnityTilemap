using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;


public class WfcCollection<T> : WfcCollection
{
    public int Count => Elements.Length;

    [field:SerializeField] public T[] Elements { get; protected set; }
    [field:SerializeField] public CompatibilityArray[] Compatibilities { get; protected set; }
    
    
    [System.Serializable]
    public struct CompatibilityArray
    {
        [field:SerializeField] public Side[] Sides { get; private set; }
        public CompatibilityArray(Side[] sides)
        {
            Sides = sides;
        }
    }
    
    [System.Serializable]
    public struct Side
    {
        [field:SerializeField]  public Pair[] Values { get; private set; }
        public Side(Pair[] pairs)
        {
            Values = pairs;
        }
    }
    
    [System.Serializable]
    public struct Pair
    {
        [field:SerializeField] public T Tile { get; private set; }
        [field:SerializeField] public int Weight { get; private set; }
        
        public Pair(T tile, int weight)
        {
            Tile = tile;
            Weight = weight;
        }
    }

    public override bool IsCompatible(int x, int y, int dir)
    {
        T elementY = Elements[y];
        
        for (int i = 0; i < Compatibilities[x].Sides[dir].Values.Length; i++)
        {
            if (Compatibilities[x].Sides[dir].Values[i].Tile.Equals(elementY))
                return true;
        }

        return false;
    }

    public override bool[] GetCompatibles(int x, int y, int dir)
    {
        T elementY = Elements[y];

        bool[] compatibles = new bool[Compatibilities[x].Sides[dir].Values.Length];
        
        for (int i = 0; i < Compatibilities[x].Sides[dir].Values.Length; i++)
            compatibles[i] = (Compatibilities[x].Sides[dir].Values[i].Tile.Equals(elementY));

        return compatibles;
    }
}


public abstract class WfcCollection : ScriptableObject
{
    public abstract bool IsCompatible(int x, int y, int dir);

    public abstract bool[] GetCompatibles(int x, int y, int dir);

}