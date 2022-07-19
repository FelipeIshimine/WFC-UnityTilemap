using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

public abstract class WaveFunctionCollapseBuilder
{
    private readonly (int coordinate, int valueIndex)[] _startingValues;
    protected readonly int NodeCount;
    protected readonly int[][] Adjacencies;
    public readonly int[] Entropy;
    public readonly int[,] NodesStates;
    public readonly int[] Results;
    public readonly int[,] Weight;
    private readonly int _nodeTypeCounts;

    private readonly int Directions;
    private readonly WfcCollection _compatibilityCollection;
    private readonly bool _stepByStep;

    public readonly int[] OrderedResults;
    public int ResultCount;
    
    protected readonly Stack<Command> History;

    private readonly IndexPriorityQueue _queue;

    private int _collapsingIndex;

    private bool _paused = false;
    private CancellationToken _cancellationToken;

    protected readonly List<int> Propagation = new List<int>();

    public IndexPriorityQueue Queue => _queue;

    private readonly System.Random _random;

    #region Commands

    protected abstract class Command
    {
        public readonly int Index;
        private bool _done = false;

        protected Command(int index)
        {
            Index = index;
        }

        public Command Do()
        {
            if (_done) throw new Exception("Cant Redo. Command already done");
            _done = true;
            OnDo();
            return this;
        }

        protected abstract void OnDo();

        public Command Undo()
        {
            if (!_done) throw new Exception("Cant Undo. command not done yet");
            _done = false;
            OnUndo();
            return this;
        }
        
        protected abstract void OnUndo();

    }
    
    protected class RemoveStateCommand : Command
    {
        private readonly WaveFunctionCollapseBuilder _builder;
        private readonly int _tileIndex;
        private readonly int _valueIndex;

        public RemoveStateCommand(WaveFunctionCollapseBuilder builder, int tileIndex, int valueIndex) : base(tileIndex)
        {
            _builder = builder;
            _tileIndex = tileIndex;
            _valueIndex = valueIndex;
        }

        protected override void OnDo()
        {
            _builder.RemovePossibility(_tileIndex, _valueIndex);
        }

        protected  override void OnUndo()
        {
            _builder.AddPossibility(_tileIndex, _valueIndex);
        }

        public override string ToString() => $"<color=orange>R</color> I:{_tileIndex} VI:{_valueIndex} V:{_builder.NodesStates[_tileIndex,_valueIndex]}";
    }

    protected class CollapseStateCommand : Command
    {
        private readonly WaveFunctionCollapseBuilder _builder;
        private readonly int _index;
        private int _oldValue;
        private int _oldEntropy;
        public readonly int SelectedStateIndex;

        public CollapseStateCommand(WaveFunctionCollapseBuilder builder, int index, int selectedStateIndex): base(index)
        {
            _builder = builder;
            _index = index;
            SelectedStateIndex = selectedStateIndex;
        }

        protected override void OnDo()
        {
            _oldEntropy = _builder.Entropy[_index];
            
            _builder.Swap(_index,0, SelectedStateIndex);
            for (int i = _builder.Entropy[_index] - 1; i >= 1; i--)
                _builder.RemovePossibility(_index,i);
            
            
            _oldValue = _builder.Results[_index];
         
            _builder.PushResult(_index);
            _builder.Results[_index] = _builder.NodesStates[_index,0];
            _builder.Entropy[_index] = 0;
        }

        protected override void OnUndo()
        {
            _builder.Entropy[_index] = _oldEntropy;
            _builder.Results[_index] = _oldValue;
            _builder.PopResult();
            
            for (int i = 1; i < _oldEntropy; i++)
                _builder.AddPossibility(_index,i);

            _builder.Swap(_index,0, SelectedStateIndex);
            
            /*if(!_builder.Queue.Contains(_index))
                _builder.Queue.Enqueue(_index, _oldEntropy);*/
            
        }
        
        public override string ToString() => $"<color=cyan>C</color> I:{_index} VI:{SelectedStateIndex} V:{_builder.Results[_index]}";
    }

    #endregion

    protected WaveFunctionCollapseBuilder(int nodeCount,  int nodeTypeCounts, int[][] adjacencies, WfcCollection compatibilityCollection, int seed) : this(nodeCount,nodeTypeCounts,adjacencies,compatibilityCollection,seed, Array.Empty<(int, int)>())
    {
    }
    
