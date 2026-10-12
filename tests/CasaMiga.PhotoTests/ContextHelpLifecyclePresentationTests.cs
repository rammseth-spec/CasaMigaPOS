using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class ContextHelpLifecyclePresentationTests
{
    [TestMethod]
    [DataRow(1280, 820)]
    [DataRow(1920, 1080)]
    public Task ContextHelpDoesNotRemainVisibleWhenOwnerWindowLosesActivationOrIsMinimized(int width, int height) => WpfTestHost.OnUi(() =>
    {
        var (owner, help, button, neighbor) = CreateHelpWindow(width, height);
        var other = CreateWindow(new Border(), 420, 260, -9500);
        try
        {
            owner.Show(); owner.Activate(); WpfTestHost.Layout(owner, width, height);
            var neighborY = neighbor.TranslatePoint(new Point(), (UIElement)owner.Content).Y;
            Open(button); WpfTestHost.Layout(owner, width, height);
            Assert.IsTrue(help.IsOpen);
            Assert.AreEqual(neighborY, neighbor.TranslatePoint(new Point(), (UIElement)owner.Content).Y, 0.1, "ContextHelp must not move neighboring content.");
            AssertSharesOwnerHwnd(owner, help.HelpSurface);

            owner.WindowState = WindowState.Minimized; Dispatch();
            Assert.IsFalse(help.IsOpen, "ContextHelp does not remain visible when owner window loses activation or is minimized.");
            Assert.IsFalse(help.HelpSurface.IsVisible);
            owner.WindowState = WindowState.Normal; Dispatch();
            Assert.IsFalse(help.IsOpen, "Restore must never reopen contextual help automatically.");

            owner.Activate(); Open(button); WpfTestHost.Layout(owner, width, height); Assert.IsTrue(help.IsOpen);
            other.Show(); other.Activate(); Dispatch();
            Assert.IsFalse(help.IsOpen, "ContextHelp does not remain visible when owner window loses activation or is minimized.");
            Assert.IsFalse(help.HelpSurface.IsVisible);
            owner.Activate(); Dispatch(); Assert.IsFalse(help.IsOpen, "Reactivation must keep help closed.");

            Open(button); WpfTestHost.Layout(owner, width, height); Assert.IsTrue(help.IsOpen);
            SendEscape(help.HelpSurface, owner); WpfTestHost.Layout(owner, width, height);
            Assert.IsFalse(help.IsOpen, "The overlay must reopen manually and close normally with Escape.");
        }
        finally
        {
            other.Close();
            owner.Close();
        }
    });

    [TestMethod]
    public Task ContextHelpClosesWhenViewIsHiddenResizedOrOwnerCloses() => WpfTestHost.OnUi(() =>
    {
        var (owner, help, button, _) = CreateHelpWindow(1280, 820);
        owner.Show(); WpfTestHost.Layout(owner, 1280, 820);
        Open(button); Assert.IsTrue(help.IsOpen);
        help.Visibility = Visibility.Collapsed; Dispatch(); Assert.IsFalse(help.IsOpen, "Navigation or logout hides the view and closes its help.");
        help.Visibility = Visibility.Visible; WpfTestHost.Layout(owner, 1280, 820);
        Open(button); Assert.IsTrue(help.IsOpen);
        owner.Width = 900; owner.Height = 600; Dispatch(); Assert.IsFalse(help.IsOpen, "A size/fullscreen transition closes help with stale coordinates.");
        Open(button); Assert.IsTrue(help.IsOpen);
        owner.Close(); Dispatch(); Assert.IsFalse(help.IsOpen, "Closing the owner removes its overlay.");
        Assert.IsFalse(help.HelpSurface.IsVisible);
        return Task.CompletedTask;
    });

    private static (Window Window, ContextHelp Help, Button Button, Border Neighbor) CreateHelpWindow(double width, double height)
    {
        var neighbor = new Border { Height = 92, Margin = new Thickness(24), Background = Brushes.Transparent };
        var help = new ContextHelp
        {
            HelpText = "Cómo se usa: esta ayuda debe pertenecer siempre a la ventana de Casa Miga.",
            Label = "Ayuda de ciclo de vida",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(24)
        };
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(neighbor); Grid.SetRow(help, 1); root.Children.Add(help);
        var window = CreateWindow(root, width, height, -10000);
        return (window, help, (Button)help.FindName("HelpButton"), neighbor);
    }

    private static Window CreateWindow(UIElement content, double width, double height, double left) => new()
    {
        Content = content,
        Width = width,
        Height = height,
        Left = left,
        Top = -10000,
        ShowInTaskbar = false,
        ShowActivated = true,
        WindowStartupLocation = WindowStartupLocation.Manual
    };

    private static void Open(Button button)
        => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

    private static void AssertSharesOwnerHwnd(Window owner, Visual helpSurface)
    {
        var ownerSource = (HwndSource?)PresentationSource.FromVisual(owner);
        var helpSource = (HwndSource?)PresentationSource.FromVisual(helpSurface);
        Assert.IsNotNull(ownerSource); Assert.IsNotNull(helpSource);
        Assert.AreEqual(ownerSource.Handle, helpSource.Handle, "The overlay must render inside the owner HWND.");
    }

    private static void SendEscape(UIElement source, Window owner)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(owner)!, Environment.TickCount, Key.Escape)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        source.RaiseEvent(args);
    }

    private static void Dispatch()
        => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
