namespace CasaMiga.Application;

// Physical acceptance is deliberately distinct from successful transport.
public enum DeviceOutcome { Submitted, Failed, Unknown }
public sealed record TicketData(Guid SaleId, string Folio, DateTimeOffset OccurredAt, string Text);
public interface ITicketRenderer { ReadOnlyMemory<byte> Render(TicketData ticket); }
public interface IReceiptPrinter
{
    Task<DeviceOutcome> PrintAsync(ReadOnlyMemory<byte> receipt, CancellationToken cancellationToken);
}
public interface ICashDrawer
{
    Task<DeviceOutcome> OpenAsync(Guid auditedOperationId, CancellationToken cancellationToken);
}
