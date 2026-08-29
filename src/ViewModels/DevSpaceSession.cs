using System;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public enum DevSpaceSessionKind
    {
        Terminal,
        Roslyn,
    }

    public abstract class DevSpaceSession : ObservableObject, IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        public string Title { get; protected set; }

        public abstract DevSpaceSessionKind Kind { get; }

        protected DevSpaceSession(string title)
        {
            Title = title;
        }

        public abstract void Dispose();
    }
}
