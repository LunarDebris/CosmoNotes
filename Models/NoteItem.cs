namespace CosmoNote.Models;

public sealed class NoteItem
{
    public int LineIndex { get; init; }
    public string Text { get; init; } = "";
    public int LineCount { get; init; } = 1;
    public bool IsEditable { get; init; }
}
