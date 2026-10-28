using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ResumeMaker.Desktop.Services;
using ResumeMaker.Desktop.Views;
using ResumeMaker.Infrastructure;

namespace ResumeMaker.Desktop;

public sealed class App : Application
{
    private CompositionRoot? _root;
    private DispatcherTimer? _backupTimer;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            TopLevel? Top() => window;
            _root = CompositionRoot.Create(
                InfrastructureFactory.Create(),
                new AvaloniaClipboardService(Top),
                new AvaloniaFilePicker(Top),
                new SystemLauncher(),
                new AvaloniaThemeService()
            );
            window.DataContext = _root.Main;
            window.Opened += async (_, _) => await StartAsync();
            window.Closing += (_, _) => Shutdown();
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartAsync()
    {
        var root = _root!;
        await root.Main.InitializeAsync();
        var interval = await root.Backup.GetIntervalAsync();
        _backupTimer = new DispatcherTimer { Interval = interval };
        _backupTimer.Tick += async (_, _) => await root.Backup.TickAsync();
        _backupTimer.Start();
    }

    private void Shutdown()
    {
        _backupTimer?.Stop();
        if (_root is not { } root)
            return;
        // Flush pending edits and take a last snapshot; the repository uses ConfigureAwait(false), so blocking is safe.
        Task.Run(async () =>
            {
                await root.Session.FlushAsync();
                await root.Backup.TickAsync();
            })
            .Wait(TimeSpan.FromSeconds(5));
        root.Dispose();
    }
}
