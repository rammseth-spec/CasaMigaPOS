using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

public sealed partial class RecipePersistenceTests
{
    [TestMethod]
    public async Task Phase1K4GCB2BuilderStartsEmptyAndExplainsDisabledActions()
    {
        ProductionSetup();var vm=ProductionVm();await vm.LoadAsync();vm.NewProduction();
        Assert.IsEmpty(vm.Plan,"A new production must not start with a placeholder row of quantity 1.");
        Assert.AreEqual("Agrega al menos un producto al plan.",vm.PlanBlockingMessage);
        Assert.IsFalse(vm.CanAddSelectedProduct);StringAssert.Contains(vm.AddProductHint,"Selecciona un producto");
        Assert.IsFalse(vm.CanRemoveOutput);Assert.AreEqual("El plan está vacío.",vm.RemoveOutputHint);
        Assert.IsFalse(vm.CanCreateDraft);StringAssert.Contains(vm.CreateDraftHint,"completa un plan válido");

        var profile=vm.Profiles.Single(x=>x.VersionId==profileVersion.Id);
        vm.SelectedProductChoice=profile;Assert.IsTrue(vm.CanAddSelectedProduct);StringAssert.Contains(vm.AddProductHint,"listo para agregarse");
        vm.AddOutput();
        Assert.HasCount(1,vm.Plan);Assert.AreEqual("1",vm.Plan[0].Planned);Assert.AreSame(vm.Plan[0],vm.SelectedPlan);
        vm.SelectedProductChoice=profile;Assert.IsFalse(vm.CanAddSelectedProduct);StringAssert.Contains(vm.AddProductHint,"ya está en el plan");
        StringAssert.Contains(vm.CreateDraftHint,"Calcular necesidades");
        await vm.PreviewAsync();Assert.IsTrue(vm.CanCreateDraft,vm.PreviewState);StringAssert.Contains(vm.CreateDraftHint,"quedará fijado");

        Assert.IsTrue(vm.CanRemoveOutput,"The only selected row can be removed now that the plan starts empty.");vm.RemoveOutput();
        Assert.IsEmpty(vm.Plan);Assert.IsFalse(vm.PreviewReady);Assert.IsFalse(vm.CanCalculate);Assert.IsFalse(vm.CanCreateDraft);

        Assert.IsTrue(vm.CanRecordWaste);vm.OpenWaste();Assert.AreEqual("",vm.WasteHint);
        vm.WasteQuantity="0";Assert.IsFalse(vm.CanSaveWaste);StringAssert.Contains(vm.WasteHint,"número entero");
        vm.WasteQuantity="1";vm.SelectedWasteReason=vm.WasteReasons.Single(x=>x.Value==CasaMiga.Domain.WasteReason.Other);
        Assert.IsFalse(vm.CanSaveWaste);StringAssert.Contains(vm.WasteHint,"observación es obligatoria");

        await vm.ShowHistoryAsync();Assert.AreEqual("Selecciona una producción de la lista para ver su detalle.",vm.OpenDetailHint);
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM production_batch"));
        Assert.AreEqual(0L,Sql("SELECT count(*) FROM inventory_operation WHERE kind='WASTE'"));
    }
}
