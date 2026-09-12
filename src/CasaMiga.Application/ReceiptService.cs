using System.Text.Json;
using System.Text;
using System.Collections.Concurrent;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public sealed class ReceiptService(
    ILocalUnitOfWork database,
    UserSession identity,
    IClock clock,
    Guid terminalId,
    TimeZoneInfo timeZone,
    IPrintingSettingsProvider settingsProvider,
    IReceiptPayloadRenderer payloadRenderer,
    IReceiptPrintTransport transport)
{
    private readonly ConcurrentDictionary<Guid, byte> activeReprints = new();
    private sealed record Prepared(ReceiptPrintRequest Request, ReceiptDocument? Document, ReceiptPrintEvent? ExistingEvent);

    public bool CanReprint()=>Read(store=>
        identity.Current is { } actor
        && store.FindUser(actor.Id) is {Active:true}
        && store.HasPermission(actor.Id,"sales.receipt.reprint"));

    private T Read<T>(Func<ILocalStore,T> operation)=>database is ILocalReadUnitOfWork read
        ? read.Read(operation)
        : database.Run(operation);

    public ReceiptPrintResult PrintOriginal(Sale sale)
    {
        ArgumentNullException.ThrowIfNull(sale);
        return Execute(sale.Id, ReceiptCopyKind.Original, null);
    }
    public ReceiptPrintResult Retry(Guid requestId)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("Intento de impresión inválido.");
        var parent=database.Run(s=>s.FindReceiptPrintRequest(requestId))
            ?? throw new InvalidOperationException("El intento de impresión ya no está disponible.");
        return parent.Kind==ReceiptCopyKind.Reprint
            ? ExecuteReprint(parent.SaleId,parent.Id)
            : Execute(parent.SaleId,ReceiptCopyKind.Retry,parent.Id);
    }
    public ReceiptPrintResult Reprint(Guid saleId)
    {
        if (saleId == Guid.Empty) throw new ArgumentException("Venta inválida.");
        return ExecuteReprint(saleId,null);
    }

    private ReceiptPrintResult ExecuteReprint(Guid saleId,Guid? parent)
    {
        if(!activeReprints.TryAdd(saleId,0))
            throw new InvalidOperationException("Ya hay una reimpresión en curso para esta venta.");
        try{return Execute(saleId,ReceiptCopyKind.Reprint,parent);}
        finally{activeReprints.TryRemove(saleId,out _);}
    }

    private ReceiptPrintResult Execute(Guid saleId,ReceiptCopyKind kind,Guid? parent)
    {
        var configuration=settingsProvider.Read();
        var prepared=database.Run(store=>Prepare(store,saleId,kind,parent,configuration));
        if(prepared.ExistingEvent is { } existing)
            return Result(prepared.Request.Id,existing.State,existing.SpoolerJobId,existing.ErrorCode);
        if(!configuration.Available || prepared.Document is null)
            return Finish(prepared.Request,ReceiptPrintState.Failed,null,configuration.ErrorCode??"PRINTING_DISABLED");

        try
        {
            var payload=payloadRenderer.Render(prepared.Document,kind,configuration.Settings!);
            var submitted=transport.Submit(new(configuration.Settings!.QueueName,
                $"Casa Miga {prepared.Document.Folio} {prepared.Request.Id:N}",payload));
            return Finish(prepared.Request,ReceiptPrintState.Submitted,submitted.JobId,null);
        }
        catch(PrintTransportException error)
        {
            return Finish(prepared.Request,error.OutcomeUncertain?ReceiptPrintState.Unknown:ReceiptPrintState.Failed,null,error.ErrorCode);
        }
        catch(Exception error) when(error is ArgumentException or EncoderFallbackException or InvalidDataException)
        {
            return Finish(prepared.Request,ReceiptPrintState.Failed,null,"RENDER_FAILED");
        }
    }

    private Prepared Prepare(ILocalStore store,Guid saleId,ReceiptCopyKind kind,Guid? parent,PrintingSettingsResult configuration)
    {
        var actor=Authorize(store,kind);
        var detail=store.FindSaleDetail(saleId)??throw new InvalidOperationException("La venta ya no está disponible.");
        if(kind!=ReceiptCopyKind.Reprint && detail.Sale.UserId!=actor)throw new AccessDeniedException();
        if(kind==ReceiptCopyKind.Original && store.FindOriginalReceiptPrintRequest(saleId) is { } prior)
            return new(prior,null,store.FindLatestReceiptPrintEvent(prior.Id));
        if(parent is { } parentId)
        {
            var parentRequest=store.FindReceiptPrintRequest(parentId);
            if(parentRequest is null || parentRequest.SaleId!=saleId)throw new InvalidOperationException("El intento anterior no corresponde a la venta.");
        }

        ReceiptDocument? document=null;string? hash=null;
        if(configuration.Available)
        {
            var snapshot=store.FindReceiptSnapshot(saleId);
            if(snapshot is null)
            {
                document=ReceiptDocument.FromSale(detail,configuration.Settings!.Business,timeZone);
                var json=ReceiptDocumentCodec.Serialize(document);hash=ReceiptDocumentCodec.Hash(json);
                snapshot=new(saleId,document.Version,json,hash,clock.UtcNow);store.AddReceiptSnapshot(snapshot);
            }
            else
            {
                if(ReceiptDocumentCodec.Hash(snapshot.ContentJson)!=snapshot.Sha256)throw new InvalidDataException("El snapshot de ticket fue alterado.");
                document=ReceiptDocumentCodec.Deserialize(snapshot.ContentJson);
                if(document.Version!=snapshot.SchemaVersion || document.Sha256!=snapshot.Sha256)throw new InvalidDataException("El snapshot de ticket no es coherente.");
                hash=snapshot.Sha256;
            }
        }
        var request=new ReceiptPrintRequest(Guid.NewGuid(),saleId,kind,parent,actor,clock.UtcNow,
            configuration.Available?configuration.Settings!.QueueName:null,hash);
        store.AddReceiptPrintRequest(request);
        store.AddReceiptPrintEvent(new(Guid.NewGuid(),request.Id,ReceiptPrintState.Requested,clock.UtcNow));
        store.AddAudit(new(Guid.NewGuid(),actor,terminalId,"receipt.print.requested",request.Id,clock.UtcNow,
            JsonSerializer.Serialize(new{saleId,kind=kind.ToString(),parentRequestId=parent,queue=request.QueueName,snapshotSha256=hash})));
        return new(request,document,null);
    }

    private ReceiptPrintResult Finish(ReceiptPrintRequest request,ReceiptPrintState state,int? jobId,string? errorCode)
    {
        try
        {
            database.Run(store=>
            {
                _=Authorize(store,request.Kind);
                store.AddReceiptPrintEvent(new(Guid.NewGuid(),request.Id,state,clock.UtcNow,jobId,errorCode));
                store.AddAudit(new(Guid.NewGuid(),request.RequestedBy,terminalId,$"receipt.print.{state.ToString().ToLowerInvariant()}",request.Id,clock.UtcNow,
                    JsonSerializer.Serialize(new{saleId=request.SaleId,kind=request.Kind.ToString(),spoolerJobId=jobId,errorCode})));
                return true;
            });
        }
        catch when(state==ReceiptPrintState.Submitted)
        {
            return Result(request.Id,ReceiptPrintState.Unknown,jobId,"RESULT_NOT_RECORDED");
        }
        return Result(request.Id,state,jobId,errorCode);
    }

    private Guid Authorize(ILocalStore store,ReceiptCopyKind kind)
    {
        if(identity.Current is not { } actor || store.FindUser(actor.Id) is not {Active:true})throw new AccessDeniedException();
        var permission=kind==ReceiptCopyKind.Reprint?"sales.receipt.reprint":"sales.complete";
        if(!store.HasPermission(actor.Id,permission))throw new AccessDeniedException();
        return actor.Id;
    }
    private static ReceiptPrintResult Result(Guid request,ReceiptPrintState state,int? job,string? code)
        =>new(request,state,state switch
        {
            ReceiptPrintState.Submitted=>$"Ticket aceptado por la cola de impresión. Trabajo {job}.",
            ReceiptPrintState.Completed=>"La cola informó que el trabajo terminó; verifica el papel físicamente.",
            ReceiptPrintState.Unknown=>"El resultado de impresión es incierto. Revisa la impresora antes de reintentar.",
            ReceiptPrintState.Failed=>code is "PRINTING_DISABLED" or "CONFIG_MISSING"?"La impresora no está configurada. La venta permanece confirmada.":"No se pudo enviar el ticket. La venta permanece confirmada.",
            _=>"El ticket está pendiente de envío."
        },job);
}
