using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        private static Category ReadCategory(SqliteDataReader r)=>new(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetString(2),r.GetInt32(3),r.GetInt32(4)==1,Date(r,5)!.Value,Date(r,6)!.Value);
        public Category? FindCategory(Guid id)
        {
            using var cmd=Command("SELECT id,name,description,sort_order,active,created_utc,modified_utc FROM category WHERE id=$id",("$id",Id(id)));
            using var r=cmd.ExecuteReader();return r.Read()?ReadCategory(r):null;
        }
        public IReadOnlyList<Category> ListCategories(bool includeInactive)
        {
            using var cmd=Command("SELECT id,name,description,sort_order,active,created_utc,modified_utc FROM category WHERE $all=1 OR active=1 ORDER BY sort_order,name COLLATE CATALOG_KEY_V1,id",("$all",includeInactive?1:0));
            using var r=cmd.ExecuteReader();var list=new List<Category>();while(r.Read())list.Add(ReadCategory(r));return list;
        }
        public bool CategoryHasActiveProducts(Guid id)
        {
            using var cmd=Command("SELECT EXISTS(SELECT 1 FROM product WHERE category_id=$id AND active=1)",("$id",Id(id)));return Convert.ToInt64(cmd.ExecuteScalar())==1;
        }
        public IReadOnlyList<Category> ListPosCategories()
        {
            using var cmd=Command("SELECT c.id,c.name,c.description,c.sort_order,c.active,c.created_utc,c.modified_utc FROM category c WHERE c.active=1 AND EXISTS(SELECT 1 FROM product p WHERE p.category_id=c.id AND p.active=1) ORDER BY c.sort_order,c.name COLLATE CATALOG_KEY_V1,c.id");
            using var rows=cmd.ExecuteReader();var result=new List<Category>();
            while(rows.Read())result.Add(ReadCategory(rows));return result;
        }
        public void SaveCategory(Category c,bool create)
        {
            using var cmd=Command(create?
                "INSERT INTO category(id,name,description,sort_order,active,created_utc,modified_utc) VALUES($id,$name,$description,$sort,$active,$created,$updated)":
                "UPDATE category SET name=$name,description=$description,sort_order=$sort,active=$active,modified_utc=$updated WHERE id=$id",
                ("$id",Id(c.Id)),("$name",c.Name),("$description",c.Description),("$sort",c.SortOrder),("$active",c.IsActive?1:0),("$created",At(c.CreatedAt)),("$updated",At(c.UpdatedAt)));
            try{if(cmd.ExecuteNonQuery()!=1)throw new CatalogException("No se pudo guardar la categoría.");}
            catch(SqliteException e)when(e.SqliteExtendedErrorCode==2067){throw new CatalogException("Ya existe una categoría con ese nombre, incluso si está inactiva.");}
        }
        private static Product ReadProduct(SqliteDataReader r)=>new(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3),r.GetString(4),new Money(r.GetInt64(5)),r.GetInt32(6)==1,r.GetInt32(7)==1,Date(r,8)!.Value,Date(r,9)!.Value);
        private const string ProductColumns="p.id,p.category_id,p.sku,p.name,p.description,p.price_cents,p.active,p.track_inventory,p.created_utc,p.modified_utc";
        public Product? FindProduct(Guid id)
        {
            using var cmd=Command("SELECT "+ProductColumns+" FROM product p WHERE p.id=$id",("$id",Id(id)));
            using var r=cmd.ExecuteReader();return r.Read()?ReadProduct(r):null;
        }
        public ProductPage QueryProducts(CatalogQuery query)
        {
            const int pageSize=50;
            const string filter=" FROM product p JOIN category c ON c.id=p.category_id WHERE ($all=1 OR (p.active=1 AND c.active=1)) AND ($category IS NULL OR p.category_id=$category) AND ($search='' OR instr(catalog_key_v1(p.name),$search)>0 OR instr(catalog_key_v1(p.sku),$search)>0)";
            using var cmd=Command("SELECT count(*)"+filter,("$all",query.IncludeInactive?1:0),("$category",query.CategoryId is {} cat?Id(cat):null),("$search",CatalogText.Key(query.Search)));
            var total=Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText="SELECT "+ProductColumns+",c.name"+filter+" ORDER BY p.name COLLATE CATALOG_KEY_V1,p.id LIMIT $limit OFFSET $offset";
            cmd.Parameters.AddWithValue("$limit",pageSize);cmd.Parameters.AddWithValue("$offset",checked(query.Page*pageSize));
            using var r=cmd.ExecuteReader();var items=new List<ProductRow>();while(r.Read())items.Add(new(ReadProduct(r),r.GetString(10)));
            return new(items,total,query.Page,pageSize);
        }
        public void SaveProduct(Product p,bool create)
        {
            using var cmd=Command(create?
                "INSERT INTO product(id,category_id,sku,name,description,price_cents,active,track_inventory,created_utc,modified_utc) VALUES($id,$category,$sku,$name,$description,$price,$active,$track,$created,$updated)":
                "UPDATE product SET category_id=$category,sku=$sku,name=$name,description=$description,price_cents=$price,active=$active,track_inventory=$track,modified_utc=$updated WHERE id=$id",
                ("$id",Id(p.Id)),("$category",Id(p.CategoryId)),("$sku",p.Sku),("$name",p.Name),("$description",p.Description),("$price",p.SalePrice.Cents),("$active",p.IsActive?1:0),("$track",p.TrackInventory?1:0),("$created",At(p.CreatedAt)),("$updated",At(p.UpdatedAt)));
            try{if(cmd.ExecuteNonQuery()!=1)throw new CatalogException("No se pudo guardar el producto.");}
            catch(SqliteException e)when(e.SqliteExtendedErrorCode==2067){throw new CatalogException("El SKU ya pertenece a otro producto, incluso si está inactivo.");}
        }
    }
}