    protected WaveFunctionCollapseBuilder(int nodeCount,  int nodeTypeCounts, int[][] adjacencies, WfcCollection compatibilityCollection, int seed, (int,int)[] startingValues)
    {
        _nodeTypeCounts = nodeTypeCounts;
        _startingValues = startingValues;
        _random = new Random(seed);
        NodeCount = nodeCount;
        Adjacencies = adjacencies;
        _compatibilityCollection = compatibilityCollection;
        _stepByStep = false;
        Entropy = new int[nodeCount];
        NodesStates = new int[nodeCount,nodeTypeCounts];
        History = new Stack<Command>();
        _queue = new IndexPriorityQueue(nodeCount);
        Results = new int[nodeCount];
        Weight = new int[nodeCount, nodeTypeCounts];
        Directions = 4;

        HashSet<int> startingValuesIndex = new HashSet<int>();

        foreach ((int, int) tuple in startingValues)
            startingValuesIndex.Add(tuple.Item1);

        for (int i = 0; i < NodeCount; i++)
        {
            Results[i] = -1;
            
            /*if(!startingValuesIndex.Contains(i)) 
                _queue.Enqueue(i,nodeTypeCounts);
            for (int j = 0; j < nodeTypeCounts; j++)
                NodesStates[i, j] = j;*/

            for (int j = 0; j < nodeTypeCounts; j++)
            {
                NodesStates[i, j] = j;
                AddPossibility(i,j);
            }

            Entropy[i] = nodeTypeCounts;
        }

        foreach ((int index, int valueIndex) in startingValues)
        {
            Results[index] = NodesStates[index,valueIndex];
            Entropy[index] = 0;

            for (int j = 0; j < nodeTypeCounts; j++)
                NodesStates[index, j] = Results[index];
        }
        
        OrderedResults = new int[NodeCount];
        
        
        
    }

    public void Continue() => _paused = false;

    public async Task<(bool, int[])> Build(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        bool backtrack = false;
        Stack<Command> milestones = new Stack<Command>();
        
        if(_startingValues.Length == 0)
        {
            int startingIndex = _random.Next(0, NodeCount);
            _queue.Update(startingIndex, 0);
        }
        else
        {
            foreach (var value in _startingValues)
            {
                new CollapseStateCommand(this, value.coordinate, value.valueIndex).Do();/*
                var milestone = 
                        ;
                milestones.Push(milestone);
                History.Push(milestone);*/
                bool error = !await StartPropagation(value.coordinate);
                if (error) throw new Exception("Error");
            }
        }
  

        while (_queue.Size > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            if (backtrack)
            {
                do
                {
                    Command prevMilestone = null;
                    prevMilestone = milestones.Pop();

                    Command lastCommand;

                    do
                    {
                        lastCommand = History.Pop().Undo();
                    } while (lastCommand != prevMilestone);

               
                    if (prevMilestone is CollapseStateCommand collapseStateCommand)
                    {
                        if (Entropy[prevMilestone.Index] > 1) //Estado utilizable
                        {
                            backtrack = false;

                            if (Entropy[prevMilestone.Index] == 0)
                                throw new Exception("ENTROPY 0 ERROR");

                            _queue.EnqueueOrUpdate(prevMilestone.Index, Entropy[prevMilestone.Index]);
                            
                            
                            var command = new RemoveStateCommand(this, collapseStateCommand.Index, collapseStateCommand.SelectedStateIndex).Do();
                            History.Push(command);
                            milestones.Push(command);
                            
                        }
                    }

                } while (backtrack);

                await Pause();
            }

            if (ResultCount == Results.Length)
            {
                Debug.LogWarning("SUCCESSFUL FAUL. Se llego a un resultado, pero quedaronobjetos invalidos en cola");
                break;
            }
            
            int index = _queue.DequeueIndex();


            
            var milestone = new CollapseStateCommand(this, index, GetRandomIndex(index)).Do();
            
            
            
            milestones.Push(milestone);
            History.Push(milestone);

            //Propagate
            bool value = await StartPropagation(index); 
            if (!value)
                backtrack = true;
            
        }

        bool success = true;
        for (int i = 0; i < NodeCount; i++)
        {
            if (Entropy[i] > 0)
                success = false;
        }

        return (success, Results);
    }

    private int GetRandomIndex(int index)
    {
        int entropy = Entropy[index];

        int totalWeight = 0;

        for (int i = 0; i < entropy; i++)
            totalWeight += Weight[index,NodesStates[index,i]];
        int selectedWeight = 0;

        try
        {
            selectedWeight = _random.Next(0, totalWeight);
        }
        catch (Exception e)
        {
            Debug.Log(totalWeight);
            throw;
        }
       
        //Debug.Log($"SelectedWeight {selectedWeight}/{totalWeight}");
        totalWeight = 0;
        for (int i = 0; i < entropy; i++)
        {
            totalWeight += Weight[index, NodesStates[index,i]];
            if (selectedWeight < totalWeight)
                return i;
        }

        throw new Exception($"Value outside of weights. {index}> {selectedWeight}/{totalWeight}");
    }

