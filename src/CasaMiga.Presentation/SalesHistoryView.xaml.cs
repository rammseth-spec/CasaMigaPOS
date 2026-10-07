using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CasaMiga.Presentation;

public partial class SalesHistoryView : UserControl
{
    public SalesHistoryView()=>InitializeComponent();
    private SalesHistoryViewModel Model=>(SalesHistoryViewModel)DataContext;
    private async void AllClick(object sender,RoutedEventArgs e)=>await Model.ShowAllAsync();
    private async void TodayClick(object sender,RoutedEventArgs e)=>await Model.TodayAsync();
    private async void YesterdayClick(object sender,RoutedEventArgs e)=>await Model.YesterdayAsync();
    private async void CurrentSessionClick(object sender,RoutedEventArgs e)=>await Model.CurrentSessionAsync();
    private async void SearchDatesClick(object sender,RoutedEventArgs e)=>await Model.SearchDatesAsync(FromDatePicker.Text,ThroughDatePicker.Text);
    private void DatePickerValidationError(object sender,DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException=false;Model.ReportDateInputError();
    }
    private async void ClearClick(object sender,RoutedEventArgs e)=>await Model.ClearAsync();
    private async void PreviousClick(object sender,RoutedEventArgs e)=>await Model.PageAsync(-1);
    private async void NextClick(object sender,RoutedEventArgs e)=>await Model.PageAsync(1);
    private async void DetailClick(object sender,RoutedEventArgs e)=>await Model.OpenDetailAsync();
    private async void ReprintClick(object sender,RoutedEventArgs e)=>await Model.ReprintAsync();
    private void BackClick(object sender,RoutedEventArgs e)=>Model.Back();
    private async void HistoryKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape&&Model.IsDetail){e.Handled=true;Model.Back();}
    }
}
