using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CasaMiga.Presentation;

/// <summary>Gives an open ComboBox the first Escape before page/window navigation.</summary>
internal static class PopupEscape
{
    public static bool TryHandle(KeyEventArgs args)
    {
        if (args.Handled || args.Key != Key.Escape) return false;
        var combo = OpenCombo(args.OriginalSource as DependencyObject)
            ?? OpenCombo(Keyboard.FocusedElement as DependencyObject)
            ?? OpenCombo(Mouse.Captured as DependencyObject);
        if (combo is null) return false;
        combo.IsDropDownOpen = false;
        args.Handled = true;
        return true;
    }

    private static ComboBox? OpenCombo(DependencyObject? source)
    {
        for (var current = source; current is not null;)
        {
            if (current is ComboBox { IsDropDownOpen: true } combo) return combo;
            // Popup items have a separate visual root, but still belong to their ComboBox.
            if (current is ComboBoxItem item && ItemsControl.ItemsControlFromItemContainer(item)
                is ComboBox { IsDropDownOpen: true } owner) return owner;
            current = current is Visual or Visual3D ? VisualTreeHelper.GetParent(current)
                : current is FrameworkContentElement content ? content.Parent
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }
}
