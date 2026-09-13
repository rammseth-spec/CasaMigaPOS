using System.Text.Json;
using CasaMiga.Domain;
namespace CasaMiga.Application;

public enum ConsumptionAction { Enable, Pause, Resume }
public enum ConsumptionError { InvalidTransition, ConcurrencyConflict, PreflightBlocked }
public enum PreflightCode { TrackedProductWithoutInventoryLink, UnexpectedInventoryLink, InvalidLink,
    InventoryItemInactive, InventoryItemMissing, DimensionMismatch, MainInvalid, NotInitialized, ReconciliationMismatch, InvalidQuantity }
public sealed record ConsumptionStatus(InventorySaleConsumptionState State,long Version,DateTimeOffset UpdatedUtc,Guid? UpdatedBy);
public sealed record PreflightIssue(PreflightCode Code,Guid? ProductId=null,string? ProductName=null,
    Guid? InventoryItemId=null,string? InventoryItemName=null,long? LedgerAtomic=null,long? BalanceAtomic=null);
public sealed record ConsumptionPreflight(ConsumptionStatus Status,DateTimeOffset CheckedUtc,IReadOnlyList<PreflightIssue> Problems)
{
    public bool CanEnable=>Problems.Count==0;
    public InventorySaleConsumptionState CurrentState=>Status.State;
    public long CurrentVersion=>Status.Version;
}
public sealed record ConsumptionTransition(bool NoChange,ConsumptionStatus Status);
public sealed class ConsumptionException(ConsumptionError error,ConsumptionPreflight? preflight=null):Exception(error.ToString())
{public ConsumptionError Error {get;}=error;public ConsumptionPreflight? Preflight {get;}=preflight;}

public sealed class SaleConsumptionService(ILocalUnitOfWork write,ILocalReadUnitOfWork read,UserSession session,IClock clock,Guid terminal)
{
    public const string Permission="inventory.sale_consumption.configure";
    private Guid Authorize(ILocalStore store,string permission)
    {
        if(session.Current is not {} user || store.FindUser(user.Id) is not {Active:true}
            || !store.HasPermission(user.Id,permission) || store.FindTerminal(terminal) is null)throw new AccessDeniedException();
        return user.Id;
    }
    public bool CanConfigure()=>read.Read(s=>{try{Authorize(s,Permission);return true;}catch(AccessDeniedException){return false;}});
    public ConsumptionStatus GetStatus()=>read.Read(s=>{Authorize(s,"inventory.view");return s.GetConsumptionStatus();});
    public ConsumptionPreflight Preflight()=>read.Read(s=>{Authorize(s,Permission);return s.CheckConsumption(clock.UtcNow);});
    public ConsumptionTransition Change(ConsumptionAction action,ConsumptionStatus expected)=>write.Run(s=>
    {
        var actor=Authorize(s,Permission);var current=s.GetConsumptionStatus();
        if(current.Version!=expected.Version || current.State!=expected.State)throw new ConsumptionException(ConsumptionError.ConcurrencyConflict);
        var target=action switch {ConsumptionAction.Enable or ConsumptionAction.Resume=>InventorySaleConsumptionState.Enabled,
            ConsumptionAction.Pause=>InventorySaleConsumptionState.Paused,_=>throw new ConsumptionException(ConsumptionError.InvalidTransition)};
        if(current.State==target)return new ConsumptionTransition(true,current);
        if(!((action==ConsumptionAction.Enable && current.State==InventorySaleConsumptionState.Disabled)
            || (action==ConsumptionAction.Resume && current.State==InventorySaleConsumptionState.Paused)
            || (action==ConsumptionAction.Pause && current.State==InventorySaleConsumptionState.Enabled)))
            throw new ConsumptionException(ConsumptionError.InvalidTransition);
        if(target==InventorySaleConsumptionState.Enabled)
        {var check=s.CheckConsumption(clock.UtcNow);if(!check.CanEnable)throw new ConsumptionException(ConsumptionError.PreflightBlocked,check);}
        var next=new ConsumptionStatus(target,checked(current.Version+1),clock.UtcNow,actor);
        s.UpdateConsumption(current,next);
        var verb=action switch {ConsumptionAction.Enable=>"enabled",ConsumptionAction.Pause=>"paused",_=>"resumed"};
        s.AddAudit(new(Guid.NewGuid(),actor,terminal,"inventory.sale_consumption."+verb,terminal,next.UpdatedUtc,
            JsonSerializer.Serialize(new{stateBefore=current.State.ToString(),stateAfter=next.State.ToString(),versionBefore=current.Version,versionAfter=next.Version})));
        return new ConsumptionTransition(false,next);
    });
}
