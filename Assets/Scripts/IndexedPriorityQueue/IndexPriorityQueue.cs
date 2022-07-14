using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IndexPriorityQueue
{
    public readonly int MaxSize;

    public int Size { get; private set; }
    
    private readonly int[] _priority;
    private readonly int[] _positionToIndex;
    private readonly int[] _indexToPosition;
    
    private bool[] contains;

    public IReadOnlyList<int> Priority => _priority;
    public IReadOnlyList<int> PositionToIndex => _positionToIndex;
    public IReadOnlyList<int> IndexToPosition => _indexToPosition;

    public IndexPriorityQueue(int capacity)
    {
        MaxSize = capacity;
        Size = 0;
        _priority = new int[capacity];
        _positionToIndex = new int[capacity];
        _indexToPosition = new int[capacity];
        contains = new bool[capacity];
        for (var index = 0; index < capacity; index++)
        {
            _priority[index] = MaxValue();
            _positionToIndex[index] = -1;
            _indexToPosition[index] = -1;
        }
    }

    public void Enqueue(int index, int priority)
    {
        if (contains[index])
            throw new Exception("Value already in queue");
        
        contains[index] = true;
        Debug.Log($"Enqueue {index}:{priority}");

        _positionToIndex[Size] = index;
        _indexToPosition[index] = Size;
        _priority[index] = priority;
        Size++;
        Up(Size-1);
    }

    public bool IsEmpty() => Size == 0;

    public int PeekPriority() => _positionToIndex[0];
    public int PeekIndex() => _priority[_positionToIndex[0]];

    public int DequeueIndex()
    {
        if (Size == 0) 
            throw new Exception("Empty");
        
        int index = _positionToIndex[0];
        contains[index] = false;
        
        Swap(0, --Size);

        _positionToIndex[Size] = -1;
        _indexToPosition[index] = -1;
        
        return index;
    }


    public void Update(int index, int priority)
    {
        if(!contains[index])
            throw new Exception($"Index:{index} not in queue");

        int oldPriority = _priority[index];
        if(oldPriority == priority)
            return;
        
        _priority[index] = priority;
        
        if (priority < oldPriority)
            Up(_indexToPosition[index]);
        else
            Down(_indexToPosition[index]);
    }

    private void Up(int index)
    {
        while (HasParent(index) && Compare(index, GetParentIndex(index)))
        {
            int parentIndex = GetParentIndex(index);
            Swap(index,parentIndex);
            index = parentIndex;
        }
    }

    private void Down(int index)
    {
        while (HasLeftChild(index))
        {
            int smallestChildIndex = GetLeftChildIndex(index);
            int leftChildPriority = _priority[GetLeftChildIndex(index)];
            if (HasRightChild(index) && _priority[GetRightChildIndex(index)] < leftChildPriority)
                smallestChildIndex = GetRightChildIndex(index);

            if (Compare(index,smallestChildIndex))
                break;
            Swap(index,smallestChildIndex);
            index = smallestChildIndex;
        }
    }


    private bool Compare(int i, int j)
    {
        //Debug.Log($"Compare {i}|{j}     {_priority[_positionToIndex[i]]}<{_priority[_positionToIndex[j]]}");
        return _priority[_positionToIndex[i]] < _priority[_positionToIndex[j]]; 
    }

    private void Swap(int i, int j)
    {
        int iIndex = _positionToIndex[i];
        int jIndex = _positionToIndex[j];
            
        //Debug.Log($"Swap  I:{_positionToIndex[i]}|{_positionToIndex[j]}  Pos:{i}|{j} {_indexToPosition[iIndex]}|{_indexToPosition[jIndex]}");

        /*
        _positionToIndex[i] = 1;
        _positionToIndex[j] = 2;

        _indexToPosition[1] = i;
        _indexToPosition[2] = j;*/
        
        
        (_indexToPosition[iIndex], _indexToPosition[jIndex]) = (_indexToPosition[jIndex], _indexToPosition[iIndex]);
        (_positionToIndex[i], _positionToIndex[j]) = (_positionToIndex[j], _positionToIndex[i]);
        
        
        /*
        _positionToIndex[i] = 2;
        _positionToIndex[j] = 1;

        _indexToPosition[1] = j;
        _indexToPosition[2] = i;*/
        
        //_indexToPosition[_positionToIndex[i]] = j;
       // _indexToPosition[_positionToIndex[j]] = i;
       
       //Debug.Log($"Swap  I:{_positionToIndex[i]}|{_positionToIndex[j]}  Pos:{i}|{j} {_indexToPosition[iIndex]}|{_indexToPosition[jIndex]}");

    }
    
    public int GetParentIndex(int i) => Mathf.CeilToInt((i - 2f) / 2);
    public int GetLeftChildIndex(int i) => i * 2 + 1;
    public int GetRightChildIndex(int i) => i * 2 + 2;

    public bool HasLeftChild(int i) => GetLeftChildIndex(i) < Size;
    public bool HasRightChild(int i) => GetRightChildIndex(i) < Size;
    public bool HasParent(int i) => GetParentIndex(i) >= 0;


    private int MaxValue() => int.MaxValue;
    private int MinValue() => int.MinValue;
    

    
}
