namespace WindowsTimer.ViewModels;

public sealed record PresetOption(int Minutes, string Label, string ToolTip)
{
    public override string ToString() => ToolTip;
}
