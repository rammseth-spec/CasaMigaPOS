using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class TicketsPresentationTests
{
    [TestMethod]
    public Task TicketsViewShowsSentTicketsAndTheQueueWithActionsDisabledUntilSomethingIsSelected() => WpfTestHost.OnUi(() =>
    {
        var view = new TicketsView { DataContext = new { Ready = true, Tickets = Array.Empty<object>(), QueueJobs = Array.Empty<object>(),
            QueueTitle = "Cola de impresión · POS-80", QueueStatus = "0 trabajos pendientes.", CanReprint = false, CanCancelJob = false, CanClearQueue = false } };
        var window = new Window { Content = view, Width = 1100, Height = 900, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
        try
        {
            window.Show(); WpfTestHost.Layout(window, 1100, 900);
            var grids = Descendants<DataGrid>(view).Select(AutomationProperties.GetName).ToArray();
            CollectionAssert.Contains(grids, "Tickets enviados"); CollectionAssert.Contains(grids, "Cola de impresión");
            Assert.IsTrue(Descendants<TextBlock>(view).Any(t => t.Text == "Cola de impresión · POS-80"));
            foreach (var name in new[] { "Reimprimir ticket seleccionado", "Cancelar trabajo de impresión seleccionado", "Vaciar cola de impresión" })
                Assert.IsFalse(Descendants<Button>(view).Single(b => AutomationProperties.GetName(b) == name).IsEnabled, name);
            Assert.IsTrue(Descendants<Button>(view).Single(b => AutomationProperties.GetName(b) == "Actualizar tickets y cola").IsEnabled);
        }
        finally { window.Close(); }
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
