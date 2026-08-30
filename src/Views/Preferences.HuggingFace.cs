using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class Preferences
    {
        private void InitializeHuggingFacePanels()
        {
            EnsureHuggingFacePanels();
            if (_huggingFaceLayoutHooked)
                return;

            LayoutUpdated += (_, _) => EnsureHuggingFacePanels();
            _huggingFaceLayoutHooked = true;
        }

        private void EnsureHuggingFacePanels()
        {
            foreach (var stack in this.GetVisualDescendants().OfType<StackPanel>())
            {
                if (stack.DataContext is not AI.Service { IsLocalLlm: true } service)
                    continue;
                if (stack.Children.OfType<HuggingFaceDownloadPanel>().Any())
                    continue;

                var modelLabelIndex = -1;
                for (var i = 0; i < stack.Children.Count; i++)
                {
                    if (stack.Children[i] is TextBlock { Text: "Default Model (.gguf)" })
                    {
                        modelLabelIndex = i;
                        break;
                    }
                }

                if (modelLabelIndex < 0)
                    continue;

                var insertIndex = Math.Min(modelLabelIndex + 3, stack.Children.Count);
                stack.Children.Insert(insertIndex, new HuggingFaceDownloadPanel(service));
            }
        }

        private bool _huggingFaceLayoutHooked = false;
    }
}
