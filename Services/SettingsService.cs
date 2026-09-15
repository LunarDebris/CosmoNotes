using System.Text.Json;
using CosmoBoard.Models;

namespace CosmoBoard;

public sealed class SettingsService
{
    private readonly string _filePath;
    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(baseDir, "CosmoNotes");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");

        // Preserve settings from the previous CosmoBoard application name.
        var oldFilePath = Path.Combine(baseDir, "CosmoBoard", "settings.json");
        if (!File.Exists(_filePath) && File.Exists(oldFilePath))
        {
            try
            {
                File.Copy(oldFilePath, _filePath);
            }
            catch
            {
                // If migration fails, fall back to defaults rather than blocking startup.
            }
        }

        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
