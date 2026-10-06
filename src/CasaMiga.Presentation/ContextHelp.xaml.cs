using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CasaMiga.Presentation;

// Operational help stays in the owning view model; this control only provides the reusable popover.
public partial class ContextHelp : UserControl
{
    private const double MaximumWidth = 520;
    private const double MinimumWidth = 280;
    private const double ViewportMargin = 16;
    private static readonly List<WeakReference<ContextHelp>> openHelp = [];
    private readonly KeyEventHandler ownerWindowKeyDownHandler;
    private Window? ownerWindow;

    public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register(
        nameof(HelpText), typeof(string), typeof(ContextHelp), new PropertyMetadata(""));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ContextHelp), new PropertyMetadata("Mostrar ayuda contextual"));

    public string HelpText { get => (string)GetValue(HelpTextProperty); set => SetValue(HelpTextProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public ContextHelp()
    {
        ownerWindowKeyDownHandler = OwnerWindowKeyDown;
        InitializeComponent();
    }

    internal static bool TryHandleEscape(KeyEventArgs args)
    {
        if (args.Handled || args.Key != Key.Escape) return false;
        CompactRegistry();
        for (var index = openHelp.Count - 1; index >= 0; index--)
        {
            if (!openHelp[index].TryGetTarget(out var help) || !help.HelpPopup.IsOpen) continue;
            help.CloseHelp();
            args.Handled = true;
            return true;
        }
        return false;
    }

    private void HelpLoaded(object sender, RoutedEventArgs args)
    {
        ownerWindow = Window.GetWindow(this);
        if (ownerWindow is null) return;
        ownerWindow.AddHandler(Keyboard.PreviewKeyDownEvent, ownerWindowKeyDownHandler, true);
        ownerWindow.SizeChanged += OwnerWindowChanged;
        ownerWindow.LocationChanged += OwnerWindowChanged;
    }

    private void HelpUnloaded(object sender, RoutedEventArgs args)
    {
        CloseHelp();
        if (ownerWindow is null) return;
        ownerWindow.RemoveHandler(Keyboard.PreviewKeyDownEvent, ownerWindowKeyDownHandler);
        ownerWindow.SizeChanged -= OwnerWindowChanged;
        ownerWindow.LocationChanged -= OwnerWindowChanged;
        ownerWindow = null;
    }

    private void HelpButtonClick(object sender, RoutedEventArgs args)
    {
        if (HelpPopup.IsOpen) { CloseHelp(); return; }
        PreparePlacement();
        HelpPopup.IsOpen = true;
    }

    private void HelpPopupOpened(object? sender, EventArgs args)
    {
        CompactRegistry();
        openHelp.RemoveAll(reference => reference.TryGetTarget(out var target) && ReferenceEquals(target, this));
        openHelp.Add(new(this));
        HelpButton.ToolTip = $"Cerrar {Label.ToLowerInvariant()}";
        PreparePlacement();
    }

    private void HelpPopupClosed(object? sender, EventArgs args)
    {
        openHelp.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, this));
        HelpButton.ToolTip = Label;
    }

    private void OwnerWindowChanged(object? sender, EventArgs args)
    {
        if (HelpPopup.IsOpen) PreparePlacement();
    }

    private void OwnerWindowKeyDown(object sender, KeyEventArgs args) => TryHandleEscape(args);
    private void HelpKeyDown(object sender, KeyEventArgs args)
        => TryHandleEscape(args);

    private void CloseHelp()
    {
        if (!HelpPopup.IsOpen) return;
        var focusWasInside = PopupCard.IsKeyboardFocusWithin;
        HelpPopup.IsOpen = false;
        if (focusWasInside) HelpButton.Focus();
    }

    private void PreparePlacement()
    {
        var window = ownerWindow ?? Window.GetWindow(this);
        if (window is null || window.ActualWidth <= 0 || window.ActualHeight <= 0) return;
        var target = HelpButton.TransformToAncestor(window).Transform(new Point());
        var availableWidth = Math.Max(MinimumWidth, window.ActualWidth - (ViewportMargin * 2));
        var width = Math.Min(MaximumWidth, availableWidth);
        PopupCard.Width = width;
        PopupCard.Measure(new(width, double.PositiveInfinity));
        var height = PopupCard.DesiredSize.Height;
        var x = Math.Clamp(target.X, ViewportMargin, Math.Max(ViewportMargin, window.ActualWidth - width - ViewportMargin));
        var below = target.Y + HelpButton.ActualHeight + 6;
        var y = below + height <= window.ActualHeight - ViewportMargin
            ? below
            : Math.Max(ViewportMargin, target.Y - height - 6);
        HelpPopup.HorizontalOffset = x - target.X;
        HelpPopup.VerticalOffset = y - target.Y;
    }

    private static void CompactRegistry()
        => openHelp.RemoveAll(reference => !reference.TryGetTarget(out var help) || !help.IsLoaded);
}
