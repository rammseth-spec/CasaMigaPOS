namespace CasaMiga.Presentation;

/// <summary>
/// Pedido del dueño (24/11/2026): al instalar, la base ya trae el catálogo de Casa Miga con sus fotos. La base se crea sola al abrir
/// el programa; la primera vez que el administrador entra a una base sin ningún pan, se carga solo desde catalogo\ (lo mismo que
/// Catálogo → «Cargar catálogo Casa Miga»). Una base restaurada o con panes nunca se toca, y las copias de desarrollo y las pruebas
/// (IsDevelopment) no lo hacen.
/// </summary>
public sealed partial class MainViewModel
{
    private IProductPhotoUiGateway? starterPhotos;

    private async Task LoadStarterCatalogAsync()
    {
        if (foundation is not { IsDevelopment: false } || catalogService is not { } catalog || starterPhotos is not { } photos) return;
        if (!CasaMigaCatalog.IsAvailable(CasaMigaCatalog.Folder)) return;
        try
        {
            if (!await Task.Run(() => CasaMigaCatalog.HasNoProducts(catalog))) return;
            MessageIsSuccess = true; Message = "Preparando el catálogo de Casa Miga con sus fotos (sólo la primera vez)..."; Notify();
            var result = await CasaMigaCatalog.LoadAsync(catalog, photos, CasaMigaCatalog.Folder);
            runtime?.TryLog("catalog.starter.loaded");
            MessageIsSuccess = true; Message = result.Summary;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Nunca impide entrar a la caja: queda el botón para cargarlo a mano.
            runtime?.TryLog("catalog.starter.failed", error.GetType().Name);
            MessageIsSuccess = false; Message = "No se pudo cargar solo el catálogo de Casa Miga. Cárgalo en Catálogo → «Cargar catálogo Casa Miga».";
        }
    }
}
