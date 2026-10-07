using System.Windows;
using System.Windows.Controls;
using CasaMiga.Application;

namespace CasaMiga.Presentation;

public partial class ConsignmentView : UserControl
{
    public ConsignmentView() => InitializeComponent();
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
    private void TrayMinusClick(object sender, RoutedEventArgs e) { if (Item<TrayLineView>(sender) is { } line) line.Quantity--; }
    private void TrayPlusClick(object sender, RoutedEventArgs e) { if (Item<TrayLineView>(sender) is { } line) line.Quantity++; }
    private void ReturnMinusClick(object sender, RoutedEventArgs e) { if (Item<CollectLineView>(sender) is { } line) line.Returned--; }
    private void ReturnPlusClick(object sender, RoutedEventArgs e) { if (Item<CollectLineView>(sender) is { } line) line.Returned++; }
    private void MethodChanged(object sender, SelectionChangedEventArgs e) => Model.MethodChanged();
    private async void ConfirmDeliverClick(object sender, RoutedEventArgs e) => await Model.ConfirmDeliverAsync();
    private async void ConfirmCollectClick(object sender, RoutedEventArgs e) => await Model.ConfirmCollectAsync();
    private async void ConfirmPayClick(object sender, RoutedEventArgs e) => await Model.ConfirmPayAsync();
    private async void VoidClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this), "¿Anular esta charola? No se cobrará ni contará en los reportes.", "Anular charola",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
            await Model.VoidAsync();
    }

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
