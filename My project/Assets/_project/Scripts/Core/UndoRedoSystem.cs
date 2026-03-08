using System;
using System.Collections.Generic;

namespace Maptifier.Core
{
    public interface IUndoableCommand
    {
        void Execute();
        void Undo();
        string Description { get; }
    }

    public class UndoRedoSystem
    {
        private readonly List<IUndoableCommand> _undoStack;
        private readonly List<IUndoableCommand> _redoStack;
        private readonly int _maxSteps;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public event Action OnStateChanged;

        public UndoRedoSystem(int maxSteps = 20)
        {
            _maxSteps = maxSteps;
            _undoStack = new List<IUndoableCommand>(maxSteps);
            _redoStack = new List<IUndoableCommand>(maxSteps);
        }

        public void Execute(IUndoableCommand command)
        {
            command.Execute();
            _undoStack.Add(command);
            if (_undoStack.Count > _maxSteps)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
            OnStateChanged?.Invoke();
        }

        public void Undo()
        {
            if (!CanUndo) return;
            var cmd = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            cmd.Undo();
            _redoStack.Add(cmd);
            OnStateChanged?.Invoke();
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var cmd = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            cmd.Execute();
            _undoStack.Add(cmd);
            OnStateChanged?.Invoke();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnStateChanged?.Invoke();
        }
    }
}
