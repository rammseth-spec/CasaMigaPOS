using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CasaMiga.Tests;

public sealed partial class RecipePersistenceTests
{
    private RecipeViewModel RecipeVm()=>new(RecipeApplication(),inventory,catalog,(_,_)=>{});
    private ProductionViewModel ProductionVm()=>new(Production(),RecipeApplication(),inventory,catalog,user,terminal.Id,(_,_)=>{});

    [TestMethod] public async Task RecipeUiAdministratorCreatesPublishesAndActivatesFormula()
    {
        var vm=RecipeVm();await vm.LoadAsync();Assert.IsTrue(vm.CanView,vm.Message);Assert.IsTrue(vm.CanConfigure,vm.Message);Assert.IsTrue(vm.Items.Any(x=>x.Id==flour.Id),vm.Message);vm.NewFormula();vm.Code="UI-FORMULA";vm.Name="Fórmula sintética UI";await vm.CreateFormulaAsync();
        vm.NewFormulaVersion();vm.Name="Versión sintética";vm.Yield="1";vm.YieldDimension=InventoryDimension.Mass;vm.YieldUnit="kg";vm.AddIngredient(false);
        vm.Ingredients[0].Item=vm.Items.Single(x=>x.Id==flour.Id);vm.Ingredients[0].Quantity="500";vm.Ingredients[0].Unit="g";await vm.SaveFormulaVersionAsync();Assert.IsTrue(vm.MessageIsSuccess,vm.Message);Assert.HasCount(1,vm.Versions,vm.Message);
        vm.SelectedVersion=vm.Versions.Single();await vm.PublishAsync();Assert.IsTrue(vm.PublishedFormulas.Any(x=>x.Id==vm.Versions.Single().Id));vm.SelectedVersion=vm.Versions.Single();await vm.ActivateAsync();
        Assert.IsTrue(vm.Versions.Single().Active);Assert.AreEqual("Publicada",vm.Versions.Single().State);Assert.IsTrue(vm.MessageIsSuccess);
        Assert.AreEqual("Versión 1",vm.Formulas.Single(x=>x.Code=="UI-FORMULA").ActiveVersion);
        Assert.AreEqual("No",vm.Formulas.Single(x=>x.Code=="UI-FORMULA").Draft);
        vm.SelectedVersion=vm.Versions.Single();await vm.EditFormulaVersionAsync();Assert.IsFalse(vm.IsFormulaEditor);StringAssert.Contains(vm.Message,"publicadas");
    }

    [TestMethod] public async Task RecipeUiSupportsAlternativeGroupsAndOperationalUnits()
    {
        var vm=RecipeVm();await vm.LoadAsync();Assert.IsTrue(vm.Items.Any(x=>x.Id==flour.Id),vm.Message);vm.NewFormula();vm.Code="UI-ALT";vm.Name="Alternativas sintéticas";await vm.CreateFormulaAsync();vm.NewFormulaVersion();vm.Name="Alternativas";vm.Yield="100";vm.YieldUnit="g";
        vm.AddIngredient(false);vm.Ingredients[0].Item=vm.Items.Single(x=>x.Id==flour.Id);vm.Ingredients[0].Quantity="50";vm.Ingredients[0].Unit="g";
        vm.AddIngredient(true);vm.Alternatives[0].Item=vm.Items.Single(x=>x.Id==fresh.Id);vm.Alternatives[0].Quantity="10";vm.Alternatives[0].Unit="g";
        vm.AddIngredient(true);vm.Alternatives[1].Group=vm.Alternatives[0].Group;vm.Alternatives[1].Preference=2;vm.Alternatives[1].Item=vm.Items.Single(x=>x.Id==packet.Id);vm.Alternatives[1].Quantity="1";vm.Alternatives[1].Unit="pieza";vm.Alternatives[1].Policy=IngredientCountPolicy.CeilingWholeCount;
        await vm.SaveFormulaVersionAsync();Assert.IsTrue(vm.MessageIsSuccess,vm.Message);Assert.IsFalse(vm.Items.Any(x=>x.Name.Contains("mg")||x.Name.Contains("µL")));
        Assert.AreEqual("Redondear hacia arriba a pieza completa",RecipeViewModel.PolicyLabel(IngredientCountPolicy.CeilingWholeCount));
    }

