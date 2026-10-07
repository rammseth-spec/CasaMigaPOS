using System.Windows;
using System.Windows.Controls;

namespace CasaMiga.Presentation;

public partial class CashMovementsView : UserControl
{
    public CashMovementsView() => InitializeComponent();
    private CashMovementsViewModel Model => (CashMovementsViewModel)DataContext;
    private static T? Item<T>(object sender) where T : class => (sender as FrameworkElement)?.DataContext as T;

    private void KindClick(object sender, RoutedEventArgs e)
    {
        if (Item<CashMovementKindOption>(sender) is { } option) { Model.SelectKind(option.Kind); AmountBox.Focus(); }
    }
    private void CategoryClick(object sender, RoutedEventArgs e)
    {
        if (Item<ExpenseCategoryOption>(sender) is { } option) { Model.SelectCategory(option.Category); AmountBox.Focus(); }
    }
    private async void RecordClick(object sender, RoutedEventArgs e) => await Model.RecordAsync();
}
