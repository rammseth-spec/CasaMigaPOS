using System.Text.Json;
using CasaMiga.Domain;
namespace CasaMiga.Application;

public interface ICheckoutJournal
{
    Guid? Read();
    void Save(Guid operation);
    void Clear(Guid operation);
}
public sealed class CheckoutException(string message) : Exception(message);
public sealed record CheckoutRequest(Guid OperationId, Cart Cart, Money Received);

public sealed class CheckoutService(ILocalUnitOfWork database, UserSession identity, IClock clock,
    Guid terminal, ICheckoutJournal journal)
{
    private CheckoutRequest? unresolved { get => identity.UnresolvedCheckout; set => identity.UnresolvedCheckout=value; }
    public bool HasPending => journal.Read() is not null;
    private Guid Authorize(ILocalStore store)
    {
        if (identity.Current is not { } actor || store.FindUser(actor.Id) is not { Active:true }
            || !store.HasPermission(actor.Id,"catalog.view") || !store.HasPermission(actor.Id,"pos.use")
            || !store.HasPermission(actor.Id,"sales.complete")) throw new AccessDeniedException();
        return actor.Id;
    }
    public CheckoutRequest Prepare(Money received) => identity.Synchronized(() =>
    {
        if (HasPending) throw new CheckoutException("Hay un cobro pendiente de verificación.");
        var cart=identity.Cart ?? throw new CheckoutException("El carrito está vacío.");
        ValidateCart(cart, received);
        database.Run(s=>{Authorize(s);ValidateContext(s,cart);return true;});
        return new CheckoutRequest(Guid.NewGuid(),cart,received);
    });
    private static void ValidateCart(Cart cart, Money received)
    {
        if(cart.IsEmpty) throw new CheckoutException("El carrito está vacío.");
        _=cart.Subtotal.ChangeFrom(received);
    }
    private CashSession ValidateContext(ILocalStore store, Cart cart)
    {
        if(cart.UserId!=identity.Current?.Id || cart.TerminalId!=terminal)
            throw new CheckoutException("El carrito pertenece a otro contexto.");
        var cash=store.FindSession(cart.CashSessionId);
        if(cash is null || cash.State!=CashState.Open || cash.TerminalId!=terminal || store.FindCount(cash.Id) is not null)
            throw new CheckoutException("La caja debe estar abierta y sin conteo confirmado.");
        return cash;
    }
    private static bool Matches(Sale sale, CheckoutRequest request) => sale.OperationId==request.OperationId
        && sale.UserId==request.Cart.UserId && sale.TerminalId==request.Cart.TerminalId && sale.CashSessionId==request.Cart.CashSessionId
        && sale.Payment.Received==request.Received && sale.Items.SequenceEqual(request.Cart.Lines.Select(l=>new SaleItem(l.ProductId,l.ProductName,l.UnitPrice,l.Quantity)));
    private void Finish(Sale sale, CheckoutRequest? request)
    {
        // Do not clear a different cart when returning an old idempotent result.
        if(request is not null && ReferenceEquals(identity.Cart,request.Cart)) identity.Cart=null;
        // Keep the durable marker until the operator acknowledges the displayed result.
        identity.CheckoutPending=true;unresolved=null;
    }
    public Sale Confirm(CheckoutRequest request) => identity.Synchronized(() =>
    {
        if(request.OperationId==Guid.Empty) throw new CheckoutException("Operación inválida.");
        if(request.Cart.UserId!=identity.Current?.Id || request.Cart.TerminalId!=terminal) throw new AccessDeniedException();
        database.Run(s=>Authorize(s));
        ValidateCart(request.Cart,request.Received);
        var pending=journal.Read();
        if(pending is not null && pending!=request.OperationId) throw new CheckoutException("Verifica el cobro anterior.");
        var previousRequest=unresolved;
        unresolved=request;
        journal.Save(request.OperationId);identity.CheckoutPending=true;
        Sale result;
        try
        {
            result=database.Run(store=>
            {
                var actor=Authorize(store);
                if(store.FindSaleByOperation(request.OperationId) is { } existing)
                {
                    if(existing.UserId!=actor || existing.TerminalId!=terminal || !Matches(existing,request))
                        throw new CheckoutException("El identificador de operación ya corresponde a otro contenido.");
                    return existing;
                }
                if(!ReferenceEquals(identity.Cart,request.Cart)) throw new CheckoutException("El carrito cambió. Vuelve a preparar el cobro.");
                var cash=ValidateContext(store,request.Cart);
                foreach(var line in request.Cart.Lines)
                {
                    var product=store.FindProduct(line.ProductId);
                    if(product is not { IsActive:true } || store.FindCategory(product.CategoryId) is not { IsActive:true })
                        throw new CheckoutException("Un producto o su categoría ya no están activos. El carrito no cambió.");
                }
                _=cash.OpeningFund + store.CashSalesTotal(cash.Id) + request.Cart.Subtotal;
                var sale=new Sale(Guid.NewGuid(),request.OperationId,actor,terminal,cash.Id,store.NextSaleSequence(terminal),clock.UtcNow,
                    request.Cart.Lines.Select(l=>new SaleItem(l.ProductId,l.ProductName,l.UnitPrice,l.Quantity)),Guid.NewGuid(),request.Received);
                store.AddSale(sale);
                store.AddAudit(new(Guid.NewGuid(),actor,terminal,"sale.completed",sale.Id,sale.ConfirmedAt,
                    JsonSerializer.Serialize(new{operationId=sale.OperationId,folio=sale.Folio,cashSessionId=cash.Id,paymentId=sale.Payment.Id,
                        totalCents=sale.Total.Cents,receivedCents=sale.Payment.Received.Cents,changeCents=sale.Payment.Change.Cents})));
                return sale;
            });
        }
        catch
        {
            // Run has disposed its transaction. An independent read resolves an uncertain commit.
            Sale? stored;
            try { stored=database.Run(s=>s.FindSaleByOperation(request.OperationId)); }
            catch { throw new CheckoutException("No se pudo confirmar el resultado. Verifica el cobro pendiente; no vuelvas a recibir dinero."); }
            if(stored is not null)
            {
                if(!Matches(stored,request))
                {
                    // A conflicting retry must not erase an earlier unresolved operation.
                    if(pending is null){journal.Clear(request.OperationId);identity.CheckoutPending=false;unresolved=null;}
                    else {identity.CheckoutPending=true;unresolved=previousRequest;}
                    throw new CheckoutException("El identificador de operación ya corresponde a otro contenido.");
                }
                Finish(stored,request);return stored;
            }
            journal.Clear(request.OperationId);identity.CheckoutPending=false;unresolved=null;
            throw;
        }
        Finish(result,request);return result;
    });
    public Sale? Resolve() => identity.Synchronized(() =>
    {
        if(journal.Read() is not { } operation){identity.CheckoutPending=false;return null;}
        var sale=database.Run(store=>
        {
            var actor=Authorize(store);var found=store.FindSaleByOperation(operation);
            if(found is not null && (found.UserId!=actor || found.TerminalId!=terminal))
                throw new CheckoutException("Inicia sesión con el usuario que realizó el cobro pendiente.");
            return found;
        });
        if(sale is not null) Finish(sale,unresolved is { } request && Matches(sale,request)?request:null);
        else {journal.Clear(operation);identity.CheckoutPending=false;unresolved=null;}
        return sale;
    });
    public void Acknowledge(Guid operation) => identity.Synchronized(() =>
    {
        database.Run(store=>
        {
            var actor=Authorize(store);var sale=store.FindSaleByOperation(operation);
            if(sale is null || sale.UserId!=actor || sale.TerminalId!=terminal)throw new AccessDeniedException();
            return true;
        });
        journal.Clear(operation);identity.CheckoutPending=false;unresolved=null;return true;
    });
}
