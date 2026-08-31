using System;
using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using DevBoard.ViewModels;
using TextMateSharp.Grammars;

using TextMateInstallation = AvaloniaEdit.TextMate.TextMate.Installation;

namespace DevBoard.Views
{
    public partial class CodeViewer : UserControl
    {
        public CodeViewer()
        {
            InitializeComponent();

            _editor = this.FindControl<TextEditor>("Editor");
            _editor.ShowLineNumbers = true;
            _editor.IsReadOnly = true;
            _editor.WordWrap = false;
            _editor.Options.HighlightCurrentLine = true;
            _editor.Options.EnableTextDragDrop = false;
            _editor.TextArea.RightClickMovesCaret = true;
            _editor.TextChanged += OnEditorTextChanged;

            DataContextChanged += OnDataContextChanged;
            ActualThemeVariantChanged += (_, _) => ApplyTheme();
            AttachedToLogicalTree += (_, _) =>
            {
                _isAttached = true;
                SetFile(DataContext as DevSpaceWorkspaceFile);
                EnsureTextMate();
                UpdateDocument();
            };
            DetachedFromLogicalTree += (_, _) =>
            {
                _isAttached = false;
                SetFile(null);
                DisposeTextMate();
            };
        }

        private void OnDataContextChanged(object sender, EventArgs e)
        {
            if (_isAttached)
                SetFile(DataContext as DevSpaceWorkspaceFile);
        }

        private void SetFile(DevSpaceWorkspaceFile file)
        {
            if (ReferenceEquals(_file, file))
            {
                UpdateDocument();
                return;
            }

            if (_file != null)
                _file.PropertyChanged -= OnFilePropertyChanged;

            _file = file;
            if (_file != null)
                _file.PropertyChanged += OnFilePropertyChanged;

            UpdateDocument();
        }

        private void OnFilePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DevSpaceWorkspaceFile.Content)
                or nameof(DevSpaceWorkspaceFile.EditableContent)
                or nameof(DevSpaceWorkspaceFile.IsEditing)
                or nameof(DevSpaceWorkspaceFile.IsBusy))
            {
                UpdateEditorState();
            }
        }

        private void OnEditorTextChanged(object sender, EventArgs e)
        {
            if (_updatingEditor || _file == null || !_file.IsEditing || _file.IsBusy)
                return;

            _file.EditableContent = _editor.Text ?? string.Empty;
        }

        private void EnsureTextMate()
        {
            if (_textMateInstallation != null)
                return;

            _registryOptions = new RegistryOptions(GetThemeName());
            _textMateInstallation = _editor.InstallTextMate(_registryOptions);
            _textMateInstallation.AppliedTheme += OnAppliedTheme;
            ApplyThemeColors(_textMateInstallation);
        }

        private void DisposeTextMate()
        {
            if (_textMateInstallation == null)
                return;

            _textMateInstallation.AppliedTheme -= OnAppliedTheme;
            _textMateInstallation.Dispose();
            _textMateInstallation = null;
            _registryOptions = null;
        }

        private void UpdateDocument()
        {
            UpdateEditorState();

            if (_file == null)
                return;

            EnsureTextMate();

            var grammarExtension = DevSpaceCodeLanguageResolver.ResolveGrammarExtension(_file.Path);
            var language = string.IsNullOrEmpty(grammarExtension)
                ? null
                : _registryOptions?.GetLanguageByExtension(grammarExtension);

            if (language == null || _registryOptions == null)
            {
                _textMateInstallation?.SetGrammar(null);
                return;
            }

            _textMateInstallation?.SetGrammar(_registryOptions.GetScopeByLanguageId(language.Id));
        }

        private void UpdateEditorState()
        {
            var text = _file == null
                ? string.Empty
                : _file.IsEditing ? _file.EditableContent : _file.Content;

            if (!string.Equals(_editor.Text, text, StringComparison.Ordinal))
            {
                _updatingEditor = true;
                try
                {
                    _editor.Text = text;
                }
                finally
                {
                    _updatingEditor = false;
                }
            }

            _editor.IsReadOnly = _file == null || !_file.IsEditing || _file.IsBusy;
        }

        private void ApplyTheme()
        {
            EnsureTextMate();
            var themeOptions = new RegistryOptions(GetThemeName());
            _textMateInstallation?.SetTheme(themeOptions.GetDefaultTheme());
        }

        private ThemeName GetThemeName() => ActualThemeVariant == ThemeVariant.Light
            ? ThemeName.LightPlus
            : ThemeName.DarkPlus;

        private void OnAppliedTheme(object sender, TextMateInstallation installation) => ApplyThemeColors(installation);

        private void ApplyThemeColors(TextMateInstallation installation)
        {
            ApplyBrush(installation, "editor.background", brush =>
            {
                _editor.Background = brush;
                _editor.TextArea.Background = brush;
            });
            ApplyBrush(installation, "editor.foreground", brush => _editor.Foreground = brush);
            ApplyBrush(installation, "editor.selectionBackground", brush => _editor.TextArea.SelectionBrush = brush);

            if (!ApplyBrush(installation, "editor.lineHighlightBackground", brush =>
                {
                    _editor.TextArea.TextView.CurrentLineBackground = brush;
                    _editor.TextArea.TextView.CurrentLineBorder = new Pen(brush);
                }))
            {
                _editor.TextArea.TextView.SetDefaultHighlightLineColors();
            }

            if (!ApplyBrush(installation, "editorLineNumber.foreground", brush => _editor.LineNumbersForeground = brush))
                _editor.LineNumbersForeground = _editor.Foreground;
        }

        private static bool ApplyBrush(TextMateInstallation installation, string key, Action<IBrush> apply)
        {
            if (!installation.TryGetThemeColor(key, out var colorText) || !Color.TryParse(colorText, out var color))
                return false;

            apply(new SolidColorBrush(color));
            return true;
        }

        private readonly TextEditor _editor;
        private DevSpaceWorkspaceFile _file;
        private RegistryOptions _registryOptions;
        private TextMateInstallation _textMateInstallation;
        private bool _updatingEditor;
        private bool _isAttached;
    }
}
