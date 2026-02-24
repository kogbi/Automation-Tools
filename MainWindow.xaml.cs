using System;
using System.IO;
using System.Windows;
using WormholeAutomationUI.Services;
using WormholeAutomationUI.ViewModels;

namespace WormholeAutomationUI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new FileDialogService());
    }

    private void OnToggleLanguage(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var lang = element.Tag as string;
        var next = string.Equals(lang, "en-US", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";
        element.Tag = next;
        LocalizationService.ApplyLanguage(next);
    }

    private void OnTemplateDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0 && File.Exists(files[0]))
            {
                vm.ApplyTemplateFromFile(files[0]);
            }
            return;
        }

        if (System.Windows.Clipboard.ContainsImage())
        {
            vm.PasteTemplateCommand.Execute(null);
        }
    }
}