using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.ViewModels
{
    public sealed class DevSpaceWorkspaceFile : ObservableObject
    {
        public string Path { get; }

        public string Content
        {
            get => _content;
            private set
            {
                if (SetProperty(ref _content, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasContent));
            }
        }

        public string EditableContent
        {
            get => _editableContent;
            set => SetProperty(ref _editableContent, value ?? string.Empty);
        }

        public string Message
        {
            get => _message;
            internal set => SetProperty(ref _message, value ?? string.Empty);
        }

        public bool HasContent => !string.IsNullOrEmpty(Content);
        public bool CanEdit => _canEdit;
        public bool ShowEditButton => !IsEditing;
        public bool ShowEditingButtons => IsEditing;
        public bool ActionsEnabled => !IsBusy;
        public bool CanBeginEdit => CanEdit && !IsBusy;

        public bool IsEditing
        {
            get => _isEditing;
            private set
            {
                if (!SetProperty(ref _isEditing, value))
                    return;

                OnPropertyChanged(nameof(ShowEditButton));
                OnPropertyChanged(nameof(ShowEditingButtons));
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            internal set
            {
                if (!SetProperty(ref _isBusy, value))
                    return;

                OnPropertyChanged(nameof(ActionsEnabled));
                OnPropertyChanged(nameof(CanBeginEdit));
            }
        }

        public DevSpaceWorkspaceFile(string path, string content, string message = "")
        {
            Path = path;
            _content = content ?? string.Empty;
            _editableContent = _content;
            _message = message ?? string.Empty;
            _canEdit = string.IsNullOrEmpty(_message);
        }

        public void BeginEdit()
        {
            if (!CanBeginEdit)
                return;

            Message = string.Empty;
            EditableContent = Content;
            IsEditing = true;
        }

        public void CancelEdit()
        {
            if (IsBusy)
                return;

            EditableContent = Content;
            Message = string.Empty;
            IsEditing = false;
        }

        internal void CommitEdit()
        {
            Content = EditableContent;
            Message = string.Empty;
            IsEditing = false;
        }

        private readonly bool _canEdit;
        private string _content;
        private string _editableContent;
        private string _message;
        private bool _isEditing;
        private bool _isBusy;
    }
}
