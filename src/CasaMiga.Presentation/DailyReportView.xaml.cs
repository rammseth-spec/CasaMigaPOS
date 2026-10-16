using System.Windows;
using System.Windows.Controls;

namespace CasaMiga.Presentation;

public partial class DailyReportView : UserControl
{
    public DailyReportView() => InitializeComponent();
    private DailyReportViewModel Model => (DailyReportViewModel)DataContext;
    private async void RefreshClick(object sender, RoutedEventArgs e) => await Model.RefreshAsync();
    private void DateCommitted(object? sender, EventArgs e) => Model.ValidateDateText();
}
