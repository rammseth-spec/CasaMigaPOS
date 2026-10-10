using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public enum ConsignmentTab { Route, Stores, Prices, Report, Deliveries, Payments }
public enum ConsignmentAction { None, Deliver, Collect, Pay }
public sealed record MethodOption(PaymentMethod Method, string Label);

/// <summary>Una línea editable de charola (entregar o charola habitual): piezas de un pan, con su foto para la tarjeta.</summary>
public sealed class TrayLineView(Guid productId, string name, string category, string? sku, Money unitPrice, Money retailPrice, Action changed) : INotifyPropertyChanged
{
    private int quantity;
    private ProductPhotoVisual photo = new(0, null, null, "Sin foto", null, null);
    public Guid ProductId => productId;
    public string Name => name;
    public string CategoryName => category;
    public string? Sku => sku;
    public bool HasPhoto => photo.HasPhoto;
    public bool IsFallback => photo.IsFallback;
    public System.Windows.Media.Imaging.BitmapSource? Photo => photo.Image;
    public void SetPhoto(ProductPhotoVisual value) { photo = value; Notify(); }
    public string LineAmount => ConsignmentViewModel.Format(unitPrice.Multiply(quantity));
    public Money UnitPrice => unitPrice;
    public Money RetailPrice => retailPrice;
    public string Price => ConsignmentViewModel.Format(unitPrice);
    public int Quantity
    {
        get => quantity;
        set { var next = Math.Clamp(value, 0, ConsignmentService.MaxPiecesPerLine); if (next == quantity) return; quantity = next; Notify(); changed(); }
    }
    public string QuantityText
    {
        get => quantity.ToString(CultureInfo.InvariantCulture);
        set => Quantity = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var q) ? q : 0;
    }
    public bool HasPieces => quantity > 0;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

/// <summary>Una partida al recoger: cuántas piezas regresaron; las demás se vendieron. Con foto para la tarjeta.</summary>
public sealed class CollectLineView(ConsignmentDeliveryItem item, Action changed) : INotifyPropertyChanged
{
    private int returned;
    private ProductPhotoVisual photo = new(0, null, null, "Sin foto", null, null);
    public Guid ProductId => item.ProductId;
    public bool HasPhoto => photo.HasPhoto;
    public bool IsFallback => photo.IsFallback;
    public System.Windows.Media.Imaging.BitmapSource? Photo => photo.Image;
    public void SetPhoto(ProductPhotoVisual value) { photo = value; Notify(); }
    public bool HasReturns => returned > 0;
    public string DeliveredText => $"Dejadas {item.Quantity}";
    public string SoldText => $"Vendidas {Sold} · {Amount}";
    public int Position => item.Position;
    public string Name => item.ProductName;
    public int Delivered => item.Quantity;
    public string Price => ConsignmentViewModel.Format(item.UnitPrice);
    public int Returned
    {
        get => returned;
        set { var next = Math.Clamp(value, 0, item.Quantity); if (next == returned) return; returned = next; Notify(); changed(); }
    }
    public string ReturnedLabel => returned == 1 ? "Regresó 1" : $"Regresaron {returned}";
    public string ReturnedText
    {
        get => returned.ToString(CultureInfo.InvariantCulture);
        set => Returned = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var q) ? q : 0;
    }
    public int Sold => item.Quantity - returned;
    public Money SoldAmount => item.UnitPrice.Multiply(Sold);
    public string Amount => ConsignmentViewModel.Format(SoldAmount);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

