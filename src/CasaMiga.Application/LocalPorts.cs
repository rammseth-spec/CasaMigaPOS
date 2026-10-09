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
    bool Active, int FailedAttempts, DateTimeOffset? LockedUntil, string Role = "", bool MustChangePassword = false);
public sealed record UserSummary(Guid Id, string Username, string DisplayName, string Role, bool Active, bool MustChangePassword, DateTimeOffset? LockedUntil);
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
public interface ILocalCheckoutUnitOfWork { T RunCheckout<T>(Func<ILocalStore, T> operation); }
public interface ILocalReadUnitOfWork { T Read<T>(Func<ILocalStore, T> operation); }
public interface ILocalStore
{
    ConsumptionStatus GetConsumptionStatus();
    ConsumptionPreflight CheckConsumption(DateTimeOffset at);
    void UpdateConsumption(ConsumptionStatus expected,ConsumptionStatus next);
    int UserCount();
    LocalUser? FindUser(string normalizedName);
    LocalUser? FindUser(Guid id);
    void AddAdministrator(Guid id, string username, string displayName, string hash, DateTimeOffset at);
    void UpdateLogin(Guid id, int failures, DateTimeOffset? lockedUntil, string? replacementHash);
    void AddUser(Guid id, string username, string displayName, string role, string hash, bool mustChangePassword, DateTimeOffset at);
    void SetPassword(Guid id, string hash, bool mustChangePassword);
    IReadOnlyList<UserSummary> ListUsers();
    void UpdateUser(Guid id, string displayName, string role);
    void SetUserActive(Guid id, bool active);
    bool HasPermission(Guid user, string permission);
    bool HasRole(Guid user, string role);
    TerminalIdentity? FindTerminal(Guid id);
    void EnsureTerminal(TerminalIdentity terminal, DateTimeOffset at);
    CashSessionView? FindOpenSession(Guid terminal);
    void AddCashSession(CashSession session);
    CashSession? FindSession(Guid id);
    CashCount? FindCount(Guid id);
    void AddCount(CashCount count);
    void CloseSession(CashSession session);
    CashClosureHistoryPage QueryCashClosures(Guid terminal, int page);
    CashClosureHistoryRow? FindCashClosure(Guid terminal, Guid id);
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
    void ValidateSaleInventoryRecovery(Sale sale);
    Sale? FindSaleByOperation(Guid operation);
    Money CashSalesTotal(Guid session);
    Money CashRefundsTotal(Guid session);
    void AddCashMovement(CashMovement movement);
    CashMovement? FindCashMovementByOperation(Guid operation);
    IReadOnlyList<CashMovement> ListCashMovements(Guid session);
    CashMovementTotals CashMovementTotals(Guid session);
    SalesReportData QuerySalesReport(DateTimeOffset fromUtc, DateTimeOffset untilUtc);
    IReadOnlyList<SaleLineFact> QuerySaleLines(DateTimeOffset fromUtc, DateTimeOffset untilUtc);
    IReadOnlyList<SaleFact> QuerySaleFacts(DateTimeOffset fromUtc, DateTimeOffset untilUtc);
    IReadOnlyList<RefundFact> QueryRefundFacts(DateTimeOffset fromUtc, DateTimeOffset untilUtc);
    // Charolas a consignación
    IReadOnlyList<ConsignmentStore> ListConsignmentStores(bool includeInactive);
    ConsignmentStore? FindConsignmentStore(Guid id);
    /// <summary>Costo de referencia de un insumo (migración 025). False si el insumo no existe.</summary>
    bool SetInventoryItemCost(Guid itemId, long costCents, long perAtomic, string unitLabel, DateTimeOffset at, Guid by);
    // Cambios de la versión web aplicados en esta caja (migración 024).
    long LastCloudCommandSequence();
    CloudCommandResult? FindCloudCommand(Guid id);
    void AddCloudCommand(CloudCommandResult result, string kind, string requestedBy, DateTimeOffset at);
    IReadOnlyList<CloudCommandResult> UnreportedCloudCommands();
    void MarkCloudCommandsReported(IReadOnlyCollection<Guid> ids, DateTimeOffset at);
    bool ConsignmentStoreNameTaken(string name, Guid? except);
    void SaveConsignmentStore(ConsignmentStore store, bool create, DateTimeOffset at);
    IReadOnlyList<ConsignmentTemplateLine> ConsignmentTemplate(Guid storeId);
    void ReplaceConsignmentTemplate(Guid storeId, IReadOnlyList<ConsignmentLine> lines);
    IReadOnlyList<ConsignmentPrice> ConsignmentPrices();
    Money? FindConsignmentPrice(Guid productId);
    void SetConsignmentPrice(Guid productId, Money? price, DateTimeOffset at);
    void AddConsignmentDelivery(ConsignmentDelivery delivery);
    ConsignmentDelivery? FindConsignmentDelivery(Guid id);
    ConsignmentDelivery? FindConsignmentDeliveryByOperation(Guid operation);
    ConsignmentDelivery? FindConsignmentDeliveryBySettlement(Guid operation);
    IReadOnlyList<ConsignmentDelivery> QueryConsignmentDeliveries(DateTimeOffset? fromUtc, DateTimeOffset? untilUtc, Guid? storeId, bool openOnly);
    DateTimeOffset? LastConsignmentDelivery(Guid storeId);
    void AddConsignmentSettlement(ConsignmentSettlement settlement);
    void AddConsignmentVoid(ConsignmentVoid cancellation);
    void AddConsignmentPayment(ConsignmentPayment payment);
    ConsignmentPayment? FindConsignmentPaymentByOperation(Guid operation);
    IReadOnlyList<ConsignmentPayment> QueryConsignmentPayments(DateTimeOffset? fromUtc, DateTimeOffset? untilUtc, Guid? storeId);
    Money ConsignmentBalance(Guid storeId);
    void AddRefund(SaleRefundRecord refund);
    IReadOnlyList<SaleRefundRecord> ListRefunds(Guid saleId);
    SaleRefundRecord? FindRefundByOperation(Guid operation);
    SalesHistoryPage QuerySales(SalesHistoryFilter filter);
    SalesHistoryDetail? FindSaleDetail(Guid saleId);
    ReceiptSnapshot? FindReceiptSnapshot(Guid saleId);
    void AddReceiptSnapshot(ReceiptSnapshot snapshot);
    ReceiptPrintRequest? FindOriginalReceiptPrintRequest(Guid saleId);
    ReceiptPrintRequest? FindReceiptPrintRequest(Guid requestId);
    ReceiptPrintEvent? FindLatestReceiptPrintEvent(Guid requestId);
    void AddReceiptPrintRequest(ReceiptPrintRequest request);
    void AddReceiptPrintEvent(ReceiptPrintEvent entry);
}
