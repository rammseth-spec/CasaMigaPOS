using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record InventoryKindFilter(InventoryItemKind? Value, string Label);
public sealed record InventoryActiveFilter(bool? Value, string Label);
public sealed record InventoryUnitChoice(string Code, string Label);
public sealed record InventorySearchChoice(Guid Id, string Code, string Name)
{
    public string Label => $"{Name} · {Code}";
}
public sealed record InventoryItemKindChoice(InventoryItemKind Value, string Label);
public sealed record InventoryDimensionChoice(InventoryDimension Value, string Label);
public sealed record WasteReasonChoice(WasteReason Value, string Label);
public sealed record AdjustmentReasonChoice(AdjustmentReason Value, string Label);
public sealed record InventoryUnitView(InventoryItemUnit Unit)
{
    public string Code => Unit.Code;
    public string Name => Unit.Name;
    public string Quantity => InventoryDisplay.Format(Unit.AtomicUnitsPerWholeUnit, Unit.Dimension);
    public string Equivalence => $"1 {Name} = {Quantity}";
    public bool IsActive => Unit.IsActive;
}
public sealed record InventoryLinkView(InventoryProductLinkView Link, string ProductName = "Producto no disponible", string ProductSku = "—")
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
    public string Date => Row.OccurredAt.ToLocalTime().ToString("dd/MM/yyyy · HH:mm", CultureInfo.InvariantCulture);
    public string Kind => InventoryViewModel.MovementLabel(Row.Kind);
    public InventoryDimension Dimension { get; init; }
    public string Delta => InventoryViewModel.SignedQuantity(Row.DeltaAtomic, Dimension);
    public string Before => InventoryDisplay.Format(Row.BalanceBeforeAtomic, Dimension);
    public string After => InventoryDisplay.Format(Row.BalanceAfterAtomic, Dimension);
    public string User => Row.ActorName;
    public string Reason => UiLabels.MovementReason(Row.ReasonCode);
    public string Reference => Row.ExternalReference ?? "—";
    public string BalanceChange => $"{Before} → {After}";
    public string Detail => $"Motivo: {Reason}\nReferencia: {Reference}";
    public string OperationId => Row.OperationId.ToString("D");
}

