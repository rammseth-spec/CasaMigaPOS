using System.Windows.Controls;
using CasaMiga.Application;

namespace CasaMiga.Presentation;

public sealed record ChecklistItem(string Label, InputState State)
{
    public string Glyph => State switch { InputState.Valid => "✓", InputState.Invalid => "✗", _ => "○" };
    public string Spoken => $"{Label}: {State switch { InputState.Valid => "cumplido", InputState.Invalid => "pendiente", _ => "sin capturar" }}";
}

/// <summary>
/// Requisitos de contraseña en vivo (misma lista que <see cref="PasswordPolicy.Validate"/>) y coincidencia con la confirmación.
/// En gris antes de escribir; después, verde lo cumplido y rojo lo pendiente.
/// </summary>
public partial class PasswordChecklist : UserControl
{
    public PasswordChecklist() { InitializeComponent(); Update("", ""); }

    public IReadOnlyList<ChecklistItem> Current { get; private set; } = [];
    public bool RequirementsMet { get; private set; }
    public bool Matches { get; private set; }
    public bool IsSatisfied => RequirementsMet && Matches;

    /// <summary>Recalcula; devuelve el estado para el borde del campo de contraseña y el de la confirmación.</summary>
    public (InputState Password, InputState Confirmation) Update(string password, string confirmation)
    {
        var typed = password.Length > 0;
        var requirements = PasswordPolicy.Evaluate(password);
        RequirementsMet = requirements.All(r => r.Met);
        Matches = confirmation.Length > 0 && confirmation == password;
        var items = requirements.Select(r => new ChecklistItem(r.Label, !typed ? InputState.Neutral : r.Met ? InputState.Valid : InputState.Invalid)).ToList();
        items.Add(confirmation.Length == 0
            ? new ChecklistItem("Confirma la contraseña", InputState.Neutral)
            : Matches ? new ChecklistItem("Las contraseñas coinciden", InputState.Valid) : new ChecklistItem("Las contraseñas no coinciden", InputState.Invalid));
        Current = items;
        Items.ItemsSource = items;
        return (!typed ? InputState.Neutral : RequirementsMet ? InputState.Valid : InputState.Invalid,
                confirmation.Length == 0 ? InputState.Neutral : Matches ? InputState.Valid : InputState.Invalid);
    }
}
