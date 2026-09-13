using System.ComponentModel;
using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Presentation;

public sealed class ConsumptionViewModel(SaleConsumptionService service):INotifyPropertyChanged
{
    public ConsumptionStatus? Status {get;private set;}
    public bool CanConfigure {get;private set;}
    public bool Busy {get;private set;}
    public bool Ready=>!Busy;
    public bool CanEnable=>CanConfigure && Status?.State==InventorySaleConsumptionState.Disabled;
    public bool CanPause=>CanConfigure && Status?.State==InventorySaleConsumptionState.Enabled;
    public bool CanResume=>CanConfigure && Status?.State==InventorySaleConsumptionState.Paused;
    public string StateLabel=>Status?.State switch {InventorySaleConsumptionState.Disabled=>"DESACTIVADO",InventorySaleConsumptionState.Enabled=>"ACTIVO",InventorySaleConsumptionState.Paused=>"PAUSADO",_=>"No disponible"};
    public string Explanation=>Status?.State==InventorySaleConsumptionState.Enabled?"Las ventas configuradas descuentan existencias automáticamente.":"Las ventas se registran sin descontar existencias automáticamente.";
    public string Updated=>Status is {} s?$"Última actualización: {s.UpdatedUtc.ToLocalTime():dd/MM/yyyy HH:mm}":"";
    public string Message {get;private set;}="";
    public IReadOnlyList<string> Problems {get;private set;}=[];
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify()=>PropertyChanged?.Invoke(this,new(null));
    public Task LoadAsync()=>Run(async()=>{Status=await Task.Run(service.GetStatus);CanConfigure=await Task.Run(service.CanConfigure);});
    public Task CheckAsync()=>Run(async()=>Show(await Task.Run(service.Preflight)));
    private void Show(ConsumptionPreflight result)
    {Status=result.Status;Problems=result.Problems.Select(Describe).ToArray();Message=result.CanEnable?"Configuración válida. Se verificará nuevamente al activar o reanudar.":"Corrige los siguientes problemas antes de activar o reanudar:";}
    public Task ChangeAsync(ConsumptionAction action,bool confirmed)
    {
        if(!confirmed || Status is null)return Task.CompletedTask;
        var expected=Status;
        return Run(async()=>{var result=await Task.Run(()=>service.Change(action,expected));Status=result.Status;Message=result.NoChange?"El estado ya estaba aplicado.":"Estado actualizado.";});
    }
    private async Task Run(Func<Task> action)
    {
        if(Busy)return;Busy=true;Message="";Problems=[];Notify();
        try{await action();}
        catch(AccessDeniedException){CanConfigure=false;Status=null;Message="No tienes permiso para esta operación.";}
        catch(ConsumptionException e)
        {
            if(e.Preflight is {} check)Show(check);
            else Message=e.Error==ConsumptionError.ConcurrencyConflict?"Otro administrador cambió el estado. Actualiza y revisa antes de intentar nuevamente.":"Este cambio no está disponible desde el estado actual.";
        }
        catch(Exception){Message="No se pudo verificar el resultado. Actualiza el estado antes de intentar nuevamente.";}
        finally{Busy=false;Notify();}
    }
    public static string Describe(PreflightIssue issue)
    {
        var product=issue.ProductName??issue.InventoryItemName??"El artículo";
        return issue.Code switch
        {
            PreflightCode.TrackedProductWithoutInventoryLink=>$"{product} controla inventario pero no tiene un artículo asociado.",
            PreflightCode.UnexpectedInventoryLink=>$"{product} tiene un artículo asociado, pero no está marcado para controlar inventario.",
            PreflightCode.InventoryItemInactive=>$"{product} está asociado a un artículo de inventario inactivo.",
            PreflightCode.NotInitialized=>$"{product} no tiene saldo inicial registrado.",
            PreflightCode.ReconciliationMismatch=>"Hay diferencias entre los movimientos y las existencias. Solicita revisión administrativa; no se repararán automáticamente.",
            PreflightCode.MainInvalid=>$"{product} requiere un almacén principal válido y activo.",
            _=>$"{product} tiene una asociación o cantidad de inventario inválida. Revisa su configuración."
        };
    }
}
