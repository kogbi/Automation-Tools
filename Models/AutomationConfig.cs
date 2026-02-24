using System.Collections.Generic;

namespace WormholeAutomationUI.Models;

public class AutomationConfig
{
    public long WindowHandle { get; set; }
    public string WindowTitle { get; set; } = "wormhole";
    public double MatchConfidence { get; set; } = 0.9;
    public string TemplatesFolder { get; set; } = string.Empty;
    public string PinHost { get; set; } = "192.168.100.1";
    public int PinPort { get; set; } = 6666;
    public string DoneMessage { get; set; } = "DONE";
    public bool LoopEnabled { get; set; }
    public List<StepConfig> Steps { get; set; } = new();
}
