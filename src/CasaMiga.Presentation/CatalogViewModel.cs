using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Presentation;

public sealed record CategoryFilter(Guid? Id,string Name);
public sealed class CatalogViewModel(CatalogService service,IProductPhotoUiGateway photos,bool development,Action<string,string?> log):INotifyPropertyChanged
{
    private string screen="products";
    private Guid? editingId;
    private int page;
    private CancellationTokenSource photoLoad=new();
    private ProductPhotoVisual photo=new(0,null,null,"Sin foto",null,null);
    public bool Busy {get;private set;}
    public bool Ready=>!Busy;
    public bool IsProducts=>screen=="products";
    public bool IsCategories=>screen=="categories";
    public bool IsProductEditor=>screen=="productEditor";
    public bool IsCategoryEditor=>screen=="categoryEditor";
    public bool IsDevelopment=>development;
    public bool CanCreateProduct {get;private set;}
    public bool CanEditProduct {get;private set;}
    public bool CanToggleProduct {get;private set;}
    public bool CanCreateCategory {get;private set;}
    public bool CanEditCategory {get;private set;}
    public bool CanToggleCategory {get;private set;}
    public bool CanSave=>IsProductEditor?(editingId is null?CanCreateProduct:CanEditProduct):(editingId is null?CanCreateCategory:CanEditCategory);
    public bool CanManagePhoto=>IsProductEditor&&editingId is not null&&CanEditProduct;
    public bool CannotManagePhoto=>!CanManagePhoto;
    public bool HasCurrentPhoto=>photo.HasPhoto;
    public bool HasNoCurrentPhoto=>!photo.HasPhoto;
    public bool CanRemovePhoto=>CanManagePhoto&&photo.PhotoId is not null;
    public System.Windows.Media.Imaging.BitmapSource? CurrentPhoto=>photo.Image;
    public string CurrentPhotoStatus=>photo.Error is null?photo.Status:$"{photo.Status} · {photo.Error}";
    public string PhotoActionLabel=>HasCurrentPhoto?"Cambiar foto":"Agregar foto";
    public string PhotoProvenance {get;set;}="REAL";
    public string Message {get;private set;}="";
    public string EditorTitle=>(editingId is null?"Crear ":"Editar ")+(IsProductEditor?"producto":"categoría");
    public IReadOnlyList<Category> Categories {get;private set;}=[];
    public IReadOnlyList<CategoryFilter> Filters {get;private set;}=[];
    public IReadOnlyList<ProductRow> Products {get;private set;}=[];
    public Category? SelectedCategory {get;set;}
    public ProductRow? SelectedProduct {get;set;}
    public Guid? FilterCategoryId {get;set;}
    public bool ShowInactive {get;set;}
    public string Search {get;set;}="";
    public string PageLabel {get;private set;}="";
    public bool HasPrevious=>page>0;
    public bool HasNext {get;private set;}
    public string Name {get;set;}="";
    public string Description {get;set;}="";
    public string SortOrder {get;set;}="0";
    public string Sku {get;set;}="";
    public string Price {get;set;}="0.00";
    public Guid? ProductCategoryId {get;set;}
    public bool TrackInventory {get;set;}=true;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
    public Task LoadAsync()=>Run(async()=>
    {
        Categories=[];Products=[];Filters=[];SelectedCategory=null;SelectedProduct=null;Name="";Description="";Sku="";Search="";FilterCategoryId=null;ShowInactive=false;page=0;screen="products";
        CanCreateProduct=await Task.Run(()=>service.Can("products.create"));CanEditProduct=await Task.Run(()=>service.Can("products.edit"));CanToggleProduct=await Task.Run(()=>service.Can("products.deactivate"));
        CanCreateCategory=await Task.Run(()=>service.Can("categories.create"));CanEditCategory=await Task.Run(()=>service.Can("categories.edit"));CanToggleCategory=await Task.Run(()=>service.Can("categories.deactivate"));
        await Refresh();
    });
    private async Task Refresh()
    {
        Categories=await Task.Run(()=>service.Categories(true));
        var filters=new[]{new CategoryFilter(null,"Todas las categorías")}.Concat(Categories.Select(c=>new CategoryFilter(c.Id,c.ToString()))).ToArray();
        // Keep the items instance when unchanged: replacing it clears WPF's selected value.
        if(!Filters.SequenceEqual(filters))Filters=filters;
        var result=await Task.Run(()=>service.Products(new(Search,FilterCategoryId,ShowInactive,page)));
        Products=result.Items;PageLabel=$"Página {page+1} · {result.Total} productos · hasta {result.PageSize} por página";HasNext=(page+1)*result.PageSize<result.Total;
        SelectedCategory=null;SelectedProduct=null;
    }
    public Task SearchAsync()=>Run(async()=>{page=0;await Refresh();});
    public Task PageAsync(int delta)=>Run(async()=>{page=Math.Max(0,page+delta);await Refresh();});
    public Task ShowAsync(bool categories)=>Run(async()=>{screen=categories?"categories":"products";page=0;await Refresh();});
    public void NewCategory(){if(Busy||!CanCreateCategory)return;editingId=null;Name="";Description="";SortOrder="0";screen="categoryEditor";Message="";Notify();}
    public void EditCategory()
    {
        if(Busy||!CanEditCategory)return;if(SelectedCategory is not {} c){Message="Selecciona una categoría.";Notify();return;}
        editingId=c.Id;Name=c.Name;Description=c.Description;SortOrder=c.SortOrder.ToString(CultureInfo.InvariantCulture);screen="categoryEditor";Message="";Notify();
    }
    public void NewProduct(){if(Busy||!CanCreateProduct)return;photoLoad.Cancel();editingId=null;photo=new(0,null,null,"Guarda el producto antes de agregar una foto",null,null);Name="";Description="";Sku="";Price="0.00";ProductCategoryId=null;TrackInventory=true;screen="productEditor";Message="";Notify();}
    public void EditProduct()
    {
        if(Busy||!CanEditProduct)return;if(SelectedProduct is not {} row){Message="Selecciona un producto.";Notify();return;}var p=row.Product;
        editingId=p.Id;Name=p.Name;Description=p.Description;Sku=p.Sku??"";Price=(p.SalePrice.Cents/100m).ToString("F2",CultureInfo.InvariantCulture);ProductCategoryId=p.CategoryId;TrackInventory=p.TrackInventory;screen="productEditor";Message="";PhotoProvenance="REAL";photo=new(0,null,null,"Cargando foto…",null,null);Notify();StartEditorPhotoLoad(p.Id);
    }
    private void StartEditorPhotoLoad(Guid productId)
    {
        photoLoad.Cancel();photoLoad.Dispose();photoLoad=new();_=LoadEditorPhotoSafeAsync(productId,photoLoad.Token);
    }
    private async Task LoadEditorPhotoSafeAsync(Guid productId,CancellationToken cancellationToken)
    {
        try{photo=await photos.LoadAsync(productId,cancellationToken);Notify();}
        catch(OperationCanceledException){ }
        catch(Exception error){photo=new(0,null,null,"Foto no disponible",null,"No se pudo cargar la foto.");log("catalog.photo.load.failed",error.GetType().Name);Notify();}
    }
    public Task SetPhotoAsync(string originalName,byte[] original,PhotoCrop crop)=>Run(async()=>
    {
        if(editingId is not {} product)throw new ArgumentException("Guarda el producto antes de agregar una foto.");
        photo=await photos.ImportAsync(product,photo.Revision,originalName,PhotoProvenance,crop,original);
        Message=photo.Status+" guardada.";log("catalog.photo.saved",null);
    });
    public Task RemovePhotoAsync()=>Run(async()=>
    {
        if(editingId is not {} product||photo.PhotoId is null)throw new ArgumentException("Este producto no tiene foto.");
        photo=await photos.RemoveAsync(product,photo.Revision);Message="Foto quitada. El producto conserva todos sus datos.";log("catalog.photo.removed",null);
    });
    public Task SaveAsync()=>Run(async()=>
    {
        if(IsCategoryEditor)
        {
            if(!int.TryParse(SortOrder,NumberStyles.Integer,CultureInfo.InvariantCulture,out var order))throw new ArgumentException("El orden debe ser un número entero.");
            await Task.Run(()=>editingId is {} id?service.EditCategory(id,Name,Description,order):service.CreateCategory(Name,Description,order));screen="categories";
        }
        else
        {
            if(ProductCategoryId is not {} category)throw new ArgumentException("Selecciona una categoría.");
            if(!decimal.TryParse(Price,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var amount))throw new ArgumentException("Escribe el precio con punto decimal, sin comas.");
            var money=Money.FromPesos(amount);
            await Task.Run(()=>editingId is {} id?service.EditProduct(id,category,Sku,Name,Description,money,TrackInventory):service.CreateProduct(category,Sku,Name,Description,money,TrackInventory));screen="products";
        }
        log("catalog.saved",null);await Refresh();
    });
    public Task CancelAsync(){photoLoad.Cancel();return ShowAsync(IsCategoryEditor);}
    public Task ToggleCategoryAsync()=>Run(async()=>{var c=SelectedCategory??throw new ArgumentException("Selecciona una categoría.");await Task.Run(()=>service.SetCategoryActive(c.Id,!c.IsActive));await Refresh();});
    public Task ToggleProductAsync()=>Run(async()=>{var p=SelectedProduct?.Product??throw new ArgumentException("Selecciona un producto.");await Task.Run(()=>service.SetProductActive(p.Id,!p.IsActive));await Refresh();});
    public Task DemoAsync()=>Run(async()=>{await Task.Run(service.SeedDemo);await Refresh();});
    private async Task Run(Func<Task> action)
    {
        if(Busy)return;Busy=true;Message="";Notify();
        try{await action();}
        catch(ArgumentOutOfRangeException){Message="El precio no puede ser negativo ni exceder el límite permitido.";}
        catch(OverflowException){Message="El importe excede el límite permitido.";}
        catch(ArgumentException e){Message=e.Message;}
        catch(CatalogException e){Message=e.Message;}
        catch(ProductPhotoException e){Message=e.Message;}
        catch(AccessDeniedException){Message="No tienes permiso para esta operación de catálogo.";log("catalog.authorization.denied",null);}
        catch(Exception e){Message="No se completó la operación. Revisa permisos y espacio del equipo.";log("catalog.operation.failed",e.GetType().Name);}
        finally{Busy=false;Notify();}
    }
}
