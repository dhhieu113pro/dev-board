using System;

namespace DevBoard.DevSpaces
{
    internal sealed class TerminalViewportState
    {
        public void Update(int columns, int rows)
        {
            if (columns <= 0 || rows <= 0)
                return;

            lock (_gate)
            {
                _columns = columns;
                _rows = rows;
                _apply?.Invoke(columns, rows);
            }
        }

        public void Attach(Action<int, int> apply)
        {
            ArgumentNullException.ThrowIfNull(apply);

            lock (_gate)
            {
                _apply = apply;
                apply(_columns, _rows);
            }
        }

        public void Detach()
        {
            lock (_gate)
                _apply = null;
        }

        public (int Columns, int Rows) Current
        {
            get
            {
                lock (_gate)
                    return (_columns, _rows);
            }
        }

        private readonly object _gate = new();
        private Action<int, int> _apply;
        private int _columns = 80;
        private int _rows = 25;
    }
}
