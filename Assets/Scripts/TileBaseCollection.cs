using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "WFC/Create WfcBaseCollection", fileName = "WfcBaseCollection", order = 0)]
public class TileBaseCollection : WfcCollection<TileBase>
{
    private List<TileBase> tiles = new List<TileBase>();

    [Button]
    public void LoadFromTilemap(Tilemap tilemap)
    {
        tiles.Clear();
        var cellBounds = tilemap.cellBounds;
        HashSet<TileBase> tileSet = new HashSet<TileBase>();

        Dictionary<TileBase, Multiset<TileBase>[]> adjacencies = new Dictionary<TileBase, Multiset<TileBase>[]>();

        for (int z = cellBounds.zMin; z < cellBounds.zMax; z++)
        {
            for (int y = cellBounds.yMin; y < cellBounds.yMax; y++)
            {
                for (int x = cellBounds.xMin; x < cellBounds.xMax; x++)
                {
                    var coordinate = new Vector3Int(x, y, z);
                    var tile = tilemap.GetTile(coordinate);
                    if(!tile) continue;

                    
                    tileSet.Add(tile);

                    if (!adjacencies.TryGetValue(tile, out var multiset))
                    {
                        adjacencies[tile] = multiset = new Multiset<TileBase>[6];
                        multiset[0] = new Multiset<TileBase>();
                        multiset[1] = new Multiset<TileBase>();
                        multiset[2] = new Multiset<TileBase>();
                        multiset[3] = new Multiset<TileBase>();
                        multiset[4] = new Multiset<TileBase>();
                        multiset[5] = new Multiset<TileBase>();
                    }
                    
                    //-X
                    var neighbour = tilemap.GetTile(coordinate + new Vector3Int(-1,0,0));
                    if (neighbour) multiset[0].Add(neighbour);

                    //+X
                    neighbour = tilemap.GetTile(coordinate + new Vector3Int(1,0,0));
                    if (neighbour) multiset[1].Add(neighbour);

                    //-Y
                    neighbour = tilemap.GetTile(coordinate + new Vector3Int(0,-1,0));
                    if (neighbour) multiset[2].Add(neighbour);

                    //+Y
                    neighbour = tilemap.GetTile(coordinate + new Vector3Int(0,1,0));
                    if (neighbour) multiset[3].Add(neighbour);

                    //-Z
                    neighbour = tilemap.GetTile(coordinate + new Vector3Int(0,0,-1));
                    if (neighbour) multiset[4].Add(neighbour);  

                    //+Z
                    neighbour = tilemap.GetTile(coordinate + new Vector3Int(0,0,1));
                    if (neighbour) multiset[5].Add(neighbour);  
                    
                }  
            }
        }

        tileSet.Remove(null);
        Elements = new TileBase[tileSet.Count];
        Compatibilities = new CompatibilityArray[tileSet.Count];
        
        int i = 0;
        foreach (TileBase tileBase in tileSet)
        {
            tiles.Add(tileBase);
            Elements[i] = tileBase;

            List<Side> sides = new List<Side>();

            for (var index = 0; index < adjacencies[tileBase].Length; index++)
            {
                List<Pair> pairs = new List<Pair>();
                var multiset = adjacencies[tileBase][index];

                foreach (var pair in multiset)
                    pairs.Add(new Pair(pair.Key, pair.Value));
                
                sides.Add(new Side(pairs.ToArray()));
            }
            Compatibilities[i++] = new CompatibilityArray(sides.ToArray());
        }
    }

    [Button]
    public Side GetAdjacencies(TileBase tileBase, int side)
    {
        for (int i = 0; i < Elements.Length; i++)
        {
            if (Elements[i] == tileBase)
            {
                return Compatibilities[i].Sides[side];
            }
        }
        return new Side();
    }

    public int GetIndexOf(TileBase tile) => tiles.IndexOf(tile);

    public Dictionary<TileBase, int> GetTileToIndex()
    {
        Dictionary<TileBase, int> map = new Dictionary<TileBase, int>();
        for (int i = 0; i < tiles.Count; i++)
            map[tiles[i]] = i;
        return map;
    }
}


public class Multiset<T>
{
    private readonly Dictionary<T, int> _values = new Dictionary<T,int>();
    
    public void Add(T value)
    {
        if (_values.TryGetValue(value, out var count))
            _values[value] = count+1;
        else
            _values[value] = 1;
    }
    
    public void Remove(T value)
    {
        _values[value]--;
    }

    public IEnumerator<KeyValuePair<T,int>> GetEnumerator() => _values.GetEnumerator();

}