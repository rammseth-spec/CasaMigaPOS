using System.Globalization;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public static class InventoryDisplay
{
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
