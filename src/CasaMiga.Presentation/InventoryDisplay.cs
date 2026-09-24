using System.Globalization;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public static class InventoryDisplay
{
    public static IReadOnlyList<string> Units(InventoryDimension dimension) => dimension switch
    {
        InventoryDimension.Mass => ["g", "kg"], InventoryDimension.Volume => ["ml", "L"], _ => ["pieza"]
    };

    public static long ParseOperational(string text, InventoryDimension dimension, string unit)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
            throw new ArgumentException("Escribe una cantidad mayor que cero con punto decimal.");
        var factor = (dimension, unit) switch
        {
            (InventoryDimension.Mass, "g") or (InventoryDimension.Volume, "ml") => 1_000m,
            (InventoryDimension.Mass, "kg") or (InventoryDimension.Volume, "L") => 1_000_000m,
            (InventoryDimension.Count, "pieza") => 1m,
            _ => throw new ArgumentException("La unidad seleccionada no corresponde al artículo.")
        };
        var atomic=checked(quantity*factor);
        if(atomic!=decimal.Truncate(atomic))throw new ArgumentException("La cantidad no puede representarse exactamente con la precisión disponible.");
        return checked((long)atomic);
    }

    public static string CaptureUnit(InventoryDimension dimension) => dimension switch
    {
        InventoryDimension.Mass => "g", InventoryDimension.Volume => "ml", _ => "pieza"
    };

    public static long ParseCapture(string text, InventoryDimension dimension)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity))
            throw new ArgumentException("Escribe una cantidad válida con punto decimal, sin separadores de miles.");
        var atomic = checked(quantity * (dimension == InventoryDimension.Count ? 1m : 1000m));
        if (atomic != decimal.Truncate(atomic)) throw new ArgumentException("La cantidad excede la precisión admitida.");
        return checked((long)atomic);
    }
    private static decimal Factor(long atomic, InventoryDimension dimension) =>
        dimension == InventoryDimension.Count ? 1m : Math.Abs((decimal)atomic) >= 1_000_000m ? 1_000_000m : 1_000m;

    public static string Unit(long atomic, InventoryDimension dimension) => dimension switch
    {
        InventoryDimension.Mass => Factor(atomic, dimension) == 1_000_000m ? "kg" : "g",
        InventoryDimension.Volume => Factor(atomic, dimension) == 1_000_000m ? "L" : "ml",
        InventoryDimension.Count => "pieza",
        _ => throw new ArgumentOutOfRangeException(nameof(dimension))
    };

    public static string Number(long atomic, InventoryDimension dimension) =>
        (atomic / Factor(atomic, dimension)).ToString("0.######", CultureInfo.InvariantCulture);

    public static string Format(long atomic, InventoryDimension dimension) =>
        $"{Number(atomic, dimension)} {Unit(atomic, dimension)}";
}
