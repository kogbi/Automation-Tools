using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace WormholeAutomationUI.Models;

public class StepConfig : INotifyPropertyChanged
{
    private string _name = "Step";
    private StepAction _action = StepAction.ClickImage;
    private string _templateFile = string.Empty;
    private int _timeoutSec = 60;
    private int _startX;
    private int _startY;
    private int _endX;
    private int _endY;
    private string _text = string.Empty;
    private bool _useValidation;
    private FailAction _failAction = FailAction.Continue;
    private int _failTargetStepIndex;
    private string _failMessage = string.Empty;
    private byte[]? _templateImageBytes;
    private ImageSource? _templatePreview;
    private string _templateImageBase64 = string.Empty;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public StepAction Action
    {
        get => _action;
        set => SetField(ref _action, value);
    }

    public string TemplateFile
    {
        get => _templateFile;
        set => SetField(ref _templateFile, value);
    }

    public int TimeoutSec
    {
        get => _timeoutSec;
        set => SetField(ref _timeoutSec, value);
    }

    public int StartX
    {
        get => _startX;
        set => SetField(ref _startX, value);
    }

    public int StartY
    {
        get => _startY;
        set => SetField(ref _startY, value);
    }

    public int EndX
    {
        get => _endX;
        set => SetField(ref _endX, value);
    }

    public int EndY
    {
        get => _endY;
        set => SetField(ref _endY, value);
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public bool UseValidation
    {
        get => _useValidation;
        set => SetField(ref _useValidation, value);
    }

    public FailAction FailAction
    {
        get => _failAction;
        set => SetField(ref _failAction, value);
    }

    public int FailTargetStepIndex
    {
        get => _failTargetStepIndex;
        set => SetField(ref _failTargetStepIndex, value);
    }

    public string FailMessage
    {
        get => _failMessage;
        set => SetField(ref _failMessage, value);
    }

    public string TemplateImageBase64
    {
        get => _templateImageBase64;
        set => SetField(ref _templateImageBase64, value);
    }

    [JsonIgnore]
    public byte[]? TemplateImageBytes
    {
        get => _templateImageBytes;
        set => SetField(ref _templateImageBytes, value);
    }

    [JsonIgnore]
    public ImageSource? TemplatePreview
    {
        get => _templatePreview;
        set => SetField(ref _templatePreview, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
