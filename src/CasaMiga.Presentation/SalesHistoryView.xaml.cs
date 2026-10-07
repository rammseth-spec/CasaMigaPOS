using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CasaMiga.Presentation;

public partial class SalesHistoryView : UserControl
{
    public SalesHistoryView()=>InitializeComponent();
    private SalesHistoryViewModel Model=>(SalesHistoryViewModel)DataContext;
    private async void SearchFolioClick(object sender,RoutedEventArgs e)=>await Model.SearchFolioAsync();
    private async void SearchDatesClick(object sender,RoutedEventArgs e)=>await Model.SearchDatesAsync();
    private async void ClearClick(object sender,RoutedEventArgs e)=>await Model.ClearAsync();
    private async void PreviousClick(object sender,RoutedEventArgs e)=>await Model.PageAsync(-1);
    private async void NextClick(object sender,RoutedEventArgs e)=>await Model.PageAsync(1);
    private async void DetailClick(object sender,RoutedEventArgs e)=>await Model.OpenDetailAsync();
    private async void ReprintClick(object sender,RoutedEventArgs e)=>await Model.ReprintAsync();
    private void BackClick(object sender,RoutedEventArgs e)=>Model.Back();
    private async void CancelSaleClick(object sender,RoutedEventArgs e)
    {
        if(Model.Detail is null)return;
        if(AppDialog.Show(Window.GetWindow(this),Model.CancelConfirmation,
            "Cancelar venta",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        await Model.CancelSaleAsync();
    }
    private async void ReturnItemsClick(object sender,RoutedEventArgs e)
    {
        if(Model.Detail is null)return;
        if(AppDialog.Show(Window.GetWindow(this),Model.ReturnConfirmation,
            "Devolver productos",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        await Model.ReturnItemsAsync();
    }
    private async void HistoryKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape&&Model.IsDetail){e.Handled=true;Model.Back();}
        else if(e.Key==Key.Enter&&Keyboard.FocusedElement==FolioBox){e.Handled=true;await Model.SearchFolioAsync();}
    }
}
