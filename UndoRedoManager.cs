using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HBPakEditor
{
    public interface IUndoableCommand
    {
        void Execute();
        void Undo();
        string Description { get; }
    }

    public class UndoRedoManager
    {
        private readonly Stack<IUndoableCommand> _undoStack = new();
        private readonly Stack<IUndoableCommand> _redoStack = new();
        private const int MaxStackSize = 100;

        public event Action? StateChanged;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public string? UndoDescription => _undoStack.TryPeek(out var cmd) ? cmd.Description : null;
        public string? RedoDescription => _redoStack.TryPeek(out var cmd) ? cmd.Description : null;

        public void Execute(IUndoableCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();

            while (_undoStack.Count > MaxStackSize)
            {
                var temp = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = 0; i < temp.Length - 1; i++)
                    _undoStack.Push(temp[temp.Length - 1 - i]);
            }

            StateChanged?.Invoke();
        }

        public void Undo()
        {
            if (_undoStack.TryPop(out var command))
            {
                command.Undo();
                _redoStack.Push(command);
                StateChanged?.Invoke();
            }
        }

        public void Redo()
        {
            if (_redoStack.TryPop(out var command))
            {
                command.Execute();
                _undoStack.Push(command);
                StateChanged?.Invoke();
            }
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke();
        }
    }
}
