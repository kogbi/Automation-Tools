namespace WormholeAutomationUI.Services;

public interface IFileDialogService
{
    string? PickImageFile();
    string? PickFolder();
    string? PickSaveFlowFile();
    string? PickLoadFlowFile();
}
