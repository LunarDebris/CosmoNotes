namespace CosmoNote.Models;

public sealed class DailyEntry
{
    public DateOnly Date { get; init; }
    public string FilePath { get; init; } = "";
    public List<TaskItem> Tasks { get; init; } = [];
    public List<NoteItem> Notes { get; init; } = [];
}
