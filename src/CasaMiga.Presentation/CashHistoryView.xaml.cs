using System.Windows;
using System.Windows.Controls;
namespace CasaMiga.Presentation;
public partial class CashHistoryView : UserControl
{
    public CashHistoryView()=>InitializeComponent();
    private MainViewModel Model=>(MainViewModel)DataContext;
    private async void PreviousClick(object sender,RoutedEventArgs e)=>await Model.PreviousCashHistoryAsync();
    private async void NextClick(object sender,RoutedEventArgs e)=>await Model.NextCashHistoryAsync();
    private async void DetailClick(object sender,RoutedEventArgs e)=>await Model.ShowCashClosureAsync();
    private void HideClick(object sender,RoutedEventArgs e)=>Model.HideCashClosure();
}
