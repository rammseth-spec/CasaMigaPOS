using System.Windows;
using System.Windows.Controls;

namespace CasaMiga.Presentation;

public partial class TicketsView : UserControl
{
    public TicketsView() => InitializeComponent();
    private TicketsViewModel Model => (TicketsViewModel)DataContext;
    private async void RefreshClick(object sender, RoutedEventArgs e) => await Model.RefreshAsync();
    private async void ReprintClick(object sender, RoutedEventArgs e) => await Model.ReprintAsync();
    private async void CancelJobClick(object sender, RoutedEventArgs e)
    {
        if (Model.SelectedJob is not { } job) return;
        if (MessageBox.Show(Window.GetWindow(this), $"¿Cancelar el trabajo {job.JobId} de la cola de impresión? La venta no cambia; si el ticket hace falta, se puede reimprimir.",
            "Cancelar trabajo de impresión", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        await Model.CancelJobAsync();
    }
    private async void ClearQueueClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this), $"¿Cancelar los {Model.QueueJobs.Count} trabajos pendientes de la cola? Las ventas no cambian.",
            "Vaciar cola de impresión", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        await Model.ClearQueueAsync();
    }
}
