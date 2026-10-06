using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

[TestClass]
public sealed class CatalogTests
{
    private string root=null!,directory=null!,password=null!;
    private Database db=null!;
    private SqliteLocalUnitOfWork work=null!;
    private UserSession user=null!;
    private TerminalIdentity terminal=null!;
    private CatalogService catalog=null!;
    private TestClock clock=null!;
    private static readonly LocalPasswords Passwords=new();
    private sealed class TestClock:IClock{public DateTimeOffset UtcNow{get;set;}=new(2026,9,6,14,0,0,TimeSpan.Zero);}
    [TestInitialize]public void Setup()
    {
        root=Path.Combine(Path.GetTempPath(),"CasaMigaTests",Guid.NewGuid().ToString("N"));directory=Path.Combine(root,"source");Directory.CreateDirectory(directory);
        db=new(Path.Combine(directory,"casamiga.db"));db.Initialize();work=new(db);user=new();clock=new();password="Aa1!"+Guid.NewGuid().ToString("N")[..12];
        terminal=TerminalConfiguration.LoadOrCreate(directory);work.Run(s=>{s.EnsureTerminal(terminal,clock.UtcNow);return true;});
        var identity=new IdentityService(work,Passwords,clock,user);identity.Bootstrap("admin","Prueba catálogo",password);Assert.IsTrue(identity.Login("admin",password));
        catalog=new(work,user,clock,terminal.Id);
    }
    [TestCleanup]public void Cleanup()=>Directory.Delete(root,true);
    private object? Sql(string text){using var c=db.Open();using var cmd=c.CreateCommand();cmd.CommandText=text;return cmd.ExecuteScalar();}
    private Category Category(string name="Pan dulce")=>catalog.CreateCategory(name,"Descripción",2);
    private Product Product(Category c,string name="Concha",string? sku=null,long cents=1800)=>catalog.CreateProduct(c.Id,sku,name,"Producto de prueba",new Money(cents));
    [TestMethod]public void CategoryNormalizesVisualEdgesAndAuditsDeterministicCreation()
    {
        var c=Category("  Pan Dulce  ");Assert.AreEqual("Pan Dulce",c.Name);Assert.AreEqual(2,c.SortOrder);Assert.IsTrue(c.IsActive);Assert.AreEqual(clock.UtcNow,c.CreatedAt);
        Assert.AreEqual(c,catalog.Categories().Single());Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='category.create'"));
        Assert.AreEqual(user.Current!.Id.ToString("D"),Sql("SELECT actor_id FROM audit_entry WHERE action='category.create'"));
    }
    [TestMethod][DataRow("")][DataRow("   ")][DataRow("\t\n")]
    public void EmptyNamesAreRejectedByDomain(string name)
    {
        Assert.ThrowsExactly<ArgumentException>(()=>Category(name));var c=Category();Assert.ThrowsExactly<ArgumentException>(()=>Product(c,name));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM product"));
    }
    [TestMethod]public void CategoryDuplicatePolicyIncludesCaseWhitespaceAndCanonicalUnicode()
    {
        Category("Café");Assert.ThrowsExactly<CatalogException>(()=>Category("  CAFE\u0301  "));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("INSERT INTO category(id,name,active) VALUES('duplicate',' café ',1)"));
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM category"));Category("Cafe"); // Accents deliberately remain significant.
    }
    [TestMethod]public void CategoryEditOrderDeactivateAndReactivatePreserveHistory()
    {
        var c=Category();clock.UtcNow=clock.UtcNow.AddMinutes(1);var edited=catalog.EditCategory(c.Id,"Pan especial","Nueva",-1);
        Assert.AreEqual(c.CreatedAt,edited.CreatedAt);Assert.AreEqual(clock.UtcNow,edited.UpdatedAt);Assert.AreEqual("Pan especial",edited.Name);
        Category("Otra");Assert.AreEqual(c.Id,catalog.Categories()[0].Id);
        catalog.SetCategoryActive(c.Id,false);Assert.AreEqual(1,catalog.Categories().Count);Assert.AreEqual(2,catalog.Categories(true).Count);
        catalog.SetCategoryActive(c.Id,true);Assert.AreEqual(2,catalog.Categories().Count);
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='category.deactivate'"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='category.activate'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("DELETE FROM category"));
    }
    [TestMethod]public void CategoryWithActiveProductsCannotDeactivateInApplicationOrSql()
    {
        var c=Category();var p=Product(c);
        Assert.ThrowsExactly<CatalogException>(()=>catalog.SetCategoryActive(c.Id,false));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("UPDATE category SET active=0"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM audit_entry WHERE action='category.deactivate'"));
        catalog.SetProductActive(p.Id,false);Assert.IsFalse(catalog.SetCategoryActive(c.Id,false).IsActive);
    }
    [TestMethod]public void CategoryMutationAndAuditRollbackTogether()
    {
        var c=Category();Sql("CREATE TRIGGER fail_category BEFORE INSERT ON audit_entry WHEN NEW.action LIKE 'category.%' BEGIN SELECT RAISE(ABORT,'fault'); END");
        Assert.ThrowsExactly<SqliteException>(()=>catalog.EditCategory(c.Id,"Renamed","",0));Assert.AreEqual(c,catalog.Categories().Single());
        Assert.ThrowsExactly<SqliteException>(()=>Category("Other"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM category"));
    }
    [TestMethod][DataRow(0L)][DataRow(1800L)][DataRow(long.MaxValue)]
    public void ProductPriceAndMetadataPersistExactly(long cents)
    {
        var p=Product(Category(),cents:cents);Assert.AreEqual(cents,p.SalePrice.Cents);Assert.IsNull(p.Sku);Assert.IsTrue(p.TrackInventory);Assert.AreEqual(clock.UtcNow,p.CreatedAt);
        Assert.AreEqual(p,catalog.ActiveProducts().Items.Single().Product);
    }
    [TestMethod]public void NegativeOrFractionalCentPriceIsRejected()
    {
        var c=Category();Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>Product(c,cents:-1));Assert.ThrowsExactly<ArgumentException>(()=>Money.FromPesos(18.001m));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM product"));
    }
    [TestMethod]public void MissingOrInactiveCategoryRejectsActiveProductAndReactivation()
    {
        Assert.ThrowsExactly<CatalogException>(()=>catalog.CreateProduct(Guid.NewGuid(),null,"Pan","",new Money(0)));
        var active=Category();var inactive=Category("Inactive");catalog.SetCategoryActive(inactive.Id,false);
        Assert.ThrowsExactly<CatalogException>(()=>Product(inactive));var p=Product(active);
        Assert.ThrowsExactly<CatalogException>(()=>catalog.EditProduct(p.Id,inactive.Id,null,p.Name,"",p.SalePrice,true));
        catalog.SetProductActive(p.Id,false);catalog.EditProduct(p.Id,inactive.Id,null,p.Name,"",p.SalePrice,true);
        Assert.ThrowsExactly<CatalogException>(()=>catalog.SetProductActive(p.Id,true));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("UPDATE product SET active=1"));
        catalog.SetCategoryActive(inactive.Id,true);Assert.IsTrue(catalog.SetProductActive(p.Id,true).IsActive);
    }
    [TestMethod]public void OptionalSkuNormalizesAndRemainsUniqueForInactiveProducts()
    {
        var c=Category();Product(c,sku:"  ");Product(c,name:"Otro",sku:null);
        var p=Product(c,"Código"," con-vai ");Assert.AreEqual("CON-VAI",p.Sku);catalog.SetProductActive(p.Id,false);
        Assert.ThrowsExactly<CatalogException>(()=>Product(c,"Duplicado","Con-Vai"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("INSERT INTO product SELECT 'duplicate', ' con-vai ', name,category_id,description,price_cents,track_inventory,active,created_utc,modified_utc FROM product WHERE sku='CON-VAI'"));
    }
    [TestMethod]public void EditingNameCategorySkuPriceAndTrackingHasBeforeAfterAudit()
    {
        var c=Category();var other=Category("Pan salado");var p=Product(c,sku:"OLD");clock.UtcNow=clock.UtcNow.AddMinutes(2);
        var edited=catalog.EditProduct(p.Id,other.Id," new ","Concha especial","Detalle",new Money(1900),false);
        Assert.AreEqual("NEW",edited.Sku);Assert.AreEqual(other.Id,edited.CategoryId);Assert.IsFalse(edited.TrackInventory);Assert.AreEqual(p.CreatedAt,edited.CreatedAt);Assert.AreEqual(clock.UtcNow,edited.UpdatedAt);
        using var json=JsonDocument.Parse((string)Sql("SELECT details_json FROM audit_entry WHERE action='product.edit'")!);
        Assert.AreEqual(1800L,json.RootElement.GetProperty("before").GetProperty("priceCents").GetInt64());Assert.AreEqual(1900L,json.RootElement.GetProperty("after").GetProperty("priceCents").GetInt64());
        Assert.AreEqual(user.Current!.Id.ToString("D"),Sql("SELECT actor_id FROM audit_entry WHERE action='product.edit'"));Assert.AreEqual(clock.UtcNow.ToString("O"),Sql("SELECT occurred_utc FROM audit_entry WHERE action='product.edit'"));
    }
    [TestMethod]public void NoOpEditsAndReadsDoNotAddAuditNoise()
    {
        var c=Category();var p=Product(c);var before=Sql("SELECT count(*) FROM audit_entry");
        catalog.EditCategory(c.Id,c.Name,c.Description,c.SortOrder);catalog.EditProduct(p.Id,p.CategoryId,p.Sku,p.Name,p.Description,p.SalePrice,p.TrackInventory);
        catalog.SetProductActive(p.Id,true);catalog.SetCategoryActive(c.Id,true);catalog.Products(new());catalog.Categories();
        Assert.AreEqual(before,Sql("SELECT count(*) FROM audit_entry"));
    }
    [TestMethod]public void ProductActivationAndDeactivationPreserveDataAndAudit()
    {
        var p=Product(Category());catalog.SetProductActive(p.Id,false);Assert.AreEqual(0,catalog.ActiveProducts().Total);
        var inactive=catalog.Products(new(IncludeInactive:true)).Items.Single().Product;Assert.AreEqual(p.Name,inactive.Name);Assert.AreEqual(p.SalePrice,inactive.SalePrice);Assert.IsFalse(inactive.IsActive);
        catalog.SetProductActive(p.Id,true);Assert.AreEqual(1,catalog.ActiveProducts().Total);
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='product.activate'"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='product.deactivate'"));
        Assert.ThrowsExactly<SqliteException>(()=>Sql("DELETE FROM product"));
    }
    [TestMethod]public void ProductPriceAuditFailureRollsBackAndCreateHasNoPartialRow()
    {
        var c=Category();var p=Product(c);Sql("CREATE TRIGGER fail_product BEFORE INSERT ON audit_entry WHEN NEW.action LIKE 'product.%' BEGIN SELECT RAISE(ABORT,'fault'); END");
        Assert.ThrowsExactly<SqliteException>(()=>catalog.EditProduct(p.Id,c.Id,null,p.Name,"",new Money(1900),true));
        Assert.AreEqual(1800L,catalog.ActiveProducts().Items.Single().Product.SalePrice.Cents);
        Assert.ThrowsExactly<SqliteException>(()=>Product(c,"Nuevo"));Assert.AreEqual(1L,Sql("SELECT count(*) FROM product"));
    }
    [TestMethod][DataRow("categories.create")][DataRow("categories.edit")][DataRow("categories.deactivate")][DataRow("products.create")][DataRow("products.edit")][DataRow("products.deactivate")][DataRow("catalog.view")]
    public void GranularPermissionsAreRevalidatedAfterLogin(string permission)
    {
        var c=Category();var p=Product(c);var empty=Category("Empty");Sql($"DELETE FROM role_permission WHERE permission_name='{permission}'");
        Action action=permission switch
        {
            "categories.create"=>()=>Category("Denied"),"categories.edit"=>()=>catalog.EditCategory(c.Id,"Denied","",0),"categories.deactivate"=>()=>catalog.SetCategoryActive(empty.Id,false),
            "products.create"=>()=>Product(c,"Denied"),"products.edit"=>()=>catalog.EditProduct(p.Id,c.Id,null,"Denied","",p.SalePrice,true),"products.deactivate"=>()=>catalog.SetProductActive(p.Id,false),_=>()=>catalog.Products(new())
        };
        Assert.ThrowsExactly<AccessDeniedException>(action);
    }
    [TestMethod]public void CashierCanReadButCannotEditAndInactiveUserIsRejected()
    {
        var c=Category();Product(c);Sql("UPDATE app_user SET role_name='Cashier'");Assert.AreEqual(1,catalog.ActiveProducts().Total);
        Assert.ThrowsExactly<AccessDeniedException>(()=>Category("Denied"));Assert.ThrowsExactly<AccessDeniedException>(()=>Product(c,"Denied"));
        Sql("UPDATE app_user SET active=0");Assert.ThrowsExactly<AccessDeniedException>(()=>catalog.Products(new()));user.SignOut();Assert.ThrowsExactly<AccessDeniedException>(()=>catalog.Categories());
    }
    [TestMethod]public void SearchAndCategoryFilterRespectUnicodeSkuAndInactivePolicy()
    {
        var a=Category();var b=Category("Bebidas");Product(a,"Concha vainilla","CON-VAI");var coffee=Product(b,"Café de olla","CAF-1");
        Assert.AreEqual(1,catalog.Products(new("VAINILLA")).Total);Assert.AreEqual(1,catalog.Products(new(" con-vai ")).Total);Assert.AreEqual(1,catalog.Products(new("CAFE\u0301")).Total);
        Assert.AreEqual(1,catalog.Products(new(CategoryId:b.Id)).Total);catalog.SetProductActive(coffee.Id,false);
        Assert.AreEqual(0,catalog.Products(new(CategoryId:b.Id)).Total);Assert.AreEqual(1,catalog.Products(new(CategoryId:b.Id,IncludeInactive:true)).Total);
        catalog.SetCategoryActive(b.Id,false);Assert.AreEqual(1,catalog.Categories().Count);Assert.AreEqual(1,catalog.ActiveProducts().Total);
    }
    [TestMethod]public void RestartAndCompleteBackupPreserveCatalogAndPriceAudit()
    {
        var c=Category();var p=Product(c);catalog.EditProduct(p.Id,c.Id,"NEW",p.Name,"",new Money(1900),true);
        var restarted=new CatalogService(new SqliteLocalUnitOfWork(new Database(db.FilePath)),user,clock,terminal.Id);Assert.AreEqual(1900L,restarted.Products(new()).Items.Single().Product.SalePrice.Cents);
        string package;using(var lease=new DirectoryLease(directory))package=new InstallationBackup(directory).Create();
        var target=Path.Combine(root,"restored");InstallationBackup.Restore(package,target);
        var restoredWork=new SqliteLocalUnitOfWork(new Database(Path.Combine(target,"casamiga.db")));Assert.AreEqual(terminal,TerminalConfiguration.LoadOrCreate(target));
        var restoredCatalog=new CatalogService(restoredWork,user,clock,terminal.Id);Assert.AreEqual(c.Id,restoredCatalog.Categories().Single().Id);Assert.AreEqual(1900L,restoredCatalog.ActiveProducts().Items.Single().Product.SalePrice.Cents);
    }
    [TestMethod]public async Task ConcurrentDuplicateCategoryCreationHasOneRowAndAudit()
    {
        using var gate=new ManualResetEventSlim(false);
        Task<bool> Attempt(string name)=>Task.Run(()=>{gate.Wait();try{new CatalogService(new SqliteLocalUnitOfWork(db),user,clock,terminal.Id).CreateCategory(name);return true;}catch(CatalogException){return false;}});
        var a=Attempt("Pan");var b=Attempt(" PAN ");gate.Set();var result=await Task.WhenAll(a,b);Assert.AreEqual(1,result.Count(x=>x));Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='category.create'"));
    }
    [TestMethod]public void DemoIsExplicitDevelopmentOnlyAtomicAndDoesNotOverwrite()
    {
        Assert.ThrowsExactly<CatalogException>(catalog.SeedDemo);Assert.AreEqual(0,catalog.Categories(true).Count);
        var demo=new CatalogService(work,user,clock,terminal.Id,true);demo.SeedDemo();Assert.AreEqual(3,catalog.Categories().Count);Assert.AreEqual(10,catalog.ActiveProducts().Total);
        Assert.IsTrue(catalog.ActiveProducts().Items.All(x=>x.Product.Description.Contains("DEMO",StringComparison.Ordinal)));
        Assert.ThrowsExactly<CatalogException>(demo.SeedDemo);Assert.AreEqual(10,catalog.ActiveProducts().Total);
    }
    [TestMethod]public void ThousandsOfProductsArePagedWithStableOrder()
    {
        var category=Category();
        using(var c=db.Open())using(var tx=c.BeginTransaction())using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;cmd.CommandText="INSERT INTO product(id,category_id,name,price_cents,track_inventory,active,created_utc,modified_utc) VALUES($id,$category,$name,1800,1,1,$at,$at)";
            cmd.Parameters.AddWithValue("$id","");cmd.Parameters.AddWithValue("$category",category.Id.ToString("D"));cmd.Parameters.AddWithValue("$name","");cmd.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));
            for(int i=0;i<2000;i++){cmd.Parameters["$id"].Value=Guid.NewGuid().ToString("D");cmd.Parameters["$name"].Value=$"Producto {i:0000}";cmd.ExecuteNonQuery();}tx.Commit();
        }
        var first=catalog.ActiveProducts();var next=catalog.ActiveProducts(page:1);Assert.AreEqual(2000,first.Total);Assert.AreEqual(50,first.Items.Count);Assert.AreEqual(50,next.Items.Count);
        Assert.IsFalse(first.Items.Select(x=>x.Product.Id).Intersect(next.Items.Select(x=>x.Product.Id)).Any());Assert.AreEqual("Producto 0050",next.Items[0].Name);
        Assert.AreEqual(1,catalog.Products(new("1999")).Total);
    }
    private Database VersionThree()
    {
        var previous=new Database(Path.Combine(root,"v3.db"));var assembly=typeof(Database).Assembly;
        using var c=previous.Open();using var cmd=c.CreateCommand();cmd.CommandText="CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT";cmd.ExecuteNonQuery();
        for(int i=1;i<=3;i++)
        {
            using var reader=new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.Contains($".{i:000}_",StringComparison.Ordinal)))!);var sql=reader.ReadToEnd();cmd.Parameters.Clear();cmd.CommandText=sql;cmd.ExecuteNonQuery();
            cmd.CommandText="INSERT INTO schema_migration VALUES($v,$hash,$at)";cmd.Parameters.AddWithValue("$v",i);cmd.Parameters.AddWithValue("$hash",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));cmd.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O",CultureInfo.InvariantCulture));cmd.ExecuteNonQuery();
        }
        return previous;
    }
    [TestMethod]public void VersionThreeUpgradePreservesCategoryProductAndPrice()
    {
        var previous=VersionThree();var categoryId=Guid.NewGuid();var productId=Guid.NewGuid();
        using(var c=previous.Open())using(var cmd=c.CreateCommand())
        {
            cmd.CommandText="INSERT INTO category VALUES($c,'Pan original',1); INSERT INTO product VALUES($p,NULL,'Producto original',$c,'',1800,1,1,$at,$at)";
            cmd.Parameters.AddWithValue("$c",categoryId.ToString("D"));cmd.Parameters.AddWithValue("$p",productId.ToString("D"));cmd.Parameters.AddWithValue("$at",clock.UtcNow.ToString("O"));cmd.ExecuteNonQuery();
        }
        previous.Initialize();previous.Validate();var next=new SqliteLocalUnitOfWork(previous);
        Assert.AreEqual("Pan original",next.Run(s=>s.FindCategory(categoryId))!.Name);Assert.AreEqual(1800L,next.Run(s=>s.FindProduct(productId))!.SalePrice.Cents);
    }
    [TestMethod]public void ConflictingLegacyNamesRejectUpgradeWithoutSilentlyRenaming()
    {
        var previous=VersionThree();using(var c=previous.Open())using(var cmd=c.CreateCommand()){cmd.CommandText="INSERT INTO category VALUES('one','Pan',1),('two',' PAN ',1)";cmd.ExecuteNonQuery();}
        using(var inspect=previous.Open())using(var command=inspect.CreateCommand())
        {
            command.CommandText="SELECT count(*),count(DISTINCT catalog_key_v1(name)) FROM category";using var rows=command.ExecuteReader();rows.Read();
            Assert.AreEqual(2L,rows.GetInt64(0),"Legacy row count");Assert.AreEqual(1L,rows.GetInt64(1),"Legacy normalized count");
        }
        Assert.ThrowsExactly<InvalidOperationException>(previous.Initialize);
        using var connection=previous.Open();using var query=connection.CreateCommand();query.CommandText="SELECT count(*) FROM schema_migration";Assert.AreEqual(3L,query.ExecuteScalar());
        query.CommandText="SELECT count(*) FROM category";Assert.AreEqual(2L,query.ExecuteScalar());
    }
}


