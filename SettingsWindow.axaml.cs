using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CosmoNote.Models;

namespace CosmoNote;

public sealed class FontOption
{
    public required string Name { get; init; }
    public required FontFamily Family { get; init; }
}

public partial class SettingsWindow : Window
{
    public event EventHandler<AppSettings>? Saved;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        FolderTextBox.Text = settings.NotesFolder ?? string.Empty;
        CarryOverCheckBox.IsChecked = settings.CarryOverUncompletedToToday;

        var fonts = FontManager.Current.SystemFonts
            .GroupBy(font => font.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(font => font.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(font => new FontOption { Name = font.Name, Family = font })
            .ToList();

        fonts.Insert(0, new FontOption
        {
            Name = "Default",
            Family = FontManager.Current.DefaultFontFamily
        });

        FontComboBox.ItemsSource = fonts;
        FontComboBox.SelectedItem = fonts.FirstOrDefault(x =>
            string.Equals(x.Name, settings.FontFamily, StringComparison.OrdinalIgnoreCase)) ?? fonts[0];

        TitleFontSizeBox.Value = (decimal)settings.TitleBarFontSize;
        ContentFontSizeBox.Value = (decimal)settings.ContentFontSize;
        ToggleSizeBox.Value = (decimal)settings.ToggleBoxSize;

        ToggleColorPicker.Color = ParseColor(settings.ToggleBoxColor, "#72517A");
        BackgroundColorPicker.Color = ParseColor(settings.BackgroundColor, "#D4BBD8");
        FontColorPicker.Color = ParseColor(settings.FontColor, "#302732");
    }

    private static Color ParseColor(string? value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(value) && Color.TryParse(value, out var color))
            return color;
        return Color.Parse(fallback);
    }

    private static string ToHex(Color color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Markdown folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
            FolderTextBox.Text = folders[0].Path.LocalPath;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var selectedFont = FontComboBox.SelectedItem as FontOption;
        var settings = new AppSettings
        {
            NotesFolder = string.IsNullOrWhiteSpace(FolderTextBox.Text) ? null : FolderTextBox.Text.Trim(),
            CarryOverUncompletedToToday = CarryOverCheckBox.IsChecked == true,
            FontFamily = selectedFont?.Name ?? "Default",
            TitleBarFontSize = (double)(TitleFontSizeBox.Value ?? 12),
            ContentFontSize = (double)(ContentFontSizeBox.Value ?? 12),
            ToggleBoxColor = ToHex(ToggleColorPicker.Color),
            ToggleBoxSize = (double)(ToggleSizeBox.Value ?? 16),
            BackgroundColor = ToHex(BackgroundColorPicker.Color),
            FontColor = ToHex(FontColorPicker.Color)
        };

        Saved?.Invoke(this, settings);
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
