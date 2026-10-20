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


    [TestMethod] public async Task RecipeUiAddsAPreparationInsideAnotherAndEditsTheDraftAgain()
    {
        var butter=Item("BUTTER",InventoryDimension.Mass);
        var vm=RecipeVm();await vm.LoadAsync();vm.NewFormula();vm.Code="UI-ENRIQUECIDA";vm.Name="Enriquecida de prueba";await vm.CreateFormulaAsync();
        vm.NewFormulaVersion();vm.Name="Versión de prueba";vm.Yield="100";vm.YieldUnit="g";
        Assert.IsFalse(vm.PreparationChoices.Any(x=>x.Name.StartsWith("Enriquecida",StringComparison.Ordinal)),"A recipe never offers itself.");
        vm.AddFormulaPreparation();vm.FormulaPreparations[0].Formula=vm.PreparationChoices.Single(x=>x.Id==version.Id);vm.FormulaPreparations[0].Quantity="0.05";vm.FormulaPreparations[0].Unit="g";
        StringAssert.Contains(vm.PreparationChoices.Single(x=>x.Id==version.Id).Name,"(borrador)");
        vm.AddIngredient(false);vm.Ingredients[0].Item=vm.Items.Single(x=>x.Id==butter.Id);vm.Ingredients[0].Quantity="0.05";vm.Ingredients[0].Unit="g";
        Assert.IsTrue(vm.CanSaveFormulaDraft);await vm.SaveFormulaVersionAsync();Assert.IsTrue(vm.MessageIsSuccess,vm.Message);
        vm.SelectedVersion=vm.Versions.Single();await vm.EditFormulaVersionAsync();Assert.IsTrue(vm.IsFormulaEditor,vm.Message);
        Assert.AreEqual(version.Id,vm.FormulaPreparations.Single().Formula!.Id,"The draft comes back with its preparation.");
        vm.FormulaPreparations[0].Quantity="0.06";await vm.SaveFormulaVersionAsync();Assert.IsTrue(vm.MessageIsSuccess,"Editing an existing draft saves: "+vm.Message);
        Assert.AreEqual(60L,RecipeApplication().GetFormulaVersion(vm.Versions.Single().Id)!.Value.Preparations.Single().Quantity.AtomicUnits);
        vm.SelectedVersion=vm.Versions.Single();await vm.PublishAsync();StringAssert.Contains(vm.Message,"Publica primero las preparaciones");
    }

    [TestMethod] public async Task RecipeUiEditsAProfileDraftAgain()
    {
        ProductionSetup();var next=catalog.CreateProduct(product.CategoryId,"UI-EDIT","Edición sintética UI","",new Money(100));var nextItem=inventory.CreateInventoryItem("UI-EDIT","Edición sintética UI",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        var vm=RecipeVm();await vm.LoadAsync();await vm.ShowProfilesAsync();vm.NewProfile();vm.ProfileProduct=vm.Products.Single(x=>x.Id==next.Id);vm.ProfileFinishedItem=vm.Items.Single(x=>x.Id==nextItem.Id);await vm.CreateProfileAsync();
        vm.NewProfileVersion();vm.Name="Perfil sintético";vm.AddDirectIngredient();vm.DirectIngredients[0].Item=vm.Items.Single(x=>x.Id==flour.Id);vm.DirectIngredients[0].Quantity="10";vm.DirectIngredients[0].Unit="g";
        await vm.SaveProfileVersionAsync();Assert.IsTrue(vm.MessageIsSuccess,vm.Message);
        vm.SelectedVersion=vm.Versions.Single();await vm.EditProfileVersionAsync();vm.DirectIngredients[0].Quantity="12";await vm.SaveProfileVersionAsync();
        Assert.IsTrue(vm.MessageIsSuccess,"Editing an existing profile draft saves: "+vm.Message);
        Assert.AreEqual(12_000L,RecipeApplication().GetProfileVersion(vm.Versions.Single().Id)!.Value.DirectIngredients.Single().Ingredient.Quantity.AtomicUnits);
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
        ProductionSetup();var vm=ProductionVm();await vm.LoadAsync();var profile=vm.Profiles.Single(x=>x.VersionId==profileVersion.Id);StringAssert.Contains(profile.ToString(),"Versión 1");vm.NewProduction();vm.SelectedProductChoice=profile;vm.AddOutput();vm.Plan[0].Planned="2";var beforePreview=AllRows(true);await vm.PreviewAsync();
        Assert.AreEqual(beforePreview,AllRows(true),"La vista previa debe ser de sólo lectura.");Assert.IsTrue(vm.PreviewReady);Assert.IsTrue(vm.PreviewConfirmable,vm.PreviewState);Assert.IsNotEmpty(vm.FormulaRows);Assert.IsTrue(vm.FormulaRows.All(x=>x.VersionLabel.StartsWith("Versión ",StringComparison.Ordinal)));Assert.IsTrue(vm.RequirementRows.All(x=>!x.Required.Contains("mg")&&!x.Required.Contains("µL")));Assert.IsTrue(vm.RequirementRows.All(x=>x.State=="Suficiente"&&x.Shortage=="—"));Assert.IsNotEmpty(vm.Alternatives);
        await vm.CreateDraftAsync();Assert.IsTrue(vm.DetailIsDraft);StringAssert.Contains(vm.ConfirmationRestriction,"quedó fijado");Assert.AreEqual("Plan de producción",vm.DetailPlanTitle);Assert.AreEqual("Preparaciones del plan",vm.DetailFormulasTitle);Assert.AreEqual("Ingredientes que se descontarán",vm.DetailInputsTitle);vm.Actuals[0].Actual="3";Assert.AreEqual("1 pieza más de lo planeado",vm.Actuals[0].Difference);
        await Task.WhenAll(vm.ConfirmAsync(),vm.ConfirmAsync());Assert.IsTrue(vm.DetailIsConfirmed);Assert.AreEqual(1L,Sql("SELECT count(*) FROM audit_entry WHERE action='production.confirmed'"));
        Assert.AreEqual("Ingredientes consumidos",vm.DetailInputsTitle);await vm.ShowHistoryAsync();Assert.HasCount(1,vm.Rows);Assert.AreEqual("Confirmada",vm.Rows.Single().State);StringAssert.Contains(vm.Rows.Single().Result,"2 planeadas");StringAssert.Contains(vm.Rows.Single().Result,"3 reales");StringAssert.Contains(vm.Rows.Single().OutputResult,"3 piezas obtenidas");Assert.IsFalse(vm.Rows.Single().Result.Contains(profileVersion.Id.ToString(),StringComparison.Ordinal));vm.SelectedRow=vm.Rows.Single();await vm.OpenDetailAsync();Assert.IsNotEmpty(vm.DetailInputs);Assert.IsFalse(vm.DetailContext.Contains("OperationId",StringComparison.Ordinal));StringAssert.Contains(vm.DetailContext,"Registrada:");Assert.AreEqual("Ingredientes consumidos",vm.DetailInputsTitle);
    }

    [TestMethod] public async Task ProductionUiAcceptsZeroAndShowsHumanErrors()
    {
        ProductionSetup();var vm=ProductionVm();await vm.LoadAsync();Assert.IsTrue(vm.Profiles.Any(x=>x.VersionId==profileVersion.Id),vm.Message);vm.NewProduction();vm.SelectedProductChoice=vm.Profiles.Single(x=>x.VersionId==profileVersion.Id);vm.AddOutput();vm.Plan[0].Planned="2";await vm.PreviewAsync();Assert.IsTrue(vm.PreviewConfirmable,vm.PreviewState);await vm.CreateDraftAsync();Assert.IsTrue(vm.DetailIsDraft,vm.Message);vm.Actuals[0].Actual="0";Assert.IsTrue(vm.TotalZero);Assert.AreEqual("2 piezas menos de lo planeado",vm.Actuals[0].Difference);await vm.ConfirmAsync();Assert.IsTrue(vm.DetailIsConfirmed,vm.Message);Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind='PRODUCTION_OUTPUT'"));await vm.ShowHistoryAsync();StringAssert.Contains(vm.Rows.Single().OutputResult,"sin producto terminado");
        Assert.AreEqual("No hay existencia suficiente para registrar esta producción.",ProductionViewModel.ErrorMessage(new(ProductionFailure.InsufficientStock)));
        Assert.AreEqual("La producción cambió o ya fue registrada desde otra operación. Actualiza la información.",ProductionViewModel.ErrorMessage(new(ProductionFailure.ConcurrencyConflict)));
        Assert.AreEqual("Un insumo o producto terminado ya no está activo.",ProductionViewModel.ErrorMessage(new(ProductionFailure.InactiveItem)));
    }

    [TestMethod] public void ProductionActualComparisonNotifiesAfterEditing()
    {
        var row=new ProductionActualEdit{Planned=5,Actual="5"};var changed=new List<string>();row.PropertyChanged+=(_,e)=>changed.Add(e.PropertyName!);
        Assert.AreEqual("Igual a lo planeado",row.Difference);row.Actual="4";Assert.AreEqual("1 pieza menos de lo planeado",row.Difference);CollectionAssert.Contains(changed,nameof(ProductionActualEdit.Difference));
        row.Actual="3";Assert.AreEqual("2 piezas menos de lo planeado",row.Difference);row.Actual="6";Assert.AreEqual("1 pieza más de lo planeado",row.Difference);row.Actual="7";Assert.AreEqual("2 piezas más de lo planeado",row.Difference);row.Actual="0";Assert.AreEqual("5 piezas menos de lo planeado",row.Difference);
    }

    [TestMethod] public void ProductionFormattingKeepsExactHumanMassVolumeAndCount()
    {
        Assert.AreEqual("1 pieza",InventoryDisplay.FormatProduction(1,InventoryDimension.Count));
        Assert.AreEqual("2 piezas",InventoryDisplay.FormatProduction(2,InventoryDimension.Count));
        Assert.AreEqual("6 piezas",InventoryDisplay.FormatProduction(6,InventoryDimension.Count));
        Assert.AreEqual("99 kg 687.5 g",InventoryDisplay.FormatProduction(99_687_500,InventoryDimension.Mass));
        Assert.AreEqual("9 kg 995 g",InventoryDisplay.FormatProduction(9_995_000,InventoryDimension.Mass));
        Assert.AreEqual("312.5 g",InventoryDisplay.FormatProduction(312_500,InventoryDimension.Mass));
        Assert.AreEqual("99 L 875 ml",InventoryDisplay.FormatProduction(99_875_000,InventoryDimension.Volume));
        Assert.AreEqual("125 ml",InventoryDisplay.FormatProduction(125_000,InventoryDimension.Volume));
    }

    [TestMethod] public async Task ProductionUiDisablesRegistrationUntilEveryActualIsValid()
    {
        ProductionSetup();var vm=ProductionVm();await vm.LoadAsync();vm.NewProduction();vm.SelectedProductChoice=vm.Profiles.Single(x=>x.VersionId==profileVersion.Id);vm.AddOutput();vm.Plan[0].Planned="2";await vm.PreviewAsync();await vm.CreateDraftAsync();
        Assert.IsTrue(vm.CanConfirmDetail);vm.Actuals[0].Actual="";Assert.IsFalse(vm.ActualsAreValid);Assert.IsFalse(vm.CanConfirmDetail);StringAssert.Contains(vm.ConfirmationRestriction,"entera");
        vm.Actuals[0].Actual="-1";Assert.IsFalse(vm.CanConfirmDetail);vm.Actuals[0].Actual="0";Assert.IsTrue(vm.CanConfirmDetail);Assert.IsTrue(vm.TotalZero);
        Assert.AreEqual(1L,Sql("SELECT count(*) FROM production_batch WHERE status='DRAFT'"));Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind LIKE 'PRODUCTION_%'"));
    }

    [TestMethod] public async Task ProductionUiShowsEveryProductAndExplainsReadiness()
    {
        ProductionSetup();
        var without=catalog.CreateProduct(product.CategoryId,"NO-RECIPE","Producto sin receta","",new Money(100));
        var incompleteProduct=catalog.CreateProduct(product.CategoryId,"INCOMPLETE","Producto incompleto","",new Money(100));
        var incompleteItem=inventory.CreateInventoryItem("INCOMPLETE-OUT","Salida incompleta",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        RecipeApplication().CreateProfile(new(Guid.NewGuid(),incompleteProduct.Id,incompleteItem.Id));
        var vm=ProductionVm();await vm.LoadAsync();
        Assert.AreEqual("LISTO PARA PRODUCIR",vm.Profiles.Single(x=>x.ProductId==product.Id).Status);
        Assert.AreEqual("SIN RECETA",vm.Profiles.Single(x=>x.ProductId==without.Id).Status);
        Assert.AreEqual("RECETA INCOMPLETA",vm.Profiles.Single(x=>x.ProductId==incompleteProduct.Id).Status);
        vm.ProductSearch="sin receta";Assert.HasCount(1,vm.FilteredProfiles);Assert.AreEqual(without.Id,vm.FilteredProfiles.Single().ProductId);
        vm.NewProduction();vm.SelectedProductChoice=vm.Profiles.Single(x=>x.ProductId==without.Id);vm.AddOutput();
        Assert.IsFalse(vm.CanCalculate);StringAssert.Contains(vm.PlanBlockingMessage,"no tiene perfil productivo");
        var blocked=vm.Plan.Single();vm.SelectedProductChoice=vm.Profiles.Single(x=>x.ProductId==product.Id);vm.AddOutput();vm.SelectedPlan=blocked;vm.RemoveOutput();vm.Plan.Single().Planned="2";
        Assert.IsTrue(vm.CanCalculate,vm.PlanBlockingMessage);await vm.PreviewAsync();Assert.IsTrue(vm.PreviewConfirmable,vm.PreviewState);
    }

    [TestMethod] public async Task ProductionUiRecordsFinishedGoodWasteWithReasonAndRejectsOverdraw()
    {
        ProductionSetup();inventory.ReceiveInventory(new(Guid.NewGuid(),finished.Id,ProductionContract.Main,3,InventoryUnits.Piece.Code));
        var vm=ProductionVm();await vm.LoadAsync();vm.OpenWaste();Assert.IsTrue(vm.IsWaste);Assert.IsTrue(vm.CanRecordWaste);
        vm.SelectedWasteProduct=vm.WasteProducts.Single(x=>x.ProductId==product.Id);vm.SelectedWasteReason=vm.WasteReasons.Single(x=>x.Value==WasteReason.ProductionError);vm.WasteQuantity="2";vm.WasteNote="Lote sintético";
        Assert.IsTrue(vm.CanSaveWaste);await vm.SaveWasteAsync();Assert.IsTrue(vm.MessageIsSuccess,vm.Message);
        var row=inventory.MovementHistory(new(finished.Id)).Items.Single(x=>x.Kind==InventoryMovementKind.Waste);Assert.AreEqual(-2,row.DeltaAtomic);Assert.AreEqual("PRODUCTION_ERROR",row.ReasonCode);Assert.AreEqual("Lote sintético",row.Note);
        vm.WasteQuantity="2";await vm.SaveWasteAsync();Assert.IsFalse(vm.MessageIsSuccess);StringAssert.Contains(vm.Message,"suficientes");Assert.AreEqual(1L,Sql($"SELECT quantity_atomic FROM inventory_balance WHERE item_id='{finished.Id}'"));
    }

    [TestMethod] public void ProductionDetailAggregatesRepeatedInputSnapshots()
    {
        var item=Guid.NewGuid();var rows=new[]{
            new StoredProductionInput(item,"Insumo",new(InventoryDimension.Count,6),null,Guid.NewGuid(),null,null,IngredientCountPolicy.ExactWholeCount,new(6,1)),
            new StoredProductionInput(item,"Insumo",new(InventoryDimension.Count,2),null,Guid.NewGuid(),null,null,IngredientCountPolicy.ExactWholeCount,new(2,1))};
        CollectionAssert.AreEqual(new[]{"Insumo: 8 piezas"},ProductionViewModel.InputLines(rows).ToArray());
    }

    [TestMethod] public void RecipeProductionXamlHidesTechnicalConceptsAndKeepsModules()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"Presentation");var recipe=File.ReadAllText(Path.Combine(root,"RecipeView.xaml"));var production=File.ReadAllText(Path.Combine(root,"ProductionView.xaml"))+File.ReadAllText(Path.Combine(root,"ProductionView.xaml.cs"));var main=File.ReadAllText(Path.Combine(root,"MainWindow.xaml"));
        foreach(var forbidden in new[]{"canonical hash","OperationId","numerator","denominator","µL"," mg"})Assert.IsFalse((recipe+production).Contains(forbidden,StringComparison.OrdinalIgnoreCase),forbidden);
        foreach(var required in new[]{"Caja","Catálogo","Historial de ventas","Inventario","Recetas","Producción","Punto de venta"})StringAssert.Contains(main,required);
        StringAssert.Contains(production,"Esta producción no generará producto terminado");StringAssert.Contains(production,"Piezas planeadas");StringAssert.Contains(production,"Resultado real obtenido");StringAssert.Contains(production,"Resultado obtenido");StringAssert.Contains(production,"Registrar producción");StringAssert.Contains(production,"ContextHelp");Assert.IsFalse(production.Contains("DataGridCheckBoxColumn Header=\"Sin producto",StringComparison.Ordinal));
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
