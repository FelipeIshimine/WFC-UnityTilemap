using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class WaveFunctionCollapseTilemap2D : MonoBehaviour
{
    public Vector2Int size;
    public TileBaseCollection collection;
    
    public Tilemap exampleTilemap;

    public Tilemap startingTilemap;
    
    public Tilemap resultTilemap;

    public Vector3Int value;
    
    private Tilemap2DWfcBuilder builder;

    [ShowInInspector] private int[] result;

    private Queue<int> _renderQueue = new Queue<int>();

    private int resultsCount;

    private CancellationTokenSource _cancellationTokenSource;

    public bool drawPriorityQueue = true;
    public bool drawTilesInfo;
    public bool constantUpdate = true;
    
    
    private TileBaseCollection _activeCollection;

    [ShowInInspector] private IndexPriorityQueue _queue = new IndexPriorityQueue(24);

    [ShowInInspector, ListDrawerSettings(ShowIndexLabels = true), HorizontalGroup()] public IReadOnlyList<int> Priority => _queue.Priority;
    [ShowInInspector, HorizontalGroup] public IReadOnlyList<int> PositionToIndex => _queue.PositionToIndex;
    [ShowInInspector, HorizontalGroup] public IReadOnlyList<int> IndexToPosition => _queue.IndexToPosition;
    
    [Button] public void InitializeQueue(int maxSize) => _queue = new IndexPriorityQueue(maxSize);
    [Button] public void Enqueue(int index, int priority) => _queue.Enqueue(index, priority);
    [Button] public int Dequeue() => _queue.DequeueIndex();
    [Button] public void UpdatePriority(int index, int priority) => _queue.Update(index, priority);

    
    [Button]
    public async void Go(int randomSeed)
    {
        Random.InitState(randomSeed);

        _activeCollection = this.collection;

        if (exampleTilemap)
        {
            _activeCollection = ScriptableObject.CreateInstance<TileBaseCollection>();
            _activeCollection.LoadFromTilemap(exampleTilemap);
        }

        (int coordinate, int index)[] startingValues = Array.Empty<(int,int)>();
        if (startingTilemap)
            startingValues = GetStartingValues(startingTilemap, _activeCollection);
        
        Debug.Log($"Example tilemap has {startingValues.Length} starting values");

        int nodeCount = size.x * size.y;

        int[][] adjacencies = new int[nodeCount][];
        
        for (int i = 0; i < nodeCount; i++)
        {
            adjacencies[i] = new int[4];

            int rowStartIndex = (i / size.x) * size.x;
            
            adjacencies[i][0] = Mathf.RoundToInt(Mathf.Repeat(Mathf.Repeat(i - 1, size.x) + rowStartIndex, nodeCount));
            adjacencies[i][1] = Mathf.RoundToInt(Mathf.Repeat(Mathf.Repeat(i + 1, size.x) + rowStartIndex, nodeCount));

            adjacencies[i][2] = Mathf.RoundToInt(Mathf.Repeat(i - size.x, nodeCount));
            adjacencies[i][3] = Mathf.RoundToInt(Mathf.Repeat(i + size.x, nodeCount));
        }
        
        
        builder = new Tilemap2DWfcBuilder(size, _activeCollection, adjacencies,randomSeed, startingValues);
        _queue = builder.Queue;

        if (_cancellationTokenSource is { IsCancellationRequested: false })
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        _cancellationTokenSource = new CancellationTokenSource();
        
        resultTilemap.ClearAllTiles();
        
        var res = await Task.Run(()=> builder.Build(_cancellationTokenSource.Token));

        if(res.Item1)
            Debug.Log("<COLOR=GREEN>SUCCESS</COLOR>");
        else
            Debug.Log("<COLOR=red>FAIL</COLOR>");

        if(!res.Item1 || _cancellationTokenSource.IsCancellationRequested) return;

        result = res.Item2;
        
        for (var index = 0; index < builder.Results.Length; index++)
        {
            int tileResult = builder.Results[index];
            resultTilemap.SetTile(IndexToCoordinate(index), _activeCollection.Elements[tileResult]);
        }
        
    }

    [Button]
    private (int coordinate,int index)[] GetStartingValues(Tilemap tilemap, TileBaseCollection tileBaseCollection)
    {
        List<(int, int)> startingValues = new List<(int, int)>();
        var cellBounds = tilemap.cellBounds;

        cellBounds.xMin = Mathf.Max(0, cellBounds.xMin);
        cellBounds.yMin = Mathf.Max(0, cellBounds.yMin);
        
        cellBounds.xMax = Mathf.Min(cellBounds.xMax, size.x);
        cellBounds.yMax = Mathf.Min(cellBounds.yMax, size.y);
        
        Dictionary<TileBase, int> tileToIndex = tileBaseCollection.GetTileToIndex();

        for (int x = cellBounds.xMin; x < cellBounds.xMax; x++)
        {
            for (int y = cellBounds.yMin; y < cellBounds.yMax; y++)
            {
                var coordinate = new Vector3Int(x, y, 0);
                var tile = tilemap.GetTile(coordinate);
                if (tile) startingValues.Add((CoordinateToIndex(coordinate), tileToIndex[tile]));
            }
        }
        return startingValues.ToArray();
    }

    [Button] public void Continue() => builder.Continue();

    [Button]
    public void Test()
    {
        IndexedPriorityQueue<int> queue = new IndexedPriorityQueue<int>(8);
        
        queue.Enqueue(0,10);
        queue.Enqueue(1,4);
        queue.Enqueue(2,3);
        queue.Enqueue(3,7);

        queue.Set(0,2);

        for (int i = 0; i < 5; i++)
        {
            Debug.Log(queue.DequeueIndex());
        }
        
    }

    [Button]
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(new Vector3(size.x,.1f,size.y)/2,new Vector3(size.x,0,size.y));
        
        
        if(drawPriorityQueue)
        {
#if UNITY_EDITOR

            int cycles = -1;
            float separation = .25f;
            int total = 0;

            while (total < PositionToIndex.Count)
            {
                cycles++;
                int count = Mathf.RoundToInt(Mathf.Pow(2, cycles));

                for (int i = 0; i < count; i++)
                {
                    int index = i + total;
                    if (index >= PositionToIndex.Count)
                        break;

                    var pos = GetPosition(cycles, separation, i);
                    UnityEditor.Handles.Label(pos, $"[{index}]{PositionToIndex[index]}:{(PositionToIndex[index] != -1 ? Priority[PositionToIndex[index]] : "-")}", GUI.skin.box);

                    Gizmos.color = Color.white;
                    if (_queue.HasParent(index))
                    {
                        Gizmos.DrawLine(pos, GetPosition(cycles - 1, separation, _queue.GetParentIndex(index) - Mathf.RoundToInt(Mathf.Pow(2, cycles-1)-1)));
                    }
                }

                total += count;

            }
#endif
        }

        if (drawTilesInfo)
        {
            if (builder == null) return;

            builder.DrawGizmos();

            if (resultsCount != builder.ResultCount) 
            {
                for (int countIndex = resultsCount; countIndex < builder.ResultCount; countIndex++)
                {
                    int tileIndex = builder.OrderedResults[countIndex];
                    _renderQueue.Enqueue((tileIndex));
                }

                for (int index = resultsCount; index > builder.ResultCount; --index)
                    resultTilemap.SetTile(IndexToCoordinate(index), null);
            
                resultsCount = builder.ResultCount;
            }
        }
  
        if (builder == null) return;
        
        if(!_activeCollection && constantUpdate) return;
        for (var index = 0; index < builder.Results.Length; index++)
        {
            int tileResult = builder.Results[index];
            if (tileResult != -1)
                resultTilemap.SetTile(IndexToCoordinate(index), _activeCollection.Elements[tileResult]);
            else
                resultTilemap.SetTile(IndexToCoordinate(index), null);
        }
    }

    private Vector3 GetPosition(int cycles, float separation, int index)
    {
        int count = Mathf.RoundToInt(Mathf.Pow(2, cycles));
        
        float width = count * .5f;
        
        float startOffset = -width * .5f;

        float normalizedPosition =  (float)(index+1) / (count+1);
        return transform.position + cycles * separation * Vector3.up + Vector3.right * (startOffset + normalizedPosition * width);
    }

    private Vector3 IndexToWorldPosition(int index) => new Vector3(index % size.x + .5f,0, index / size.x + .5f) ;
    private Vector3Int IndexToCoordinate(int index) => new Vector3Int(index % size.x, index / size.x);
    private int CoordinateToIndex(Vector3Int coordinate) => (coordinate.y * size.x) + coordinate.x;


    


}
