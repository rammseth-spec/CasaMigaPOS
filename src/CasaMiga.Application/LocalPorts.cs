using CasaMiga.Domain;

namespace CasaMiga.Application;

public interface IClock { DateTimeOffset UtcNow { get; } }
public enum PasswordCheck { Failed, Valid, Rehash }
public interface IPasswords
{
    string Hash(string password);
    PasswordCheck Verify(string? hash, string password);
}
public sealed record LocalUser(Guid Id, string Username, string DisplayName, string PasswordHash,
    bool Active, int FailedAttempts, DateTimeOffset? LockedUntil);
public sealed record UserIdentity(Guid Id, string DisplayName);
public sealed record TerminalIdentity(Guid Id, string Name);
public sealed record CashSessionView(CashSession Session, string OpenedByName);
public sealed record AuditEntry(Guid Id, Guid Actor, Guid? Terminal, string Action, Guid Entity,
    DateTimeOffset At, string DetailsJson);

public sealed class UserSession(ICheckoutJournal? checkoutJournal = null)
{
    private readonly object gate = new();
    public Cart? Cart { get; internal set; }
    public bool CheckoutPending { get; internal set; }
    internal CheckoutRequest? UnresolvedCheckout { get; set; }
    internal void RequireCheckoutResolved()
    {
        if (CheckoutPending || checkoutJournal?.Read() is not null) throw new CheckoutException("Verifica el cobro pendiente antes de continuar.");
    }
    internal T Synchronized<T>(Func<T> action) { lock(gate) return action(); }
    public UserIdentity? Current { get; private set; }
    private void RequireLocalCartResolved()
    {
        if(CheckoutPending)throw new CheckoutException("Verifica el cobro pendiente antes de continuar.");
        if(Cart is { IsEmpty:false })throw new CartDiscardRequiredException();
    }
    internal void SignIn(UserIdentity identity) => Synchronized(() => { RequireLocalCartResolved(); Cart=null; Current=identity; return true; });
    public void RequireEmptyCart()
    {
        RequireCheckoutResolved();
        if (Cart is { IsEmpty: false }) throw new CartDiscardRequiredException();
    }
    public void SignOut() => Synchronized(() => { RequireLocalCartResolved(); Cart=null; Current=null; return true; });
}
public sealed class AccessDeniedException() : Exception("No tienes permiso para esta operación.");
public sealed class CashAlreadyOpenException() : Exception("Esta terminal ya tiene una caja abierta.");

public interface ILocalUnitOfWork { T Run<T>(Func<ILocalStore, T> operation); }
public interface ILocalReadUnitOfWork { T Read<T>(Func<ILocalStore, T> operation); }
public interface ILocalStore
{
    int UserCount();
    LocalUser? FindUser(string normalizedName);
    LocalUser? FindUser(Guid id);
    void AddAdministrator(Guid id, string username, string displayName, string hash, DateTimeOffset at);
    void UpdateLogin(Guid id, int failures, DateTimeOffset? lockedUntil, string? replacementHash);
    bool HasPermission(Guid user, string permission);
    TerminalIdentity? FindTerminal(Guid id);
    void EnsureTerminal(TerminalIdentity terminal, DateTimeOffset at);
    CashSessionView? FindOpenSession(Guid terminal);
    void AddCashSession(CashSession session);
    CashSession? FindSession(Guid id);
    CashCount? FindCount(Guid id);
    void AddCount(CashCount count);
    void CloseSession(CashSession session);
    Category? FindCategory(Guid id);
    IReadOnlyList<Category> ListCategories(bool includeInactive);
    IReadOnlyList<Category> ListPosCategories();
    bool CategoryHasActiveProducts(Guid id);
    void SaveCategory(Category category,bool create);
    Product? FindProduct(Guid id);
    ProductPage QueryProducts(CatalogQuery query);
    void SaveProduct(Product product,bool create);
    void AddAudit(AuditEntry entry);
    long NextSaleSequence(Guid terminal);
    void AddSale(Sale sale);
    Sale? FindSaleByOperation(Guid operation);
    Money CashSalesTotal(Guid session);
    SalesHistoryPage QuerySales(SalesHistoryFilter filter);
    SalesHistoryDetail? FindSaleDetail(Guid saleId);
}
