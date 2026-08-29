using System;

namespace SourceGit.ViewModels
{
    public enum DevSpaceTerminalState
    {
        Created,
        Running,
        Exited,
        Failed,
        Stopping,
    }

    public sealed class DevSpaceTerminal : DevSpaceSession
    {
        public override DevSpaceSessionKind Kind => DevSpaceSessionKind.Terminal;

        public string Command { get; }

        public string WorkingDirectory { get; }

        public DevSpaceTerminalState State
        {
            get => _state;
            private set => SetProperty(ref _state, value);
        }

        public int ExitCode
        {
            get => _exitCode;
            private set => SetProperty(ref _exitCode, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public event Action<DevSpaceTerminal> StopRequested;

        public DevSpaceTerminal(string title, string command, string workingDirectory)
            : base(title)
        {
            Command = command;
            WorkingDirectory = workingDirectory;
        }

        public void MarkRunning()
        {
            State = DevSpaceTerminalState.Running;
        }

        public void MarkExited(int exitCode)
        {
            ExitCode = exitCode;
            State = DevSpaceTerminalState.Exited;
        }

        public void MarkFailed(string message)
        {
            ErrorMessage = message;
            State = DevSpaceTerminalState.Failed;
        }

        public override void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            State = DevSpaceTerminalState.Stopping;
            StopRequested?.Invoke(this);
        }

        private DevSpaceTerminalState _state = DevSpaceTerminalState.Created;
        private int _exitCode;
        private string _errorMessage = string.Empty;
        private bool _disposed;
    }
}
