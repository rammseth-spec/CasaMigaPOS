using System.Windows.Media;

namespace CasaMiga.Presentation;

public enum OperationIconKind { General, Oven, Fermentation, Dough, Cooling, Order, Delivery, Reminder, Clock, Urgent, Supply, Cleaning, Beverage, Production }
public enum OperationPriority { Normal, Urgent }

public sealed record OperationIconOption(OperationIconKind Kind, string Label, Geometry Geometry);
public sealed record OperationPriorityOption(OperationPriority Value, string Label);

public static class OperationIconCatalog
{
    private static readonly IReadOnlyDictionary<OperationIconKind, OperationIconOption> All = new[]
    {
        Icon(OperationIconKind.General, "General", "M12,3 A9,9 0 1 1 11.99,3 M12,10 L12,17 M12,7 L12,7.1"),
        Icon(OperationIconKind.Oven, "Horno", "M3,6 L21,6 21,21 3,21 Z M6,10 L18,10 18,18 6,18 Z M7,3 L9,3 M12,3 L14,3 M17,3 L19,3"),
        Icon(OperationIconKind.Fermentation, "Fermentación", "M5,20 C5,12 8,6 12,3 C16,6 19,12 19,20 Z M8,15 C10,13 14,13 16,15"),
        Icon(OperationIconKind.Dough, "Masa", "M4,16 C4,9 8,5 12,5 C16,5 20,9 20,16 C20,19 17,21 12,21 C7,21 4,19 4,16 Z M8,11 C10,9 14,9 16,11"),
        Icon(OperationIconKind.Cooling, "Enfriado", "M12,2 L12,22 M4,7 L20,17 M20,7 L4,17 M9,4 L12,7 15,4 M9,20 L12,17 15,20"),
        Icon(OperationIconKind.Order, "Pedido", "M5,3 L17,3 20,6 20,21 5,21 Z M17,3 L17,7 20,7 M8,11 L17,11 M8,15 L17,15"),
        Icon(OperationIconKind.Delivery, "Entrega", "M3,6 L14,6 14,17 3,17 Z M14,10 L18,10 21,14 21,17 14,17 M7,17 A2,2 0 1 0 7.01,17 M18,17 A2,2 0 1 0 18.01,17"),
        Icon(OperationIconKind.Reminder, "Recordatorio", "M6,17 L18,17 16,14 16,10 C16,6 14,4 12,4 C10,4 8,6 8,10 L8,14 Z M10,20 L14,20"),
        Icon(OperationIconKind.Clock, "Reloj", "M12,3 A9,9 0 1 1 11.99,3 M12,7 L12,12 16,14"),
        Icon(OperationIconKind.Urgent, "Urgente", "M12,3 L22,21 2,21 Z M12,9 L12,15 M12,18 L12,18.1"),
        Icon(OperationIconKind.Supply, "Compra / insumo", "M4,5 L6,5 8,16 19,16 21,8 7,8 M9,20 A1.5,1.5 0 1 0 9.01,20 M18,20 A1.5,1.5 0 1 0 18.01,20"),
        Icon(OperationIconKind.Cleaning, "Limpieza", "M8,3 L16,3 15,9 18,20 6,20 9,9 Z M8,15 L16,15"),
        Icon(OperationIconKind.Beverage, "Café / bebida", "M5,7 L17,7 17,13 C17,17 14,20 11,20 C7,20 5,17 5,13 Z M17,9 L20,9 C22,9 22,14 18,14 M8,3 C8,5 10,5 10,7 M13,3 C13,5 15,5 15,7"),
        Icon(OperationIconKind.Production, "Producción", "M4,20 L4,10 9,13 9,9 14,12 14,6 20,9 20,20 Z M8,16 L10,16 M14,16 L16,16")
    }.ToDictionary(x => x.Kind);

    public static IReadOnlyList<OperationIconOption> TimerOptions { get; } =
    [Get(OperationIconKind.Oven), Get(OperationIconKind.Fermentation), Get(OperationIconKind.Dough), Get(OperationIconKind.Cooling), Get(OperationIconKind.Order), Get(OperationIconKind.Delivery), Get(OperationIconKind.Reminder), Get(OperationIconKind.Clock), Get(OperationIconKind.Urgent), Get(OperationIconKind.Supply), Get(OperationIconKind.Cleaning), Get(OperationIconKind.Beverage)];

    public static IReadOnlyList<OperationIconOption> NoteOptions { get; } =
    [Get(OperationIconKind.General), Get(OperationIconKind.Reminder), Get(OperationIconKind.Order), Get(OperationIconKind.Supply), Get(OperationIconKind.Production), Get(OperationIconKind.Cleaning), Get(OperationIconKind.Delivery), Get(OperationIconKind.Urgent)];

    public static IReadOnlyList<OperationPriorityOption> Priorities { get; } = [new(OperationPriority.Normal, "Normal"), new(OperationPriority.Urgent, "Urgente")];
    public static OperationIconOption Get(OperationIconKind kind) => All.TryGetValue(kind, out var option) ? option : All[OperationIconKind.General];

    private static OperationIconOption Icon(OperationIconKind kind, string label, string data)
    {
        var geometry = Geometry.Parse(data); geometry.Freeze(); return new(kind, label, geometry);
    }
}