public sealed class PriceLineView(ConsignmentPrice price) : INotifyPropertyChanged
{
    private string text = price.Price is { } p ? (p.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture) : "";
    public ConsignmentPrice Source => price;
    public string Name => price.ProductName;
    public string Category => price.CategoryName;
    public string Retail => ConsignmentViewModel.Format(price.RetailPrice);
    public string PriceText { get => text; set { text = value; Notify(); } }
    public bool Changed => text.Trim() != (price.Price is { } p ? (p.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture) : "");
    public string Difference
    {
        get
        {
            if (!ConsignmentViewModel.TryParseMoney(text, out var value) || value is null) return "Sin precio";
            if (price.RetailPrice.Cents == 0) return "—";
            var off = (price.RetailPrice.Cents - value.Value.Cents) * 100m / price.RetailPrice.Cents;
            return off == 0 ? "Igual que mostrador" : off > 0 ? $"{off.ToString("0", CultureInfo.InvariantCulture)} % menos" : $"{(-off).ToString("0", CultureInfo.InvariantCulture)} % más";
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

public sealed record StoreStatusView(ConsignmentStoreStatus Status)
{
    public Guid Id => Status.Store.Id;
    public string Name => Status.Store.Name;
    public string Contact => string.Join(" · ", new[] { Status.Store.Contact, Status.Store.Phone }.Where(s => s.Length > 0));
    public bool HasContact => Contact.Length > 0;
    public ConsignmentDelivery? Tray => Status.AtStore.FirstOrDefault();
    public bool HasTray => Tray is not null;
    public string TrayText => Tray is not { } t ? "Sin charola en la tienda"
        : $"Charola del {ConsignmentViewModel.Day(t.DeliveredAt)} · {t.Pieces} pzs · {ConsignmentViewModel.Format(t.Value)}" + (Status.AtStore.Count > 1 ? $" (+{Status.AtStore.Count - 1} más)" : "");
    public bool Owes => Status.Balance.Cents > 0;
    public string Balance => Owes ? $"Debe {ConsignmentViewModel.Format(Status.Balance)}" : "Al corriente";
}
public sealed record MovementView(ConsignmentMovement Row)
{
    public string Date => ConsignmentViewModel.Day(Row.At);
    public string Concept => Row.Concept;
    public string Charge => Row.Charge.Cents == 0 ? "" : ConsignmentViewModel.Format(Row.Charge);
    public string Payment => Row.Payment.Cents == 0 ? "" : ConsignmentViewModel.Format(Row.Payment);
}
public sealed record StoreReportView(ConsignmentStoreRow Row)
{
    public string Name => Row.StoreName;
    public int Trays => Row.Trays;
    public int Delivered => Row.Delivered;
    public int Sold => Row.Sold;
    public int Returned => Row.Returned;
    public string SellThrough => Row.SellThrough is { } p ? p.ToString("0.0", CultureInfo.InvariantCulture) + " %" : "—";
    public string Charged => ConsignmentViewModel.Format(Row.Charged);
    public string Paid => Row.Paid.Cents == 0 ? "—" : ConsignmentViewModel.Format(Row.Paid);
    public string Balance => Row.Balance.Cents == 0 ? "—" : ConsignmentViewModel.Format(Row.Balance);
}
public sealed record ProductReportRowView(ConsignmentProductRow Row)
{
    public string Name => Row.ProductName;
    public int Delivered => Row.Delivered;
    public int Sold => Row.Sold;
    public int Returned => Row.Returned;
    public string SellThrough => Row.SellThrough is { } p ? p.ToString("0.0", CultureInfo.InvariantCulture) + " %" : "—";
    public string Charged => ConsignmentViewModel.Format(Row.Charged);
}

public sealed record TrayCategory(string? Name, string Label, bool Selected);

public sealed class ConsignmentViewModel(ConsignmentService service, Action<string, string?> log, IProductPhotoUiGateway? photos = null) : INotifyPropertyChanged
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");
    internal static string Format(Money value) => TrayText.Format(value);
    internal static string Day(DateTimeOffset at) => TrayText.Day(at);
    /// <summary>Acepta «500», «500.50», «$1,500.00»; vacío = sin importe.</summary>
    internal static bool TryParseMoney(string text, out Money? value)
    {
        value = null;
        var clean = text.Replace("$", "").Replace(",", "").Trim();
        if (clean.Length == 0) return true;
        if (!decimal.TryParse(clean, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var pesos)) return false;
        try { value = Money.FromPesos(pesos); return true; } catch (ArgumentException) { return false; } catch (OverflowException) { return false; }
    }
    private static Money ParseMoney(string text, string field) =>
        TryParseMoney(text, out var value) ? value ?? new Money(0) : throw new ArgumentException($"Revisa el importe de «{field}» (ejemplo: 150.00).");

    public bool Busy { get; private set; }
    public bool Ready => !Busy;
    public string Message { get; private set; } = "";
    public bool MessageIsSuccess { get; private set; }
    public bool CanManage { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public ConsignmentTab Tab { get; private set; } = ConsignmentTab.Route;
    public bool IsRoute => Tab == ConsignmentTab.Route;
    // Al entregar, la charola ocupa toda la pantalla, como el Punto de venta.
    public bool IsRouteBoard => IsRoute && !IsDelivering && !IsCollecting;
    public bool IsDeliverWorkspace => IsRoute && IsDelivering;
    public bool IsCollectWorkspace => IsRoute && IsCollecting;
    public bool ShowTabs => !IsDeliverWorkspace && !IsCollectWorkspace;
    public bool IsStores => Tab == ConsignmentTab.Stores;
    public bool IsPrices => Tab == ConsignmentTab.Prices;
    public bool IsReport => Tab == ConsignmentTab.Report;
    public bool IsDeliveries => Tab == ConsignmentTab.Deliveries;
    public bool IsPayments => Tab == ConsignmentTab.Payments;
    /// <summary>Reporte, Entregas y Cobros comparten el periodo.</summary>
    public bool IsPeriod => IsReport || IsDeliveries || IsPayments;
    public IReadOnlyList<MethodOption> Methods { get; } = [new(PaymentMethod.Cash, "Efectivo"), new(PaymentMethod.Card, "Tarjeta"), new(PaymentMethod.Transfer, "Transferencia")];

    // ---------- Ruta ----------
    public IReadOnlyList<StoreStatusView> Route { get; private set; } = [];
    public bool NoStores => Route.Count == 0;
    public string RouteSummary => Route.Count == 0 ? "" :
        $"{Route.Count(r => r.HasTray)} de {Route.Count} tiendas con charola · por cobrar {Format(new(Route.Sum(r => Math.Max(0, r.Status.Balance.Cents))))}";
    public StoreStatusView? Selected { get; private set; }
    public ConsignmentAction Action { get; private set; }
    public bool HasAction => Action != ConsignmentAction.None;
    public bool NoAction => Action == ConsignmentAction.None;
    public bool IsDelivering => Action == ConsignmentAction.Deliver;
    public bool IsCollecting => Action == ConsignmentAction.Collect;
    public bool IsPaying => Action == ConsignmentAction.Pay;
    public string ActionTitle => Action switch
    {
        ConsignmentAction.Deliver => $"Entregar charola a {Selected?.Name}",
        ConsignmentAction.Collect => $"Recoger charola · {Selected?.Name}",
        ConsignmentAction.Pay => $"Cobrar a {Selected?.Name}",
        _ => "",
    };

    public IReadOnlyList<TrayLineView> DeliverLines { get; private set; } = [];
    /// <summary>Tarjetas visibles con el filtro actual; misma instancia mientras no cambie el filtro (no se regeneran al sumar piezas).</summary>
    public IReadOnlyList<TrayLineView> DeliverTiles { get; private set; } = [];
    /// <summary>Panes con piezas, como el carrito del Punto de venta.</summary>
    public IReadOnlyList<TrayLineView> TrayLines { get; private set; } = [];
    public bool TrayIsEmpty => TrayLines.Count == 0;
    public IReadOnlyList<TrayCategory> DeliverCategories { get; private set; } = [];
    public bool NoTiles => IsDelivering && DeliverTiles.Count == 0;
    private string? deliverCategory;
    private string deliverSearch = "";
    public string DeliverSearch { get => deliverSearch; set { deliverSearch = value; ApplyDeliverFilter(); } }
    public string DeliverNote { get; set; } = "";
    public int DeliverPieces => DeliverLines.Sum(l => l.Quantity);
    public string DeliverPiecesText => DeliverPieces == 1 ? "1 pieza" : $"{DeliverPieces} piezas";
    public string DeliverValue => Format(new(DeliverLines.Sum(l => l.UnitPrice.Cents * l.Quantity)));
    public string DeliverRetail => $"En mostrador valdrían {Format(new(DeliverLines.Sum(l => l.RetailPrice.Cents * l.Quantity)))}";
    public string DeliverSummary => DeliverPieces == 0 ? "Agrega piezas a la charola." : $"{DeliverPiecesText} · {DeliverValue} a precio de charola";
    public bool CanDeliver => DeliverPieces > 0;
    private CancellationTokenSource photoLoad = new();
    private IReadOnlyList<ConsignmentTemplateLine> template = [];
    public bool HasTemplate => template.Count > 0;

    public ConsignmentDelivery? Collecting { get; private set; }
    public IReadOnlyList<CollectLineView> CollectLines { get; private set; } = [];
    public int CollectSold => CollectLines.Sum(l => l.Sold);
    public int CollectReturned => CollectLines.Sum(l => l.Returned);
    public Money CollectAmount => new(CollectLines.Sum(l => l.SoldAmount.Cents));
    public string CollectSummary => $"Vendidas {CollectSold} · Regresan {CollectReturned} para Piedra · Importe {Format(CollectAmount)}";
    public string CollectSoldText => CollectSold == 1 ? "1 pieza" : $"{CollectSold} piezas";
    public string CollectReturnedText => CollectReturned == 1 ? "1 pieza" : $"{CollectReturned} piezas";
    public string CollectAmountText => Format(CollectAmount);
    public bool HasPreviousBalance => Selected is { Status.Balance.Cents: > 0 };
    public string PreviousBalanceText => Format(Selected?.Status.Balance ?? new(0));
    public string TotalOwedText => Format((Selected?.Status.Balance ?? new(0)) + CollectAmount);
    public string OwedSummary => Selected is not { } s ? "" : s.Status.Balance.Cents == 0
        ? $"Total a pagar: {Format(CollectAmount)}"
        : $"Saldo anterior {Format(s.Status.Balance)} + esta charola {Format(CollectAmount)} = {Format(s.Status.Balance + CollectAmount)}";
    private bool paidEdited;
    private string paidText = "";
    public string PaidText { get => paidText; set { paidText = value; paidEdited = true; Notify(); } }
    public MethodOption? SelectedMethod { get; set; }
    public bool NeedsReference => SelectedMethod is { Method: not PaymentMethod.Cash };
    public string Reference { get; set; } = "";
    public string CollectNote { get; set; } = "";
    public string VoidReason { get; set; } = "";
    public string CollectHint => Collecting is { } tray ? $"Dejada el {Day(tray.DeliveredAt).ToLower(Spanish)}. Toca la foto por cada pieza que regresó." : "";
    /// <summary>El panel de la derecha cobra o, si se pidió, anula la charola (con su motivo).</summary>
    public bool IsVoidOpen { get; private set; }
    public bool IsChargeOpen => !IsVoidOpen;
    public string CollectPanelTitle => IsVoidOpen ? "Anular charola" : "Cobro";
    public void OpenVoid() { IsVoidOpen = true; VoidReason = ""; Notify(); }
    public void CloseVoid() { IsVoidOpen = false; Notify(); }
    public string PayAmountText { get; set; } = "";
    public string PayNote { get; set; } = "";
    public void MethodChanged() => Notify();

    // ---------- Tiendas ----------
    public IReadOnlyList<ConsignmentStore> Stores { get; private set; } = [];
    public ConsignmentStore? SelectedStore { get; private set; }
    public bool IsExistingStore => SelectedStore is not null;
    public string StoreFormTitle => SelectedStore is null ? "Nueva tienda" : $"Editar {SelectedStore.Name}";
    public string StoreName { get; set; } = "";
    public string StoreContact { get; set; } = "";
    public string StorePhone { get; set; } = "";
    public string StoreAddress { get; set; } = "";
    public string ToggleStoreText => SelectedStore is { Active: false } ? "Reactivar tienda" : "Desactivar tienda";
    public IReadOnlyList<TrayLineView> TemplateLines { get; private set; } = [];
    public string TemplateSummary => $"{TemplateLines.Sum(l => l.Quantity)} piezas en la charola habitual";
    public ConsignmentAccount? Account { get; private set; }
    public IReadOnlyList<MovementView> Movements { get; private set; } = [];
    public bool HasMovements => Movements.Count > 0;
    public string AccountBalance => Account is not { } a ? "" : a.Balance.Cents == 0 ? "Al corriente" : $"Debe {Format(a.Balance)}";

    // ---------- Precios ----------
    public IReadOnlyList<PriceLineView> Prices { get; private set; } = [];

    // ---------- Reporte ----------
    public DateTime? FromSelected { get; set; } = DateTime.Today.AddDays(-6);
    public DateTime? ThroughSelected { get; set; } = DateTime.Today;
    public ConsignmentReport? Report { get; private set; }
    public bool HasReport => Report is not null;
    public bool HasReportRows => Report is { Stores.Count: > 0 };
    public bool NoReportRows => Report is { Stores.Count: 0 };
    public IReadOnlyList<StoreReportView> StoreRows { get; private set; } = [];
    public IReadOnlyList<ProductReportRowView> ProductRows { get; private set; } = [];
    public IReadOnlyList<BarView> SellThroughBars { get; private set; } = [];
    public string ReportTrays => (Report?.Trays ?? 0).ToString("N0", Spanish);
    public string ReportDelivered => (Report?.Delivered ?? 0).ToString("N0", Spanish);
    public string ReportSold => Report is not { } r ? "" : $"{r.Sold.ToString("N0", Spanish)}" + (r.SellThrough is { } p ? $" · {p.ToString("0.0", CultureInfo.InvariantCulture)} %" : "");
    public string ReportReturned => (Report?.Returned ?? 0).ToString("N0", Spanish);
    public string ReportCharged => Format(Report?.Charged ?? new(0));
    public string ReportPaid => Format(Report?.Paid ?? new(0));
    public string ReportBalance => Format(Report?.Balance ?? new(0));
    public string ReportLeft => Format(Report?.LeftToStores ?? new(0));
    public string ReportAtStore => Report is { AtStore: > 0 } r ? $"{r.AtStore} piezas siguen en las tiendas (charolas sin recoger)." : "";
    public string GeneratedLabel { get; private set; } = "";
    public IReadOnlyList<PeriodDeliveryView> PeriodDeliveries { get; private set; } = [];
    public IReadOnlyList<PeriodPaymentView> PeriodPayments { get; private set; } = [];
    public bool HasPeriodDeliveries => PeriodDeliveries.Count > 0;
    public bool NoPeriodDeliveries => Report is not null && PeriodDeliveries.Count == 0;
    public bool HasPeriodPayments => PeriodPayments.Count > 0;
    public bool NoPeriodPayments => Report is not null && PeriodPayments.Count == 0;
    public string PeriodPaymentsTotal => PeriodPaymentView.Summary(PeriodPayments);

    // ---------- Navegación ----------
    public async Task LoadAsync()
    {
        CanManage = await Task.Run(service.CanManage);
        await ShowAsync(Tab);
    }

    public async Task ShowAsync(ConsignmentTab tab)
    {
        Tab = tab; Message = ""; Notify();
        await (tab switch
        {
            ConsignmentTab.Route => RefreshRouteAsync(),
            ConsignmentTab.Stores => LoadStoresAsync(SelectedStore?.Id),
            ConsignmentTab.Prices => RunAsync(async () => Prices = (await Task.Run(service.Prices)).Select(p => new PriceLineView(p)).ToArray()),
            _ => Report is null ? GenerateAsync() : Task.CompletedTask,
        });
    }

    private async Task RunAsync(Func<Task> work, string? success = null)
    {
        if (Busy) return;
        Busy = true; Message = ""; MessageIsSuccess = false; Notify();
        try { await work(); if (success is not null) { Message = success; MessageIsSuccess = true; } }
        catch (AccessDeniedException error) { Message = error.Message; log("authorization.denied", null); }
        catch (Exception error) when (error is ConsignmentException or ArgumentException or ReportException) { Message = error.Message; }
        catch (Exception error) { Message = "No se pudo completar la operación. Intenta de nuevo."; log("consignment.failed", error.GetType().Name); }
        finally { Busy = false; Notify(); }
    }

    // ---------- Ruta ----------
    public Task RefreshRouteAsync() => RunAsync(LoadRouteAsync);
    private async Task LoadRouteAsync()
    {
        Route = (await Task.Run(service.Route)).Select(r => new StoreStatusView(r)).ToArray();
        if (Selected is { } s) Selected = Route.FirstOrDefault(r => r.Id == s.Id);
    }

    public Task StartDeliverAsync(StoreStatusView store) => RunAsync(async () =>
    {
        var (prices, lines) = await Task.Run(() => (service.Prices(), service.Template(store.Id)));
        template = lines;
        Selected = store; Action = ConsignmentAction.Deliver; DeliverNote = "";
        // Primero los panes de la charola habitual, en su orden; después el resto del catálogo.
        DeliverLines = prices.Where(p => p.Price is not null).OrderBy(p => lines.Any(t => t.ProductId == p.ProductId) ? 0 : 1)
            .Select(p => new TrayLineView(p.ProductId, p.ProductName, p.CategoryName, p.Sku, p.Price!.Value, p.RetailPrice, TrayChanged)).ToArray();
        deliverCategory = null; deliverSearch = "";
        UseTemplate();
        ApplyDeliverFilter();
        StartPhotoLoad(DeliverLines.Select(l => (l.ProductId, (Action<ProductPhotoVisual>)l.SetPhoto)));
        if (DeliverLines.Count == 0) Message = "Ningún pan tiene precio de charola. Defínelos en la pestaña Precios.";
    });

    public void UseTemplate()
    {
        foreach (var line in DeliverLines) line.Quantity = template.FirstOrDefault(t => t.ProductId == line.ProductId)?.Quantity ?? 0;
        TrayChanged();
    }
    public void ClearTray() { foreach (var line in DeliverLines) line.Quantity = 0; TrayChanged(); }

    public void SelectCategory(string? name) { deliverCategory = name; ApplyDeliverFilter(); }

    private void ApplyDeliverFilter()
    {
        // Búsqueda sin distinguir mayúsculas ni acentos («azucar» encuentra «azúcar»), por nombre o SKU.
        var text = deliverSearch.Trim();
        bool Matches(string? value) => value is not null && Spanish.CompareInfo.IndexOf(value, text, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        DeliverTiles = DeliverLines.Where(l => (deliverCategory is null || l.CategoryName == deliverCategory) && (text.Length == 0 || Matches(l.Name) || Matches(l.Sku))).ToArray();
        DeliverCategories = new[] { new TrayCategory(null, "Todos", deliverCategory is null) }
            .Concat(DeliverLines.Select(l => l.CategoryName).Distinct().Select(c => new TrayCategory(c, c, c == deliverCategory))).ToArray();
        Notify();
    }

    private void TrayChanged()
    {
        var current = DeliverLines.Where(l => l.Quantity > 0).ToArray();
        if (!current.SequenceEqual(TrayLines)) TrayLines = current;
        Notify();
    }

    private void StartPhotoLoad(IEnumerable<(Guid ProductId, Action<ProductPhotoVisual> Set)> targets)
    {
        photoLoad.Cancel(); photoLoad.Dispose(); photoLoad = new();
        if (photos is null) return;
        var token = photoLoad.Token;
        _ = Task.WhenAll(targets.Select(async target =>
        {
            try { target.Set(await photos.LoadAsync(target.ProductId, token)); }
            catch (OperationCanceledException) { }
            catch (Exception error) { log("consignment.photo.load.failed", error.GetType().Name); }
        }));
    }

    public Task ConfirmDeliverAsync() => RunAsync(async () =>
    {
        if (Selected is not { } store) return;
        var lines = DeliverLines.Where(l => l.Quantity > 0).Select(l => new ConsignmentLine(l.ProductId, l.Quantity)).ToArray();
        var delivery = await Task.Run(() => service.Deliver(store.Id, lines, DeliverNote));
        Action = ConsignmentAction.None;
        await LoadRouteAsync();
        Message = $"Charola entregada a {delivery.StoreName}: {delivery.Pieces} piezas por {Format(delivery.Value)}."; MessageIsSuccess = true;
        log("consignment.delivered", null);
    });

    public void StartCollect(StoreStatusView store)
    {
        if (store.Tray is not { } tray) return;
        Selected = store; Collecting = tray; Action = ConsignmentAction.Collect; Message = "";
        CollectLines = tray.Items.Select(i => new CollectLineView(i, CollectChanged)).ToArray();
        StartPhotoLoad(CollectLines.Select(l => (l.ProductId, (Action<ProductPhotoVisual>)l.SetPhoto)));
        SelectedMethod = Methods[0]; Reference = ""; CollectNote = ""; VoidReason = ""; IsVoidOpen = false;
        paidEdited = false; paidText = (CollectAmount.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        Notify();
    }

    private void CollectChanged()
    {
        // Mientras no se edite a mano, «Cobrado ahora» sigue el importe de lo vendido.
        if (!paidEdited) paidText = (CollectAmount.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        Notify();
    }

    public Task ConfirmCollectAsync() => RunAsync(async () =>
    {
        if (Collecting is not { } tray || Selected is not { } store) return;
        var paid = ParseMoney(PaidText, "Cobrado ahora");
        var returned = CollectLines.Where(l => l.Returned > 0).ToDictionary(l => l.Position, l => l.Returned);
        var result = await Task.Run(() => service.Collect(tray.Id, returned, paid, SelectedMethod?.Method ?? PaymentMethod.Cash, Reference, CollectNote));
        Action = ConsignmentAction.None; Collecting = null;
        await LoadRouteAsync();
        var settlement = result.Delivery.Settlement!;
        Message = $"Charola de {store.Name} recogida: {settlement.Sold} vendidas por {Format(settlement.SoldTotal)}, {settlement.Returned} regresan para Piedra."
            + (result.Payment is { } p ? $" Cobrado {Format(p.Amount)}." : "")
            + (result.Balance.Cents > 0 ? $" Queda debiendo {Format(result.Balance)}." : " Queda al corriente.");
        MessageIsSuccess = true;
        log("consignment.collected", null);
    });

    public Task VoidAsync() => RunAsync(async () =>
    {
        if (Collecting is not { } tray) return;
        await Task.Run(() => service.Void(tray.Id, VoidReason));
        Action = ConsignmentAction.None; Collecting = null; IsVoidOpen = false;
        await LoadRouteAsync();
        Message = "Charola anulada. No se cobra ni cuenta en los reportes."; MessageIsSuccess = true;
    });

    public void StartPay(StoreStatusView store)
    {
        Selected = store; Action = ConsignmentAction.Pay; Message = "";
        SelectedMethod = Methods[0]; Reference = ""; PayNote = "";
        PayAmountText = (store.Status.Balance.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        Notify();
    }

    public Task ConfirmPayAsync() => RunAsync(async () =>
    {
        if (Selected is not { } store) return;
        var amount = ParseMoney(PayAmountText, "Importe");
        var payment = await Task.Run(() => service.ReceivePayment(store.Id, amount, SelectedMethod?.Method ?? PaymentMethod.Cash, Reference, PayNote));
        Action = ConsignmentAction.None;
        await LoadRouteAsync();
        Message = $"Cobro de {Format(payment.Amount)} registrado a {store.Name}."; MessageIsSuccess = true;
    });

    public void CancelAction() { Action = ConsignmentAction.None; Collecting = null; IsVoidOpen = false; photoLoad.Cancel(); Message = ""; Notify(); }

    // ---------- Tiendas ----------
    private async Task LoadStoresAsync(Guid? select) => await RunAsync(async () =>
    {
        Stores = await Task.Run(() => service.Stores(includeInactive: true));
        await SelectStoreCoreAsync(select is { } id ? Stores.FirstOrDefault(s => s.Id == id) : null);
    });

    public Task SelectStoreAsync(ConsignmentStore? store) => RunAsync(() => SelectStoreCoreAsync(store));

    private async Task SelectStoreCoreAsync(ConsignmentStore? store)
    {
        SelectedStore = store;
        StoreName = store?.Name ?? ""; StoreContact = store?.Contact ?? ""; StorePhone = store?.Phone ?? ""; StoreAddress = store?.Address ?? "";
        if (store is null) { TemplateLines = []; Account = null; Movements = []; return; }
        var (prices, lines, account) = await Task.Run(() => (service.Prices(), service.Template(store.Id), service.Account(store.Id)));
        TemplateLines = prices.Where(p => p.Price is not null).Select(p => new TrayLineView(p.ProductId, p.ProductName, p.CategoryName, p.Sku, p.Price!.Value, p.RetailPrice, Notify)).ToArray();
        foreach (var line in TemplateLines) line.Quantity = lines.FirstOrDefault(t => t.ProductId == line.ProductId)?.Quantity ?? 0;
        Account = account; Movements = account.Movements.Select(m => new MovementView(m)).ToArray();
    }

    public void NewStore() { SelectedStore = null; StoreName = StoreContact = StorePhone = StoreAddress = ""; TemplateLines = []; Account = null; Movements = []; Message = ""; Notify(); }

    public Task SaveStoreAsync() => RunAsync(async () =>
    {
        var saved = await Task.Run(() => service.SaveStore(SelectedStore?.Id, StoreName, StoreContact, StorePhone, StoreAddress));
        Stores = await Task.Run(() => service.Stores(includeInactive: true));
        await SelectStoreCoreAsync(Stores.First(s => s.Id == saved.Id));
    }, "Tienda guardada.");

    public Task ToggleStoreAsync() => RunAsync(async () =>
    {
        if (SelectedStore is not { } store) return;
        var saved = await Task.Run(() => service.SetStoreActive(store.Id, !store.Active));
        Stores = await Task.Run(() => service.Stores(includeInactive: true));
        await SelectStoreCoreAsync(Stores.First(s => s.Id == saved.Id));
        Message = saved.Active ? "Tienda reactivada." : "Tienda desactivada: ya no aparece en la ruta."; MessageIsSuccess = true;
    });

    public Task SaveTemplateAsync() => RunAsync(async () =>
    {
        if (SelectedStore is not { } store) return;
        var lines = TemplateLines.Where(l => l.Quantity > 0).Select(l => new ConsignmentLine(l.ProductId, l.Quantity)).ToArray();
        await Task.Run(() => service.SaveTemplate(store.Id, lines));
    }, "Charola habitual guardada. Se propondrá al entregar.");

    // ---------- Precios ----------
    public Task SavePricesAsync() => RunAsync(async () =>
    {
        var changed = Prices.Where(p => p.Changed).ToList();
        foreach (var line in changed)
            if (!TryParseMoney(line.PriceText, out _)) throw new ArgumentException($"Revisa el precio de {line.Name} (ejemplo: 12.50).");
        await Task.Run(() =>
        {
            foreach (var line in changed) { TryParseMoney(line.PriceText, out var price); service.SetPrice(line.Source.ProductId, price); }
        });
        Prices = (await Task.Run(service.Prices)).Select(p => new PriceLineView(p)).ToArray();
        Message = changed.Count == 0 ? "No había cambios." : $"{changed.Count} precio(s) de charola guardado(s)."; MessageIsSuccess = true;
    });

    // ---------- Reporte ----------
    public Task TodayAsync() => RangeAsync(DateTime.Today, DateTime.Today);
    public Task LastSevenDaysAsync() => RangeAsync(DateTime.Today.AddDays(-6), DateTime.Today);
    public Task MonthAsync() => RangeAsync(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);
    private Task RangeAsync(DateTime from, DateTime through) { FromSelected = from; ThroughSelected = through; return GenerateAsync(); }

    public Task GenerateAsync() => RunAsync(async () =>
    {
        if (FromSelected is not { } from || ThroughSelected is not { } through) throw new ReportException("Elige la fecha inicial y la final.");
        var (report, period) = await Task.Run(() => (service.Report(DateOnly.FromDateTime(from), DateOnly.FromDateTime(through)),
            service.Period(DateOnly.FromDateTime(from), DateOnly.FromDateTime(through))));
        PeriodDeliveries = period.Deliveries.Select(d => new PeriodDeliveryView(d)).ToArray();
        PeriodPayments = period.Payments.Select(p => new PeriodPaymentView(p)).ToArray();
        Report = report;
        StoreRows = report.Stores.Select(s => new StoreReportView(s)).ToArray();
        ProductRows = report.Products.Select(p => new ProductReportRowView(p)).ToArray();
        SellThroughBars = report.Stores.Where(s => s.SellThrough is not null).OrderByDescending(s => s.SellThrough)
            .Select(s => new BarView(s.StoreName, $"{s.SellThrough!.Value.ToString("0.0", CultureInfo.InvariantCulture)} % · {s.Sold} de {s.Sold + s.Returned}",
                (double)s.SellThrough.Value / 100, $"{s.Sold} vendidas, {s.Returned} regresaron para Piedra")).ToArray();
        GeneratedLabel = $"Actualizado a las {DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}";
    });

    public void Forget()
    {
        photoLoad.Cancel();
        Route = []; Selected = null; Action = ConsignmentAction.None; Collecting = null; DeliverLines = []; DeliverTiles = []; TrayLines = []; DeliverCategories = []; CollectLines = [];
        Stores = []; SelectedStore = null; TemplateLines = []; Account = null; Movements = []; Prices = [];
        Report = null; StoreRows = []; ProductRows = []; SellThroughBars = []; GeneratedLabel = ""; PeriodDeliveries = []; PeriodPayments = [];
        Tab = ConsignmentTab.Route; Message = ""; Notify();
    }
}
