using System.Windows;
using CasaMiga.Infrastructure;
namespace CasaMiga.Presentation;
public partial class App : System.Windows.Application
{
    private DirectoryLease? lease;
    private UnhandledFailureDiagnostics diagnostics = new(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CasaMigaPOS", "diagnostics"));
    private int terminating;
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            // Replace WPF's dialog, but always terminate; never resume the dispatcher.
            e.Handled = true;
            FatalOnUi(FailureOrigin.Dispatcher, e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            diagnostics.TryRecord(FailureOrigin.AppDomain, e.ExceptionObject as Exception);
            // Runtime terminates. Do not wait for Dispatcher or business locks here.
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            diagnostics.TryRecord(FailureOrigin.UnobservedTask, e.Exception);
            // Strict policy: an unknown task failure must not leave this POS running.
            Environment.Exit(1);
        };
    }
    private void FatalOnUi(FailureOrigin origin, Exception error)
    {
        if (Interlocked.Exchange(ref terminating, 1) != 0) Environment.Exit(1);
        var report = diagnostics.TryRecord(origin, error);
        try
        {
            if (MainWindow is { } window) window.IsEnabled = false;
            MessageBox.Show("Casa Miga encontró un error y debe cerrarse. El carrito temporal se perderá. " +
                "Vuelve a abrir la aplicación y revisa el estado de caja antes de continuar.\n\n" +
                $"Referencia: {report?.IncidentId ?? "registro no disponible"}", "Casa Miga · Cierre por error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { Environment.Exit(1); } // No normal close confirmation may cancel a fatal exit.
    }
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var options = FoundationOptions.Load();
            diagnostics = new(System.IO.Path.Combine(options.DataDirectory, "diagnostics"));
            lease = new DirectoryLease(options.DataDirectory);
            var model = new MainViewModel(); var window = new MainWindow { DataContext = model };
            if (options.IsDevelopment)
            {
                window.Title = DevelopmentBuildIdentity.WindowTitle(window.Title, options.IsDevelopment);
                if (options.PreviewWidth is >= 1024 and <= 1920 && options.PreviewHeight is >= 720 and <= 1080)
                { window.Width = options.PreviewWidth; window.Height = options.PreviewHeight; }
            }
            MainWindow = window; window.Show();
            await model.InitializeAsync(options);
            if (options.IsDevelopment && options.DiagnosticCrashOnStartup)
                _ = Dispatcher.BeginInvoke(new Action(() => throw new InvalidOperationException("DEMO-SECRET-MUST-NOT-BE-LOGGED")));
        }
        catch (Exception error) { FatalOnUi(FailureOrigin.Startup, error); }
    }
    protected override void OnExit(ExitEventArgs e) { lease?.Dispose(); base.OnExit(e); }
}
