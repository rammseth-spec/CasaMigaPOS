using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record InventoryKindFilter(InventoryItemKind? Value, string Label);
public sealed record InventoryActiveFilter(bool? Value, string Label);
public sealed record InventoryUnitChoice(string Code, string Label);
public sealed record InventoryItemKindChoice(InventoryItemKind Value, string Label);
public sealed record InventoryDimensionChoice(InventoryDimension Value, string Label);
public sealed record WasteReasonChoice(WasteReason Value, string Label);
public sealed record AdjustmentReasonChoice(AdjustmentReason Value, string Label);
public sealed record InventoryUnitView(InventoryItemUnit Unit)
{
    public string Code => Unit.Code;
    public string Name => Unit.Name;
    public string Quantity => InventoryDisplay.Format(Unit.AtomicUnitsPerWholeUnit, Unit.Dimension);
    public bool IsActive => Unit.IsActive;
}
public sealed record InventoryLinkView(InventoryProductLinkView Link)
{
    public Guid ProductId => Link.ProductId;
    public string Quantity => InventoryDisplay.Format(Link.QuantityAtomic, Link.Dimension);
}

public sealed record InventoryRowView(InventoryListRow Row)
{
    public Guid Id => Row.InventoryItemId;
    public string Code => Row.Code;
    public string Name => Row.Name;
    public string Kind => InventoryViewModel.KindLabel(Row.Kind);
    public string Balance => Row.MainBalanceAtomic is { } value ? InventoryDisplay.Number(value, Row.Dimension) : "Sin inicializar";
    public string Unit => InventoryDisplay.Unit(Row.MainBalanceAtomic ?? 0, Row.Dimension);
    public string Status => Row.Active ? "Activo" : "Inactivo";
    public string Product => Row.HasProductLink ? "Sí" : "No";
}

public sealed record InventoryHistoryRowView(InventoryMovementRow Row)
{
    public string Date => Row.OccurredAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture);
    public string Kind => InventoryViewModel.MovementLabel(Row.Kind);
    public InventoryDimension Dimension { get; init; }
    public string Delta => InventoryDisplay.Format(Row.DeltaAtomic, Dimension);
    public string Before => InventoryDisplay.Format(Row.BalanceBeforeAtomic, Dimension);
    public string After => InventoryDisplay.Format(Row.BalanceAfterAtomic, Dimension);
    public string User => Row.ActorName;
    public string Reason => Row.ReasonCode ?? "—";
    public string Reference => Row.ExternalReference ?? "—";
    public string OperationId => Row.OperationId.ToString("D");
}