public sealed class InventoryViewModel(InventoryService service, CatalogService catalog,
    Action<string, string?> log,ConsumptionViewModel? consumption=null) : INotifyPropertyChanged
{
    public ConsumptionViewModel? Consumption {get;}=consumption;
    private string screen = "list";
    private int page;
    private int historyPage;
    private int productPage;
    private Guid? detailId;
    private Guid? pendingOperationId;
    private bool editingItem;
    private string operation = "";
    private bool writeStarted;
    private bool writeConfirmed;
    private CancellationTokenSource? searchCancellation;
    private int searchRevision;
    private (string Search, InventoryItemKind? Kind, bool? Active)? appliedFilter;
    public bool IsSearching { get; private set; }

    public bool Busy { get; private set; }
    public bool RecoveryRequired { get; private set; }
    public bool Ready => !Busy && !RecoveryRequired;
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
    public bool CanOpenBalance => Ready && CanAdjust && Detail is { Item.IsActive: true, MainLocation.Active: true, Balance: null };
    public bool CanEditDetail => Ready && CanConfigure && Detail is not null;
    public bool CanAssociate => CanEditDetail && Detail?.Item.IsActive == true;
    public bool CanAddUnit => CanAssociate;
    public bool CanToggleActive => CanEditDetail && (Detail?.Item.IsActive == false || (Detail?.Balance?.QuantityAtomic ?? 0) == 0);
    private bool HasUsableBalance => Detail is { Item.IsActive: true, MainLocation.Active: true, Balance: not null };
    public bool CanReceiveDetail => Ready && CanReceive && HasUsableBalance;
    public bool CanWasteDetail => Ready && CanWaste && HasUsableBalance && Detail!.Balance!.QuantityAtomic > 0;
    public bool CanAdjustDetail => Ready && CanAdjust && HasUsableBalance && !ReconciliationWarning;
    public bool CanSubmitOperation => Ready && pendingOperationId is not null && OperationPreview is not null && OperationPermission;
    private bool OperationPermission => operation switch { "opening" => CanAdjust, "receipt" => CanReceive, "waste" => CanWaste, "count" => CanAdjust, _ => false };
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
    private InventoryKindFilter selectedKindFilter = new(null, "Todos los tipos");
    public InventoryKindFilter SelectedKindFilter { get => selectedKindFilter; set { if (selectedKindFilter == value) return; selectedKindFilter = value; Notify(); QueueSearch(); } }
    private InventoryActiveFilter selectedActiveFilter = new(true, "Activos");
    public InventoryActiveFilter SelectedActiveFilter { get => selectedActiveFilter; set { if (selectedActiveFilter == value) return; selectedActiveFilter = value; Notify(); QueueSearch(); } }
    private string search = "";
    public string Search { get => search; set { if (search == value) return; search = value; Notify(); QueueSearch(); } }
    public bool HasSearch => !string.IsNullOrEmpty(Search);
    public string InventoryEmptyMessage => IsSearching ? "Buscando artículos…" : HasSearch
        ? "No encontramos artículos con esa búsqueda." : "No hay artículos con los filtros seleccionados.";
    public IReadOnlyList<InventorySearchChoice> SearchChoices { get; private set; } = [];
    private async Task RefreshSearchChoices()
    {
        SearchChoices = await ReadSearchChoices(SelectedKindFilter.Value, SelectedActiveFilter.Value, CancellationToken.None);
    }
    private async Task<IReadOnlyList<InventorySearchChoice>> ReadSearchChoices(InventoryItemKind? kind, bool? active, CancellationToken token)
    {
        var choices = new List<InventorySearchChoice>();
        for (var choicePage = 0; ; choicePage++)
        {
            token.ThrowIfCancellationRequested();
            var result = await Task.Run(() => service.List(new(Kind: kind,
                Active: active, Page: choicePage, PageSize: 100)), token);
            choices.AddRange(result.Items.Select(x => new InventorySearchChoice(x.InventoryItemId, x.Code, x.Name)));
            if (choices.Count >= result.Total || result.Items.Count == 0) break;
        }
        return choices.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Code).ToArray();
    }
    public Task ChooseSearchAsync(InventorySearchChoice choice) => Run(async () =>
    {
        Search = choice.Code; page = 0;
        await RefreshList();
        SelectedItem = Items.FirstOrDefault(x => x.Id == choice.Id);
    });
    public IReadOnlyList<InventoryRowView> Items { get; private set; } = [];
    private InventoryRowView? selectedItem;
    public InventoryRowView? SelectedItem { get => selectedItem; set { if (selectedItem == value) return; selectedItem = value; Notify(); } }
    public bool CanOpenDetail => Ready && !IsSearching && CanView && SelectedItem is not null && Items.Any(x => x.Id == SelectedItem.Id);
    public string PageLabel { get; private set; } = "";
    public bool HasPrevious => !IsSearching && page > 0;
    private bool hasNext;
    public bool HasNext { get => !IsSearching && hasNext; private set => hasNext = value; }

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
    public IReadOnlyList<InventoryLinkView> ProductLinks { get; private set; } = [];
    private InventoryLinkView? selectedLink;
    public InventoryLinkView? SelectedLink { get => selectedLink; set { if (selectedLink == value) return; selectedLink = value; Notify(); } }
    public bool CanUnlink => Ready && CanConfigure && SelectedLink is not null && ProductLinks.Any(x => x.Link.Id == SelectedLink.Link.Id);
    public string AssociationExplanation =>
        (Detail?.Item.Kind == InventoryItemKind.RawMaterial ? "Esta asociación descuenta directamente la materia prima; no calcula una receta. " : "La asociación indica cuánto descontar de este artículo por unidad vendida. ") +
        "Sólo se utiliza si el producto controla inventario y el consumo automático está activo. Asociar no activa el consumo automático.";
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

    private string quantity = "", reference = "", note = "";
    public string Quantity { get => quantity; set { if (quantity == value) return; quantity = value; Notify(); } }
    public string Reference { get => reference; set { if (reference == value) return; reference = value; Notify(); } }
    public string Note { get => note; set { if (note == value) return; note = value; Notify(); } }
    private WasteReasonChoice selectedWasteReason = new(WasteReason.Damaged, "Dañado");
    public WasteReasonChoice SelectedWasteReason { get => selectedWasteReason; set { if (selectedWasteReason == value) return; selectedWasteReason = value; Notify(); } }
    private AdjustmentReasonChoice selectedAdjustmentReason = new(AdjustmentReason.PhysicalCountVariance, "Diferencia de conteo físico");
    public AdjustmentReasonChoice SelectedAdjustmentReason { get => selectedAdjustmentReason; set { if (selectedAdjustmentReason == value) return; selectedAdjustmentReason = value; Notify(); } }
    public IReadOnlyList<InventoryUnitChoice> UnitChoices { get; private set; } = [];
    private InventoryUnitChoice? selectedUnit;
    public InventoryUnitChoice? SelectedUnit { get => selectedUnit; set { if (selectedUnit == value) return; selectedUnit = value; Notify(); } }
    public string OperationTitle => operation switch
        { "opening" => "Registrar saldo inicial", "receipt" => "Registrar entrada", "waste" => "Registrar merma", "count" => "Conteo físico", _ => "Operación" };
    public string CurrentBalanceLabel => DetailBalance;
    public string QuantityLabel => operation switch { "opening" => "Existencia al comenzar", "receipt" => "Cantidad recibida", "waste" => "Cantidad de merma", "count" => "Nueva existencia física", _ => "Cantidad a registrar" };
    public string OperationHelpText => operation switch
    {
        "opening" => "Registra una sola vez la existencia física al comenzar a controlar el artículo. No es una entrada de compra. Puede ser cero.",
        "receipt" => "Registra mercancía o material que ingresa al inventario y aumenta la existencia. Requiere saldo inicial.",
        "waste" => "Registra material perdido, dañado, caducado o no utilizable. Disminuye la existencia y conserva el motivo.",
        "count" => "Captura todo lo contado físicamente, no la diferencia. Se compara con la existencia registrada y se deja trazabilidad del ajuste. Si coincide, no se crea un movimiento.",
        _ => ""
    };
    public string SelectedUnitEquivalence => Detail?.Units.FirstOrDefault(x => x.Code == SelectedUnit?.Code && x.IsActive) is { } unit
        ? $"1 {unit.Name} = {InventoryDisplay.Format(unit.AtomicUnitsPerWholeUnit, unit.Dimension)} · captura presentaciones enteras"
        : "";
    public InventoryOperationPreview? OperationPreview { get { TryPreview(out var preview, out _); return preview; } }
    public bool HasOperationPreview => OperationPreview is not null;
    public string PreviewError { get { TryPreview(out _, out var error); return error; } }
    public string PreviewCurrentLabel => IsCount ? "Existencia registrada" : "Existencia actual";
    public string PreviewCurrent => IsOpening ? "Sin saldo inicial" : Detail is { Balance: { } balance } ? InventoryDisplay.Format(balance.QuantityAtomic, Detail.Item.Dimension) : "—";
    public string PreviewMovementLabel => IsCount ? "Conteo físico" : IsWaste ? "Merma" : IsOpening ? "Saldo inicial" : "Entrada";
    public string PreviewMovement => OperationPreview is { } preview ? (IsCount
        ? InventoryDisplay.Format(preview.Capture.Quantity.AtomicUnits, preview.Dimension)
        : SignedQuantity(preview.DeltaAtomic, preview.Dimension)) : "—";
    public string PreviewResult => OperationPreview is { } preview ? InventoryDisplay.Format(preview.AfterAtomic, preview.Dimension) : "—";
    public string PreviewDifference => OperationPreview is { } preview ? SignedQuantity(preview.DeltaAtomic, preview.Dimension) : "—";

    public string UnitCode { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string UnitFactor { get; set; } = "";

    public string ProductSearch { get; set; } = "";
    public IReadOnlyList<ProductRow> Products { get; private set; } = [];
    private ProductRow? selectedProduct;
    public ProductRow? SelectedProduct { get => selectedProduct; set { if (selectedProduct == value) return; selectedProduct = value; Notify(); } }
    public bool CanLinkSelected => CanAssociate && SelectedProduct is not null && Products.Any(x => x.Product.Id == SelectedProduct.Product.Id) && ValidLinkQuantity;
    private string linkQuantityAtomic = "";
    public string LinkQuantityAtomic { get => linkQuantityAtomic; set { if (linkQuantityAtomic == value) return; linkQuantityAtomic = value; Notify(); } }
    private bool ValidLinkQuantity
    {
        get { try { return Detail is not null && InventoryDisplay.ParseCapture(LinkQuantityAtomic, Detail.Item.Dimension) > 0; } catch (Exception e) when (e is ArgumentException or OverflowException) { return false; } }
    }
    public string ProductPageLabel { get; private set; } = "";
    public bool HasProductPrevious => productPage > 0;
    public bool HasProductNext { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public bool CanEnter() => service.Can("inventory.view");

    public async Task LoadAsync()
    {
        if (!Ready) return;
        CancelSearch();
        Busy = true; Message = ""; Notify();
        try
        {
            CanView = await Task.Run(() => service.Can("inventory.view"));
            if (!CanView) throw new InventoryApplicationException(InventoryError.Unauthorized, "Sin acceso a inventario.");
            CanConfigure = await Task.Run(() => service.Can("inventory.configure"));
            CanReceive = await Task.Run(() => service.Can("inventory.receive"));
            CanWaste = await Task.Run(() => service.Can("inventory.waste"));
            CanAdjust = await Task.Run(() => service.Can("inventory.adjust"));
            if(Consumption is not null)await Consumption.LoadAsync();
            screen = "list"; page = 0; historyPage = 0; Search = "";
            SelectedKindFilter = KindFilters[0]; SelectedActiveFilter = ActiveFilters[0];
            await RefreshList();
        }
        catch (InventoryApplicationException error) { Message = ErrorMessage(error.Error); CanView = false; }
        catch (Exception error) { ReadFailed(error); }
        finally { Busy = false; Notify(); }
    }

    // Live reads never set Busy: typing remains responsive and obsolete results cannot win.
    private void QueueSearch()
    {
        if (Ready && CanView && IsList) _ = StartSearchAsync(debounce: true);
    }
    public Task SearchAsync() => Ready && CanView && IsList ? StartSearchAsync(debounce: false) : Task.CompletedTask;
    public Task ClearSearchAsync()
    {
        Search = "";
        return SearchAsync();
    }
    private void CancelSearch()
    {
        searchRevision++;
        searchCancellation?.Cancel(); searchCancellation?.Dispose(); searchCancellation = null;
        IsSearching = false;
    }
    private async Task StartSearchAsync(bool debounce)
    {
        CancelSearch();
        var revision = searchRevision;
        var cancellation = new CancellationTokenSource(); searchCancellation = cancellation;
        var token = cancellation.Token;
        var query = new InventoryListQuery(Search, SelectedKindFilter?.Value, SelectedActiveFilter?.Value, 0, 50);
        IsSearching = true; Notify();
        try
        {
            if (debounce) await Task.Delay(250, token);
            var result = await Task.Run(() => service.List(query), token);
            var choices = await ReadSearchChoices(query.Kind, query.Active, token);
            if (token.IsCancellationRequested || revision != searchRevision || !IsList || !CanView) return;
            page = 0; ApplyList(result); SearchChoices = choices; Message = ""; MessageIsSuccess = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (revision != searchRevision || token.IsCancellationRequested) return;
            Items = []; SelectedItem = null; SearchChoices = []; PageLabel = ""; HasNext = false;
            if (error is InventoryApplicationException inventoryError) Message = ErrorMessage(inventoryError.Error);
            else Message = "No se pudo completar la búsqueda. Intenta buscar nuevamente.";
            MessageIsSuccess = false; log("inventory.ui.search.failed", error.GetType().Name);
        }
        finally { if (revision == searchRevision) { IsSearching = false; Notify(); } }
    }
    public Task PageAsync(int delta) => Run(async () => { page = Math.Max(0, page + delta); await RefreshList(); });

    public Task OpenDetailAsync() => Run(async () =>
    {
        if (SelectedItem is null) throw new ArgumentException("Selecciona un artículo.");
        detailId = SelectedItem.Id; historyPage = 0; screen = "detail";
        await RefreshDetail();
    });

    public void Back()
    {
        if (!Ready) return;
        CancelSearch();
        if (IsDetail) { screen = "list"; detailId = null; Detail = null; History = []; ProductLinks = []; }
        else { screen = detailId is null ? "list" : "detail"; pendingOperationId = null; operation = ""; }
        Message = ""; Notify();
        if (IsList && appliedFilter != (Search, SelectedKindFilter?.Value, SelectedActiveFilter?.Value)) QueueSearch();
    }

    public void NewItem()
    {
        if (!Ready || !CanConfigure) return;
        CancelSearch();
        editingItem = false; ItemCode = ""; ItemName = ""; SelectedItemKind = ItemKinds[0];
        SelectedDimension = Dimensions[0]; screen = "item"; Message = ""; Notify();
    }

    public void EditItem()
    {
        if (!Ready || !CanConfigure || Detail is null) return;
        editingItem = true; ItemCode = Detail.Item.Code; ItemName = Detail.Item.Name;
        SelectedItemKind = ItemKinds.Single(x => x.Value == Detail.Item.Kind);
        SelectedDimension = Dimensions.Single(x => x.Value == Detail.Item.Dimension);
        screen = "item"; Message = ""; Notify();
    }

    public Task SaveItemAsync() => Run(async () =>
    {
        if (!IsItemEditor) return;
        InventoryItem item;
        if (editingItem)
            item = await Write(() => service.UpdateInventoryItemMetadata(detailId!.Value, ItemName,
                SelectedItemKind.Value, SelectedDimension.Value));
        else
            item = await Write(() => service.CreateInventoryItem(ItemCode, ItemName,
                SelectedItemKind.Value, SelectedDimension.Value));
        detailId = item.Id; screen = "detail"; MessageIsSuccess = true; Message = "Artículo guardado.";
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    });

    public Task ToggleActiveAsync() => Run(async () =>
    {
        if (Detail is null) return;
        try { await Write(() => service.SetInventoryItemActive(Detail.Item.Id, !Detail.Item.IsActive)); }
        catch (InventoryApplicationException error) when (error.Error == InventoryError.InsufficientStock)
        { writeStarted = false; throw new ArgumentException("No se puede desactivar un artículo con existencia."); }
        MessageIsSuccess = true; Message = Detail.Item.IsActive ? "Artículo desactivado." : "Artículo activado.";
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    });

    public void BeginOperation(string kind)
    {
        if (!Ready || Detail is null) return;
        if (kind is not ("opening" or "receipt" or "waste" or "count")) return;
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
        PersistedInventoryOperation? confirmed = null;
        if (operation == "opening")
            confirmed = await Write(() => service.RegisterOpeningBalance(new(operationId, item, location, quantity, unit, Optional(Reference))));
        else if (operation == "receipt")
            confirmed = await Write(() => service.ReceiveInventory(new(operationId, item, location, quantity, unit, Optional(Reference), Optional(Note))));
        else if (operation == "waste")
            confirmed = await Write(() => service.RecordWaste(new(operationId, item, location, quantity, unit,
                SelectedWasteReason.Value, Optional(Note))));
        else
        {
            var result = await Write(() => service.AdjustToPhysicalCount(new(operationId, item, location,
                quantity, unit, SelectedAdjustmentReason.Value, Optional(Note))));
            if (!result.Changed) Message = "El conteo coincide con la existencia registrada. No se realizó ningún movimiento.";
            confirmed = result.Operation;
        }
        if (confirmed is not null)
        {
            var movement = confirmed.Movements.Single();
            var label = IsCount ? "Conteo físico registrado" : IsWaste ? "Merma registrada" : IsReceipt ? "Entrada registrada" : "Saldo inicial registrado";
            Message = $"{label}: {SignedQuantity(movement.DeltaAtomic, movement.Dimension)}. Existencia resultante: {InventoryDisplay.Format(movement.BalanceAfterAtomic, movement.Dimension)}.";
            if (confirmed.ExternalReference is { } externalReference) Message += $" Referencia: {externalReference}.";
        }
        pendingOperationId = null; operation = ""; screen = "detail";
        if (string.IsNullOrEmpty(Message)) Message = "Operación de inventario confirmada.";
        MessageIsSuccess = true;
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    }, refreshOnConcurrency: true);

    public void NewUnit()
    {
        if (!Ready || !CanConfigure || Detail is null) return;
        UnitCode = ""; UnitName = ""; UnitFactor = ""; screen = "unit"; Message = ""; Notify();
    }

    public Task SaveUnitAsync() => Run(async () =>
    {
        if (!IsUnitEditor || detailId is null || Detail is null) return;
        var factor = InventoryDisplay.ParseCapture(UnitFactor, Detail.Item.Dimension);
        await Write(() => service.AddInventoryItemUnit(detailId.Value, UnitCode, UnitName, factor));
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
        await Write(() => service.LinkProductToInventoryItem(SelectedProduct.Product.Id, detailId.Value, quantity));
        screen = "detail"; MessageIsSuccess = true; Message = "Producto asociado. Descontará existencias al confirmar la venta cuando el consumo automático esté ACTIVO. También requiere que el producto controle inventario. Esta asociación no activa el consumo automático.";
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    });

    public Task UnlinkAsync() => Run(async () =>
    {
        if (SelectedLink is null) throw new ArgumentException("Selecciona una asociación.");
        await Write(() => { service.UnlinkProductFromInventoryItem(SelectedLink.ProductId); return true; });
        MessageIsSuccess = true; Message = "Producto desvinculado.";
        await RefreshList(preserveMessage: true); await RefreshDetail(preserveMessage: true);
    });

    public Task HistoryPageAsync(int delta) => Run(async () =>
    {
        historyPage = Math.Max(0, historyPage + delta); await RefreshHistory();
    });

    public void Forget()
    {
        CancelSearch();
        SearchChoices = []; Items = []; Detail = null; History = []; ProductLinks = []; Products = []; SelectedItem = null; detailId = null;
        pendingOperationId = null; screen = "list"; if (!RecoveryRequired) Message = ""; CanView = CanConfigure = CanReceive = CanWaste = CanAdjust = false;
        Notify();
    }

    private async Task RefreshList(bool preserveMessage = false)
    {
        await RefreshSearchChoices();
        var result = await Task.Run(() => service.List(new(Search, SelectedKindFilter?.Value,
            SelectedActiveFilter?.Value, page, 50)));
        ApplyList(result);
        if (!preserveMessage) Message = "";
    }

    private void ApplyList(InventoryListPage result)
    {
        var selection = SelectedItem?.Id;
        appliedFilter = (Search, SelectedKindFilter?.Value, SelectedActiveFilter?.Value);
        Items = result.Items.Select(x => new InventoryRowView(x)).ToArray();
        SelectedItem = Items.FirstOrDefault(x => x.Id == selection);
        PageLabel = $"Página {page + 1} · {result.Total} artículos · hasta {result.PageSize} por página";
        HasNext = (page + 1) * result.PageSize < result.Total;
    }

    private async Task RefreshProductLinks()
    {
        var links = Detail?.ProductLinks.Where(x => x.Active).ToArray() ?? [];
        var names = new Dictionary<Guid, ProductRow>();
        if (links.Length > 0)
        {
            try
            {
                var needed = links.Select(x => x.ProductId).ToHashSet();
                for (var index = 0; ; index++)
                {
                    var products = await Task.Run(() => catalog.Products(new(IncludeInactive: true, Page: index)));
                    foreach (var product in products.Items.Where(x => needed.Contains(x.Product.Id))) names[product.Product.Id] = product;
                    if (names.Count == needed.Count || (index + 1) * products.PageSize >= products.Total || products.Items.Count == 0) break;
                }
            }
            catch (AccessDeniedException) { log("inventory.ui.product_names.unavailable", "permission"); }
        }
        ProductLinks = links.Select(x => names.TryGetValue(x.ProductId, out var product)
            ? new InventoryLinkView(x, product.Name, string.IsNullOrWhiteSpace(product.Sku) ? "—" : product.Sku)
            : new InventoryLinkView(x)).ToArray();
    }

    private bool TryPreview(out InventoryOperationPreview? preview, out string error)
    {
        preview = null; error = "";
        if (!IsOperationEditor || Detail is null) return false;
        try
        {
            if (string.IsNullOrWhiteSpace(Quantity)) throw new ArgumentException("Escribe una cantidad para ver el resultado.");
            if (SelectedUnit is null) throw new ArgumentException("Selecciona una unidad o presentación.");
            if (IsCount && ReconciliationWarning) throw new ArgumentException("Revisa la inconsistencia de inventario antes de registrar el conteo.");
            if ((IsWaste && SelectedWasteReason.Value == WasteReason.Other || IsCount && SelectedAdjustmentReason.Value == AdjustmentReason.Other) && string.IsNullOrWhiteSpace(Note))
                throw new ArgumentException("Escribe una observación para el motivo Otro.");
            if (IsWaste) _ = new WasteReasonSelection(SelectedWasteReason.Value, Optional(Note));
            if (IsCount) _ = new AdjustmentReasonSelection(SelectedAdjustmentReason.Value, Optional(Note));
            if (Note.Length > 500) throw new ArgumentException("La observación admite hasta 500 caracteres.");
            if (ShowsReference && Reference.Length > 160) throw new ArgumentException("La referencia admite hasta 160 caracteres.");
            preview = InventoryOperationPreview.Calculate(Detail, operation, ParseDecimal(Quantity), SelectedUnit.Code);
            return true;
        }
        catch (InventoryApplicationException exception) { error = ErrorMessage(exception.Error); }
        catch (ArgumentException exception) { error = exception.Message; }
        catch (OverflowException) { error = "La cantidad ingresada excede el límite permitido."; }
        return false;
    }

    public static string SignedQuantity(long value, InventoryDimension dimension) => value == 0
        ? $"Sin cambio · {InventoryDisplay.Format(0, dimension)}"
        : $"{(value > 0 ? "+" : "−")}{InventoryDisplay.Format(value > 0 ? value : checked(-value), dimension)}";

    private async Task RefreshDetail(bool preserveMessage = false)
    {
        if (detailId is null) return;
        Detail = await Task.Run(() => service.Detail(detailId.Value));
        UnitChoices = BuildUnits(Detail); SelectedLink = null;
        await RefreshProductLinks();
        await RefreshHistory();
        var status = (await Task.Run(service.ReconciliationStatus))
            .FirstOrDefault(x => x.InventoryItemId == detailId.Value && x.InventoryLocationId == Detail.MainLocation.Id);
        ReconciliationWarning = status is { Consistent: false };
        ReconciliationMessage = ReconciliationWarning
            ? "ADVERTENCIA: La existencia registrada no coincide con el historial de movimientos. No se realizó ninguna reparación."
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
        if (!Ready) return;
        CancelSearch();
        writeStarted = writeConfirmed = false;
        Busy = true; Message = ""; MessageIsSuccess = false; Notify();
        try { await action(); log("inventory.ui.completed", null); }
        catch (Exception error) when (writeConfirmed)
        {
            ClearFailedDetail();
            Message = "Operación confirmada, pero no se pudo actualizar la consulta. No repitas el guardado; vuelve a buscar el artículo para revisar el resultado.";
            log("inventory.ui.refresh.failed_after_commit", error.GetType().Name);
        }
        catch (InventoryApplicationException error)
        {
            if (writeStarted && error.Error == InventoryError.InconsistentBalance)
            {
                RequireRecovery(error);
                return;
            }
            Message = ErrorMessage(error.Error); log("inventory.ui.rejected", error.Error.ToString());
            if (refreshOnConcurrency && error.Error == InventoryError.ConcurrencyConflict)
            {
                try { await RefreshDetail(preserveMessage: true); }
                catch (Exception refreshError) { ReadFailed(refreshError); }
            }
            else if (!writeStarted) ClearFailedDetail();
        }
        catch (ArgumentException error) when (!writeStarted) { Message = error.Message; }
        catch (OverflowException) when (!writeStarted) { Message = "La cantidad ingresada excede el límite permitido."; }
        catch (Exception error)
        {
            if (writeStarted) RequireRecovery(error);
            else ReadFailed(error);
        }
        finally { Busy = false; Notify(); }
    }

    private async Task<T> Write<T>(Func<T> action)
    {
        // A service return confirms completion; any unexpected exception before that return
        // cannot safely be interpreted as rollback from Presentation.
        writeStarted = true;
        var result = await Task.Run(action);
        writeConfirmed = true;
        return result;
    }

    private void ClearFailedDetail()
    {
        screen = "list"; Detail = null; detailId = null; History = []; ProductLinks = []; SelectedLink = null;
        pendingOperationId = null; operation = ""; MessageIsSuccess = false;
    }

    private void ReadFailed(Exception error)
    {
        ClearFailedDetail();
        Message = "No se pudo cargar la consulta de inventario. Vuelve a buscar el artículo e intenta abrir su detalle.";
        log("inventory.ui.read.failed", error.GetType().Name);
    }

    private void RequireRecovery(Exception error)
    {
        RecoveryRequired = true; MessageIsSuccess = false;
        Message = "No se pudo verificar si la operación de inventario quedó confirmada. No la repitas. Reinicia la aplicación y revisa el artículo y su historial antes de continuar.";
        log("inventory.ui.write.uncertain", error.GetType().Name);
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
    public static string DimensionLabel(InventoryDimension value) => UiLabels.Dimension(value);
    public static string MovementLabel(InventoryMovementKind value) => value switch
        { InventoryMovementKind.OpeningBalance => "Saldo inicial", InventoryMovementKind.Receipt => "Entrada",
          InventoryMovementKind.AdjustmentGain => "Ajuste positivo", InventoryMovementKind.AdjustmentLoss => "Ajuste negativo",
          InventoryMovementKind.Waste => "Merma", InventoryMovementKind.SaleConsumption => "Venta",
          InventoryMovementKind.ProductionConsumption => "Consumo de producción", InventoryMovementKind.ProductionOutput => "Salida de producción",
          InventoryMovementKind.Return => "Devolución", InventoryMovementKind.Transfer => "Transferencia", _ => "Movimiento no disponible" };
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
