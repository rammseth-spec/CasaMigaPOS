using System.Text.Json;
using CasaMiga.Domain;

namespace CasaMiga.Application;

public enum RefundKind { Cancellation, Return }
public sealed record RefundLine(int Position, int Quantity);
public sealed record SaleRefundItem(int Position, string ProductName, Money UnitPrice, int Quantity, Money LineTotal);
public sealed record SaleRefundRecord(Guid Id, Guid OperationId, Guid SaleId, RefundKind Kind, string Reason, Guid UserId, string UserName,
    Guid TerminalId, Guid CashSessionId, DateTimeOffset RefundedAt, Money Total, IReadOnlyList<SaleRefundItem> Items);
public sealed record RefundableLine(int Position, string ProductName, Money UnitPrice, int Sold, int Refunded)
{
    public int Remaining => Sold - Refunded;
}
public sealed record RefundOptions(SalesHistoryDetail Detail, IReadOnlyList<RefundableLine> Lines, IReadOnlyList<SaleRefundRecord> Refunds,
    bool CanCancel, bool CanReturn, string Blocker)
{
    public Money RefundedTotal => new(Refunds.Sum(r => r.Total.Cents));
}
public sealed class RefundException(string message) : Exception(message);

/// <summary>
/// Cancelaciones y devoluciones como operaciones compensatorias inmutables (docs/REFUNDS.md).
/// La venta original no cambia; el efectivo sale de la caja abierta de esta terminal y el inventario no se modifica.
/// </summary>
public sealed class RefundService(ILocalUnitOfWork database, UserSession identity, IClock clock, Guid terminalId)
{
    public const string Permission = "sales.refund";

    public bool CanRefund() => database.Run(store => Actor(store) is not null);

    public RefundOptions Options(Guid saleId) => identity.Synchronized(() => database.Run(store =>
    {
        _ = Actor(store) ?? throw new AccessDeniedException();
        return Build(store, saleId);
    }));

    public SaleRefundRecord Cancel(Guid saleId, string reason) => Execute(saleId, RefundKind.Cancellation, null, reason);

    public SaleRefundRecord Return(Guid saleId, IReadOnlyList<RefundLine> lines, string reason) => Execute(saleId, RefundKind.Return, lines, reason);

    private SaleRefundRecord Execute(Guid saleId, RefundKind kind, IReadOnlyList<RefundLine>? requested, string reason)
    {
        reason = reason.Trim();
        if (reason.Length is < 3 or > 200) throw new ArgumentException("Escribe el motivo (de 3 a 200 caracteres).");
        var operation = Guid.NewGuid();
        try
        {
            return identity.Synchronized(() => database.Run(store =>
            {
                var actor = Actor(store) ?? throw new AccessDeniedException();
                var options = Build(store, saleId);
                if (options.Blocker.Length > 0) throw new RefundException(options.Blocker);
                if (kind == RefundKind.Cancellation && !options.CanCancel)
                    throw new RefundException("Sólo se puede cancelar una venta completa, sin devoluciones previas y en el mismo turno de caja. Usa «Devolver productos».");

                var lines = kind == RefundKind.Cancellation
                    ? options.Lines.Select(l => new RefundLine(l.Position, l.Sold)).ToList()
                    : Normalize(requested ?? [], options.Lines);
                var items = lines.Select(l =>
                {
                    var line = options.Lines.Single(x => x.Position == l.Position);
                    return new SaleRefundItem(l.Position, line.ProductName, line.UnitPrice, l.Quantity, line.UnitPrice.Multiply(l.Quantity));
                }).ToList();
                var total = new Money(checked(items.Sum(i => i.LineTotal.Cents)));
                if (total.Cents <= 0) throw new RefundException("La devolución debe tener un importe mayor que cero.");

                var session = store.FindOpenSession(terminalId)!.Session;
                var available = session.OpeningFund + store.CashSalesTotal(session.Id) - store.CashRefundsTotal(session.Id);
                if (options.Detail.Sale.Payment.Kind == PaymentMethod.Cash && total.Cents > available.Cents)
                    throw new RefundException($"No hay suficiente efectivo esperado en caja para devolver {Format(total)}.");

                var refund = new SaleRefundRecord(Guid.NewGuid(), operation, saleId, kind, reason, actor.Id, actor.DisplayName,
                    terminalId, session.Id, clock.UtcNow, total, items);
                store.AddRefund(refund);
                var sale = options.Detail.Sale;
                store.AddAudit(new(Guid.NewGuid(), actor.Id, terminalId, kind == RefundKind.Cancellation ? "sale.cancelled" : "sale.returned", refund.Id, refund.RefundedAt,
                    JsonSerializer.Serialize(new { saleId, folio = sale.Folio, cashSessionId = session.Id, totalCents = total.Cents,
                        items = items.Select(i => new { i.Position, i.Quantity, lineTotalCents = i.LineTotal.Cents }) })));
                return refund;
            }));
        }
        catch (Exception error) when (error is not (RefundException or AccessDeniedException or ArgumentException))
        {
            // Resultado incierto (p. ej. fallo al confirmar): si la operación quedó guardada, se informa como hecha.
            var stored = database.Run(store => store.FindRefundByOperation(operation));
            if (stored is not null) return stored;
            throw;
        }
    }

