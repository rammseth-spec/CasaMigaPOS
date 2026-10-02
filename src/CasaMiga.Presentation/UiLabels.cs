using CasaMiga.Domain;

namespace CasaMiga.Presentation;

// Display text only. Domain values and persisted codes remain authoritative.
public static class UiLabels
{
    public static string Dimension(InventoryDimension value) => value switch
    {
        InventoryDimension.Mass => "Masa",
        InventoryDimension.Volume => "Volumen",
        InventoryDimension.Count => "Piezas",
        _ => "Dimensión no disponible"
    };

    public static string MovementReason(string? code) => code switch
    {
        null or "" => "—",
        "DAMAGED" => "Dañado",
        "EXPIRED" => "Caducado",
        "PRODUCTION_ERROR" => "Error de producción",
        "SPILL" => "Derrame",
        "OTHER" => "Otro",
        "PHYSICAL_COUNT_VARIANCE" => "Diferencia de conteo físico",
        "DATA_CORRECTION" => "Corrección de datos",
        _ => "Motivo no disponible"
    };
}
