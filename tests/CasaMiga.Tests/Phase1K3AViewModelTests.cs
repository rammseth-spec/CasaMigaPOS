using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

public sealed partial class RecipePersistenceTests
{
    [TestMethod]
    public async Task UsabilityDimensionsAreSpanishAndFinishedChoicesExcludeRawPieces()
    {
        var vm=RecipeVm();await vm.LoadAsync();
        CollectionAssert.AreEqual(new[]{"Masa","Volumen","Piezas"},vm.Dimensions.Select(x=>x.Label).ToArray());
        foreach(var choice in vm.Dimensions)Assert.AreEqual(choice.Label,InventoryViewModel.DimensionLabel(choice.Value));
        Assert.IsFalse(vm.FinishedItems.Any(x=>x.Id==packet.Id));
        var finished=inventory.CreateInventoryItem("UI-FIN","Terminado",InventoryItemKind.FinishedGood,InventoryDimension.Count);
        await vm.LoadAsync();Assert.IsTrue(vm.FinishedItems.Any(x=>x.Id==finished.Id));
        Assert.IsFalse(vm.CanCreateProfile);vm.NewProfile();
        var category=catalog.CreateCategory("Prueba UX");var product=catalog.CreateProduct(category.Id,"UX-P","Pan","",new Money(100));
        vm.ProfileProduct=new(product.Id,product.Name);Assert.IsFalse(vm.CanCreateProfile);
        vm.ProfileFinishedItem=vm.FinishedItems.Single(x=>x.Id==finished.Id);Assert.IsTrue(vm.CanCreateProfile);
    }

    [TestMethod]
    public async Task UsabilityRecipeVersionActionsFollowSelectionAndExistingPermissions()
    {
        var vm=RecipeVm();await vm.LoadAsync();
        Assert.IsFalse(vm.CanEditSelectedVersion);Assert.IsFalse(vm.CanActivateSelectedVersion);
        vm.SelectedVersion=new(Guid.NewGuid(),1,"Borrador","V1",1,false);
        Assert.IsTrue(vm.CanEditSelectedVersion);Assert.IsFalse(vm.CanActivateSelectedVersion);
        vm.SelectedVersion=vm.SelectedVersion with{State="Publicada"};
        Assert.IsFalse(vm.CanEditSelectedVersion);Assert.IsTrue(vm.CanActivateSelectedVersion);
        vm.SelectedVersion=vm.SelectedVersion with{Active=true};Assert.IsFalse(vm.CanActivateSelectedVersion);
        Sql("UPDATE app_user SET role_name='Manager'");await vm.LoadAsync();
        Assert.IsFalse(vm.CanEditSelectedVersion);Assert.IsFalse(vm.CanActivateSelectedVersion);
        Assert.AreEqual("Consulta · sólo lectura",vm.ModeLabel);
    }

    [TestMethod]
    public async Task UsabilityProductionInvalidPlanAndEditsDisableCreationImmediately()
    {
        ProductionSetup();var vm=ProductionVm();await vm.LoadAsync();vm.NewProduction();
        Assert.IsFalse(vm.CanCalculate);Assert.IsFalse(vm.CanCreateDraft);Assert.IsFalse(vm.CanRemoveOutput);
        vm.SelectedProductChoice=vm.Profiles.Single(x=>x.VersionId==profileVersion.Id);vm.AddOutput();vm.Plan[0].Planned="2";
        Assert.IsTrue(vm.CanCalculate);await vm.PreviewAsync();Assert.IsTrue(vm.CanCreateDraft,vm.Message);
        vm.Plan[0].Planned="3";Assert.IsFalse(vm.PreviewReady);Assert.IsFalse(vm.CanCreateDraft);
        vm.Plan[0].Planned="0";Assert.IsFalse(vm.CanCalculate);
        vm.Plan[0].Planned="2";vm.AddOutput();Assert.IsFalse(vm.CanCalculate);
        vm.Plan[1].Profile=vm.Plan[0].Profile;Assert.IsFalse(vm.CanCalculate,"Repeated profile cannot enable calculation.");
        vm.SelectedPlan=vm.Plan[1];Assert.IsTrue(vm.CanRemoveOutput);vm.RemoveOutput();
        Assert.IsTrue(vm.CanCalculate);Assert.IsFalse(vm.CanRemoveOutput);
        await vm.PreviewAsync();Assert.IsTrue(vm.CanCreateDraft);
        var alternative=vm.Alternatives.First();alternative.Selected=null;Assert.IsFalse(vm.CanCreateDraft);
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM production_batch"));
    }

    [TestMethod]
    public async Task UsabilityProductionEmptyNavigationDoesNotCreateData()
    {
        var vm=ProductionVm();await vm.LoadAsync();Assert.IsFalse(vm.HasProfiles);
        vm.NewProduction();Assert.IsTrue(vm.IsBuilder);Assert.IsFalse(vm.CanCalculate);
        await vm.ShowDraftsAsync();Assert.IsTrue(vm.IsDrafts);Assert.IsEmpty(vm.Rows);Assert.IsFalse(vm.CanOpenDetail);
        Assert.AreEqual("No hay producciones en borrador.",vm.EmptyRowsMessage);
        await vm.ShowHistoryAsync();Assert.IsTrue(vm.IsHistory);Assert.IsEmpty(vm.Rows);Assert.IsFalse(vm.CanOpenDetail);
        Assert.AreEqual("Aún no hay producciones confirmadas.",vm.EmptyRowsMessage);
        vm.Back();Assert.IsTrue(vm.IsHome);Assert.AreEqual(0L,Sql("SELECT count(*) FROM production_batch"));
    }

    [TestMethod]
    [DataRow("DAMAGED","Dañado")]
    [DataRow("EXPIRED","Caducado")]
    [DataRow("PRODUCTION_ERROR","Error de producción")]
    [DataRow("SPILL","Derrame")]
    [DataRow("OTHER","Otro")]
    [DataRow("PHYSICAL_COUNT_VARIANCE","Diferencia de conteo físico")]
    [DataRow("DATA_CORRECTION","Corrección de datos")]
    public void UsabilityReasonCodesHaveSpanishDisplayLabels(string code,string label)=>Assert.AreEqual(label,UiLabels.MovementReason(code));
}
