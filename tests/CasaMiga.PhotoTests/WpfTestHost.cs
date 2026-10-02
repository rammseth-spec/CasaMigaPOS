using System.Windows;
using System.Windows.Threading;

namespace CasaMiga.PhotoTests;

internal static class WpfTestHost
{
    private static readonly Lazy<Task<Dispatcher>> Ui = new(Start);
    private static Task<Dispatcher> Start()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/CasaMiga.Presentation;component/Resources/Theme.xaml", UriKind.Relative) });
                ready.SetResult(app.Dispatcher);
                Dispatcher.Run();
            }
            catch (Exception error) { ready.TrySetException(error); }
        }) { IsBackground = true, Name = "WPF regression tests" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task;
    }
    public static async Task OnUi(Func<Task> action) => await (await Ui.Value).InvokeAsync(action).Task.Unwrap();
    public static Task OnUi(Action action) => OnUi(() => { action(); return Task.CompletedTask; });
    public static void Layout(FrameworkElement view, double width = 1100, double height = 1800)
    {
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); view.UpdateLayout();
    }
}
