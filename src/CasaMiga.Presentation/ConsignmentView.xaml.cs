using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CasaMiga.Application;

namespace CasaMiga.Presentation;

public partial class ConsignmentView : UserControl
{
    private ScrollViewer? page;
    /// <summary>Relleno (28 + 28) y borde (1 + 1) de la tarjeta de la página que contiene la vista.</summary>
    public const double WorkspaceInset = 58;

    public ConsignmentView()
    {
        InitializeComponent();
        // Al entregar, tarjetas y charola llenan la ventana como en el Punto de venta; cada lista se desplaza por su cuenta.
        Loaded += (_, _) =>
        {
            if (page is not null) { FitWorkspace(page.ActualHeight); return; }
            for (DependencyObject? parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is ScrollViewer viewer) { page = viewer; break; }
            if (page is null) return;
            // Usar la altura nueva del evento: ViewportHeight todavía conserva la anterior cuando cambia el tamaño (F11, maximizar).
            page.SizeChanged += (_, e) => FitWorkspace(e.NewSize.Height);
            FitWorkspace(page.ActualHeight);
        };
    }

    private void FitWorkspace(double pageHeight)
    {
        DeliverWorkspace.Height = Math.Max(DeliverWorkspace.MinHeight, pageHeight - WorkspaceInset);
        CollectWorkspace.Height = Math.Max(CollectWorkspace.MinHeight, pageHeight - WorkspaceInset);
    }
    private ConsignmentViewModel Model => (ConsignmentViewModel)DataContext;
    private static T? Item<T>(object sender) where T : class => (sender as FrameworkElement)?.DataContext as T;

    private async void RouteClick(object sender, RoutedEventArgs e) => await Model.ShowAsync(ConsignmentTab.Route);
    private async void StoresClick(object sender, RoutedEventArgs e) => await Model.ShowAsync(ConsignmentTab.Stores);
    private async void PricesClick(object sender, RoutedEventArgs e) => await Model.ShowAsync(ConsignmentTab.Prices);
    private async void ReportClick(object sender, RoutedEventArgs e) => await Model.ShowAsync(ConsignmentTab.Report);

    private async void DeliverClick(object sender, RoutedEventArgs e) { if (Item<StoreStatusView>(sender) is { } store) await Model.StartDeliverAsync(store); }
    private void CollectClick(object sender, RoutedEventArgs e) { if (Item<StoreStatusView>(sender) is { } store) Model.StartCollect(store); }
    private void PayClick(object sender, RoutedEventArgs e) { if (Item<StoreStatusView>(sender) is { } store) Model.StartPay(store); }
    private void CancelClick(object sender, RoutedEventArgs e) => Model.CancelAction();
    private void UseTemplateClick(object sender, RoutedEventArgs e) => Model.UseTemplate();
    private void ClearTrayClick(object sender, RoutedEventArgs e) => Model.ClearTray();
    private void TileClick(object sender, RoutedEventArgs e) { if (Item<TrayLineView>(sender) is { } line) line.Quantity++; }
    private void CategoryClick(object sender, RoutedEventArgs e) { if (Item<TrayCategory>(sender) is { } category) Model.SelectCategory(category.Name); }
    private void TrayMinusClick(object sender, RoutedEventArgs e) { if (Item<TrayLineView>(sender) is { } line) line.Quantity--; }
    private void TrayPlusClick(object sender, RoutedEventArgs e) { if (Item<TrayLineView>(sender) is { } line) line.Quantity++; }
    private void ReturnTileClick(object sender, RoutedEventArgs e) { if (Item<CollectLineView>(sender) is { } line) line.Returned++; }
    private void ReturnMinusClick(object sender, RoutedEventArgs e) { if (Item<CollectLineView>(sender) is { } line) line.Returned--; }
    private void ReturnPlusClick(object sender, RoutedEventArgs e) { if (Item<CollectLineView>(sender) is { } line) line.Returned++; }
    private void MethodChanged(object sender, SelectionChangedEventArgs e) => Model.MethodChanged();
    private async void ConfirmDeliverClick(object sender, RoutedEventArgs e) => await Model.ConfirmDeliverAsync();
    private async void ConfirmCollectClick(object sender, RoutedEventArgs e) => await Model.ConfirmCollectAsync();
    private async void ConfirmPayClick(object sender, RoutedEventArgs e) => await Model.ConfirmPayAsync();
    // Anular es un paso aparte con su motivo: el formulario es la confirmación.
    private void OpenVoidClick(object sender, RoutedEventArgs e) => Model.OpenVoid();
    private void CloseVoidClick(object sender, RoutedEventArgs e) => Model.CloseVoid();
    private async void VoidClick(object sender, RoutedEventArgs e) => await Model.VoidAsync();

    private void NewStoreClick(object sender, RoutedEventArgs e) => Model.NewStore();
    private async void StoreSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && e.AddedItems[0] is ConsignmentStore store && store.Id != Model.SelectedStore?.Id) await Model.SelectStoreAsync(store);
    }
    private async void SaveStoreClick(object sender, RoutedEventArgs e) => await Model.SaveStoreAsync();
    private async void ToggleStoreClick(object sender, RoutedEventArgs e) => await Model.ToggleStoreAsync();
    private async void SaveTemplateClick(object sender, RoutedEventArgs e) => await Model.SaveTemplateAsync();
    private async void SavePricesClick(object sender, RoutedEventArgs e) => await Model.SavePricesAsync();

    private async void GenerateClick(object sender, RoutedEventArgs e) => await Model.GenerateAsync();
    private async void TodayClick(object sender, RoutedEventArgs e) => await Model.TodayAsync();
    private async void LastSevenDaysClick(object sender, RoutedEventArgs e) => await Model.LastSevenDaysAsync();
    private async void MonthClick(object sender, RoutedEventArgs e) => await Model.MonthAsync();
}
