using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CosmoBoard.Models;

namespace CosmoBoard;

public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private FileSystemWatcher? _watcher;
    private SettingsWindow? _settingsWindow;
    private DateTime _lastRefresh = DateTime.MinValue;
    private MenuItem? _topmostMenuItem;
    private MenuItem? _backgroundMenuItem;
    private DateOnly? _selectedDate;

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;

        Topmost = services.Settings.Current.AlwaysOnTop;
        Width = services.Settings.Current.WindowWidth;
        Height = services.Settings.Current.WindowHeight;
        Position = new PixelPoint(services.Settings.Current.WindowX, services.Settings.Current.WindowY);

        ApplyAppearance();
        BuildContextMenu();
        Opened += OnOpened;
        Closing += OnClosing;
        PropertyChanged += OnWindowPropertyChanged;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Refresh();
        StartWatcher();

        if (_services.Settings.Current.BackgroundMode)
            Hide();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowBounds();
        _watcher?.Dispose();
        _services.Settings.Save();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WidthProperty || e.Property == HeightProperty)
            SaveWindowBounds();
    }

    private void SaveWindowBounds()
    {
        _services.Settings.Current.WindowWidth = Width;
        _services.Settings.Current.WindowHeight = Height;
        _services.Settings.Current.WindowX = Position.X;
        _services.Settings.Current.WindowY = Position.Y;
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = BuildInfo.DisplayVersion, IsEnabled = false });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Refresh", Command = new RelayCommand(Refresh) });
        menu.Items.Add(new MenuItem { Header = "Open New CosmoNote", Command = new RelayCommand(OpenNewCosmoNote) });
        menu.Items.Add(new MenuItem { Header = "Change Date", Command = new RelayCommand(() => _ = ChangeDateAsync()) });
        menu.Items.Add(new MenuItem { Header = "Today", Command = new RelayCommand(GoToToday) });
        menu.Items.Add(new MenuItem { Header = "Add Task", Command = new RelayCommand(() => BeginAddTask(GetActiveDate())) });
        menu.Items.Add(new Separator());
        _topmostMenuItem = new MenuItem { Header = "Always on Top", IsChecked = Topmost, ToggleType = MenuItemToggleType.CheckBox, Command = new RelayCommand(ToggleTopmost) };
        _backgroundMenuItem = new MenuItem { Header = "Background / Tray", IsChecked = _services.Settings.Current.BackgroundMode, ToggleType = MenuItemToggleType.CheckBox, Command = new RelayCommand(ToggleBackgroundMode) };
        menu.Items.Add(_topmostMenuItem);
        menu.Items.Add(_backgroundMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Settings", Command = new RelayCommand(OpenSettings) });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Exit", Command = new RelayCommand(CloseThisWindow) });
        menu.Opening += (_, _) =>
        {
            if (_topmostMenuItem is not null) _topmostMenuItem.IsChecked = Topmost;
            if (_backgroundMenuItem is not null) _backgroundMenuItem.IsChecked = _services.Settings.Current.BackgroundMode;
        };

        RootBorder.ContextMenu = menu;
    }

    public void Refresh()
    {
        if ((DateTime.UtcNow - _lastRefresh).TotalMilliseconds < 150)
            return;
        _lastRefresh = DateTime.UtcNow;

        var folder = _services.Settings.Current.NotesFolder;
        var entry = _selectedDate is { } selectedDate
            ? _services.Markdown.LoadDate(folder, selectedDate)
            : _services.Markdown.LoadLatest(folder);
        ItemsPanel.Children.Clear();

        if (_selectedDate is null && entry is not null)
            _selectedDate = entry.Date;

        if (entry is null)
        {
            if (_selectedDate is { } requestedDate)
            {
                DateText.Text = requestedDate.ToString("yyyy-MM-dd");
                AddText(string.IsNullOrWhiteSpace(folder)
                    ? "Right-click → Settings to select a folder."
                    : "No file exists for this date yet.", muted: true);
            }
            else
            {
                DateText.Text = "No daily file";
                AddText(string.IsNullOrWhiteSpace(folder)
                    ? "Right-click → Settings to select a folder."
                    : "No YYYY-MM-DD.md file was found.", muted: true);
            }

            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                AddTaskButton(GetActiveDate());

            AddSectionTitle("Notes");
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                AddNotesEditor(GetActiveDate(), string.Empty);
            return;
        }

        DateText.Text = entry.Date.ToString("yyyy-MM-dd");

        if (entry.Tasks.Count > 0)
        {
            var taskHeader = AddSectionTitle("Task");
            taskHeader.ContextMenu = BuildTaskHeaderContextMenu(entry);
            foreach (var task in entry.Tasks)
                AddTask(entry, task);
        }

        AddTaskButton(entry.Date);

        AddSectionTitle("Notes");
        AddNotesEditor(entry.Date, string.Join(Environment.NewLine, entry.Notes.Select(note => note.Text)));
    }

    private FontFamily SelectedFontFamily
        => string.IsNullOrWhiteSpace(_services.Settings.Current.FontFamily) ||
           string.Equals(_services.Settings.Current.FontFamily, "Default", StringComparison.OrdinalIgnoreCase)
            ? FontManager.Current.DefaultFontFamily
            : new FontFamily(_services.Settings.Current.FontFamily);

    private Color FontColor => ParseColor(_services.Settings.Current.FontColor, "#302732");
    private Color BackgroundColor => ParseColor(_services.Settings.Current.BackgroundColor, "#D4BBD8");
    private Color ToggleBoxColor => ParseColor(_services.Settings.Current.ToggleBoxColor, "#72517A");

    private static Color ParseColor(string? value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(value) && Color.TryParse(value, out var color))
            return color;
        return Color.Parse(fallback);
    }

    private static Color Shade(Color color, double factor)
    {
        byte Scale(byte value) => (byte)Math.Clamp(Math.Round(value * factor), 0, 255);
        return new Color(color.A, Scale(color.R), Scale(color.G), Scale(color.B));
    }

    private static Color WithOpacity(Color color, double opacity)
        => new((byte)Math.Clamp(Math.Round(255 * opacity), 0, 255), color.R, color.G, color.B);

    private static bool IsDark(Color color)
    {
        // Relative luminance approximation is sufficient for choosing whether UI chrome
        // should move lighter or darker than the selected background.
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
        return luminance < 0.5;
    }

    private static Color BlendToward(Color color, Color target, double amount)
    {
        byte Blend(byte source, byte destination)
            => (byte)Math.Clamp(Math.Round(source + ((destination - source) * amount)), 0, 255);

        return new Color(color.A,
            Blend(color.R, target.R),
            Blend(color.G, target.G),
            Blend(color.B, target.B));
    }

    private Color SurfaceAccent(double amount)
        => BlendToward(BackgroundColor, IsDark(BackgroundColor) ? Colors.White : Colors.Black, amount);

    private void ApplyAppearance()
    {
        var background = BackgroundColor;
        var foreground = FontColor;

        Background = new SolidColorBrush(background);
        RootBorder.Background = new SolidColorBrush(background);
        RootBorder.BorderBrush = new SolidColorBrush(SurfaceAccent(0.18));
        Header.Background = new SolidColorBrush(SurfaceAccent(0.09));
        ResizeGlyph.Stroke = new SolidColorBrush(SurfaceAccent(0.30));

        // Keep note editors theme-aware and prevent Fluent's focused TextBox
        // template from substituting its default (black in dark theme) fill.
        Resources["NoteEditorIdleBrush"] = new SolidColorBrush(background);
        Resources["NoteEditorFocusBrush"] = new SolidColorBrush(SurfaceAccent(0.08));

        DateText.FontFamily = SelectedFontFamily;
        DateText.FontSize = _services.Settings.Current.TitleBarFontSize;
        DateText.Foreground = new SolidColorBrush(foreground);
    }

    private TextBlock AddSectionTitle(string title)
    {
        var header = new TextBlock
        {
            Text = title,
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize + 1,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(FontColor),
            Margin = new Thickness(0, 8, 0, 3)
        };
        ItemsPanel.Children.Add(header);
        return header;
    }

    private void AddTask(DailyEntry entry, TaskItem task)
    {
        var size = Math.Clamp(_services.Settings.Current.ToggleBoxSize, 10, 40);
        var toggleColor = ToggleBoxColor;
        var fontColor = FontColor;

        var mark = new TextBlock
        {
            Text = task.IsCompleted ? "✓" : string.Empty,
            FontFamily = FontManager.Current.DefaultFontFamily,
            FontSize = Math.Max(8, size * 0.72),
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var box = new Border
        {
            Width = size,
            Height = size,
            MinWidth = size,
            MinHeight = size,
            BorderThickness = new Thickness(Math.Max(1, size / 10)),
            BorderBrush = new SolidColorBrush(toggleColor),
            Background = task.IsCompleted ? new SolidColorBrush(toggleColor) : Brushes.Transparent,
            CornerRadius = new CornerRadius(Math.Max(2, size / 6)),
            Child = mark,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, Math.Max(0, (_services.Settings.Current.ContentFontSize - size) / 2 + 2), 0, 0)
        };

        var text = new TextBlock
        {
            Text = task.Text,
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize,
            LineHeight = Math.Max(_services.Settings.Current.ContentFontSize + 5, 13),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(fontColor),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 7,
            Margin = new Thickness(0, 1, 0, 2)
        };
        Grid.SetColumn(box, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(box);
        row.Children.Add(text);

        box.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(box).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
                return;

            if (_services.Markdown.SetTaskCompleted(entry.FilePath, task.Index, !task.IsCompleted))
            {
                _lastRefresh = DateTime.MinValue;
                Refresh();
            }

            e.Handled = true;
        };

        text.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(text).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
                return;

            BeginEditTask(row, text, entry, task);
            e.Handled = true;
        };

        row.ContextMenu = BuildTaskContextMenu(entry, task);
        ItemsPanel.Children.Add(row);
    }

    private void BeginEditTask(Grid row, TextBlock textBlock, DailyEntry entry, TaskItem task)
    {
        // Prevent opening a second editor for the same task.
        if (row.Children.OfType<TextBox>().Any(editor => Equals(editor.Tag, "task-edit")))
            return;

        var editor = new TextBox
        {
            Text = task.Text,
            Tag = "task-edit",
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize,
            Foreground = new SolidColorBrush(FontColor),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2, 0),
            Margin = new Thickness(-2, 0, 0, 0),
            AcceptsReturn = false,
            TextWrapping = TextWrapping.NoWrap,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        ApplyNoteEditorStyle(editor);
        Grid.SetColumn(editor, 1);

        row.Children.Remove(textBlock);
        row.Children.Add(editor);

        var finished = false;

        void Restore(string text)
        {
            if (finished)
                return;
            finished = true;
            textBlock.Text = text;
            row.Children.Remove(editor);
            if (!row.Children.Contains(textBlock))
                row.Children.Add(textBlock);
        }

        void Commit()
        {
            if (finished)
                return;

            var updatedText = editor.Text?.Trim() ?? string.Empty;
            if (updatedText.Length == 0 || string.Equals(updatedText, task.Text, StringComparison.Ordinal))
            {
                Restore(task.Text);
                return;
            }

            if (_services.Markdown.UpdateTaskText(entry.FilePath, task.Index, updatedText))
                Restore(updatedText);
            else
                Restore(task.Text);
        }

        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Restore(task.Text);
                e.Handled = true;
            }
        };
        editor.LostFocus += (_, _) => Commit();

        Dispatcher.UIThread.Post(() =>
        {
            editor.Focus();
            editor.SelectAll();
        }, DispatcherPriority.Background);
    }

    private ContextMenu BuildTaskContextMenu(DailyEntry entry, TaskItem task)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem
        {
            Header = "Move Task to Day",
            Command = new RelayCommand(() => _ = CopyTaskToDayAsync(entry, task))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Move Task to Today",
            IsEnabled = entry.Date != DateOnly.FromDateTime(DateTime.Today),
            Command = new RelayCommand(() => CopyTaskToDate(entry, task, DateOnly.FromDateTime(DateTime.Today)))
        });
        return menu;
    }

    private ContextMenu BuildTaskHeaderContextMenu(DailyEntry entry)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem
        {
            Header = "Add Task",
            Command = new RelayCommand(() => BeginAddTask(entry.Date))
        });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = "Move All Uncompleted Tasks to Day",
            Command = new RelayCommand(() => _ = CopyUncompletedTasksToDayAsync(entry))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Move All Uncompleted Tasks to Tomorrow",
            Command = new RelayCommand(() => CopyUncompletedTasksToDate(entry, entry.Date.AddDays(1)))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Move All Uncompleted Tasks to Today",
            IsEnabled = entry.Date != DateOnly.FromDateTime(DateTime.Today),
            Command = new RelayCommand(() => CopyUncompletedTasksToDate(entry, DateOnly.FromDateTime(DateTime.Today)))
        });
        return menu;
    }

    private async Task CopyTaskToDayAsync(DailyEntry entry, TaskItem task)
    {
        var target = await PickDateAsync("Move Task to Day", entry.Date);
        if (target is { } date)
            CopyTaskToDate(entry, task, date);
    }

    private void CopyTaskToDate(DailyEntry entry, TaskItem task, DateOnly targetDate)
    {
        if (targetDate == entry.Date)
            return;

        if (_services.Markdown.AppendTasks(_services.Settings.Current.NotesFolder, targetDate, [task]))
        {
            _lastRefresh = DateTime.MinValue;
            Refresh();
        }
    }

    private async Task CopyUncompletedTasksToDayAsync(DailyEntry entry)
    {
        var target = await PickDateAsync("Move Tasks to Day", entry.Date);
        if (target is { } date)
            CopyUncompletedTasksToDate(entry, date);
    }

    private void CopyUncompletedTasksToDate(DailyEntry entry, DateOnly targetDate)
    {
        if (targetDate == entry.Date)
            return;

        var tasks = entry.Tasks.Where(task => !task.IsCompleted).ToList();
        if (tasks.Count == 0)
            return;

        if (_services.Markdown.AppendTasks(_services.Settings.Current.NotesFolder, targetDate, tasks))
        {
            _lastRefresh = DateTime.MinValue;
            Refresh();
        }
    }

    private DateOnly GetActiveDate()
        => _selectedDate ?? DateOnly.FromDateTime(DateTime.Today);

    private void AddTaskButton(DateOnly date)
    {
        var button = new Button
        {
            Content = "+ Add Task",
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize,
            Foreground = new SolidColorBrush(WithOpacity(FontColor, 0.78)),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 2),
            Margin = new Thickness(0, 1, 0, 4),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        button.Click += (_, _) =>
        {
            var index = ItemsPanel.Children.IndexOf(button);
            if (index < 0)
                return;
            ItemsPanel.Children.RemoveAt(index);
            AddNewTaskEditor(date, index);
        };

        ItemsPanel.Children.Add(button);
    }

    private void BeginAddTask(DateOnly date)
    {
        // If a task editor is already visible, simply focus it.
        foreach (var child in ItemsPanel.Children)
        {
            if (child is TextBox existing && existing.Tag is string tag && tag == "new-task")
            {
                existing.Focus();
                return;
            }
        }

        // Insert before the Notes header when possible so the new task stays in the task area.
        var insertIndex = ItemsPanel.Children.Count;
        for (var i = 0; i < ItemsPanel.Children.Count; i++)
        {
            if (ItemsPanel.Children[i] is TextBlock text && text.Text == "Notes")
            {
                insertIndex = i;
                break;
            }
        }
        AddNewTaskEditor(date, insertIndex);
    }

    private void AddNewTaskEditor(DateOnly date, int insertIndex)
    {
        var editor = new TextBox
        {
            Text = string.Empty,
            Watermark = "Add task…",
            Tag = "new-task",
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2, 1),
            Margin = new Thickness(-2, -1, -2, 3),
            AcceptsReturn = false,
            TextWrapping = TextWrapping.Wrap
        };
        ApplyNoteEditorStyle(editor);

        var finished = false;
        void RestoreButton()
        {
            if (finished)
                return;
            finished = true;
            ItemsPanel.Children.Remove(editor);
            var button = new Button
            {
                Content = "+ Add Task",
                FontFamily = SelectedFontFamily,
                FontSize = _services.Settings.Current.ContentFontSize,
                Foreground = new SolidColorBrush(WithOpacity(FontColor, 0.78)),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 2),
                Margin = new Thickness(0, 1, 0, 4),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            button.Click += (_, _) =>
            {
                var index = ItemsPanel.Children.IndexOf(button);
                if (index < 0) return;
                ItemsPanel.Children.RemoveAt(index);
                AddNewTaskEditor(date, index);
            };
            ItemsPanel.Children.Insert(Math.Clamp(insertIndex, 0, ItemsPanel.Children.Count), button);
        }

        void Commit()
        {
            if (finished)
                return;

            var text = editor.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                RestoreButton();
                return;
            }

            finished = true;
            var task = new TaskItem(0, text, false);
            if (_services.Markdown.AppendTasks(_services.Settings.Current.NotesFolder, date, [task]))
            {
                _selectedDate = date;
                _lastRefresh = DateTime.MinValue;
                Refresh();
            }
            else
            {
                finished = false;
            }
        }

        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                RestoreButton();
                e.Handled = true;
            }
        };
        editor.LostFocus += (_, _) =>
        {
            if (!finished && !string.IsNullOrWhiteSpace(editor.Text))
                Commit();
        };

        insertIndex = Math.Clamp(insertIndex, 0, ItemsPanel.Children.Count);
        ItemsPanel.Children.Insert(insertIndex, editor);
        Dispatcher.UIThread.Post(() => editor.Focus(), DispatcherPriority.Background);
    }

    private void ApplyNoteEditorStyle(TextBox editor)
    {
        var foreground = new SolidColorBrush(FontColor);
        var selectionColor = SurfaceAccent(0.16);

        editor.Classes.Add("note-editor");
        editor.Foreground = foreground;
        editor.BorderBrush = Brushes.Transparent;
        editor.CaretBrush = foreground;
        editor.SelectionForegroundBrush = foreground;
        editor.SelectionBrush = new SolidColorBrush(selectionColor);
        editor.Background = Brushes.Transparent;
    }

    private void AddNotesEditor(DateOnly date, string initialText)
    {
        var originalText = initialText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var editor = new TextBox
        {
            Text = originalText,
            Watermark = "No notes. Click here to add notes…",
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 4),
            Margin = new Thickness(0, 0, 0, 2),
            MinHeight = 100,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        ApplyNoteEditorStyle(editor);

        var saving = false;
        void Commit()
        {
            if (saving)
                return;

            var updatedText = (editor.Text ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            if (string.Equals(updatedText, originalText, StringComparison.Ordinal))
                return;

            saving = true;
            if (_services.Markdown.SaveNotes(_services.Settings.Current.NotesFolder, date, updatedText))
            {
                _selectedDate = date;
                _lastRefresh = DateTime.MinValue;
                Refresh();
            }
            else
            {
                saving = false;
            }
        }

        editor.LostFocus += (_, _) => Commit();
        ItemsPanel.Children.Add(editor);
    }

    private void AddNoNotesPrompt(DateOnly date)
    {
        var prompt = new TextBlock
        {
            Text = "No notes.",
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize,
            Foreground = new SolidColorBrush(WithOpacity(FontColor, 0.62)),
            Cursor = new Cursor(StandardCursorType.Ibeam),
            Margin = new Thickness(0, 0, 0, 2)
        };

        prompt.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(prompt).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
                return;

            var index = ItemsPanel.Children.IndexOf(prompt);
            if (index < 0)
                return;

            ItemsPanel.Children.RemoveAt(index);
            AddNewNoteEditor(date, index);
            e.Handled = true;
        };

        ItemsPanel.Children.Add(prompt);
    }

    private void AddNewNoteEditor(DateOnly date, int insertIndex)
    {
        var editor = new TextBox
        {
            Text = string.Empty,
            Watermark = "Add note…",
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2, 1),
            Margin = new Thickness(-2, -1, -2, 1),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };
        ApplyNoteEditorStyle(editor);

        var finished = false;
        void Cancel()
        {
            if (finished)
                return;
            finished = true;
            ItemsPanel.Children.Remove(editor);
            AddNoNotesPrompt(date);
        }

        void Commit()
        {
            if (finished)
                return;

            var text = editor.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                Cancel();
                return;
            }

            finished = true;
            if (_services.Markdown.AppendNote(_services.Settings.Current.NotesFolder, date, text))
            {
                _selectedDate = date;
                _lastRefresh = DateTime.MinValue;
                Refresh();
            }
            else
            {
                finished = false;
            }
        }

        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Cancel();
                e.Handled = true;
            }
        };
        editor.LostFocus += (_, _) =>
        {
            if (!finished)
                Commit();
        };

        insertIndex = Math.Clamp(insertIndex, 0, ItemsPanel.Children.Count);
        ItemsPanel.Children.Insert(insertIndex, editor);
        Dispatcher.UIThread.Post(() => editor.Focus(), DispatcherPriority.Background);
    }

    private void AddEditableNote(DailyEntry entry, NoteItem note)
    {
        var originalText = note.Text;
        var committed = false;
        var editor = new TextBox
        {
            Text = note.Text,
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2, 1),
            Margin = new Thickness(0, 0, 0, 2),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };
        ApplyNoteEditorStyle(editor);

        void Commit()
        {
            if (committed)
                return;

            var updatedText = editor.Text?.Trim() ?? string.Empty;
            if (updatedText.Length == 0)
            {
                editor.Text = originalText;
                return;
            }

            if (string.Equals(updatedText, originalText, StringComparison.Ordinal))
                return;

            committed = true;
            if (_services.Markdown.UpdateNoteAndMoveToBottom(entry.FilePath, note.LineIndex, note.LineCount, updatedText))
            {
                _lastRefresh = DateTime.MinValue;
                Refresh();
            }
            else
            {
                editor.Text = originalText;
                committed = false;
            }
        }

        editor.LostFocus += (_, _) => Commit();

        ItemsPanel.Children.Add(editor);
    }

    private void AddText(string text, bool muted = false)
    {
        var color = muted ? WithOpacity(FontColor, 0.62) : FontColor;
        ItemsPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontFamily = SelectedFontFamily,
            FontSize = _services.Settings.Current.ContentFontSize,
            LineHeight = Math.Max(_services.Settings.Current.ContentFontSize + 5, 13),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(color),
            Margin = new Thickness(0, 0, 0, 2)
        });
    }

    private void StartWatcher()
    {
        // Deliberately do not watch the notes directory. A CosmoNote keeps the
        // date/content it currently displays until the user explicitly chooses
        // Refresh (or performs an action in this window that changes its own data).
        _watcher?.Dispose();
        _watcher = null;
    }

    private async Task ChangeDateAsync()
    {
        var initial = _selectedDate ?? DateOnly.FromDateTime(DateTime.Today);
        var date = await PickDateAsync("Change Date", initial);
        if (date is null)
            return;

        _selectedDate = date.Value;
        _lastRefresh = DateTime.MinValue;
        Refresh();
    }

    private void GoToToday()
    {
        _selectedDate = DateOnly.FromDateTime(DateTime.Today);
        _lastRefresh = DateTime.MinValue;
        Refresh();
    }

    private async Task<DateOnly?> PickDateAsync(string title, DateOnly initialDate)
    {
        var picker = new CalendarDatePicker
        {
            SelectedDate = initialDate.ToDateTime(TimeOnly.MinValue),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };

        var ok = new Button { Content = "OK", MinWidth = 72 };
        var cancel = new Button { Content = "Cancel", MinWidth = 72 };
        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, ok }
        };

        var dialog = new Window
        {
            Title = title,
            Width = 300,
            Height = 145,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 14,
                Children =
                {
                    new TextBlock { Text = "Select a date:" },
                    picker,
                    buttons
                }
            }
        };

        ok.Click += (_, _) =>
        {
            var selected = picker.SelectedDate;
            if (selected is null)
                return;
            dialog.Close(DateOnly.FromDateTime(selected.Value));
        };
        cancel.Click += (_, _) => dialog.Close((DateOnly?)null);

        return await dialog.ShowDialog<DateOnly?>(this);
    }

    private void OpenNewCosmoNote()
    {
        var window = new MainWindow(_services)
        {
            Position = new PixelPoint(Position.X + 24, Position.Y + 24),
            _selectedDate = this._selectedDate
        };
        window.Show();
        window.Activate();
    }

    private void ToggleTopmost()
    {
        Topmost = !Topmost;
        _services.Settings.Current.AlwaysOnTop = Topmost;
        if (_topmostMenuItem is not null) _topmostMenuItem.IsChecked = Topmost;
        _services.Settings.Save();
    }

    private void ToggleBackgroundMode()
    {
        _services.Settings.Current.BackgroundMode = !_services.Settings.Current.BackgroundMode;
        if (_backgroundMenuItem is not null) _backgroundMenuItem.IsChecked = _services.Settings.Current.BackgroundMode;
        _services.Settings.Save();
        if (_services.Settings.Current.BackgroundMode)
            Hide();
        else
            ShowFromTray();
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        Activate();
        Refresh();
    }

    public void OpenSettings()
    {
        if (_settingsWindow is { } existing)
        {
            existing.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_services.Settings.Current);
        _settingsWindow.Saved += SettingsWindow_Saved;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show(this);
    }

    private void SettingsWindow_Saved(object? sender, AppSettings settings)
    {
        _services.Settings.Current.NotesFolder = settings.NotesFolder;
        _services.Settings.Current.FontFamily = settings.FontFamily;
        _services.Settings.Current.TitleBarFontSize = settings.TitleBarFontSize;
        _services.Settings.Current.ContentFontSize = settings.ContentFontSize;
        _services.Settings.Current.ToggleBoxColor = settings.ToggleBoxColor;
        _services.Settings.Current.ToggleBoxSize = settings.ToggleBoxSize;
        _services.Settings.Current.BackgroundColor = settings.BackgroundColor;
        _services.Settings.Current.FontColor = settings.FontColor;
        _services.Settings.Save();
        ApplyAppearance();
        StartWatcher();
        _lastRefresh = DateTime.MinValue;
        Refresh();
    }

    private void CloseThisWindow()
    {
        Close();
    }

    public void ExitApplication()
    {
        _services.Settings.Save();
        (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void ResizeEdge_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        if (sender is not Control { Tag: string edgeName } || !Enum.TryParse<WindowEdge>(edgeName, out var edge))
            return;

        BeginResizeDrag(edge, e);
        e.Handled = true;
    }

}