public sealed class InventoryViewModel(InventoryService service, CatalogService catalog,
    Action<string, string?> log) : INotifyPropertyChanged
{
    private string screen = "list";
    private int page;
    private int historyPage;
    private int productPage;
    private Guid? detailId;
    private Guid? pendingOperationId;
    private bool editingItem;
    private string operation = "";

    public bool Busy { get; private set; }
    public bool Ready => !Busy;
    public bool IsList => screen == "list";
    public bool IsDetail => screen == "detail";
    public bool IsItemEditor => screen == "item";
    public bool IsOperationEditor => screen == "operation";
    public bool IsUnitEditor => screen == "unit";
    public bool IsLinkEditor => screen == "link";
    public bool IsOpening => operation == "opening";
    public bool IsReceipt => operation == "receipt";
    public bool IsWaste => operation == "waste";
    public bool IsCount => operation == "count";
    public bool IsEditingItem => editingItem;
    public bool CanEditIdentity => !editingItem;
    public bool ShowsReference => IsOpening || IsReceipt;
    public bool ShowsNote => IsReceipt || IsWaste || IsCount;
    public bool CanView { get; private set; }
    public bool CanConfigure { get; private set; }
    public bool CanReceive { get; private set; }
    public bool CanWaste { get; private set; }
    public bool CanAdjust { get; private set; }
    public bool CanOpenBalance => CanAdjust && Detail?.Balance is null;
    public bool CanSubmitOperation => !Busy && pendingOperationId is not null;
    public string PendingOperationId => pendingOperationId?.ToString("D") ?? "";
    public string Message { get; private set; } = "";
    public bool MessageIsSuccess { get; private set; }

    public IReadOnlyList<InventoryKindFilter> KindFilters { get; } =
        [new(null, "Todos los tipos"), .. Enum.GetValues<InventoryItemKind>().Select(x => new InventoryKindFilter(x, KindLabel(x)))];
    public IReadOnlyList<InventoryActiveFilter> ActiveFilters { get; } =
        [new(true, "Activos"), new(false, "Inactivos"), new(null, "Todos")];
    public IReadOnlyList<InventoryItemKindChoice> ItemKinds { get; } = Enum.GetValues<InventoryItemKind>().Select(x => new InventoryItemKindChoice(x, KindLabel(x))).ToArray();
    public IReadOnlyList<InventoryDimensionChoice> Dimensions { get; } = Enum.GetValues<InventoryDimension>().Select(x => new InventoryDimensionChoice(x, DimensionLabel(x))).ToArray();
    public IReadOnlyList<WasteReasonChoice> WasteReasons { get; } =
        [new(WasteReason.Damaged,"Dañado"),new(WasteReason.Expired,"Caducado"),new(WasteReason.ProductionError,"Error de producción"),new(WasteReason.Spill,"Derrame"),new(WasteReason.Other,"Otro")];
    public IReadOnlyList<AdjustmentReasonChoice> AdjustmentReasons { get; } =
        [new(AdjustmentReason.PhysicalCountVariance,"Diferencia de conteo físico"),new(AdjustmentReason.DataCorrection,"Corrección de datos"),new(AdjustmentReason.Other,"Otro")];
    public InventoryKindFilter SelectedKindFilter { get; set; } = new(null, "Todos los tipos");
    public InventoryActiveFilter SelectedActiveFilter { get; set; } = new(true, "Activos");
    public string Search { get; set; } = "";
    public IReadOnlyList<InventoryRowView> Items { get; private set; } = [];
    public InventoryRowView? SelectedItem { get; set; }
    public string PageLabel { get; private set; } = "";
    public bool HasPrevious => page > 0;
    public bool HasNext { get; private set; }

    public InventoryItemDetail? Detail { get; private set; }
    public string DetailTitle => Detail is null ? "" : $"{Detail.Item.Code} · {Detail.Item.Name}";
    public string DetailMetadata => Detail is null ? "" :
        $"Tipo: {KindLabel(Detail.Item.Kind)}\nDimensión: {DimensionLabel(Detail.Item.Dimension)}\nEstado: {(Detail.Item.IsActive ? "Activo" : "Inactivo")}\nUbicación: {Detail.MainLocation.Name}";
    public string DetailBalance => Detail?.Balance is { } balance
        ? $"Existencia actual: {InventoryDisplay.Format(balance.QuantityAtomic, Detail.Item.Dimension)}"
        : "Existencia actual: sin saldo inicial";
    public string ToggleActiveLabel => Detail?.Item.IsActive == true ? "Desactivar" : "Activar";
    public string ReconciliationMessage { get; private set; } = "";
    public bool ReconciliationWarning { get; private set; }
    public IReadOnlyList<InventoryUnitView> Units => Detail?.Units.Select(x => new InventoryUnitView(x)).ToArray() ?? [];
    public IReadOnlyList<InventoryLinkView> ProductLinks =>
        Detail?.ProductLinks.Where(x => x.Active).Select(x => new InventoryLinkView(x)).ToArray() ?? [];
    public InventoryLinkView? SelectedLink { get; set; }
    public string UnitFactorLabel => $"Cantidad por presentación completa ({InventoryDisplay.CaptureUnit(Detail?.Item.Dimension ?? InventoryDimension.Count)})";
    public string LinkQuantityLabel => $"Cantidad por unidad vendida ({InventoryDisplay.CaptureUnit(Detail?.Item.Dimension ?? InventoryDimension.Count)})";
    public IReadOnlyList<InventoryHistoryRowView> History { get; private set; } = [];
    public string HistoryPageLabel { get; private set; } = "";
    public bool HasHistoryPrevious => historyPage > 0;
    public bool HasHistoryNext { get; private set; }

    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public InventoryItemKindChoice SelectedItemKind { get; set; } = new(InventoryItemKind.RawMaterial, "Materia prima");
    public InventoryDimensionChoice SelectedDimension { get; set; } = new(InventoryDimension.Mass, "Masa");
    public string ItemEditorTitle => editingItem ? "Editar artículo" : "Crear artículo";

    public string Quantity { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Note { get; set; } = "";
    public WasteReasonChoice SelectedWasteReason { get; set; } = new(WasteReason.Damaged, "Dañado");
    public AdjustmentReasonChoice SelectedAdjustmentReason { get; set; } =
        new(AdjustmentReason.PhysicalCountVariance, "Diferencia de conteo físico");
    public IReadOnlyList<InventoryUnitChoice> UnitChoices { get; private set; } = [];
    public InventoryUnitChoice? SelectedUnit { get; set; }
    public string OperationTitle => operation switch
        { "opening" => "Registrar saldo inicial", "receipt" => "Registrar entrada", "waste" => "Registrar merma", "count" => "Conteo físico", _ => "Operación" };
    public string CurrentBalanceLabel => DetailBalance;

    public string UnitCode { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string UnitFactor { get; set; } = "";

    public string ProductSearch { get; set; } = "";
    public IReadOnlyList<ProductRow> Products { get; private set; } = [];
    public ProductRow? SelectedProduct { get; set; }
    public string LinkQuantityAtomic { get; set; } = "";
    public string ProductPageLabel { get; private set; } = "";
    public bool HasProductPrevious => productPage > 0;
    public bool HasProductNext { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public bool CanEnter() => service.Can("inventory.view");

    public async Task LoadAsync()
    {
        if (Busy) return;
        Busy = true; Message = ""; Notify();
        try
        {
            CanView = await Task.Run(() => service.Can("inventory.view"));
            if (!CanView) throw new InventoryApplicationException(InventoryError.Unauthorized, "Sin acceso a inventario.");
            CanConfigure = await Task.Run(() => service.Can("inventory.configure"));
            CanReceive = await Task.Run(() => service.Can("inventory.receive"));
            CanWaste = await Task.Run(() => service.Can("inventory.waste"));
            CanAdjust = await Task.Run(() => service.Can("inventory.adjust"));
            screen = "list"; page = 0; historyPage = 0; Search = "";
            SelectedKindFilter = KindFilters[0]; SelectedActiveFilter = ActiveFilters[0];
            await RefreshList();
        }
        catch (InventoryApplicationException error) { Message = ErrorMessage(error.Error); CanView = false; }
        catch (Exception error) { Failed("inventory.ui.load.failed", error); }
        finally { Busy = false; Notify(); }
    }

    public Task SearchAsync() => Run(async () => { page = 0; await RefreshList(); });
    public Task PageAsync(int delta) => Run(async () => { page = Math.Max(0, page + delta); await RefreshList(); });

    public Task OpenDetailAsync() => Run(async () =>
    {
        if (SelectedItem is null) throw new ArgumentException("Selecciona un artículo.");
        detailId = SelectedItem.Id; historyPage = 0; screen = "detail";
        await RefreshDetail();
    });

    public void Back()
    {
        if (Busy) return;
        if (IsDetail) { screen = "list"; detailId = null; Detail = null; History = []; }
        else { screen = detailId is null ? "list" : "detail"; pendingOperationId = null; operation = ""; }
        Message = ""; Notify();
    }

    public void NewItem()
    {
        if (Busy || !CanConfigure) return;
        editingItem = false; ItemCode = ""; ItemName = ""; SelectedItemKind = ItemKinds[0];
        SelectedDimension = Dimensions[0]; screen = "item"; Message = ""; Notify();
    }

    public void EditItem()
    {
        if (Busy || !CanConfigure || Detail is null) return;
        editingItem = true; ItemCode = Detail.Item.Code; ItemName = Detail.Item.Name;
        SelectedItemKind = ItemKinds.Single(x => x.Value == Detail.Item.Kind);
        SelectedDimension = Dimensions.Single(x => x.Value == Detail.Item.Dimension);
        screen = "item"; Message = ""; Notify();
    }

    public Task SaveItemAsync() => Run(async () =>
    {
        InventoryItem item;
        if (editingItem)
            item = await Task.Run(() => service.UpdateInventoryItemMetadata(detailId!.Value, ItemName,
                SelectedItemKind.Value, SelectedDimension.Value));
        else
            item = await Task.Run(() => service.CreateInventoryItem(ItemCode, ItemName,
                SelectedItemKind.Value, SelectedDimension.Value));
        detailId = item.Id; screen = "detail"; MessageIsSuccess = true; Message = "Artículo guardado.";
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    });

    public Task ToggleActiveAsync() => Run(async () =>
    {
        if (Detail is null) return;
        try { await Task.Run(() => service.SetInventoryItemActive(Detail.Item.Id, !Detail.Item.IsActive)); }
        catch (InventoryApplicationException error) when (error.Error == InventoryError.InsufficientStock)
        { throw new ArgumentException("No se puede desactivar un artículo con existencia."); }
        MessageIsSuccess = true; Message = Detail.Item.IsActive ? "Artículo desactivado." : "Artículo activado.";
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    });

    public void BeginOperation(string kind)
    {
        if (Busy || Detail is null) return;
        if ((kind == "opening" && !CanOpenBalance) || (kind == "receipt" && !CanReceive) ||
            (kind == "waste" && !CanWaste) || (kind == "count" && !CanAdjust)) return;
        operation = kind; pendingOperationId = Guid.NewGuid(); Quantity = kind == "opening" ? "0" : "";
        Reference = ""; Note = ""; SelectedWasteReason = WasteReasons[0];
        SelectedAdjustmentReason = AdjustmentReasons[0];
        UnitChoices = BuildUnits(Detail); SelectedUnit = UnitChoices.FirstOrDefault();
        screen = "operation"; Message = ""; Notify();
    }

    public Task SubmitOperationAsync() => Run(async () =>
    {
        if (Detail is null || pendingOperationId is not { } operationId || SelectedUnit is null) return;
        var quantity = ParseDecimal(Quantity);
        var item = Detail.Item.Id; var location = Detail.MainLocation.Id; var unit = SelectedUnit.Code;
        if (operation == "opening")
            await Task.Run(() => service.RegisterOpeningBalance(new(operationId, item, location, quantity, unit, Optional(Reference))));
        else if (operation == "receipt")
            await Task.Run(() => service.ReceiveInventory(new(operationId, item, location, quantity, unit, Optional(Reference), Optional(Note))));
        else if (operation == "waste")
            await Task.Run(() => service.RecordWaste(new(operationId, item, location, quantity, unit,
                SelectedWasteReason.Value, Optional(Note))));
        else
        {
            var result = await Task.Run(() => service.AdjustToPhysicalCount(new(operationId, item, location,
                quantity, unit, SelectedAdjustmentReason.Value, Optional(Note))));
            if (!result.Changed) Message = "El conteo coincide con la existencia registrada. No se realizó ningún movimiento.";
        }
        pendingOperationId = null; operation = ""; screen = "detail";
        if (string.IsNullOrEmpty(Message)) Message = "Operación de inventario confirmada.";
        MessageIsSuccess = true;
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    }, refreshOnConcurrency: true);

    public void NewUnit()
    {
        if (Busy || !CanConfigure || Detail is null) return;
        UnitCode = ""; UnitName = ""; UnitFactor = ""; screen = "unit"; Message = ""; Notify();
    }

    public Task SaveUnitAsync() => Run(async () =>
    {
        if (detailId is null || Detail is null) return;
        var factor = InventoryDisplay.ParseCapture(UnitFactor, Detail.Item.Dimension);
        await Task.Run(() => service.AddInventoryItemUnit(detailId.Value, UnitCode, UnitName, factor));
        screen = "detail"; MessageIsSuccess = true; Message = "Presentación agregada.";
        await RefreshDetail(preserveMessage: true);
    });

    public Task ShowLinkEditorAsync() => Run(async () =>
    {
        if (!CanConfigure || Detail is null) return;
        ProductSearch = ""; productPage = 0; LinkQuantityAtomic = ""; screen = "link";
        await RefreshProducts();
    });

    public Task SearchProductsAsync() => Run(async () => { productPage = 0; await RefreshProducts(); });
    public Task ProductPageAsync(int delta) => Run(async () => { productPage = Math.Max(0, productPage + delta); await RefreshProducts(); });

    public Task SaveLinkAsync() => Run(async () =>
    {
        if (detailId is null || Detail is null || SelectedProduct is null)
            throw new ArgumentException("Selecciona un producto.");
        var quantity = InventoryDisplay.ParseCapture(LinkQuantityAtomic, Detail.Item.Dimension);
        await Task.Run(() => service.LinkProductToInventoryItem(SelectedProduct.Product.Id, detailId.Value, quantity));
        screen = "detail"; MessageIsSuccess = true; Message = "Producto asociado. Las ventas todavía no descuentan inventario.";
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    });

    public Task UnlinkAsync() => Run(async () =>
    {
        if (SelectedLink is null) throw new ArgumentException("Selecciona una asociación.");
        await Task.Run(() => service.UnlinkProductFromInventoryItem(SelectedLink.ProductId));
        MessageIsSuccess = true; Message = "Producto desvinculado.";
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    });

    public Task HistoryPageAsync(int delta) => Run(async () =>
    {
        historyPage = Math.Max(0, historyPage + delta); await RefreshHistory();
    });

    public void Forget()
    {
        Items = []; Detail = null; History = []; Products = []; SelectedItem = null; detailId = null;
        pendingOperationId = null; screen = "list"; Message = ""; CanView = CanConfigure = CanReceive = CanWaste = CanAdjust = false;
        Notify();
    }

    private async Task RefreshList(bool preserveMessage = false)
    {
        var result = await Task.Run(() => service.List(new(Search, SelectedKindFilter?.Value,
            SelectedActiveFilter?.Value, page, 50)));
        Items = result.Items.Select(x => new InventoryRowView(x)).ToArray(); SelectedItem = null;
        PageLabel = $"Página {page + 1} · {result.Total} artículos · hasta {result.PageSize} por página";
        HasNext = (page + 1) * result.PageSize < result.Total;
        if (!preserveMessage) Message = "";
    }

    private async Task RefreshDetail(bool preserveMessage = false)
    {
        if (detailId is null) return;
        Detail = await Task.Run(() => service.Detail(detailId.Value));
        UnitChoices = BuildUnits(Detail); SelectedLink = null;
        await RefreshHistory();
        var status = (await Task.Run(service.ReconciliationStatus))
            .FirstOrDefault(x => x.InventoryItemId == detailId.Value && x.InventoryLocationId == Detail.MainLocation.Id);
        ReconciliationWarning = status is { Consistent: false };
        ReconciliationMessage = ReconciliationWarning
            ? "ADVERTENCIA: El saldo materializado no coincide con el ledger. No se realizó ninguna reparación."
            : "Inventario consistente.";
        if (!preserveMessage) Message = "";
    }

    private async Task RefreshHistory()
    {
        if (detailId is null) return;
        var result = await Task.Run(() => service.MovementHistory(new(detailId, historyPage, 25)));
        History = result.Items.Select(x => new InventoryHistoryRowView(x) { Dimension = Detail!.Item.Dimension }).ToArray();
        HistoryPageLabel = $"Página {historyPage + 1} · {result.Total} movimientos · hasta {result.PageSize} por página";
        HasHistoryNext = (historyPage + 1) * result.PageSize < result.Total;
    }

    private async Task RefreshProducts()
    {
        var result = await Task.Run(() => catalog.Products(new(ProductSearch, null, false, productPage)));
        Products = result.Items; SelectedProduct = null;
        ProductPageLabel = $"Página {productPage + 1} · {result.Total} productos";
        HasProductNext = (productPage + 1) * result.PageSize < result.Total;
    }

    private async Task Run(Func<Task> action, bool refreshOnConcurrency = false)
    {
        if (Busy) return;
        Busy = true; Message = ""; MessageIsSuccess = false; Notify();
        try { await action(); log("inventory.ui.completed", null); }
        catch (InventoryApplicationException error)
        {
            Message = ErrorMessage(error.Error); log("inventory.ui.rejected", error.Error.ToString());
            if (refreshOnConcurrency && error.Error == InventoryError.ConcurrencyConflict)
                await RefreshDetail(preserveMessage: true);
        }
        catch (ArgumentException error) { Message = error.Message; }
        catch (OverflowException) { Message = "La cantidad ingresada excede el límite permitido."; }
        catch (Exception error) { Failed("inventory.ui.failed", error); }
        finally { Busy = false; Notify(); }
    }

    private void Failed(string eventName, Exception error)
    {
        Message = "No se pudo completar la operación de inventario."; log(eventName, error.GetType().Name);
    }

    private static IReadOnlyList<InventoryUnitChoice> BuildUnits(InventoryItemDetail detail) =>
        InventoryUnits.All.Where(x => x.Dimension == detail.Item.Dimension && x.Code is not "mg" and not "uL")
            .Select(x => new InventoryUnitChoice(x.Code, $"{x.Symbol} · unidad estándar"))
            .Concat(detail.Units.Where(x => x.IsActive).Select(x => new InventoryUnitChoice(x.Code,
                $"{x.Name} · {InventoryDisplay.Format(x.AtomicUnitsPerWholeUnit, detail.Item.Dimension)}"))).ToArray();

    private static decimal ParseDecimal(string value)
    {
        const NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;
        if (decimal.TryParse(value, style, CultureInfo.CurrentCulture, out var parsed) ||
            decimal.TryParse(value, style, CultureInfo.InvariantCulture, out parsed)) return parsed;
        throw new ArgumentException("La cantidad ingresada no es válida.");
    }

    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static string KindLabel(InventoryItemKind value) => value switch
        { InventoryItemKind.RawMaterial => "Materia prima", InventoryItemKind.FinishedGood => "Producto terminado",
          InventoryItemKind.Packaging => "Empaque", InventoryItemKind.Supply => "Insumo", _ => value.ToString() };
    public static string DimensionLabel(InventoryDimension value) => value switch
        { InventoryDimension.Mass => "Masa", InventoryDimension.Volume => "Volumen", InventoryDimension.Count => "Conteo", _ => value.ToString() };
    public static string MovementLabel(InventoryMovementKind value) => value switch
        { InventoryMovementKind.OpeningBalance => "Saldo inicial", InventoryMovementKind.Receipt => "Entrada",
          InventoryMovementKind.AdjustmentGain => "Ajuste positivo", InventoryMovementKind.AdjustmentLoss => "Ajuste negativo",
          InventoryMovementKind.Waste => "Merma", _ => value.ToString() };
    public static string ErrorMessage(InventoryError error) => error switch
    {
        InventoryError.Unauthorized => "No tienes permiso para realizar esta operación.",
        InventoryError.InactiveUser => "El usuario actual ya no está activo.",
        InventoryError.InvalidUnit => "La unidad seleccionada no es válida.",
        InventoryError.DimensionMismatch => "La unidad no corresponde al tipo de artículo.",
        InventoryError.InvalidQuantity => "La cantidad ingresada no es válida.",
        InventoryError.InvalidNote => "La observación no es válida.",
        InventoryError.NotInitialized => "Este artículo todavía no tiene saldo inicial.",
        InventoryError.AlreadyInitialized => "Este artículo ya fue inicializado.",
        InventoryError.InsufficientStock => "No hay existencia suficiente para realizar la operación.",
        InventoryError.IdempotencyConflict => "La operación no puede repetirse porque sus datos cambiaron.",
        InventoryError.InventoryItemInactive => "El artículo está inactivo.",
        InventoryError.NoChange => "El conteo coincide con la existencia registrada. No se realizó ningún movimiento.",
        InventoryError.ConcurrencyConflict => "La existencia cambió mientras realizabas la operación. Actualiza los datos y vuelve a revisar el conteo.",
        InventoryError.InconsistentBalance => "Se detectó una inconsistencia de inventario. La operación no fue reparada automáticamente.",
        InventoryError.InventoryItemNotFound => "El artículo ya no está disponible.",
        InventoryError.ProductNotFound => "El producto ya no está disponible.",
        _ => "No se pudo completar la operación de inventario."
    };
}
