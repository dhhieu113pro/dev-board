using System;

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

            DataContextChanged += (_, _) => UpdateDocument();
            ActualThemeVariantChanged += (_, _) => ApplyTheme();
            AttachedToLogicalTree += (_, _) =>
            {
                EnsureTextMate();
                UpdateDocument();
            };
            DetachedFromLogicalTree += (_, _) => DisposeTextMate();
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
            if (DataContext is not DevSpaceWorkspaceFile file)
            {
                _editor.Text = string.Empty;
                return;
            }

            _editor.Text = file.Content;
            EnsureTextMate();

            var grammarExtension = DevSpaceCodeLanguageResolver.ResolveGrammarExtension(file.Path);
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
        private RegistryOptions _registryOptions;
        private TextMateInstallation _textMateInstallation;
    }
}
