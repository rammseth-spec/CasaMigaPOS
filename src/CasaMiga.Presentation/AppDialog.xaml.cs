using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CasaMiga.Presentation;

/// <summary>
/// Aviso o confirmación con el estilo de la app (reemplaza a MessageBox). Misma firma y mismo resultado:
/// Enter acepta la opción por omisión y Esc cancela.
/// </summary>
public partial class AppDialog : Window
{
    private MessageBoxResult result = MessageBoxResult.None;
    private MessageBoxResult cancelResult = MessageBoxResult.None;

    private AppDialog() => InitializeComponent();

    public static MessageBoxResult Show(Window? owner, string message, string title, MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        var dialog = new AppDialog();
        owner ??= System.Windows.Application.Current?.MainWindow;
        if (owner is { IsVisible: true } && !ReferenceEquals(owner, dialog)) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.Title = title;
        dialog.Heading.Text = title.StartsWith("Casa Miga · ", StringComparison.Ordinal) ? title["Casa Miga · ".Length..] : title;
        dialog.Body.Text = message;
        dialog.SetBadge(image);
        var danger = image is MessageBoxImage.Warning or MessageBoxImage.Error;
        switch (button)
        {
            case MessageBoxButton.OK:
                dialog.cancelResult = MessageBoxResult.OK;
                dialog.AddAction("Aceptar", MessageBoxResult.OK, primary: true, danger: false, isDefault: true);
                break;
            case MessageBoxButton.OKCancel:
                dialog.cancelResult = MessageBoxResult.Cancel;
                dialog.AddAction("Cancelar", MessageBoxResult.Cancel, primary: false, danger: false, isDefault: defaultResult == MessageBoxResult.Cancel);
                dialog.AddAction("Aceptar", MessageBoxResult.OK, primary: true, danger: danger, isDefault: defaultResult != MessageBoxResult.Cancel);
                break;
            case MessageBoxButton.YesNoCancel:
                dialog.cancelResult = MessageBoxResult.Cancel;
                dialog.AddAction("Cancelar", MessageBoxResult.Cancel, primary: false, danger: false, isDefault: defaultResult == MessageBoxResult.Cancel);
                dialog.AddAction("No", MessageBoxResult.No, primary: false, danger: false, isDefault: defaultResult == MessageBoxResult.No);
                dialog.AddAction("Sí", MessageBoxResult.Yes, primary: true, danger: danger, isDefault: defaultResult is not (MessageBoxResult.No or MessageBoxResult.Cancel));
                break;
            default:
                dialog.cancelResult = MessageBoxResult.No;
                dialog.AddAction("No", MessageBoxResult.No, primary: false, danger: false, isDefault: defaultResult == MessageBoxResult.No);
                dialog.AddAction("Sí", MessageBoxResult.Yes, primary: true, danger: danger, isDefault: defaultResult != MessageBoxResult.No);
                break;
        }
        dialog.ShowDialog();
        return dialog.result == MessageBoxResult.None ? dialog.cancelResult : dialog.result;
    }

    private void SetBadge(MessageBoxImage image)
    {
        var (text, back, fore) = image switch
        {
            MessageBoxImage.Warning => ("!", "WarningSoftBrush", "WarningTextBrush"),
            MessageBoxImage.Error => ("×", "ErrorSoftBrush", "ErrorBrush"),
            MessageBoxImage.Question => ("?", "PrimarySoftBrush", "AccentTextBrush"),
            MessageBoxImage.Information => ("i", "PrimarySoftBrush", "AccentTextBrush"),
            _ => ("", "", ""),
        };
        if (text.Length == 0) return;
        Badge.Visibility = Visibility.Visible;
        Badge.Background = (Brush)FindResource(back);
        BadgeText.Foreground = (Brush)FindResource(fore);
        BadgeText.Text = text;
    }

    private void AddAction(string label, MessageBoxResult value, bool primary, bool danger, bool isDefault)
    {
        var action = new Button
        {
            Content = label, IsDefault = isDefault, MinWidth = 110, Margin = new Thickness(Actions.Children.Count == 0 ? 0 : 10, 0, 0, 0),
            Style = (Style)FindResource(danger ? "DestructiveButton" : primary ? (object)typeof(Button) : "SecondaryButton"),
        };
        action.Click += (_, _) => { result = value; Close(); };
        Actions.Children.Add(action);
        if (isDefault) Loaded += (_, _) => action.Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; result = cancelResult; Close(); return; }
        base.OnPreviewKeyDown(e);
    }
}
