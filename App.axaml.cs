using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using System.Windows.Input;

namespace CosmoNote;

public partial class App : Application
{
    public MainWindow? MainWindow { get; private set; }
    public AppServices Services { get; } = new();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow = new MainWindow(Services);
            desktop.MainWindow = MainWindow;

            var tray = new TrayIcon
            {
                Icon = LoadIcon(),
                ToolTipText = "CosmoNote",
                Menu = CreateTrayMenu()
            };
            tray.Clicked += (_, _) => MainWindow.ShowFromTray();
            TrayIcon.SetIcons(this, new TrayIcons { tray });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private WindowIcon? LoadIcon()
    {
        var uri = new Uri("avares://CosmoNote/Assets/cosmonote.ico");
        return new WindowIcon(AssetLoader.Open(uri));
    }

    private NativeMenu CreateTrayMenu()
    {
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem("Show CosmoNote") { Command = new RelayCommand(() => MainWindow?.ShowFromTray()) });
        menu.Items.Add(new NativeMenuItem("Refresh") { Command = new RelayCommand(() => MainWindow?.Refresh()) });
        menu.Items.Add(new NativeMenuItem("Settings") { Command = new RelayCommand(() => MainWindow?.OpenSettings()) });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Exit") { Command = new RelayCommand(() => MainWindow?.ExitApplication()) });
        return menu;
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}

public sealed class AppServices
{
    public SettingsService Settings { get; } = new();
    public MarkdownFileService Markdown { get; } = new();
    public GlobalNotesService GlobalNotes { get; } = new();
}
