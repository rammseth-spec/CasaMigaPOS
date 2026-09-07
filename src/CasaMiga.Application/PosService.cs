using CasaMiga.Domain;
namespace CasaMiga.Application;

public sealed class PosException(string message) : Exception(message);
public sealed class CartDiscardRequiredException() : Exception("Hay un carrito pendiente. Descártalo antes de salir del usuario o cerrar caja.");
public sealed record PosPage(IReadOnlyList<Category> Categories, ProductPage Products, Cart Cart);

public sealed class PosService(ILocalUnitOfWork database, UserSession identity, Guid terminal)
{
    private Cart Context(ILocalStore store)
    {
        if (identity.Current is not { } actor || store.FindUser(actor.Id) is not { Active: true }
            || !store.HasPermission(actor.Id, "catalog.view") || !store.HasPermission(actor.Id, "pos.use"))
            throw new AccessDeniedException();
        if (store.FindTerminal(terminal) is null) throw new PosException("Terminal no configurada.");
        var cash = store.FindOpenSession(terminal)?.Session ?? throw new PosException("Abre una caja antes de usar el POS.");
        if (store.FindCount(cash.Id) is not null) throw new PosException("El conteo de caja ya está confirmado. Termina el cierre antes de usar el POS.");
        if (identity.Cart is { } cart)
        {
            if (cart.UserId == actor.Id && cart.TerminalId == terminal && cart.CashSessionId == cash.Id) return cart;
            if (!cart.IsEmpty) throw new CartDiscardRequiredException();
        }
        return new(actor.Id, terminal, cash.Id);
    }
    public PosPage Load(string search = "", Guid? category = null, int page = 0) => identity.Synchronized(() =>
    {
        if (search.Length > 160 || page < 0 || page > 100000) throw new ArgumentException("Filtro inválido.");
        var result = database.Run(s =>
        {
            var cart=Context(s);
            return new PosPage(s.ListPosCategories(),s.QueryProducts(new(search, category, false, page)),cart);
        });
        identity.Cart = result.Cart;
        return result;
    });
    private Cart Change(Func<ILocalStore, Cart, Cart> change) => identity.Synchronized(() =>
    {
        var next = database.Run(s => change(s, Context(s)));
        identity.Cart = next; // Publish only after validation and checked arithmetic succeed.
        return next;
    });
    public Cart Add(Guid id) => Change((s, cart) =>
    {
        var product = s.FindProduct(id);
        if (product is not { IsActive: true } || s.FindCategory(product.CategoryId) is not { IsActive: true })
            throw new PosException("El producto ya no está disponible. Actualiza el catálogo.");
        return cart.Add(product.Id, product.Name, product.SalePrice);
    });
    public Cart Increment(Guid id) => Change((_, cart) => cart.Increment(id));
    public Cart Decrement(Guid id) => Change((_, cart) => cart.Decrement(id));
    public Cart Remove(Guid id) => Change((_, cart) => cart.Remove(id));
    // Discard is always possible, including after permission revocation or an external cash close.
    public void Discard(bool confirmed) => identity.Synchronized(() =>
    {
        if (identity.Cart is { IsEmpty: false } && !confirmed) throw new CartDiscardRequiredException();
        identity.Cart = null;
        return true;
    });
}
