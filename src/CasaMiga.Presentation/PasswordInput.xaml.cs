using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace CasaMiga.Presentation;

public enum InputState { Neutral, Valid, Invalid }

/// <summary>
/// Campo de contraseña con botón para mostrarla u ocultarla. Expone la misma superficie que se usaba de PasswordBox
/// (Password, Clear, Focus, PasswordChanged). Al mostrarla no se permite copiar ni cortar, y el campo visible no guarda historial de deshacer.
/// </summary>
public partial class PasswordInput : UserControl
{
    private bool syncing;
    public event RoutedEventHandler? PasswordChanged;

    public PasswordInput()
    {
        InitializeComponent();
        CommandManager.AddPreviewExecutedHandler(Plain, BlockClipboard);
        CommandManager.AddPreviewCanExecuteHandler(Plain, DisableClipboard);
        Loaded += (_, _) =>
        {
            var name = AutomationProperties.GetName(this);
            if (!string.IsNullOrEmpty(name)) { AutomationProperties.SetName(Hidden, name); AutomationProperties.SetName(Plain, name); }
        };
    }

    public bool IsRevealed => Reveal.IsChecked == true;

    public string Password
    {
        get => IsRevealed ? Plain.Text : Hidden.Password;
        set
        {
            syncing = true;
            try { if (IsRevealed) Plain.Text = value; else Hidden.Password = value; }
            finally { syncing = false; }
            PasswordChanged?.Invoke(this, new RoutedEventArgs());
        }
    }

    public int MaxLength
    {
        get => Hidden.MaxLength;
        set { Hidden.MaxLength = value; Plain.MaxLength = value; }
    }

    /// <summary>Color del borde según la validación en vivo (la plantilla del tema lo lee de Tag).</summary>
    public InputState State
    {
        get => (Hidden.Tag as string) switch { "Valid" => InputState.Valid, "Invalid" => InputState.Invalid, _ => InputState.Neutral };
        set
        {
            object? tag = value switch { InputState.Valid => "Valid", InputState.Invalid => "Invalid", _ => null };
            Hidden.Tag = tag; Plain.Tag = tag;
        }
    }

    public void Clear() => Password = "";

    public new bool Focus() => IsRevealed ? Plain.Focus() : Hidden.Focus();

    private void RevealChanged(object sender, RoutedEventArgs e)
    {
        var hadFocus = Hidden.IsKeyboardFocusWithin || Plain.IsKeyboardFocusWithin;
        syncing = true;
        try
        {
            if (IsRevealed)
            {
                Plain.Text = Hidden.Password; Hidden.Clear();
                Hidden.Visibility = Visibility.Collapsed; Plain.Visibility = Visibility.Visible;
                Plain.CaretIndex = Plain.Text.Length;
            }
            else
            {
                Hidden.Password = Plain.Text; Plain.Clear();
                Plain.Visibility = Visibility.Collapsed; Hidden.Visibility = Visibility.Visible;
            }
        }
        finally { syncing = false; }
        var label = IsRevealed ? "Ocultar contraseña" : "Mostrar contraseña";
        Reveal.ToolTip = label; AutomationProperties.SetName(Reveal, label);
        if (hadFocus) Focus();
    }

    private void HiddenChanged(object sender, RoutedEventArgs e) { if (!syncing) PasswordChanged?.Invoke(this, e); }
    private void PlainChanged(object sender, TextChangedEventArgs e) { if (!syncing) PasswordChanged?.Invoke(this, e); }

    private static bool IsClipboard(ICommand command) => command == ApplicationCommands.Copy || command == ApplicationCommands.Cut;
    private static void BlockClipboard(object sender, ExecutedRoutedEventArgs e) { if (IsClipboard(e.Command)) e.Handled = true; }
    private static void DisableClipboard(object sender, CanExecuteRoutedEventArgs e) { if (IsClipboard(e.Command)) { e.CanExecute = false; e.Handled = true; } }
}