    private static List<RefundLine> Normalize(IReadOnlyList<RefundLine> requested, IReadOnlyList<RefundableLine> lines)
    {
        var result = new List<RefundLine>();
        foreach (var group in requested.Where(l => l.Quantity != 0).GroupBy(l => l.Position))
        {
            var line = lines.FirstOrDefault(l => l.Position == group.Key) ?? throw new ArgumentException("La partida no pertenece a la venta.");
            var quantity = group.Sum(l => l.Quantity);
            if (quantity < 0) throw new ArgumentException("La cantidad a devolver no puede ser negativa.");
            if (quantity > line.Remaining) throw new ArgumentException($"Sólo quedan {line.Remaining} pieza(s) de {line.ProductName} por devolver.");
            result.Add(new(group.Key, quantity));
        }
        if (result.Count == 0) throw new ArgumentException("Indica al menos una pieza a devolver.");
        return result.OrderBy(l => l.Position).ToList();
    }

    private RefundOptions Build(ILocalStore store, Guid saleId)
    {
        var detail = store.FindSaleDetail(saleId) ?? throw new RefundException("No se encontró la venta.");
        var refunds = store.ListRefunds(saleId);
        var refunded = refunds.SelectMany(r => r.Items).GroupBy(i => i.Position).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var lines = detail.Sale.Items.Select((item, index) => new RefundableLine(index + 1, item.ProductName, item.UnitPrice, item.Quantity,
            refunded.GetValueOrDefault(index + 1))).ToList();

        var open = store.FindOpenSession(terminalId);
        string blocker = "";
        if (open is null) blocker = "Abre la caja de esta terminal para devolver efectivo.";
        else if (store.FindCount(open.Session.Id) is not null) blocker = "La caja ya tiene conteo confirmado; ciérrala y abre una nueva para devolver efectivo.";
        else if (lines.All(l => l.Remaining == 0)) blocker = "Esta venta ya fue cancelada o devuelta por completo.";

        var canCancel = blocker.Length == 0 && refunds.Count == 0 && open!.Session.Id == detail.Sale.CashSessionId;
        return new(detail, lines, refunds, canCancel, blocker.Length == 0, blocker);
    }

    private LocalUser? Actor(ILocalStore store) =>
        identity.Current is { } current && store.FindUser(current.Id) is { Active: true } user && store.HasPermission(user.Id, Permission) ? user : null;

    private static string Format(Money value) => (value.Cents / 100m).ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("es-MX"));
}
