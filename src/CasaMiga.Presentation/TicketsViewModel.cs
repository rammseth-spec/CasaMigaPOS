using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;

namespace CasaMiga.Presentation;

public sealed record TicketActivityRow(ReceiptActivity Activity, TimeZoneInfo TimeZone)
{
    public Guid SaleId => Activity.SaleId;
    public string Date => TimeZoneInfo.ConvertTime(Activity.RequestedAt, TimeZone).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    public string Folio => Activity.Folio;
    public string Kind => Activity.Kind switch
    {
        ReceiptCopyKind.Original => "Original",
        ReceiptCopyKind.Retry => "Reintento",
        ReceiptCopyKind.Reprint => "Reimpresión",
        _ => Activity.Kind.ToString()
    };
    public string State => Activity.State switch
    {
        ReceiptPrintState.Submitted => "Enviado a la impresora",
        ReceiptPrintState.Completed => "Impreso",
        ReceiptPrintState.Failed => Activity.ErrorCode is "PRINTING_DISABLED" or "CONFIG_MISSING" ? "Impresora no configurada" : "No se envió",
        ReceiptPrintState.Unknown => "Resultado incierto",
        _ => "Pendiente"
    };
    public string Job => Activity.SpoolerJobId?.ToString(CultureInfo.InvariantCulture) ?? "—";
    public string User => Activity.RequestedBy;
    public string Total => PosViewModel.Format(new CasaMiga.Domain.Money(Activity.TotalCents));
}

public sealed record PrintQueueRow(PrintQueueJob Job, TimeZoneInfo TimeZone)
{
    public int JobId => Job.JobId;
    public string Document => Job.Document;
    public string Status => Job.Status;
    public string Submitted => Job.SubmittedAt is { } at ? TimeZoneInfo.ConvertTime(at, TimeZone).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) : "—";
    public string Pages => Job.TotalPages > 0 ? $"{Job.PagesPrinted}/{Job.TotalPages}" : "—";
}

/// <summary>Apartado Tickets: tickets enviados con su estado, reimpresión y la cola de Windows de la impresora de tickets.</summary>
public sealed class TicketsViewModel(ReceiptService receipts, TimeZoneInfo timeZone, Action<string, string?> log) : INotifyPropertyChanged
{
    private TicketActivityRow? selectedTicket; private PrintQueueRow? selectedJob;
    public bool Busy { get; private set; }
    public bool Ready => !Busy;
    public string Message { get; private set; } = "";
    public bool MessageIsSuccess { get; private set; }
    public IReadOnlyList<TicketActivityRow> Tickets { get; private set; } = [];
    public IReadOnlyList<PrintQueueRow> QueueJobs { get; private set; } = [];
    public string QueueTitle { get; private set; } = "Cola de impresión";
    public string QueueStatus { get; private set; } = "";
    public bool QueueAvailable { get; private set; }
    public TicketActivityRow? SelectedTicket { get => selectedTicket; set { selectedTicket = value; Notify(); } }
    public PrintQueueRow? SelectedJob { get => selectedJob; set { selectedJob = value; Notify(); } }
    public bool CanReprint => Ready && SelectedTicket is not null;
    public string ReprintButtonText => SelectedTicket is { } t ? $"Reimprimir {t.Folio}" : "Reimprimir ticket";
    public string ReprintTarget => SelectedTicket is { } t
        ? $"Se reimprimirá: venta {t.Folio} · {t.Date} · {t.Total} · {t.Kind}"
        : "Selecciona en la tabla el ticket que quieres reimprimir.";
    public bool CanCancelJob => Ready && QueueAvailable && SelectedJob is not null;
    public bool CanClearQueue => Ready && QueueAvailable && QueueJobs.Count > 0;
    public string TicketsEmptyMessage => Tickets.Count == 0 ? "Todavía no se ha enviado ningún ticket." : "";
    public string QueueEmptyMessage => QueueAvailable && QueueJobs.Count == 0 ? "No hay trabajos pendientes en la cola." : "";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public bool CanView() => receipts.CanReprint();

    public Task LoadAsync() => Run(async () => { await LoadTicketsAsync(); await LoadQueueAsync(); });
    public Task RefreshAsync() => LoadAsync();

    public Task ReprintAsync() => Run(async () =>
    {
        if (SelectedTicket is not { } ticket) throw new ArgumentException("Selecciona un ticket.");
        var result = await Task.Run(() => receipts.Reprint(ticket.SaleId));
        log("receipt.reprint.from_tickets", null);
        await LoadTicketsAsync(); await LoadQueueAsync();
        MessageIsSuccess = result.State == ReceiptPrintState.Submitted;
        Message = $"Venta {ticket.Folio}: {result.Message}";
    });

    public Task CancelJobAsync() => Run(async () =>
    {
        if (SelectedJob is not { } job) throw new ArgumentException("Selecciona un trabajo de la cola.");
        await Task.Run(() => receipts.CancelQueueJob(job.JobId));
        await LoadQueueAsync();
        MessageIsSuccess = true; Message = $"Trabajo {job.JobId} cancelado. La venta no cambia.";
    });

    public Task ClearQueueAsync() => Run(async () =>
    {
        var cancelled = await Task.Run(receipts.ClearQueue);
        await LoadQueueAsync();
        MessageIsSuccess = true;
        Message = cancelled == 1 ? "Se canceló 1 trabajo pendiente. Las ventas no cambian." : $"Se cancelaron {cancelled} trabajos pendientes. Las ventas no cambian.";
    });

    public void Forget() { Tickets = []; QueueJobs = []; SelectedTicket = null; SelectedJob = null; Message = ""; QueueStatus = ""; Notify(); }

    private async Task LoadTicketsAsync()
    {
        var keep = SelectedTicket?.Activity.RequestId;
        Tickets = (await Task.Run(() => receipts.RecentActivity(200))).Select(a => new TicketActivityRow(a, timeZone)).ToList();
        selectedTicket = Tickets.FirstOrDefault(t => t.Activity.RequestId == keep);
    }

    private async Task LoadQueueAsync()
    {
        var snapshot = await Task.Run(receipts.PrintQueue);
        QueueAvailable = snapshot.Available;
        QueueTitle = snapshot.QueueName is { } name ? $"Cola de impresión · {name}" : "Cola de impresión";
        QueueJobs = snapshot.Jobs.Select(j => new PrintQueueRow(j, timeZone)).ToList();
        selectedJob = null;
        QueueStatus = snapshot.ErrorCode switch
        {
            null => QueueJobs.Count == 1 ? "1 trabajo pendiente." : $"{QueueJobs.Count} trabajos pendientes.",
            "CONFIG_MISSING" or "PRINTING_DISABLED" => "La impresora de tickets no está configurada (printing.json).",
            "QUEUE_UNAVAILABLE" => "La cola de impresión no está disponible en este equipo.",
            var code when code.StartsWith("OPEN_PRINTER", StringComparison.Ordinal) => "No se encontró la impresora configurada en Windows.",
            var code => $"No se pudo leer la cola de impresión ({code})."
        };
    }

    private async Task Run(Func<Task> action)
    {
        if (Busy) return;
        Busy = true; Message = ""; MessageIsSuccess = false; Notify();
        try { await action(); }
        catch (AccessDeniedException error) { Message = error.Message; log("authorization.denied", null); }
        catch (PrintTransportException error) { Message = $"No se pudo cambiar la cola de impresión ({error.ErrorCode})."; log("receipt.queue.failed", error.ErrorCode); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { Message = error.Message; }
        finally { Busy = false; Notify(); }
    }
}
