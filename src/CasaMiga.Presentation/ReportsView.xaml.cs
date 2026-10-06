using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace CasaMiga.Presentation;

public partial class ReportsView : UserControl
{
    public ReportsView() => InitializeComponent();
    private ReportsViewModel Model => (ReportsViewModel)DataContext;
    private async void GenerateClick(object sender, RoutedEventArgs e) => await Model.GenerateAsync();
    private async void TodayClick(object sender, RoutedEventArgs e) => await Model.TodayAsync();
    private async void MonthClick(object sender, RoutedEventArgs e) => await Model.MonthAsync();
    private async void YesterdayClick(object sender, RoutedEventArgs e) => await Model.YesterdayAsync();
    private async void LastSevenDaysClick(object sender, RoutedEventArgs e) => await Model.LastSevenDaysAsync();
    private void ExportClick(object sender, RoutedEventArgs e)
    {
        if (Model.Csv() is not { } csv) return;
        var dialog = new SaveFileDialog { FileName = Model.SuggestedFileName, DefaultExt = ".csv", Filter = "CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            // UTF-8 con BOM para que Excel muestre acentos correctamente.
            File.WriteAllText(dialog.FileName, csv, new UTF8Encoding(true));
            Model.Exported(dialog.FileName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Model.ExportFailed(); }
    }
}