    private async Task<bool> StartPropagation(int index)
    {
        Propagation.Clear();
        Propagation.Add(index);
        
        var selfState = Results[index];
        for (int dir = 0; dir < Adjacencies[index].Length; dir++)
        {
            int adjacentIndex = Adjacencies[index][dir];

            bool dirty = false;

            for (var adjacentStateIndex = Entropy[adjacentIndex] - 1; adjacentStateIndex >= 0; adjacentStateIndex--)
            {
                int adjacentState = NodesStates[adjacentIndex, adjacentStateIndex];
                if (!_compatibilityCollection.IsCompatible(selfState,adjacentState, dir))
                {
                    
                    if (Entropy[adjacentIndex] == 1)
                        return false;
                    
                    PushRemoveCommand(adjacentIndex, adjacentStateIndex);
                    
                    dirty = true;
                }
            }

            if (dirty)
            {
                bool value = await Propagate(adjacentIndex);
                if (!value)
                    return false;
            }
        }

        return true;
    }
    
    private async Task<bool> Propagate(int index)
    {
        Propagation.Add(index);

        StringBuilder builder = new StringBuilder();

        foreach (int i in Propagation)
            builder.Append($"{i}-");

        builder.Remove(builder.Length - 1, 1);
        
        await Pause();

        for (int dir = 0; dir < Adjacencies[index].Length; dir++)
        {
            int adjacentIndex = Adjacencies[index][dir];
            bool propagate = false;
            for (var adjacentStateIndex = Entropy[adjacentIndex] - 1; adjacentStateIndex >= 0; adjacentStateIndex--)
            {
                int adjacentState = NodesStates[adjacentIndex, adjacentStateIndex];
                bool remove = true;
                for (int i = 0; i < Entropy[index]; i++)
                {
                    var selfState = NodesStates[index, i];
                    
                    if (_compatibilityCollection.IsCompatible(selfState, adjacentState, dir))
                    {
                        remove = false;
                        break;
                    }
                }

                if (remove)
                {
                    if (Entropy[adjacentIndex] == 1)
                        return false;
                    
                    PushRemoveCommand(adjacentIndex, adjacentStateIndex);
                    propagate = true;
                }
            }
            if (propagate)
            {
                bool value = await Propagate(adjacentIndex);
                if (!value)
                    return false;
            }
        }
        
        Propagation.RemoveAt(Propagation.Count-1);
        return true;
    }

    private void PushRemoveCommand(int adjacentIndex, int adjacentStateIndex)
    {
        var command = new RemoveStateCommand(this, adjacentIndex, adjacentStateIndex).Do();
        History.Push(command);
    }

    private async Task Pause()
    {
        if(!_stepByStep) return;
        _paused = true;
        while (_paused && !_cancellationToken.IsCancellationRequested)
            await Task.Yield();
    }

  

    private void AddPossibility(int index, int i)
    {
        var count = Entropy[index]++;
        
        Swap(index, i, count);

        //if(_queue.Contains(index))
        
        if(Results[index] == -1) _queue.EnqueueOrUpdate(index, Entropy[index]);

        int tileIndex = NodesStates[index, i];
        for (int dir = 0; dir < Directions; dir++)
        {
            var neighbour = Adjacencies[index][dir];
            for (int neighbourTileIndex = 0; neighbourTileIndex < _nodeTypeCounts; neighbourTileIndex++)
                Weight[neighbour, neighbourTileIndex] += _compatibilityCollection.GetWeight(tileIndex,neighbourTileIndex,dir);
        }
        
    }
    
    
    //Retorna false si el estado es incompatible y no hay mas posibles estados
    private void RemovePossibility(int index, int i)
    {
        int tileIndex = NodesStates[index, i];
        
        var count = --Entropy[index];

        Swap(index, i, count);

        if(_queue.Contains(index))
            _queue.Update(index, Entropy[index]);
        
        for (int dir = 0; dir < Directions; dir++)
        {
            var neighbour = Adjacencies[index][dir];
            for (int neighbourTileIndex = 0; neighbourTileIndex < _nodeTypeCounts; neighbourTileIndex++)
                Weight[neighbour, neighbourTileIndex] -= _compatibilityCollection.GetWeight(tileIndex,neighbourTileIndex,dir);
        }
    }
    
    private void PushResult(int index) => OrderedResults[ResultCount++] = index;

    private void PopResult() => --ResultCount;

    private static readonly string[] ArrowSymbol = { "←","→","↓","↑" };

    private void Swap(int index, int x, int y) => (NodesStates[index, x], NodesStates[index, y]) = (NodesStates[index, y], NodesStates[index, x]);
    
    
}