    [TestMethod] public async Task RecipeUiCreatesProfileWithFormulaAndDirectIngredient()
    {
        ProductionSetup();var next=catalog.CreateProduct(product.CategoryId,"UI-OUT","Salida sintética UI","",new Money(100));var nextItem=inventory.CreateInventoryItem("UI-OUT","Salida sintética UI",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        var vm=RecipeVm();await vm.LoadAsync();Assert.IsTrue(vm.Items.Any(x=>x.Id==nextItem.Id),vm.Message);Assert.IsTrue(vm.Products.Any(x=>x.Id==next.Id),vm.Message);await vm.ShowProfilesAsync();vm.NewProfile();vm.ProfileProduct=vm.Products.Single(x=>x.Id==next.Id);vm.ProfileFinishedItem=vm.Items.Single(x=>x.Id==nextItem.Id);await vm.CreateProfileAsync();
        vm.NewProfileVersion();vm.Name="Perfil sintético";vm.AddFormulaRequirement();vm.FormulaRequirements[0].Formula=vm.PublishedFormulas.Single(x=>x.Id==version.Id);vm.FormulaRequirements[0].Quantity="25";vm.FormulaRequirements[0].Unit="g";vm.AddDirectIngredient();vm.DirectIngredients[0].Item=vm.Items.Single(x=>x.Id==topping.Id);vm.DirectIngredients[0].Quantity="1";vm.DirectIngredients[0].Unit="pieza";vm.DirectIngredients[0].Policy=IngredientCountPolicy.ExactWholeCount;
        await vm.SaveProfileVersionAsync();vm.SelectedVersion=vm.Versions.Single();await vm.PublishProfileAsync();vm.SelectedVersion=vm.Versions.Single();await vm.ActivateProfileAsync();Assert.IsTrue(vm.Versions.Single().Active);
        vm.Back();Assert.AreEqual("Versión 1",vm.Profiles.Single(x=>x.Summary.ProductId==next.Id).ActiveVersion);
        Assert.AreEqual("No",vm.Profiles.Single(x=>x.Summary.ProductId==next.Id).Draft);
    }

    [TestMethod] public async Task RecipeAndProductionNavigationRespectRoles()
    {
        ProductionSetup();
        foreach(var role in new[]{"Administrator","Manager","Cashier"})
        {
            Sql($"UPDATE app_user SET role_name='{role}'");var recipe=RecipeVm();await recipe.LoadAsync();var production=ProductionVm();await production.LoadAsync();
            if(role=="Cashier"){Assert.IsFalse(recipe.CanView);Assert.IsFalse(production.CanView);StringAssert.Contains(recipe.Message,"permiso");}
            else{Assert.IsTrue(recipe.CanView);Assert.IsTrue(production.CanView);Assert.AreEqual(role=="Administrator",recipe.CanConfigure);Assert.IsTrue(production.CanCreate);Assert.IsTrue(production.CanConfirm);}
        }
    }

    [TestMethod] public async Task ProductionUiPreviewsCreatesConfirmsAndShowsHistory()
    {
        ProductionSetup();var vm=ProductionVm();await vm.LoadAsync();Assert.IsTrue(vm.Profiles.Any(x=>x.VersionId==profileVersion.Id),vm.Message);vm.NewProduction();vm.Plan[0].Profile=vm.Profiles.Single(x=>x.VersionId==profileVersion.Id);vm.Plan[0].Planned="2";await vm.PreviewAsync();
        Assert.IsTrue(vm.PreviewReady);Assert.IsTrue(vm.PreviewConfirmable,vm.PreviewState);Assert.IsNotEmpty(vm.FormulaRows);Assert.IsTrue(vm.RequirementRows.All(x=>!x.Required.Contains("mg")&&!x.Required.Contains("µL")));Assert.IsNotEmpty(vm.Alternatives);
        await vm.CreateDraftAsync();Assert.IsTrue(vm.DetailIsDraft);StringAssert.Contains(vm.ConfirmationRestriction,"bloqueado");vm.Actuals[0].Actual="3";Assert.AreEqual("Mayor que lo planeado",vm.Actuals[0].Difference);
        await Task.WhenAll(vm.ConfirmAsync(),vm.ConfirmAsync());Assert.IsTrue(vm.DetailIsConfirmed);Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='production.confirmed'"));
        await vm.ShowHistoryAsync();Assert.HasCount(1,vm.Rows);Assert.AreEqual("Confirmada",vm.Rows.Single().State);vm.SelectedRow=vm.Rows.Single();await vm.OpenDetailAsync();Assert.IsNotEmpty(vm.DetailInputs);Assert.IsFalse(vm.DetailContext.Contains("OperationId",StringComparison.Ordinal));
    }

    [TestMethod] public async Task ProductionUiAcceptsZeroAndShowsHumanErrors()
    {
        ProductionSetup();var vm=ProductionVm();await vm.LoadAsync();Assert.IsTrue(vm.Profiles.Any(x=>x.VersionId==profileVersion.Id),vm.Message);vm.NewProduction();vm.Plan[0].Profile=vm.Profiles.Single(x=>x.VersionId==profileVersion.Id);vm.Plan[0].Planned="2";await vm.PreviewAsync();Assert.IsTrue(vm.PreviewConfirmable,vm.PreviewState);await vm.CreateDraftAsync();Assert.IsTrue(vm.DetailIsDraft,vm.Message);vm.Actuals[0].Actual="0";Assert.IsTrue(vm.TotalZero);await vm.ConfirmAsync();Assert.IsTrue(vm.DetailIsConfirmed,vm.Message);Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind='PRODUCTION_OUTPUT'"));
        Assert.AreEqual("No hay existencia suficiente para confirmar esta producción.",ProductionViewModel.ErrorMessage(new(ProductionFailure.InsufficientStock)));
        Assert.AreEqual("La producción cambió o fue confirmada desde otra operación. Actualiza la información.",ProductionViewModel.ErrorMessage(new(ProductionFailure.ConcurrencyConflict)));
        Assert.AreEqual("Un insumo o producto terminado ya no está activo.",ProductionViewModel.ErrorMessage(new(ProductionFailure.InactiveItem)));
    }

    [TestMethod] public void ProductionActualComparisonNotifiesAfterEditing()
    {
        var row=new ProductionActualEdit{Planned=2,Actual="2"};var changed=new List<string>();row.PropertyChanged+=(_,e)=>changed.Add(e.PropertyName!);
        row.Actual="3";Assert.AreEqual("Mayor que lo planeado",row.Difference);CollectionAssert.Contains(changed,nameof(ProductionActualEdit.Difference));
        row.Actual="1";Assert.AreEqual("Menor que lo planeado",row.Difference);row.Actual="2";Assert.AreEqual("Igual a lo planeado",row.Difference);
    }

    [TestMethod] public void ProductionDetailAggregatesRepeatedInputSnapshots()
    {
        var item=Guid.NewGuid();var rows=new[]{
            new StoredProductionInput(item,"Insumo",new(InventoryDimension.Count,6),null,Guid.NewGuid(),null,null,IngredientCountPolicy.ExactWholeCount,new(6,1)),
            new StoredProductionInput(item,"Insumo",new(InventoryDimension.Count,2),null,Guid.NewGuid(),null,null,IngredientCountPolicy.ExactWholeCount,new(2,1))};
        CollectionAssert.AreEqual(new[]{"Insumo: 8 pieza"},ProductionViewModel.InputLines(rows).ToArray());
    }

    [TestMethod] public void RecipeProductionXamlHidesTechnicalConceptsAndKeepsModules()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"Presentation");var recipe=File.ReadAllText(Path.Combine(root,"RecipeView.xaml"));var production=File.ReadAllText(Path.Combine(root,"ProductionView.xaml"))+File.ReadAllText(Path.Combine(root,"ProductionView.xaml.cs"));var main=File.ReadAllText(Path.Combine(root,"MainWindow.xaml"));
        foreach(var forbidden in new[]{"canonical hash","OperationId","numerator","denominator","µL"," mg"})Assert.IsFalse((recipe+production).Contains(forbidden,StringComparison.OrdinalIgnoreCase),forbidden);
        foreach(var required in new[]{"Caja","Catálogo","Historial de ventas","Inventario","Recetas","Producción","Punto de venta"})StringAssert.Contains(main,required);
        StringAssert.Contains(production,"Esta producción no generará producto terminado");StringAssert.Contains(production,"Planeado");StringAssert.Contains(production,"Real obtenido");
    }

    [TestMethod] public async Task RecipeUiExplainsInvalidDraftFields()
    {
        var vm=RecipeVm();await vm.LoadAsync();vm.NewFormula();vm.Code="UI-VALIDATION";vm.Name="Validación";await vm.CreateFormulaAsync();vm.NewFormulaVersion();
        vm.AddIngredient(false);vm.Ingredients[0].Item=vm.Items.Single(x=>x.Id==flour.Id);vm.Ingredients[0].Quantity="1";vm.Ingredients[0].Unit="g";
        await vm.SaveFormulaVersionAsync();Assert.AreEqual("Escribe el nombre de la versión.",vm.Message);
        Assert.AreEqual("Una unidad o política no corresponde al tipo de artículo seleccionado.",RecipeViewModel.ErrorMessage(new(ProductionError.DimensionMismatch)));
        Assert.AreEqual("Un mismo artículo no puede repetirse en la receta.",RecipeViewModel.ErrorMessage(new(ProductionError.DuplicateIngredient)));
    }
}
