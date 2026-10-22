using System.Globalization;
using CasaMiga.Domain;

namespace CasaMiga.Application;

/// <summary>
/// Rendimientos sospechosos: escribir gramos con la unidad en kg (o al revés) multiplica el rendimiento por 1,000.
/// Sólo avisa; nunca corrige el valor. Compara en la misma medida (g, ml o piezas) y nunca peso contra volumen o piezas.
/// </summary>
public static class RecipeYieldCheck
{
    /// <summary>Más de este número de veces mayor o menor que el rendimiento anterior pide confirmación.</summary>
    public const int Factor = 10;

    /// <summary>Lo más que rinde una sola receta en Casa Miga (decisión del dueño, 22/10/2026): 20 kg, 50 L o 500 piezas.</summary>
    public static InventoryQuantity Maximum(InventoryDimension dimension) => dimension switch
    {
        InventoryDimension.Mass => new(dimension, 20_000_000),
        InventoryDimension.Volume => new(dimension, 50_000_000),
        _ => new(dimension, 500)
    };

    /// <summary>
    /// Texto de confirmación, o null si no hace falta. Pregunta si el rendimiento pasa del máximo de Casa Miga, aunque no haya
    /// versión anterior, y si cambia más de 10 veces o de tipo respecto al anterior. Sin rendimiento anterior válido (versión nueva
    /// sin versiones previas, o un valor anterior no positivo) sólo se revisa el máximo; el rendimiento nuevo sigue validándose igual.
    /// </summary>
    public static string? Warning(InventoryQuantity? previous, InventoryQuantity next)
    {
        if (next.AtomicUnits <= 0) return null;
        var reasons = new List<string>();
        var maximum = Maximum(next.Dimension);
        if (next.AtomicUnits > maximum.AtomicUnits)
            reasons.Add($"El rendimiento ({Normalized(next)}) pasa del máximo que rinde una receta en Casa Miga: {Normalized(maximum)}.");
        if (previous is { AtomicUnits: > 0 } before)
        {
            if (before.Dimension != next.Dimension)
                reasons.Add($"Cambiaste el tipo de rendimiento: antes {Kind(before.Dimension)} ({Normalized(before)}), ahora {Kind(next.Dimension)} ({Normalized(next)}).\n\n" +
                    "Peso, volumen y piezas no se comparan entre sí. Revisa que esta receta realmente se mida así.");
            else
            {
                decimal old = before.AtomicUnits, now = next.AtomicUnits;
                if (now > old * Factor || old > now * Factor)
                {
                    var times = Math.Round(Math.Max(old, now) / Math.Min(old, now), 0, MidpointRounding.AwayFromZero);
                    reasons.Add($"El rendimiento cambia más de {Factor} veces respecto al anterior.\n\n" +
                        $"Antes: {Normalized(before)}\nAhora: {Normalized(next)} (unas {times.ToString("#,0", CultureInfo.InvariantCulture)} veces {(now > old ? "más" : "menos")})");
                }
            }
        }
        if (reasons.Count == 0) return null;
        return string.Join("\n\n", reasons) + "\n\n" +
            "Puede ser un error de unidad, por ejemplo escribir gramos con la unidad en kg. El valor no se corrige solo: si es un error, cancela y revisa la cantidad y la unidad.\n\n¿Guardar de todos modos?";
    }

    /// <summary>En la unidad base para comparar sin confusión: g, ml o piezas.</summary>
    public static string Normalized(InventoryQuantity value) => value.Dimension switch
    {
        InventoryDimension.Mass => $"{(value.AtomicUnits / 1000m).ToString("#,0.###", CultureInfo.InvariantCulture)} g",
        InventoryDimension.Volume => $"{(value.AtomicUnits / 1000m).ToString("#,0.###", CultureInfo.InvariantCulture)} ml",
        _ => $"{value.AtomicUnits.ToString("#,0", CultureInfo.InvariantCulture)} {(value.AtomicUnits == 1 ? "pieza" : "piezas")}"
    };

    private static string Kind(InventoryDimension dimension) => dimension switch
    {
        InventoryDimension.Mass => "peso", InventoryDimension.Volume => "volumen", _ => "piezas"
    };
}
