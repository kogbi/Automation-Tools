using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WormholeAutomationUI.Models;
using WormholeAutomationUI.Services;

namespace WormholeAutomationUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AutomationRunner _runner;
    private readonly IFileDialogService _fileDialogService;
    private CancellationTokenSource? _runCts;

    private string _windowTitle = "wormhole";
    private long _windowHandle;
    private double _matchConfidence = 0.9;
    private string _templatesFolder = string.Empty;
    private string _pinHost = "192.168.100.1";
    private int _pinPort = 6666;
    private string _doneMessage = "DONE";
    private bool _loopEnabled;
    private StepConfig? _selectedStep;
    private string _status = "Idle";

    public MainViewModel(IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
        _runner = new AutomationRunner();

        Steps = new ObservableCollection<StepConfig>();

        AddStepCommand = new RelayCommand(AddStep);
        RemoveStepCommand = new RelayCommand(RemoveStep, () => SelectedStep != null);
        MoveUpCommand = new RelayCommand(MoveUp, () => SelectedStep != null);
        MoveDownCommand = new RelayCommand(MoveDown, () => SelectedStep != null);
        PickTemplateCommand = new RelayCommand(PickTemplate, () => SelectedStep != null);
        PickTemplatesFolderCommand = new RelayCommand(PickTemplatesFolder);
        PickWindowCommand = new RelayCommand(PickWindowDelayed);
        SaveFlowCommand = new RelayCommand(SaveFlow);
        LoadFlowCommand = new RelayCommand(LoadFlow);
        PasteTemplateCommand = new RelayCommand(PasteTemplateFromClipboard, () => SelectedStep != null);
        ClearTemplateCommand = new RelayCommand(ClearTemplate, () => SelectedStep != null);
        RunCommand = new RelayCommand(async () => await RunAsync(), () => _runCts == null);
        StopCommand = new RelayCommand(Stop, () => _runCts != null);

        LoadDefaults();
        if (Steps.Count == 0)
        {
            AddStep();
        }

    }

    public ObservableCollection<StepConfig> Steps { get; }

    public StepConfig? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (SetField(ref _selectedStep, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetField(ref _windowTitle, value);
    }

    public long WindowHandle
    {
        get => _windowHandle;
        set => SetField(ref _windowHandle, value);
    }

    public double MatchConfidence
    {
        get => _matchConfidence;
        set => SetField(ref _matchConfidence, value);
    }

    public string TemplatesFolder
    {
        get => _templatesFolder;
        set => SetField(ref _templatesFolder, value);
    }

    public string PinHost
    {
        get => _pinHost;
        set => SetField(ref _pinHost, value);
    }

    public int PinPort
    {
        get => _pinPort;
        set => SetField(ref _pinPort, value);
    }

    public string DoneMessage
    {
        get => _doneMessage;
        set => SetField(ref _doneMessage, value);
    }

    public bool LoopEnabled
    {
        get => _loopEnabled;
        set => SetField(ref _loopEnabled, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public Array StepActions => Enum.GetValues(typeof(StepAction));
    public Array FailActions => Enum.GetValues(typeof(FailAction));

    public RelayCommand AddStepCommand { get; }
    public RelayCommand RemoveStepCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand PickTemplateCommand { get; }
    public RelayCommand PickTemplatesFolderCommand { get; }
    public RelayCommand PickWindowCommand { get; }
    public RelayCommand SaveFlowCommand { get; }
    public RelayCommand LoadFlowCommand { get; }
    public RelayCommand PasteTemplateCommand { get; }
    public RelayCommand ClearTemplateCommand { get; }
    public RelayCommand RunCommand { get; }
    public RelayCommand StopCommand { get; }

    private void AddStep()
    {
        var step = new StepConfig
        {
            Name = $"Step {Steps.Count + 1}"
        };
        Steps.Add(step);
        SelectedStep = step;
    }

    private void RemoveStep()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        Steps.Remove(SelectedStep);
        SelectedStep = index >= 0 && index < Steps.Count ? Steps[index] : Steps.LastOrDefault();
    }

    private void MoveUp()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index <= 0)
        {
            return;
        }

        Steps.Move(index, index - 1);
    }

    private void MoveDown()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index < 0 || index >= Steps.Count - 1)
        {
            return;
        }

        Steps.Move(index, index + 1);
    }

    private void LoadDefaults()
    {
        WindowTitle = "wormhole";
        WindowHandle = 0;
        MatchConfidence = 0.9;
        PinHost = "192.168.100.1";
        PinPort = 6666;
        DoneMessage = "DONE";
        LoopEnabled = false;
    }

    private void SaveFlow()
    {
        var path = _fileDialogService.PickSaveFlowFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var config = BuildConfigForSave();
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json, Encoding.UTF8);
        Status = $"Flow saved: {path}";
    }

    private void LoadFlow()
    {
        var path = _fileDialogService.PickLoadFlowFile();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        var config = JsonSerializer.Deserialize<AutomationConfig>(json) ?? new AutomationConfig();
        ApplyLoadedConfig(config);
        Status = $"Flow loaded: {path}";
    }

    private AutomationConfig BuildConfigForSave()
    {
        var config = new AutomationConfig
        {
            WindowHandle = 0,
            WindowTitle = WindowTitle,
            MatchConfidence = MatchConfidence,
            TemplatesFolder = TemplatesFolder,
            PinHost = PinHost,
            PinPort = PinPort,
            DoneMessage = DoneMessage,
            LoopEnabled = LoopEnabled
        };

        foreach (var step in Steps)
        {
            var savedStep = new StepConfig
            {
                Name = step.Name,
                Action = step.Action,
                TemplateFile = step.TemplateFile,
                TimeoutSec = step.TimeoutSec,
                StartX = step.StartX,
                StartY = step.StartY,
                EndX = step.EndX,
                EndY = step.EndY,
                Text = step.Text,
                UseValidation = step.UseValidation,
                FailAction = step.FailAction,
                FailTargetStepIndex = step.FailTargetStepIndex,
                FailMessage = step.FailMessage
            };

            var imageBytes = step.TemplateImageBytes;
            if ((imageBytes == null || imageBytes.Length == 0) && !string.IsNullOrWhiteSpace(step.TemplateFile) && File.Exists(step.TemplateFile))
            {
                imageBytes = File.ReadAllBytes(step.TemplateFile);
            }

            if (imageBytes != null && imageBytes.Length > 0)
            {
                savedStep.TemplateImageBase64 = Convert.ToBase64String(imageBytes);
            }

            config.Steps.Add(savedStep);
        }

        return config;
    }

    private void ApplyLoadedConfig(AutomationConfig config)
    {
        WindowHandle = 0;
        WindowTitle = config.WindowTitle;
        MatchConfidence = config.MatchConfidence;
        TemplatesFolder = config.TemplatesFolder;
        PinHost = config.PinHost;
        PinPort = config.PinPort;
        DoneMessage = config.DoneMessage;
        LoopEnabled = config.LoopEnabled;

        Steps.Clear();
        foreach (var step in config.Steps)
        {
            if (!string.IsNullOrWhiteSpace(step.TemplateImageBase64))
            {
                var bytes = Convert.FromBase64String(step.TemplateImageBase64);
                step.TemplateImageBytes = bytes;
                step.TemplatePreview = LoadPreview(bytes);
                if (string.IsNullOrWhiteSpace(step.TemplateFile))
                {
                    step.TemplateFile = "Embedded";
                }
            }

            Steps.Add(step);
        }

        SelectedStep = Steps.FirstOrDefault();
        if (SelectedStep == null)
        {
            AddStep();
        }
    }

    private void PickTemplate()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var file = _fileDialogService.PickImageFile();
        if (!string.IsNullOrWhiteSpace(file))
        {
            ApplyTemplateFromFile(file);
        }
    }

    public void ApplyTemplateFromFile(string file)
    {
        if (SelectedStep == null)
        {
            return;
        }

        var bytes = File.ReadAllBytes(file);
        SelectedStep.TemplateImageBytes = bytes;
        SelectedStep.TemplatePreview = LoadPreview(bytes);
        SelectedStep.TemplateFile = file;
    }

    private void PasteTemplateFromClipboard()
    {
        if (SelectedStep == null)
        {
            return;
        }

        if (!System.Windows.Clipboard.ContainsImage())
        {
            Status = "Clipboard has no image.";
            return;
        }

        var image = System.Windows.Clipboard.GetImage();
        if (image == null)
        {
            Status = "Clipboard image read failed.";
            return;
        }

        var bytes = EncodePng(image);
        SelectedStep.TemplateImageBytes = bytes;
        SelectedStep.TemplatePreview = LoadPreview(bytes);
        SelectedStep.TemplateFile = "Clipboard";
    }

    private void ClearTemplate()
    {
        if (SelectedStep == null)
        {
            return;
        }

        SelectedStep.TemplateImageBytes = null;
        SelectedStep.TemplatePreview = null;
        SelectedStep.TemplateFile = string.Empty;
    }

    private static byte[] EncodePng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static BitmapImage LoadPreview(byte[] bytes)
    {
        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(bytes);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void PickTemplatesFolder()
    {
        var folder = _fileDialogService.PickFolder();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            TemplatesFolder = folder;
        }
    }

    private async Task RunAsync()
    {
        _runCts = new CancellationTokenSource();
        RaiseCommandStates();
        Status = "Running...";

        try
        {
            var config = new AutomationConfig
            {
                WindowHandle = WindowHandle,
                WindowTitle = WindowTitle,
                MatchConfidence = MatchConfidence,
                TemplatesFolder = TemplatesFolder,
                PinHost = PinHost,
                PinPort = PinPort,
                DoneMessage = DoneMessage,
                LoopEnabled = LoopEnabled,
                Steps = Steps.ToList()
            };

            await _runner.RunAsync(config, _runCts.Token, UpdateStatus);
            Status = "Run completed.";
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
        }
        catch (Exception ex)
        {
            Status = $"Run failed: {ex.Message}";
        }
        finally
        {
            _runCts = null;
            RaiseCommandStates();
        }
    }

    private void Stop()
    {
        _runCts?.Cancel();
        Status = "Stopping...";
    }

    private async void PickWindowDelayed()
    {
        Status = "Switch to the target window within 3 seconds...";
        await Task.Delay(3000);
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            Status = "No foreground window detected.";
            return;
        }

        var title = GetWindowTitle(handle);
        if (string.IsNullOrWhiteSpace(title))
        {
            Status = "Foreground window has no title.";
            return;
        }

        WindowHandle = handle.ToInt64();
        WindowTitle = title;
        Status = $"Selected window: {title}";
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var sb = new StringBuilder(512);
        _ = GetWindowText(handle, sb, sb.Capacity);
        return sb.ToString();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    private void UpdateStatus(string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => Status = message);
    }

    private void RaiseCommandStates()
    {
        AddStepCommand.RaiseCanExecuteChanged();
        RemoveStepCommand.RaiseCanExecuteChanged();
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
        PickTemplateCommand.RaiseCanExecuteChanged();
        PasteTemplateCommand.RaiseCanExecuteChanged();
        ClearTemplateCommand.RaiseCanExecuteChanged();
        RunCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }
}
