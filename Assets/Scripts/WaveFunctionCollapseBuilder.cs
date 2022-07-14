using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public abstract class WaveFunctionCollapseBuilder
{
    protected readonly int NodeCount;
    protected readonly int[][] Adjacencies;
    public readonly int[] Entropy;
    public readonly int[,] NodesStates;
    public readonly int[] Results;

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

    private HashSet<int> _bag = new HashSet<int>();

    public IndexPriorityQueue Queue => _queue;

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
            Debug.Log($"<color=green>>>></color> {ToString()}");
            return this;
        }

        protected abstract void OnDo();

        public Command Undo()
        {
            if (!_done) throw new Exception("Cant Undo. command not done yet");
            _done = false;
            OnUndo();
            Debug.Log($"<color=orange><<<</color> {ToString()}");
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
            _builder.Remove(_tileIndex, _valueIndex);
        }

        protected  override void OnUndo()
        {
            _builder.Add(_tileIndex, _valueIndex);
        }

        public override string ToString() => $"<color=orange>R</color> I:{_tileIndex} VI:{_valueIndex} V:{_builder.NodesStates[_tileIndex,_valueIndex]}";
    }

    protected class CollapseStateCommand : Command
    {
        private readonly WaveFunctionCollapseBuilder _builder;
        private readonly int _index;
        private int _oldValue;
        private int _oldEntropy;
        public int SelectedStateIndex;

        public CollapseStateCommand(WaveFunctionCollapseBuilder builder, int index): base(index)
        {
            _builder = builder;
            _index = index;
        }

        protected override void OnDo()
        {
            SelectedStateIndex = Random.Range(0, _builder.Entropy[_index]);

            _oldEntropy = _builder.Entropy[_index];
            _oldValue = _builder.Results[_index];
         
            _builder.PushResult(_index);
            _builder.Results[_index] = _builder.NodesStates[_index,SelectedStateIndex];
            _builder.Entropy[_index] = 0;
        }

        protected override void OnUndo()
        {
            _builder.Entropy[_index] = _oldEntropy;
            _builder.Results[_index] = _oldValue;
            _builder.PopResult();
        }
        
        public override string ToString() => $"<color=cyan>C</color> I:{_index} VI:{SelectedStateIndex} V:{_builder.Results[_index]}";
    }

    protected WaveFunctionCollapseBuilder(int nodeCount,  int nodeTypeCounts, int[][] adjacencies, WfcCollection compatibilityCollection, bool stepByStep = false)
    {
        NodeCount = nodeCount;
        Adjacencies = adjacencies;
        _compatibilityCollection = compatibilityCollection;
        _stepByStep = stepByStep;
        Entropy = new int[nodeCount];
        NodesStates = new int[nodeCount,nodeTypeCounts];
        History = new Stack<Command>();
        _queue = new IndexPriorityQueue(nodeCount);
        Results = new int[nodeCount];
        
        for (int i = 0; i < NodeCount; i++)
        {
            Results[i] = -1;
            _queue.Enqueue(i,nodeTypeCounts);
            for (int j = 0; j < nodeTypeCounts; j++)
                NodesStates[i, j] = j;

            Entropy[i] = nodeTypeCounts;
        }

        OrderedResults = new int[NodeCount];
    }

    public void Continue() => _paused = false;

    public async Task<(bool, int[])> Build(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        int startingIndex = Random.Range(0,NodeCount);
        _queue.Update(startingIndex,0);
        
        bool backtrack = false;
        Stack<Command> milestones = new Stack<Command>();

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

                            _bag.Remove(prevMilestone.Index);
                            _queue.Enqueue(prevMilestone.Index, Entropy[prevMilestone.Index]);
                            
                            var command = new RemoveStateCommand(this, collapseStateCommand.Index, collapseStateCommand.SelectedStateIndex).Do();
                            History.Push(command);
                            milestones.Push(command);
                            
                        }
                    }
                    

                } while (backtrack);

                await Pause();
            }

            var e = _queue.PeekPriority();
            int index = _queue.DequeueIndex();

            if (_bag.Contains(index))
                throw new Exception($"{index} was dequeue 2 times");
            
            _bag.Add(index);
            
            var milestone = new CollapseStateCommand(this, index).Do();
            milestones.Push(milestone);
            History.Push(milestone);
            

            //Propagate
            bool value = await StartPropagation(index); 
            if (!value)
                backtrack = true;
            
            await Task.Yield();
        }

        bool success = true;
        for (int i = 0; i < NodeCount; i++)
        {
            if (Entropy[i] > 0)
                success = false;
        }

        Debug.Log(success ? "<color=green> SUCCESS</color>" : "<color=red> FAIL </color>");

        return (success, Results);
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

    //Retorna false si el estado es incompatible y no hay mas posibles estados
    private void Remove(int index, int i)
    {
        var count = --Entropy[index];

        (NodesStates[index, i], NodesStates[index, count]) = (NodesStates[index, count], NodesStates[index, i]);
        _queue.Update(index, Entropy[index]);
    }

    private void Add(int index, int i)
    {
        var count = Entropy[index]++;
        (NodesStates[index, i], NodesStates[index, count]) = (NodesStates[index, count], NodesStates[index, i]);

        _queue.Update(index, Entropy[index]);
    }
    
    private void PushResult(int index) => OrderedResults[ResultCount++] = index;

    private void PopResult() => --ResultCount;

    private static readonly string[] ArrowSymbol = { "←","→","↓","↑" };

}