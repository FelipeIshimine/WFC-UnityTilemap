using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Tilemap2DWfcBuilder : WaveFunctionCollapseBuilder
{
    private readonly Vector2Int _size;
    public Tilemap2DWfcBuilder(Vector2Int size, TileBaseCollection tileBaseCollection, int[][] adjacencies, int seed, (int, int)[]startingValues) : 
        base(size.x*size.y, tileBaseCollection.Count, adjacencies/*new int[size.x*size.y][]*/, tileBaseCollection, seed, startingValues)
    {
        _size = size;
    
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