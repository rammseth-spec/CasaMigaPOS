namespace CasaMiga.Presentation;

/// <summary>
/// Regla del dueño (25/11/2026): los panes del catálogo de Casa Miga y sus fotos hechas con IA se usan SIEMPRE, en cualquier caja,
/// salvo que alguien ponga una foto a mano en la app. Cada vez que abre el programa, la primera vez que entra alguien con permiso de
/// catálogo se aplica catalogo\ (lo mismo que Catálogo → «Cargar catálogo Casa Miga»): agrega los panes que falten con su foto,
/// renombra los que cambiaron y pone la foto de IA a los panes que no tienen. Nunca reemplaza una foto puesta a mano, nunca cambia
/// precios y no vuelve a dar de alta un pan desactivado. En una caja recién instalada eso carga los 52 panes. Las copias de desarrollo
/// y las pruebas (IsDevelopment) no lo hacen: usan el botón o el cargador de la web.
/// </summary>
public sealed partial class MainViewModel
{
    private IProductPhotoUiGateway? starterPhotos;
    private bool casaMigaCatalogApplied;

    private async Task LoadStarterCatalogAsync()
    {
        if (casaMigaCatalogApplied || foundation is not { IsDevelopment: false } || catalogService is not { } catalog || starterPhotos is not { } photos) return;
        if (!CasaMigaCatalog.IsAvailable(CasaMigaCatalog.Folder)) return;
        try
        {
            var allowed = await Task.Run(() => catalog.Can("products.create") && catalog.Can("products.edit") && catalog.Can("categories.create"));
            if (!allowed) return;
            casaMigaCatalogApplied = true;
            var empty = await Task.Run(() => CasaMigaCatalog.HasNoProducts(catalog));
            if (empty) { MessageIsSuccess = true; Message = "Preparando el catálogo de Casa Miga con sus fotos (sólo la primera vez)..."; Notify(); }
            var result = await CasaMigaCatalog.LoadAsync(catalog, photos, CasaMigaCatalog.Folder);
            if (result.Created + result.Renamed + result.Photos == 0) { if (empty) Message = ""; return; }
            runtime?.TryLog("catalog.ia.applied");
            MessageIsSuccess = true; Message = result.Summary;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Nunca impide entrar a la caja: se reintenta al abrir otra vez y queda el botón para hacerlo a mano.
            runtime?.TryLog("catalog.ia.failed", error.GetType().Name);
            MessageIsSuccess = false; Message = "No se pudieron poner solos los panes y fotos de Casa Miga. Cárgalos en Catálogo → «Cargar catálogo Casa Miga».";
        }
    }
}
