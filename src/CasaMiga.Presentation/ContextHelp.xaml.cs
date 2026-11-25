using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace CasaMiga.Presentation;

// Operational help stays in the owning view model; this control only provides the reusable in-window overlay.
public partial class ContextHelp : UserControl
{
    private const double MaximumWidth = 520;
    private const double MinimumWidth = 280;
    private const double ViewportMargin = 16;
    private static readonly List<WeakReference<ContextHelp>> openHelp = [];
    private readonly KeyEventHandler ownerWindowKeyDownHandler;
    private readonly Border helpCard;
    private Window? ownerWindow;
    private FrameworkElement? overlayRoot;
    private AdornerLayer? adornerLayer;
    private ContextHelpAdorner? adorner;
    private bool ownerEventsAttached;

    public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register(
        nameof(HelpText), typeof(string), typeof(ContextHelp), new PropertyMetadata(""));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ContextHelp), new PropertyMetadata("Mostrar ayuda contextual"));

    public string HelpText { get => (string)GetValue(HelpTextProperty); set => SetValue(HelpTextProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsOpen => adorner is not null;
    public FrameworkElement HelpSurface => helpCard;

    public ContextHelp()
    {
        ownerWindowKeyDownHandler = OwnerWindowKeyDown;
        helpCard = CreateHelpCard();
        InitializeComponent();
    }

    internal static bool TryHandleEscape(KeyEventArgs args)
    {
        if (args.Handled || args.Key != Key.Escape) return false;
        CompactRegistry();
        for (var index = openHelp.Count - 1; index >= 0; index--)
        {
            if (!openHelp[index].TryGetTarget(out var help) || !help.IsOpen) continue;
            help.CloseHelp();
            args.Handled = true;
            return true;
        }
        return false;
    }

    internal static void CloseAll()
    {
        CompactRegistry();
        foreach (var reference in openHelp.ToArray())
            if (reference.TryGetTarget(out var help)) help.CloseHelp();
    }

    private Border CreateHelpCard()
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 22 };
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(HelpText)) { Source = this });
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        var card = new Border
        {
            Focusable = true,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Child = text
        };
        // La sombra viene del tema: el modo ligero la quita en computadoras sin aceleración gráfica.
        card.SetResourceReference(EffectProperty, "ShadowHelp");
        card.SetResourceReference(BackgroundProperty, "SurfaceSecondaryBrush");
        card.SetResourceReference(BorderBrushProperty, "BorderBrush");
        AutomationProperties.SetName(card, "Panel de ayuda contextual");
        card.PreviewKeyDown += HelpKeyDown;
        return card;
    }

    private void HelpLoaded(object sender, RoutedEventArgs args)
    {
        if (ownerEventsAttached) return;
        ownerWindow = Window.GetWindow(this);
        if (ownerWindow is null) return;
        ownerWindow.AddHandler(Keyboard.PreviewKeyDownEvent, ownerWindowKeyDownHandler, true);
        ownerWindow.Deactivated += OwnerWindowDeactivated;
        ownerWindow.StateChanged += OwnerWindowStateChanged;
        ownerWindow.SizeChanged += OwnerWindowSizeChanged;
        ownerWindow.Closing += OwnerWindowClosing;
        ownerEventsAttached = true;
    }

    private void HelpUnloaded(object sender, RoutedEventArgs args)
    {
        CloseHelp();
        if (ownerWindow is null || !ownerEventsAttached) return;
        ownerWindow.RemoveHandler(Keyboard.PreviewKeyDownEvent, ownerWindowKeyDownHandler);
        ownerWindow.Deactivated -= OwnerWindowDeactivated;
        ownerWindow.StateChanged -= OwnerWindowStateChanged;
        ownerWindow.SizeChanged -= OwnerWindowSizeChanged;
        ownerWindow.Closing -= OwnerWindowClosing;
        ownerEventsAttached = false;
        ownerWindow = null;
    }

    private void HelpVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is false) CloseHelp();
    }

    private void HelpButtonClick(object sender, RoutedEventArgs args)
    {
        if (IsOpen) { CloseHelp(); return; }
        OpenHelp();
    }

    private void OpenHelp()
    {
        var window = ownerWindow ?? Window.GetWindow(this);
        var root = window?.Content as FrameworkElement;
        var layer = root is null ? null : AdornerLayer.GetAdornerLayer(root);
        if (window is null || root is null || layer is null || window.WindowState == WindowState.Minimized || !IsVisible) return;
        ownerWindow = window;
        overlayRoot = root;
        adornerLayer = layer;
        adorner = new ContextHelpAdorner(root, helpCard);
        layer.Add(adorner);
        PositionAdorner();
        CompactRegistry();
        openHelp.RemoveAll(reference => reference.TryGetTarget(out var target) && ReferenceEquals(target, this));
        openHelp.Add(new(this));
        HelpButton.ToolTip = $"Cerrar {Label.ToLowerInvariant()}";
    }

    private void CloseHelp()
    {
        if (adorner is null) return;
        var focusWasInside = helpCard.IsKeyboardFocusWithin;
        adornerLayer?.Remove(adorner);
        adorner.Detach();
        adorner = null;
        adornerLayer = null;
        overlayRoot = null;
        openHelp.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, this));
        HelpButton.ToolTip = Label;
        if (focusWasInside && HelpButton.IsVisible) HelpButton.Focus();
    }

    private void PositionAdorner()
    {
        if (adorner is null || overlayRoot is null || overlayRoot.ActualWidth <= 0 || overlayRoot.ActualHeight <= 0) return;
        var target = HelpButton.TransformToAncestor(overlayRoot).Transform(new Point());
        var availableWidth = Math.Max(MinimumWidth, overlayRoot.ActualWidth - (ViewportMargin * 2));
        var width = Math.Min(MaximumWidth, availableWidth);
        helpCard.Width = width;
        helpCard.Measure(new Size(width, double.PositiveInfinity));
        var height = helpCard.DesiredSize.Height;
        var x = Math.Clamp(target.X, ViewportMargin, Math.Max(ViewportMargin, overlayRoot.ActualWidth - width - ViewportMargin));
        var below = target.Y + HelpButton.ActualHeight + 6;
        var y = below + height <= overlayRoot.ActualHeight - ViewportMargin
            ? below
            : Math.Max(ViewportMargin, target.Y - height - 6);
        adorner.SetBounds(new Rect(x, y, width, height));
    }

    private void OwnerWindowKeyDown(object sender, KeyEventArgs args) => TryHandleEscape(args);
    private void HelpKeyDown(object sender, KeyEventArgs args) => TryHandleEscape(args);
    private void OwnerWindowDeactivated(object? sender, EventArgs args) => CloseHelp();
    private void OwnerWindowStateChanged(object? sender, EventArgs args) { if (ownerWindow?.WindowState == WindowState.Minimized) CloseHelp(); }
    private void OwnerWindowSizeChanged(object sender, SizeChangedEventArgs args) => CloseHelp();
    private void OwnerWindowClosing(object? sender, System.ComponentModel.CancelEventArgs args) => CloseHelp();

    private static void CompactRegistry()
        => openHelp.RemoveAll(reference => !reference.TryGetTarget(out var help) || !help.IsLoaded);

    private sealed class ContextHelpAdorner : Adorner
    {
        private readonly VisualCollection visuals;
        private readonly FrameworkElement surface;
        private Rect bounds;

        public ContextHelpAdorner(UIElement adornedElement, FrameworkElement surface) : base(adornedElement)
        {
            this.surface = surface;
            visuals = new VisualCollection(this) { surface };
        }

        public void SetBounds(Rect value)
        {
            bounds = value;
            InvalidateMeasure();
            InvalidateArrange();
        }

        public void Detach()
        {
            if (visuals.Count > 0) visuals.Remove(surface);
        }

        protected override int VisualChildrenCount => visuals.Count;
        protected override Visual GetVisualChild(int index) => visuals[index];
        protected override Size MeasureOverride(Size constraint)
        {
            surface.Measure(new Size(bounds.Width, double.PositiveInfinity));
            return AdornedElement.RenderSize;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            surface.Arrange(bounds);
            return finalSize;
        }
    }
}
