namespace CosmoNote.Models;

public sealed class AppSettings
{
    public string? NotesFolder { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool BackgroundMode { get; set; }
    public bool CarryOverUncompletedToToday { get; set; }
    public string FontFamily { get; set; } = "Default";
    public double TitleBarFontSize { get; set; } = 12;
    public double ContentFontSize { get; set; } = 12;
    public string ToggleBoxColor { get; set; } = "#72517A";
    public double ToggleBoxSize { get; set; } = 16;
    public string BackgroundColor { get; set; } = "#FFEBA1";
    public string FontColor { get; set; } = "#302732";
    public double WindowWidth { get; set; } = 280;
    public double WindowHeight { get; set; } = 420;
    public int WindowX { get; set; } = 40;
    public int WindowY { get; set; } = 40;
}
