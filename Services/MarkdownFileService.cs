using System.Text;
using System.Text.RegularExpressions;
using CosmoBoard.Models;

namespace CosmoBoard;

public sealed class MarkdownFileService
{
    private static readonly Regex DateFileRegex = new(@"^(?<date>\d{4}-\d{2}-\d{2})\.md$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TaskRegex = new(@"^\s*-\s*\[(?<done>[ xX])\]\s*(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex TaskInFileRegex = new(@"(?m)^(?<prefix>\s*-\s*\[)(?<done>[ xX])(?<suffix>\]\s*.*)$", RegexOptions.Compiled);
    private static readonly Regex TaskTextInFileRegex = new(@"(?m)^(?<prefix>\s*-\s*\[[ xX]\]\s*)(?<text>[^\r\n]*)", RegexOptions.Compiled);
    private static readonly Regex BulletRegex = new(@"^\s*-\s+(?<text>.*)$", RegexOptions.Compiled);

    public DailyEntry? LoadLatest(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return null;

        var candidate = Directory.EnumerateFiles(folder, "*.md", SearchOption.TopDirectoryOnly)
            .Select(path => (path, match: DateFileRegex.Match(Path.GetFileName(path))))
            .Where(x => x.match.Success)
            .Select(x =>
            {
                if (DateOnly.TryParseExact(x.match.Groups["date"].Value, "yyyy-MM-dd", out var date))
                    return (path: x.path, date: date, valid: true);
                return (path: x.path, date: default(DateOnly), valid: false);
            })
            .Where(x => x.valid)
            .OrderByDescending(x => x.date)
            .FirstOrDefault();

        if (!candidate.valid)
            return null;

        return ParseFile(candidate.path, candidate.date);
    }

    public DailyEntry? LoadDate(string? folder, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return null;

        var path = Path.Combine(folder, $"{date:yyyy-MM-dd}.md");
        return File.Exists(path) ? ParseFile(path, date) : null;
    }

    private static DailyEntry? ParseFile(string path, DateOnly date)
    {
        try
        {
            var lines = File.ReadAllLines(path);
            var tasks = new List<TaskItem>();
            var notes = new List<NoteItem>();
            var taskIndex = 0;

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var rawLine = lines[lineIndex];
                var line = rawLine.TrimEnd();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var task = TaskRegex.Match(line);
                if (task.Success)
                {
                    tasks.Add(new TaskItem(taskIndex++, task.Groups["text"].Value.Trim(), task.Groups["done"].Value is "x" or "X"));
                    continue;
                }

                var bullet = BulletRegex.Match(line);
                if (bullet.Success)
                {
                    var noteLines = new List<string> { bullet.Groups["text"].Value.Trim() };
                    var lastConsumedLine = lineIndex;

                    // Consecutive non-task bullets are one multiline note in CosmoNotes.
                    // Blank lines do not become notes and do not split the editable note.
                    for (var continuationIndex = lineIndex + 1; continuationIndex < lines.Length; continuationIndex++)
                    {
                        var continuationRaw = lines[continuationIndex];
                        if (string.IsNullOrWhiteSpace(continuationRaw))
                        {
                            lastConsumedLine = continuationIndex;
                            continue;
                        }

                        var continuation = continuationRaw.TrimEnd();
                        if (TaskRegex.IsMatch(continuation))
                            break;

                        var continuationBullet = BulletRegex.Match(continuation);
                        if (!continuationBullet.Success)
                            break;

                        noteLines.Add(continuationBullet.Groups["text"].Value.Trim());
                        lastConsumedLine = continuationIndex;
                    }

                    notes.Add(new NoteItem
                    {
                        LineIndex = lineIndex,
                        LineCount = lastConsumedLine - lineIndex + 1,
                        Text = string.Join(Environment.NewLine, noteLines),
                        IsEditable = true
                    });
                    lineIndex = lastConsumedLine;
                    continue;
                }

                var note = line.TrimStart();
                while (note.StartsWith("#")) note = note[1..];
                note = note.Trim();
                if (note.Length > 0)
                {
                    notes.Add(new NoteItem
                    {
                        LineIndex = lineIndex,
                        LineCount = 1,
                        Text = note,
                        IsEditable = false
                    });
                }
            }

            return new DailyEntry
            {
                Date = date,
                FilePath = path,
                Tasks = tasks,
                Notes = notes
            };
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public bool AppendTasks(string? folder, DateOnly date, IEnumerable<TaskItem> tasks)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return false;

        var taskList = tasks.ToList();
        if (taskList.Count == 0)
            return false;

        var path = Path.Combine(folder, $"{date:yyyy-MM-dd}.md");

        try
        {
            var prefix = string.Empty;
            Encoding encoding = new UTF8Encoding(false);

            if (File.Exists(path))
            {
                string existing;
                using (var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true))
                {
                    existing = reader.ReadToEnd();
                    encoding = reader.CurrentEncoding;
                }

                if (existing.Length > 0 && !existing.EndsWith("\n", StringComparison.Ordinal))
                    prefix = Environment.NewLine;
            }

            var lines = taskList.Select(task => $"- [{(task.IsCompleted ? "x" : " ")}] {task.Text.Trim()}");
            using var writer = new StreamWriter(path, append: true, encoding);
            writer.Write(prefix);
            writer.Write(string.Join(Environment.NewLine, lines));
            writer.Write(Environment.NewLine);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }


    public bool SaveNotes(string? folder, DateOnly date, string text)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return false;

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var noteLines = normalized.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var path = Path.Combine(folder, $"{date:yyyy-MM-dd}.md");

        // Do not create a brand-new daily file merely because an empty notes editor
        // received and lost focus.
        if (!File.Exists(path) && noteLines.Count == 0)
            return true;

        try
        {
            var existingLines = new List<string>();
            Encoding encoding = new UTF8Encoding(false);
            var newline = Environment.NewLine;

            if (File.Exists(path))
            {
                string existing;
                using (var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true))
                {
                    existing = reader.ReadToEnd();
                    encoding = reader.CurrentEncoding;
                }

                newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
                existingLines = existing.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n')
                    .Split('\n')
                    .ToList();

                if (existingLines.Count > 0 && existingLines[^1].Length == 0)
                    existingLines.RemoveAt(existingLines.Count - 1);
            }

            // CosmoNotes owns non-task bullet lines as notes. Remove the old note
            // bullets wherever they currently are, leaving tasks and any unrelated
            // Markdown untouched.
            existingLines = existingLines
                .Where(line => TaskRegex.IsMatch(line) || !BulletRegex.IsMatch(line))
                .ToList();

            // Collapse trailing empty lines so there is exactly one blank line before
            // the note block at the bottom of the file.
            while (existingLines.Count > 0 && string.IsNullOrWhiteSpace(existingLines[^1]))
                existingLines.RemoveAt(existingLines.Count - 1);

            if (noteLines.Count > 0)
            {
                if (existingLines.Count > 0)
                    existingLines.Add(string.Empty);

                foreach (var noteLine in noteLines)
                    existingLines.Add($"- {noteLine}");
            }

            var updated = string.Join(newline, existingLines);
            if (existingLines.Count > 0)
                updated += newline;

            using var writer = new StreamWriter(path, append: false, encoding);
            writer.Write(updated);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool AppendNote(string? folder, DateOnly date, string text)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return false;

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var noteLines = normalized.Split('\n')
            .Select(line => line.Trim())
            .ToList();
        while (noteLines.Count > 0 && noteLines[0].Length == 0) noteLines.RemoveAt(0);
        while (noteLines.Count > 0 && noteLines[^1].Length == 0) noteLines.RemoveAt(noteLines.Count - 1);
        if (noteLines.Count == 0 || noteLines.All(string.IsNullOrWhiteSpace))
            return false;

        var path = Path.Combine(folder, $"{date:yyyy-MM-dd}.md");

        try
        {
            var prefix = string.Empty;
            Encoding encoding = new UTF8Encoding(false);

            if (File.Exists(path))
            {
                string existing;
                using (var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true))
                {
                    existing = reader.ReadToEnd();
                    encoding = reader.CurrentEncoding;
                }

                if (existing.Length > 0 && !existing.EndsWith("\n", StringComparison.Ordinal))
                    prefix = Environment.NewLine;
            }

            using var writer = new StreamWriter(path, append: true, encoding);
            writer.Write(prefix);
            foreach (var noteLine in noteLines)
            {
                if (noteLine.Length == 0)
                    continue;
                writer.Write($"- {noteLine}");
                writer.Write(Environment.NewLine);
            }
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool UpdateTaskText(string filePath, int taskIndex, string newText)
    {
        newText = newText.Trim();
        if (newText.Length == 0)
            return false;

        try
        {
            if (!File.Exists(filePath))
                return false;

            string text;
            Encoding encoding;
            using (var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true))
            {
                text = reader.ReadToEnd();
                encoding = reader.CurrentEncoding;
            }

            var matches = TaskTextInFileRegex.Matches(text);
            if (taskIndex < 0 || taskIndex >= matches.Count)
                return false;

            var taskText = matches[taskIndex].Groups["text"];
            var updated = text[..taskText.Index] + newText + text[(taskText.Index + taskText.Length)..];

            using var writer = new StreamWriter(filePath, append: false, encoding);
            writer.Write(updated);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool SetTaskCompleted(string filePath, int taskIndex, bool completed)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            string text;
            Encoding encoding;
            using (var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true))
            {
                text = reader.ReadToEnd();
                encoding = reader.CurrentEncoding;
            }

            var matches = TaskInFileRegex.Matches(text);
            if (taskIndex < 0 || taskIndex >= matches.Count)
                return false;

            var done = matches[taskIndex].Groups["done"];
            var replacement = completed ? "x" : " ";
            var updated = text[..done.Index] + replacement + text[(done.Index + done.Length)..];

            using var writer = new StreamWriter(filePath, append: false, encoding);
            writer.Write(updated);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool UpdateNoteAndMoveToBottom(string filePath, int lineIndex, int lineCount, string newText)
    {
        try
        {
            if (!File.Exists(filePath) || lineIndex < 0 || lineCount < 1)
                return false;

            string text;
            Encoding encoding;
            using (var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true))
            {
                text = reader.ReadToEnd();
                encoding = reader.CurrentEncoding;
            }

            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var hadTrailingNewline = text.EndsWith("\r\n", StringComparison.Ordinal) || text.EndsWith("\n", StringComparison.Ordinal);
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();

            if (hadTrailingNewline && lines.Count > 0 && lines[^1].Length == 0)
                lines.RemoveAt(lines.Count - 1);

            if (lineIndex >= lines.Count || lineIndex + lineCount > lines.Count)
                return false;

            var current = lines[lineIndex];
            if (TaskRegex.IsMatch(current) || !BulletRegex.IsMatch(current))
                return false;

            lines.RemoveRange(lineIndex, lineCount);

            var normalized = newText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var noteLines = normalized.Split('\n').Select(line => line.Trim()).ToList();
            while (noteLines.Count > 0 && noteLines[0].Length == 0) noteLines.RemoveAt(0);
            while (noteLines.Count > 0 && noteLines[^1].Length == 0) noteLines.RemoveAt(noteLines.Count - 1);

            if (noteLines.Count > 0 && noteLines.Any(line => line.Length > 0))
            {
                foreach (var noteLine in noteLines)
                {
                    if (noteLine.Length > 0)
                        lines.Add($"- {noteLine}");
                }
            }

            var updated = string.Join(newline, lines);
            if (hadTrailingNewline || lines.Count > 0)
                updated += newline;

            using var writer = new StreamWriter(filePath, append: false, encoding);
            writer.Write(updated);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

}
