using System.Text;

namespace CosmoNote;

/// <summary>Shared notes stored independently from the selected daily-notes folder.</summary>
public sealed class GlobalNotesService
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CosmoNote", "global-notes.txt");

    public string Load()
    {
        try { return File.Exists(_path) ? File.ReadAllText(_path) : string.Empty; }
        catch (IOException) { return string.Empty; }
        catch (UnauthorizedAccessException) { return string.Empty; }
    }

    public bool Save(string notes)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, notes, new UTF8Encoding(false));
                File.Move(temp, _path, overwrite: true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
