using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace WormholeAutomationUI.Services;

public class FileDialogService : IFileDialogService
{
    public string? PickImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
            Title = "Select Template Image"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Templates Folder"
        };

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    public string? PickSaveFlowFile()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Automation Flow (*.wormhole.json)|*.wormhole.json|JSON (*.json)|*.json",
            DefaultExt = "wormhole.json",
            Title = "Save Automation Flow"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickLoadFlowFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Automation Flow (*.wormhole.json)|*.wormhole.json|JSON (*.json)|*.json",
            Title = "Load Automation Flow"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
