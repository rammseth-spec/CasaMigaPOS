using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CasaMiga.Presentation;

/// <summary>Opt-in wheel chaining for module pages containing nested scrolling controls.</summary>
public static class NestedScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(NestedScrollBehavior), new PropertyMetadata(false, Changed));
    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not UIElement element) return;
        if ((bool)args.OldValue) element.PreviewMouseWheel -= Wheel;
        if ((bool)args.NewValue) element.PreviewMouseWheel += Wheel;
    }

    private static void Wheel(object sender, MouseWheelEventArgs args)
    {
        if (args.Handled || args.Delta == 0 || args.OriginalSource is not DependencyObject source) return;
        var path = Ancestors(source).ToArray();
        // Popups have their own visual tree; preserve selection/scrolling and mouse capture.
        if (path.OfType<ComboBox>().Any(x => x.IsDropDownOpen) || Mouse.Captured is ComboBox { IsDropDownOpen: true }) return;
        var closedCombo = path.OfType<ComboBox>().FirstOrDefault();
        var viewers = path.OfType<ScrollViewer>().ToArray();
        var target = viewers.FirstOrDefault(x => CanScroll(x, args.Delta));
        // The nearest scrollable control retains its native behavior (including virtualization).
        if (closedCombo is null && viewers.FirstOrDefault() == target) return;
        // A closed ComboBox must not change the selected ingredient while scrolling the page.
        if (target is null) { if (closedCombo is not null) args.Handled = true; return; }
        args.Handled = true;
        if (!target.CanContentScroll)
        {
            // Pixel scrolling preserves fractional wheel deltas emitted by precision devices.
            var lines = SystemParameters.WheelScrollLines;
            var step = lines < 0 ? target.ViewportHeight : lines * 16d;
            target.ScrollToVerticalOffset(target.VerticalOffset - args.Delta / 120d * step);
        }
        else
        {
            // Let the target's IScrollInfo use its native item/pixel units.
            target.RaiseEvent(new MouseWheelEventArgs(args.MouseDevice, args.Timestamp, args.Delta)
                { RoutedEvent = UIElement.MouseWheelEvent, Source = target });
        }
    }

    private static bool CanScroll(ScrollViewer viewer, int delta) => viewer.IsEnabled &&
        viewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && viewer.ScrollableHeight > 0 &&
        (delta < 0 ? viewer.VerticalOffset < viewer.ScrollableHeight - 0.001 : viewer.VerticalOffset > 0.001);

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject source)
    {
        for (DependencyObject? current = source; current is not null;)
        {
            yield return current;
            current = current is Visual or Visual3D ? VisualTreeHelper.GetParent(current)
                : current is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(current);
        }
    }
}
