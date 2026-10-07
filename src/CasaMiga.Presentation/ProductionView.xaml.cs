using System.Windows;
using System.Windows.Controls;
namespace CasaMiga.Presentation;
public partial class ProductionView:UserControl
{
    public ProductionView()=>InitializeComponent();private ProductionViewModel Model=>(ProductionViewModel)DataContext;
    private void NewClick(object s,RoutedEventArgs e)=>Model.NewProduction();private async void DraftsClick(object s,RoutedEventArgs e)=>await Model.ShowDraftsAsync();private async void HistoryClick(object s,RoutedEventArgs e)=>await Model.ShowHistoryAsync();private void AddOutputClick(object s,RoutedEventArgs e)=>Model.AddOutput();private void RemoveOutputClick(object s,RoutedEventArgs e)=>Model.RemoveOutput();private async void PreviewClick(object s,RoutedEventArgs e)=>await Model.PreviewAsync();private async void CreateClick(object s,RoutedEventArgs e)=>await Model.CreateDraftAsync();private async void OpenClick(object s,RoutedEventArgs e)=>await Model.OpenDetailAsync();private void BackClick(object s,RoutedEventArgs e)=>Model.Back();
    private async void ConfirmClick(object s,RoutedEventArgs e)
    {
        if(Model.TotalZero&&AppDialog.Show(Window.GetWindow(this),"Esta producción no generará producto terminado, pero los ingredientes preparados sí se descontarán del inventario.","Confirmar producción sin salida",MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)return;
        await Model.ConfirmAsync();
    }
}
