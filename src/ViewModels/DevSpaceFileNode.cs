using System.Collections.Generic;

using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.ViewModels
{
    public sealed class DevSpaceFileNode : ObservableObject
    {
        public string Name { get; }
        public string RelativePath { get; }
        public bool IsDirectory { get; }
        public int Depth { get; }
        public Thickness Indent => new(Depth * 16, 0, 0, 0);
        public List<DevSpaceFileNode> Children { get; } = [];

        public DevSpaceFileIconKind IconKind => DevSpaceFileIconResolver.Resolve(Name, IsDirectory, Depth);
        public bool IsFolderIcon => IconKind == DevSpaceFileIconKind.Folder;
        public bool IsRootFolderIcon => IconKind == DevSpaceFileIconKind.RootFolder;
        public bool IsWebRootIcon => IconKind == DevSpaceFileIconKind.WebRoot;
        public bool IsJsonIcon => IconKind == DevSpaceFileIconKind.Json;
        public bool IsCSharpIcon => IconKind == DevSpaceFileIconKind.CSharp;
        public bool IsCSharpProjectIcon => IconKind == DevSpaceFileIconKind.CSharpProject;
        public bool IsSolutionIcon => IconKind == DevSpaceFileIconKind.Solution;
        public bool IsConfigIcon => IconKind == DevSpaceFileIconKind.Config;
        public bool IsXmlIcon => IconKind == DevSpaceFileIconKind.Xml;
        public bool IsMarkdownIcon => IconKind == DevSpaceFileIconKind.Markdown;
        public bool IsImageIcon => IconKind == DevSpaceFileIconKind.Image;
        public bool IsJavaScriptIcon => IconKind == DevSpaceFileIconKind.JavaScript;
        public bool IsTypeScriptIcon => IconKind == DevSpaceFileIconKind.TypeScript;
        public bool IsCssIcon => IconKind == DevSpaceFileIconKind.Css;
        public bool IsGenericFileIcon => IconKind == DevSpaceFileIconKind.File;
        public string ExpansionGlyph => IsExpanded ? "⌄" : "›";

        public Models.Change Change
        {
            get => _change;
            set
            {
                if (SetProperty(ref _change, value))
                {
                    OnPropertyChanged(nameof(State));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public Models.ChangeState State
        {
            get
            {
                if (_change == null)
                    return Models.ChangeState.None;

                return _change.WorkTree != Models.ChangeState.None
                    ? _change.WorkTree
                    : _change.Index;
            }
        }

        public string StatusText => State switch
        {
            Models.ChangeState.Modified => "M",
            Models.ChangeState.TypeChanged => "T",
            Models.ChangeState.Added => "A",
            Models.ChangeState.Deleted => "D",
            Models.ChangeState.Renamed => "R",
            Models.ChangeState.Copied => "C",
            Models.ChangeState.Untracked => "?",
            Models.ChangeState.Conflicted => "!",
            _ => string.Empty,
        };

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                    OnPropertyChanged(nameof(ExpansionGlyph));
            }
        }

        public DevSpaceFileNode(string name, string relativePath, bool isDirectory, int depth)
        {
            Name = name;
            RelativePath = relativePath;
            IsDirectory = isDirectory;
            Depth = depth;
        }

        private Models.Change _change;
        private bool _isExpanded;
    }
}
