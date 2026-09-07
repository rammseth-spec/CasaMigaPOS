using System.Text;
namespace CasaMiga.Domain;

public static class CatalogText
{
    // Versioned contract shared by validation, SQLite uniqueness and search. Accents remain significant.
    public static string Key(string? value) => (value??"").Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    public static string Name(string value,int maximum)
    {
        var name=value.Trim().Normalize(NormalizationForm.FormC);
        if(name.Length is 0 || name.Length>maximum) throw new ArgumentException($"El nombre es obligatorio y admite hasta {maximum} caracteres.");
        return name;
    }
    public static string Description(string value)
    {
        if(value.Length>1000) throw new ArgumentException("La descripción admite hasta 1000 caracteres.");
        return value.Trim();
    }
    public static string? Sku(string? value)
    {
        var sku=Key(value);if(sku.Length>80) throw new ArgumentException("El SKU admite hasta 80 caracteres.");
        return sku.Length==0?null:sku;
    }
}
public sealed record Category
{
    public Guid Id {get;}
    public string Name {get;}
    public string Description {get;}
    public int SortOrder {get;}
    public bool IsActive {get;}
    public DateTimeOffset CreatedAt {get;}
    public DateTimeOffset UpdatedAt {get;}
    public Category(Guid id,string name,string description,int sortOrder,bool active,DateTimeOffset created,DateTimeOffset updated)
    {
        if(id==Guid.Empty)throw new ArgumentException("Categoría inválida.");
        Id=id;Name=CatalogText.Name(name,100);Description=CatalogText.Description(description);SortOrder=sortOrder;IsActive=active;
        CreatedAt=created.ToUniversalTime();UpdatedAt=updated.ToUniversalTime();
    }
    public override string ToString()=>Name+(IsActive?"":" · Inactiva");
}
public sealed record Product
{
    public Guid Id {get;}
    public Guid CategoryId {get;}
    public string? Sku {get;}
    public string Name {get;}
    public string Description {get;}
    public Money SalePrice {get;}
    public bool IsActive {get;}
    public bool TrackInventory {get;}
    public DateTimeOffset CreatedAt {get;}
    public DateTimeOffset UpdatedAt {get;}
    public Product(Guid id,Guid category,string? sku,string name,string description,Money price,bool active,bool trackInventory,DateTimeOffset created,DateTimeOffset updated)
    {
        if(id==Guid.Empty||category==Guid.Empty)throw new ArgumentException("Producto o categoría inválidos.");
        if(price.Cents<0)throw new ArgumentOutOfRangeException(nameof(price),"El precio no puede ser negativo.");
        Id=id;CategoryId=category;Sku=CatalogText.Sku(sku);Name=CatalogText.Name(name,160);Description=CatalogText.Description(description);
        SalePrice=price;IsActive=active;TrackInventory=trackInventory;CreatedAt=created.ToUniversalTime();UpdatedAt=updated.ToUniversalTime();
    }
}
