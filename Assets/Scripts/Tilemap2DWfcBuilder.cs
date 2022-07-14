using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Tilemap2DWfcBuilder : WaveFunctionCollapseBuilder
{
    private readonly Vector2Int _size;
    public Tilemap2DWfcBuilder(Vector2Int size, TileBaseCollection tileBaseCollection) : 
        base(size.x*size.y, tileBaseCollection.Count, new int[size.x*size.y][], tileBaseCollection)
    {
        _size = size;
        for (int i = 0; i < NodeCount; i++)
        {
            Adjacencies[i] = new int[4];

            int rowStartIndex = (i / size.x) * size.x;
            
            Adjacencies[i][0] = Mathf.RoundToInt(Mathf.Repeat(Mathf.Repeat(i - 1, size.x) + rowStartIndex, NodeCount));
            Adjacencies[i][1] = Mathf.RoundToInt(Mathf.Repeat(Mathf.Repeat(i + 1, size.x) + rowStartIndex, NodeCount));

            Adjacencies[i][2] = Mathf.RoundToInt(Mathf.Repeat(i - size.x, NodeCount));
            Adjacencies[i][3] = Mathf.RoundToInt(Mathf.Repeat(i + size.x, NodeCount));
        }
    }

    public void DrawGizmos()
    {
        //StringBuilder builder = new StringBuilder();
        for (int i = 0; i < NodeCount; i++)
        {
            #if UNITY_EDITOR
            var coordinate = new Vector3(i % _size.x, 0, i / _size.x);
    
            UnityEditor.Handles.Label(coordinate + new Vector3(.5f,0,.5f), $"{i}\n[{coordinate.x},{coordinate.z}]\n E:{Entropy[i]}|V:{Results[i]}");
            #endif
        }
    }

    public int[] GetAdjacencies(Vector3Int value) => Adjacencies[(value.y * _size.x) + value.x];

    public IEnumerable<int> GetAdjacencies(int index) => Adjacencies[index];
    
    public Vector3 IndexToWorldPosition(int index) => new Vector3(index % _size.x + .5f,0, index / _size.x + .5f) ;
    public Vector2Int IndexToCoordinate(int index) => new Vector2Int(index % _size.x, index / _size.x);
    public int CoordinateToIndex(Vector3Int coordinate) => (coordinate.y * _size.x) + coordinate.x;

}