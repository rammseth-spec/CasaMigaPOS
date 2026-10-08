using System.Text.Json;
using CasaMiga.Domain;
namespace CasaMiga.Application;

public sealed class CatalogException(string message):Exception(message);
public sealed record CatalogQuery(string Search="",Guid? CategoryId=null,bool IncludeInactive=false,int Page=0);
public sealed record ProductRow(Product Product,string CategoryName)
{
    public string Name=>Product.Name;
    public string? Sku=>Product.Sku;
    public string Price=>(Product.SalePrice.Cents/100m).ToString("C2",System.Globalization.CultureInfo.GetCultureInfo("es-MX"));
    public string Status=>Product.IsActive?"Activo":"Inactivo";
}
public sealed record ProductPage(IReadOnlyList<ProductRow> Items,int Total,int Page,int PageSize);

public sealed class CatalogService(ILocalUnitOfWork database,UserSession identity,IClock clock,Guid terminal,bool isDevelopment=false)
{
    public void SeedDemo() => database.Run(s=>
    {
        if(!isDevelopment)throw new CatalogException("Los datos DEMO sólo están habilitados en desarrollo.");
        var actor=Authorize(s,"categories.create");Authorize(s,"products.create");
        if(s.ListCategories(true).Count!=0 || s.QueryProducts(new(IncludeInactive:true)).Total!=0)
            throw new CatalogException("La carga DEMO requiere un catálogo vacío para no mezclar datos.");
        var at=clock.UtcNow;
        var categories=new[]{"PAN DULCE","PAN SALADO","BEBIDAS"}.Select((name,i)=>new Category(Guid.NewGuid(),name,"DEMO · datos sintéticos",i,true,at,at)).ToArray();
        foreach(var c in categories){s.SaveCategory(c,true);Audit(s,actor,"category.create",c.Id,null,Snapshot(c),at);}
        var names=new[]{"Concha vainilla","Concha chocolate","Cuernito mantequilla","Dona","Polvorón","Puerquito piloncillo","Rol canela","Bolillo","Cuernito jamón y queso","Café de olla"};
        for(int i=0;i<names.Length;i++)
        {
            var category=categories[i<7?0:i<9?1:2];
            var product=new Product(Guid.NewGuid(),category.Id,null,names[i],"DEMO · precio ficticio, no comercial",new Money(1800),true,i<9,at,at);
            s.SaveProduct(product,true);Audit(s,actor,"product.create",product.Id,null,Snapshot(product),at);
        }
        return true;
    });
    private Guid Authorize(ILocalStore store,string permission)
    {
        if(identity.Current is not {} actor || store.FindUser(actor.Id) is not {Active:true} || !store.HasPermission(actor.Id,permission))throw new AccessDeniedException();
        if(store.FindTerminal(terminal) is null)throw new CatalogException("Terminal no configurada.");
        return actor.Id;
    }
    public bool Can(string permission)=>database.Run(s=>identity.Current is {} actor && s.FindUser(actor.Id) is {Active:true} && s.HasPermission(actor.Id,permission));
    public IReadOnlyList<Category> Categories(bool includeInactive=false)=>database.Run(s=>{Authorize(s,"catalog.view");return s.ListCategories(includeInactive);});
    public ProductPage Products(CatalogQuery query)=>database.Run(s=>
    {
        Authorize(s,"catalog.view");if(query.Search.Length>160||query.Page<0||query.Page>100000)throw new ArgumentException("Filtro inválido.");
        return s.QueryProducts(query);
    });
    // Same read contract for the future POS; both product and category must be active.
    public ProductPage ActiveProducts(Guid? category=null,int page=0)=>Products(new("",category,false,page));
    private void Audit(ILocalStore s,Guid actor,string action,Guid entity,object? before,object after,DateTimeOffset at)
        =>s.AddAudit(new(Guid.NewGuid(),actor,terminal,action,entity,at,JsonSerializer.Serialize(new {before,after})));
    private static object Snapshot(Category c)=>new {c.Name,c.Description,c.SortOrder,c.IsActive};
    private static object Snapshot(Product p)=>new {p.Name,p.Description,p.CategoryId,p.Sku,priceCents=p.SalePrice.Cents,p.IsActive,p.TrackInventory};
    /// <param name="id">Sólo para altas hechas en la versión web: el mismo identificador en todas las cajas de la sucursal.</param>
    public Category CreateCategory(string name,string description="",int sortOrder=0,Guid? id=null)=>database.Run(s=>
    {
        var actor=Authorize(s,"categories.create");var at=clock.UtcNow;var c=new Category(id??Guid.NewGuid(),name,description,sortOrder,true,at,at);
        s.SaveCategory(c,true);Audit(s,actor,"category.create",c.Id,null,Snapshot(c),at);return c;
    });
    public Category EditCategory(Guid id,string name,string description,int sortOrder)=>database.Run(s=>
    {
        var actor=Authorize(s,"categories.edit");var old=s.FindCategory(id)??throw new CatalogException("Categoría inexistente.");
        var next=new Category(id,name,description,sortOrder,old.IsActive,old.CreatedAt,clock.UtcNow);
        if(old.Name==next.Name&&old.Description==next.Description&&old.SortOrder==next.SortOrder)return old;
        s.SaveCategory(next,false);Audit(s,actor,"category.edit",id,Snapshot(old),Snapshot(next),next.UpdatedAt);return next;
    });
    public Category SetCategoryActive(Guid id,bool active)=>database.Run(s=>
    {
        var actor=Authorize(s,"categories.deactivate");var old=s.FindCategory(id)??throw new CatalogException("Categoría inexistente.");
        if(old.IsActive==active)return old;
        if(!active&&s.CategoryHasActiveProducts(id))throw new CatalogException("La categoría contiene productos activos. Desactívalos o muévelos primero.");
        var next=new Category(id,old.Name,old.Description,old.SortOrder,active,old.CreatedAt,clock.UtcNow);
        s.SaveCategory(next,false);Audit(s,actor,active?"category.activate":"category.deactivate",id,Snapshot(old),Snapshot(next),next.UpdatedAt);return next;
    });
    private static void ValidateCategory(ILocalStore s,Guid category,bool active)
    {
        var c=s.FindCategory(category)??throw new CatalogException("Selecciona una categoría válida.");
        if(active&&!c.IsActive)throw new CatalogException("Un producto activo requiere una categoría activa.");
    }
    /// <param name="id">Sólo para altas hechas en la versión web: el mismo identificador en todas las cajas de la sucursal.</param>
    public Product CreateProduct(Guid category,string? sku,string name,string description,Money price,bool trackInventory=true,Guid? id=null)=>database.Run(s=>
    {
        var actor=Authorize(s,"products.create");ValidateCategory(s,category,true);var at=clock.UtcNow;
        var p=new Product(id??Guid.NewGuid(),category,sku,name,description,price,true,trackInventory,at,at);
        s.SaveProduct(p,true);Audit(s,actor,"product.create",p.Id,null,Snapshot(p),at);return p;
    });
    public Product EditProduct(Guid id,Guid category,string? sku,string name,string description,Money price,bool trackInventory)=>database.Run(s=>
    {
        var actor=Authorize(s,"products.edit");var old=s.FindProduct(id)??throw new CatalogException("Producto inexistente.");ValidateCategory(s,category,old.IsActive);
        var next=new Product(id,category,sku,name,description,price,old.IsActive,trackInventory,old.CreatedAt,clock.UtcNow);
        if(old.Name==next.Name&&old.Description==next.Description&&old.Sku==next.Sku&&old.CategoryId==next.CategoryId&&old.SalePrice==next.SalePrice&&old.TrackInventory==next.TrackInventory)return old;
        s.SaveProduct(next,false);Audit(s,actor,"product.edit",id,Snapshot(old),Snapshot(next),next.UpdatedAt);return next;
    });
    public Product SetProductActive(Guid id,bool active)=>database.Run(s=>
    {
        var actor=Authorize(s,"products.deactivate");var old=s.FindProduct(id)??throw new CatalogException("Producto inexistente.");
        if(old.IsActive==active)return old;ValidateCategory(s,old.CategoryId,active);
        var next=new Product(id,old.CategoryId,old.Sku,old.Name,old.Description,old.SalePrice,active,old.TrackInventory,old.CreatedAt,clock.UtcNow);
        s.SaveProduct(next,false);Audit(s,actor,active?"product.activate":"product.deactivate",id,Snapshot(old),Snapshot(next),next.UpdatedAt);return next;
    });
}
