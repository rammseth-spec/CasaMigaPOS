using CasaMiga.Domain;

namespace CasaMiga.Application;

/// <summary>Cómo se explica un error de una receta o perfil, igual en la pantalla Recetas y en lo que ve la web cuando la caja rechaza un cambio.</summary>
public static class RecipeProblems
{
    public static string Describe(ProductionDomainException error) => error.Code switch
    {
        ProductionError.InvalidVersion => "Revisa el nombre y número de la versión.",
        ProductionError.InvalidFormulaYield => "El rendimiento debe ser mayor que cero y usar una unidad compatible.",
        ProductionError.InvalidRecipeQuantity => "Revisa que todas las cantidades sean mayores que cero.",
        ProductionError.DimensionMismatch => "Una unidad o política no corresponde al tipo de artículo seleccionado.",
        ProductionError.DuplicateIngredient => "Un mismo artículo no puede repetirse en la receta.",
        ProductionError.InvalidAlternative => "Cada grupo de alternativas requiere al menos dos opciones válidas y un orden de preferencia.",
        ProductionError.Overflow => "Una cantidad excede el límite permitido.",
        _ => "Revisa cantidades, unidades, duplicados y alternativas de la receta."
    };
}
