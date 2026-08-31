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
        public DevSpaceFileNodeChildren Children { get; }
        public IReadOnlyList<DevSpaceFileTreeGuideSegment> TreeGuideSegments => _visibleTreeGuideSegments ?? BuildTreeGuideSegments();
        public bool ShowChildGuideStem => IsDirectory && Children.Count > 0 && (IsExpanded || _showVisibleChildGuideStem);

        internal DevSpaceFileNode Parent { get; set; }

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
                {
                    OnPropertyChanged(nameof(ExpansionGlyph));
                    OnPropertyChanged(nameof(ShowChildGuideStem));
                }
            }
        }

        public DevSpaceFileNode(string name, string relativePath, bool isDirectory, int depth)
        {
            Name = name;
            RelativePath = relativePath;
            IsDirectory = isDirectory;
            Depth = depth;
            Children = new DevSpaceFileNodeChildren(this);
        }

        internal void SetVisibleChildGuideStem(bool value)
        {
            if (_showVisibleChildGuideStem == value)
                return;

            _showVisibleChildGuideStem = value;
            OnPropertyChanged(nameof(ShowChildGuideStem));
        }

        internal void SetVisibleTreeGuideSegments(ISet<DevSpaceFileNode> visibleNodes)
        {
            _visibleTreeGuideSegments = BuildTreeGuideSegments(visibleNodes);
            OnPropertyChanged(nameof(TreeGuideSegments));
        }

        private IReadOnlyList<DevSpaceFileTreeGuideSegment> BuildTreeGuideSegments(ISet<DevSpaceFileNode> visibleNodes = null)
        {
            if (Parent == null)
                return [];

            var lineage = new Stack<DevSpaceFileNode>();
            for (var current = this; current.Parent != null; current = current.Parent)
                lineage.Push(current);

            var segments = new List<DevSpaceFileTreeGuideSegment>(lineage.Count);
            while (lineage.Count > 0)
            {
                var branchNode = lineage.Pop();
                var parent = branchNode.Parent;
                var isLastSibling = IsLastSibling(parent, branchNode, visibleNodes);
                var isCurrentNode = object.ReferenceEquals(branchNode, this);

                segments.Add(isCurrentNode
                    ? new DevSpaceFileTreeGuideSegment(true, !isLastSibling, true)
                    : new DevSpaceFileTreeGuideSegment(!isLastSibling, !isLastSibling, false));
            }

            return segments;
        }

        private static bool IsLastSibling(DevSpaceFileNode parent, DevSpaceFileNode branchNode, ISet<DevSpaceFileNode> visibleNodes)
        {
            if (visibleNodes == null)
                return parent.Children.Count == 0 || object.ReferenceEquals(parent.Children[^1], branchNode);

            for (var i = parent.Children.Count - 1; i >= 0; i--)
            {
                var sibling = parent.Children[i];
                if (visibleNodes.Contains(sibling))
                    return object.ReferenceEquals(sibling, branchNode);
            }

            return true;
        }

        private Models.Change _change;
        private bool _isExpanded;
        private bool _showVisibleChildGuideStem;
        private IReadOnlyList<DevSpaceFileTreeGuideSegment> _visibleTreeGuideSegments;
    }

    public sealed class DevSpaceFileNodeChildren : List<DevSpaceFileNode>
    {
        internal DevSpaceFileNodeChildren(DevSpaceFileNode parent)
        {
            _parent = parent;
        }

        public new void Add(DevSpaceFileNode item)
        {
            item.Parent = _parent;
            base.Add(item);
        }

        private readonly DevSpaceFileNode _parent;
    }
}
