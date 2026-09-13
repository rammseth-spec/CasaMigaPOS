using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CasaMiga.Presentation;

public partial class InventoryView : UserControl
{
    public InventoryView() => InitializeComponent();
    private InventoryViewModel Model => (InventoryViewModel)DataContext;
    private async void ConsumptionRefreshClick(object sender,RoutedEventArgs e)=>await Model.Consumption!.LoadAsync();
    private async void ConsumptionCheckClick(object sender,RoutedEventArgs e)=>await Model.Consumption!.CheckAsync();
    private async void ConsumptionEnableClick(object sender,RoutedEventArgs e)=>await ChangeConsumption(CasaMiga.Application.ConsumptionAction.Enable);
    private async void ConsumptionResumeClick(object sender,RoutedEventArgs e)=>await ChangeConsumption(CasaMiga.Application.ConsumptionAction.Resume);
    private async void ConsumptionPauseClick(object sender,RoutedEventArgs e)=>await ChangeConsumption(CasaMiga.Application.ConsumptionAction.Pause);
    private async Task ChangeConsumption(CasaMiga.Application.ConsumptionAction action)
    {
        var message=action==CasaMiga.Application.ConsumptionAction.Pause
            ?"Durante la pausa, las ventas continuarán registrándose pero no descontarán inventario. ¿Confirmas la pausa?"
            :"Al activar esta función, las ventas de productos configurados descontarán existencias automáticamente. ¿Deseas continuar?";
        var confirmed=MessageBox.Show(Window.GetWindow(this),message,"Consumo automático por ventas",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes;
        await Model.Consumption!.ChangeAsync(action,confirmed);
    }
    private async void SearchClick(object sender, RoutedEventArgs e) => await Model.SearchAsync();
    private async void PreviousClick(object sender, RoutedEventArgs e) => await Model.PageAsync(-1);
    private async void NextClick(object sender, RoutedEventArgs e) => await Model.PageAsync(1);
    private async void DetailClick(object sender, RoutedEventArgs e) => await Model.OpenDetailAsync();
    private void NewItemClick(object sender, RoutedEventArgs e) => Model.NewItem();
    private void EditItemClick(object sender, RoutedEventArgs e) => Model.EditItem();
    private async void SaveItemClick(object sender, RoutedEventArgs e) => await Model.SaveItemAsync();
    private async void ToggleClick(object sender, RoutedEventArgs e) => await Model.ToggleActiveAsync();
    private void OpeningClick(object sender, RoutedEventArgs e) => Model.BeginOperation("opening");
    private void ReceiptClick(object sender, RoutedEventArgs e) => Model.BeginOperation("receipt");
    private void WasteClick(object sender, RoutedEventArgs e) => Model.BeginOperation("waste");
    private void CountClick(object sender, RoutedEventArgs e) => Model.BeginOperation("count");
    private async void SubmitOperationClick(object sender, RoutedEventArgs e) => await Model.SubmitOperationAsync();
    private void NewUnitClick(object sender, RoutedEventArgs e) => Model.NewUnit();
    private async void SaveUnitClick(object sender, RoutedEventArgs e) => await Model.SaveUnitAsync();
    private async void ShowLinkClick(object sender, RoutedEventArgs e) => await Model.ShowLinkEditorAsync();
    private async void SearchProductsClick(object sender, RoutedEventArgs e) => await Model.SearchProductsAsync();
    private async void ProductPreviousClick(object sender, RoutedEventArgs e) => await Model.ProductPageAsync(-1);
    private async void ProductNextClick(object sender, RoutedEventArgs e) => await Model.ProductPageAsync(1);
    private async void SaveLinkClick(object sender, RoutedEventArgs e) => await Model.SaveLinkAsync();
    private async void UnlinkClick(object sender, RoutedEventArgs e) => await Model.UnlinkAsync();
    private async void HistoryPreviousClick(object sender, RoutedEventArgs e) => await Model.HistoryPageAsync(-1);
    private async void HistoryNextClick(object sender, RoutedEventArgs e) => await Model.HistoryPageAsync(1);
    private void BackClick(object sender, RoutedEventArgs e) => Model.Back();
    private async void InventoryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !Model.IsList) { e.Handled = true; Model.Back(); }
        else if (e.Key == Key.Enter && Keyboard.FocusedElement == SearchBox)
            { e.Handled = true; await Model.SearchAsync(); }
    }
}
