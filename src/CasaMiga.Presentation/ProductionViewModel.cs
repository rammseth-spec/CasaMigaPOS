using System.Collections.ObjectModel;
using System.ComponentModel;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record ProductionProfileChoice(Guid? VersionId,Guid ProductId,Guid? FinishedItemId,string ProductName,string ProfileName,int Version,string Status,string Detail)
{
    public bool Ready=>Status=="LISTO PARA PRODUCIR"&&VersionId.HasValue;
    public string VersionLabel=>Version>0?$"Versión {Version}":"—";
    public override string ToString()=>Ready?$"{ProductName} · {ProfileName} · Versión {Version}":$"{ProductName} · {Status}";
}
public sealed class ProductionPlanEdit : INotifyPropertyChanged
{
    private ProductionProfileChoice? profile; private string planned="1";
    public ProductionProfileChoice? Profile{get=>profile;set{if(profile==value)return;profile=value;PropertyChanged?.Invoke(this,new(nameof(Profile)));}}
    public string Planned{get=>planned;set{if(planned==value)return;planned=value;PropertyChanged?.Invoke(this,new(nameof(Planned)));}}
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed record ProductionRequirementRow(string Name,string Required,string Available,string Shortage,string State);
public sealed record ProductionFormulaRow(string Name,int Version,string Required)
{
    public string VersionLabel=>$"Versión {Version}";
}
public sealed record ProductionHistoryRow(Guid Id,string Date,string State,string Creator,string Terminal,string Products,string Result,string OutputResult);
public sealed class ProductionActualEdit:INotifyPropertyChanged
{
    private string actual="0";
    public Guid ProductId{get;init;} public string ProductName{get;init;}=""; public long Planned{get;init;}
    public string Actual{get=>actual;set{if(actual==value)return;actual=value;PropertyChanged?.Invoke(this,new(nameof(Actual)));PropertyChanged?.Invoke(this,new(nameof(Difference)));}}
    public string Difference
    {
        get
        {
            if(!long.TryParse(Actual,out var value)||value<0)return "Escribe un entero igual o mayor que cero";
            if(value==Planned)return "Igual a lo planeado";
            var difference=value>Planned?value-Planned:Planned-value;
            return $"{difference} {(difference==1?"pieza":"piezas")} {(value>Planned?"más":"menos")} de lo planeado";
        }
    }
    public bool IsValid=>long.TryParse(Actual,out var value)&&value>=0;
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class ProductionAlternativeEdit : INotifyPropertyChanged
{
    public Guid FormulaVersionId{get;init;} public Guid GroupId{get;init;} public string Label{get;init;}="";
    private ProductionAlternativeOption? selected;
    public IReadOnlyList<ProductionAlternativeOption> Options{get;init;}=[]; public ProductionAlternativeOption? Selected{get=>selected;set{if(selected==value)return;selected=value;PropertyChanged?.Invoke(this,new(nameof(Selected)));}}
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed record ProductionAlternativeOption(Guid Id,string Label,bool Eligible)
{
    public override string ToString()=>Eligible?Label:$"{Label} · no disponible";
}
public sealed record ProductionWasteReasonChoice(WasteReason Value,string Label)
{
    public override string ToString()=>Label;
}

public sealed class ProductionViewModel(ProductionService service,RecipeService recipes,InventoryService inventory,
    CatalogService catalog,UserSession user,Guid terminalId,Action<string,string?> log):INotifyPropertyChanged
{
    private string screen="home";private StoredProductionBatch? batch;private Guid? createBatchId,createOperationId;
    private IReadOnlyDictionary<Guid,RecipeItemChoice> items=new Dictionary<Guid,RecipeItemChoice>();
    public bool Busy{get;private set;} public bool Ready=>!Busy; public bool CanView{get;private set;} public bool CanCreate{get;private set;} public bool CanConfirm{get;private set;}
    public bool IsHome=>screen=="home";public bool IsBuilder=>screen=="builder";public bool IsDrafts=>screen=="drafts";public bool IsHistory=>screen=="history";public bool IsDetail=>screen=="detail";public bool IsWaste=>screen=="waste";
    public bool DetailIsDraft=>batch?.Status=="DRAFT"; public bool DetailIsConfirmed=>batch?.Status=="CONFIRMED";
    private bool OwnDraft=>DetailIsDraft&&CanConfirm&&batch?.CreatedBy==user.Current?.Id&&batch?.TerminalId==terminalId;
    public bool ActualsAreValid=>Actuals.Count>0&&Actuals.All(x=>x.IsValid);
    public bool CanConfirmDetail=>OwnDraft&&ActualsAreValid;
    public string ConfirmationRestriction=>!DetailIsDraft?"Consulta histórica de solo lectura.":!OwnDraft?"Sólo el usuario y terminal que crearon este borrador pueden registrarlo.":!ActualsAreValid?"Captura una cantidad real entera, igual o mayor que cero, para cada producto.":"El plan ya quedó fijado. Revisa el resultado real antes de registrarlo.";
    public string PlannedHelpText=>"La cantidad planeada es el número entero de piezas terminadas que se pretende elaborar. Esta cantidad determina las preparaciones y los insumos que se consumirán al registrar. Ejemplo sintético: si planeas 5 piezas, las necesidades se calculan para esas 5 piezas.";
    public string PreviewHelpText=>"Calcular necesidades sólo consulta el plan y el inventario: no reserva ni descuenta existencias, no registra movimientos ni modifica el ledger. Necesario es la cantidad que requiere este plan. Disponible es la existencia actual. Faltante es la diferencia cuando el inventario no alcanza. Estado indica si hay inventario suficiente o una condición que impide continuar. Ejemplo sintético: Necesario 500 g; Disponible 350 g; Faltante 150 g; Estado Faltante. El stock se valida de nuevo al registrar la producción.";
    public string ActualHelpText=>"La cantidad real es el número de piezas terminadas obtenidas. El inventario recibe esa cantidad real, pero los insumos se descuentan según el plan bloqueado. Ejemplo sintético: 5 planeadas y 4 reales consumen los insumos de 5 y agregan 4 terminados. Registrar cero consume los insumos planeados y no genera producto terminado.";
    public string Message{get;private set;}="";public bool MessageIsSuccess{get;private set;}
    public IReadOnlyList<ProductionProfileChoice> Profiles{get;private set;}=[];
    public IReadOnlyList<ProductionProfileChoice> FilteredProfiles{get;private set;}=[];
    private string productSearch="";private ProductionProfileChoice? selectedProductChoice;
    public string ProductSearch{get=>productSearch;set{if(productSearch==value)return;productSearch=value;FilterProducts();}}
    public ProductionProfileChoice? SelectedProductChoice{get=>selectedProductChoice;set{if(selectedProductChoice==value)return;selectedProductChoice=value;Notify();}}
    private ProductionPlanEdit? selectedPlan; private ProductionHistoryRow? selectedRow;
    private bool trackingPlan; private readonly HashSet<ProductionPlanEdit> trackedPlan=[];
    public ObservableCollection<ProductionPlanEdit> Plan{get;}=[];public ProductionPlanEdit? SelectedPlan{get=>selectedPlan;set{if(selectedPlan==value)return;selectedPlan=value;Notify();}}
    public bool HasProfiles=>Profiles.Any(x=>x.Ready);
    public bool CanAddSelectedProduct=>Ready&&IsBuilder&&SelectedProductChoice is not null&&!Plan.Any(x=>x.Profile?.ProductId==SelectedProductChoice.ProductId);
    private bool ValidPlan=>Plan.Count>0&&Plan.All(x=>x.Profile is {Ready:true}&&Profiles.Contains(x.Profile)&&long.TryParse(x.Planned,out var count)&&count>0)&&Plan.Select(x=>x.Profile!.ProductId).Distinct().Count()==Plan.Count;
    public string PlanBlockingMessage
    {
        get
        {
            if(Plan.Count==0)return "Agrega al menos un producto al plan.";
            var blocked=Plan.FirstOrDefault(x=>x.Profile is not {Ready:true});
            if(blocked?.Profile is {} product)return $"{product.ProductName}: {product.Detail}";
            if(blocked is not null)return "Selecciona un producto para cada fila.";
            if(Plan.Any(x=>!long.TryParse(x.Planned,out var count)||count<=0))return "Cada cantidad planeada debe ser un entero mayor que cero.";
            if(Plan.Select(x=>x.Profile!.ProductId).Distinct().Count()!=Plan.Count)return "No repitas el mismo producto en el plan.";
            return "Plan listo para calcular.";
        }
    }
    public bool CanCalculate=>Ready&&CanCreate&&IsBuilder&&ValidPlan;
    public bool CanCreateDraft=>CanCalculate&&PreviewReady&&PreviewConfirmable;
    public bool CanRemoveOutput=>Ready&&IsBuilder&&SelectedPlan is not null&&Plan.Contains(SelectedPlan);
    public bool CanOpenDetail=>Ready&&SelectedRow is not null;
    // Visible explanations for actions that are disabled; tooltips alone are not discoverable on touch screens.
    public string AddProductHint=>SelectedProductChoice is not {} choice?"Selecciona un producto de la lista para agregarlo al plan.":
        Plan.Any(x=>x.Profile?.ProductId==choice.ProductId)?$"{choice.ProductName} ya está en el plan; ajusta sus piezas en «Plan actual».":
        !choice.Ready?$"{choice.ProductName}: {choice.Detail}":$"{choice.ProductName} está listo para agregarse.";
    public string RemoveOutputHint=>Plan.Count==0?"El plan está vacío.":"Selecciona una fila del plan para quitarla.";
    public string CreateDraftHint=>!ValidPlan?"Para preparar el registro, primero completa un plan válido y calcula sus necesidades.":
        !PreviewReady?"Pulsa «Calcular necesidades» para revisar insumos y existencias antes de preparar el registro.":
        !PreviewConfirmable?"No se puede preparar el registro: corrige lo indicado arriba y vuelve a calcular.":"Al preparar el registro, el plan quedará fijado como borrador.";
    public string OpenDetailHint=>SelectedRow is null?"Selecciona una producción de la lista para ver su detalle.":"";
    public string WasteHint=>!CanRecordWaste?"No tienes permiso para registrar merma.":SelectedWasteProduct?.FinishedItemId is null?"Selecciona el producto terminado.":
        !long.TryParse(WasteQuantity,out var pieces)||pieces<=0?"Escribe un número entero de piezas mayor que cero.":SelectedWasteReason is null?"Selecciona el motivo.":
        SelectedWasteReason.Value==WasteReason.Other&&string.IsNullOrWhiteSpace(WasteNote)?"Con el motivo «Otro» la observación es obligatoria.":"";
    public string EmptyRowsMessage=>IsDrafts?"No hay producciones en borrador.":"Aún no hay producciones confirmadas.";
    public ObservableCollection<ProductionAlternativeEdit> Alternatives{get;}=[];
    public IReadOnlyList<ProductionFormulaRow> FormulaRows{get;private set;}=[];public IReadOnlyList<ProductionRequirementRow> RequirementRows{get;private set;}=[];
    public bool PreviewReady{get;private set;} public bool PreviewConfirmable{get;private set;} public string PreviewState{get;private set;}="";
    public IReadOnlyList<ProductionHistoryRow> Rows{get;private set;}=[];public ProductionHistoryRow? SelectedRow{get=>selectedRow;set{if(selectedRow==value)return;selectedRow=value;Notify();}}
    public ObservableCollection<ProductionActualEdit> Actuals{get;}=[];
    public bool CanRecordWaste{get;private set;} public IReadOnlyList<ProductionProfileChoice> WasteProducts=>Profiles.Where(x=>x.Ready&&x.FinishedItemId.HasValue).ToArray();
    private ProductionProfileChoice? selectedWasteProduct;private string wasteQuantity="1";private ProductionWasteReasonChoice? selectedWasteReason;private string wasteNote="";
    public ProductionProfileChoice? SelectedWasteProduct{get=>selectedWasteProduct;set{selectedWasteProduct=value;Notify();}}
    public string WasteQuantity{get=>wasteQuantity;set{wasteQuantity=value;Notify();}}
    public IReadOnlyList<ProductionWasteReasonChoice> WasteReasons{get;}=[new(WasteReason.Damaged,"Producto dañado"),new(WasteReason.Expired,"Producto vencido"),new(WasteReason.ProductionError,"Error de producción"),new(WasteReason.Other,"Otro")];
    public ProductionWasteReasonChoice? SelectedWasteReason{get=>selectedWasteReason;set{selectedWasteReason=value;Notify();}}
    public string WasteNote{get=>wasteNote;set{wasteNote=value;Notify();}}
    public bool CanSaveWaste=>Ready&&IsWaste&&CanRecordWaste&&SelectedWasteProduct?.FinishedItemId is not null&&long.TryParse(WasteQuantity,out var value)&&value>0&&SelectedWasteReason is not null&&(SelectedWasteReason.Value!=WasteReason.Other||!string.IsNullOrWhiteSpace(WasteNote));
    public string DetailHeader=>batch is null?"":$"Producción · {(batch.Status=="DRAFT"?"Borrador":"Confirmada")}";
    public string DetailContext=>batch is null?"":$"Creada: {batch.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}\nCreada por: {batch.CreatedByName}\nTerminal: {batch.TerminalName}"+(batch.ConfirmedAt is {} confirmed?$"\nRegistrada: {confirmed.ToLocalTime():dd/MM/yyyy HH:mm}":"");
    public string DetailPlanTitle=>"Plan de producción";
    public string DetailFormulasTitle=>"Preparaciones del plan";
    public string DetailInputsTitle=>DetailIsDraft?"Ingredientes que se descontarán":"Ingredientes consumidos";
    public IReadOnlyList<string> DetailProducts=>batch?.Outputs.Select(x=>$"{x.ProductName}: {x.PlannedCount} {(x.PlannedCount==1?"pieza planeada":"piezas planeadas")} · {(x.ActualCount is {} actual?$"{actual} {(actual==1?"pieza real":"piezas reales")}":"resultado pendiente")}").ToArray()??[];
    public IReadOnlyList<string> DetailFormulas=>batch?.Formulas.Select(x=>$"{x.Name}: {InventoryDisplay.FormatProduction(x.Required.AtomicUnits,x.Required.Dimension)} requeridos").ToArray()??[];
    public IReadOnlyList<string> DetailInputs=>batch is null?[]:InputLines(batch.Inputs);
    public bool TotalZero=>Actuals.Count>0&&Actuals.All(x=>long.TryParse(x.Actual,out var v)&&v==0);
    public event PropertyChangedEventHandler? PropertyChanged;private void Notify()=>PropertyChanged?.Invoke(this,new(null));

    public Task LoadAsync()=>Run(async()=>
    {
        CanView=await Task.Run(()=>catalog.Can("production.view"));CanCreate=await Task.Run(()=>catalog.Can("production.create"));CanConfirm=await Task.Run(()=>catalog.Can("production.confirm"));CanRecordWaste=await Task.Run(()=>inventory.Can("inventory.waste"));if(!CanView)throw new AccessDeniedException();
        await LoadChoices();screen="home";
    });
    private async Task LoadChoices()
    {
        var itemRows=new List<InventoryListRow>();for(int page=0;;page++){var r=await Task.Run(()=>inventory.List(new(Page:page,PageSize:100)));itemRows.AddRange(r.Items);if(itemRows.Count>=r.Total)break;}
        items=itemRows.ToDictionary(x=>x.InventoryItemId,x=>new RecipeItemChoice(x.InventoryItemId,x.Name,x.Dimension));
        var products=new List<ProductRow>();for(int page=0;;page++){var r=await Task.Run(()=>catalog.Products(new(Page:page)));products.AddRange(r.Items);if(products.Count>=r.Total)break;}
        var profiles=await Task.Run(()=>recipes.ListProfiles(200));var list=new List<ProductionProfileChoice>();
        foreach(var product in products.Where(x=>x.Product.IsActive).OrderBy(x=>x.Product.Name,StringComparer.CurrentCultureIgnoreCase))
        {
            var candidates=profiles.Where(x=>x.ProductId==product.Product.Id).ToArray();
            if(candidates.Length==0){list.Add(new(null,product.Product.Id,null,product.Product.Name,"",0,"SIN RECETA","Este producto no tiene perfil productivo. Configura y publica su receta antes de producirlo."));continue;}
            ProductionProfileChoice? ready=null;string reason="El perfil productivo no tiene una versión activa publicada.";Guid? finished=candidates[0].FinishedItemId;
            foreach(var summary in candidates)
            {
                finished=summary.FinishedItemId;
                if(summary.ActiveVersionId is not {} id)continue;
                var stored=await Task.Run(()=>recipes.GetProfileVersion(id));
                if(stored is null||stored.Value.State!=ProductionVersionState.Published){reason="La versión activa del perfil no está publicada.";continue;}
                var version=stored.Value;var output=itemRows.FirstOrDefault(x=>x.InventoryItemId==version.FinishedInventoryItemId);
                if(output is null||!output.Active||output.Kind!=InventoryItemKind.FinishedGood||output.Dimension!=InventoryDimension.Count){reason="El producto terminado debe ser un artículo activo de inventario contado en piezas.";continue;}
                var missingFormula=false;var invalidFormulaIngredients=false;
                // También las preparaciones que usan otras preparaciones (la masa dulce dentro de la masa enriquecida).
                var pendingFormulas=new Queue<Guid>(version.Formulas.Select(x=>x.FormulaVersionId));var checkedFormulas=new HashSet<Guid>();
                while(pendingFormulas.TryDequeue(out var formulaVersionId))
                {
                    if(!checkedFormulas.Add(formulaVersionId))continue;
                    var formula=await Task.Run(()=>recipes.GetFormulaVersion(formulaVersionId));
                    if(formula is null||formula.Value.State!=ProductionVersionState.Published){missingFormula=true;break;}
                    foreach(var preparation in formula.Value.Preparations)pendingFormulas.Enqueue(preparation.FormulaVersionId);
                    if(formula.Value.Ingredients.Any(x=>!itemRows.Any(i=>i.InventoryItemId==x.InventoryItemId&&i.Active))||
                        formula.Value.Alternatives.Any(group=>!group.Options.Any(option=>itemRows.Any(i=>i.InventoryItemId==option.Ingredient.InventoryItemId&&i.Active))))
                        invalidFormulaIngredients=true;
                }
                if(missingFormula){reason="Una preparación requerida no existe o no está publicada.";continue;}
                if(invalidFormulaIngredients){reason="Una preparación no tiene todos sus insumos obligatorios o una alternativa activa.";continue;}
                var ingredientIds=version.DirectIngredients.Select(x=>x.Ingredient.InventoryItemId).Distinct().ToArray();
                if(ingredientIds.Any(id2=>!itemRows.Any(x=>x.InventoryItemId==id2&&x.Active))){reason="Un ingrediente directo no existe o está inactivo.";continue;}
                ready=new(id,product.Product.Id,version.FinishedInventoryItemId,product.Product.Name,version.Name,version.Version,"LISTO PARA PRODUCIR","Receta y salida configuradas.");break;
            }
            list.Add(ready??new(null,product.Product.Id,finished,product.Product.Name,"",0,"RECETA INCOMPLETA",reason));
        }
        Profiles=list;FilterProducts();
    }
    private void FilterProducts(){var term=(ProductSearch??"").Trim();FilteredProfiles=Profiles.Where(x=>term.Length==0||x.ProductName.Contains(term,StringComparison.CurrentCultureIgnoreCase)||x.ProfileName.Contains(term,StringComparison.CurrentCultureIgnoreCase)||x.Status.Contains(term,StringComparison.OrdinalIgnoreCase)).ToArray();if(SelectedProductChoice is not null&&!FilteredProfiles.Contains(SelectedProductChoice))SelectedProductChoice=null;Notify();}
    public void NewProduction(){if(!Ready||!CanCreate)return;TrackPlan();Plan.Clear();SelectedPlan=null;SelectedProductChoice=null;ProductSearch="";Alternatives.Clear();FormulaRows=[];RequirementRows=[];PreviewReady=false;PreviewConfirmable=false;createBatchId=Guid.NewGuid();createOperationId=Guid.NewGuid();screen="builder";Message="";Notify();}
    public void AddOutput(){if(SelectedProductChoice is null){if(Ready&&CanCreate&&IsBuilder&&Plan.All(x=>x.Profile is not null))Plan.Add(new());return;}if(!CanAddSelectedProduct)return;var empty=Plan.FirstOrDefault(x=>x.Profile is null);if(empty is not null){empty.Profile=SelectedProductChoice;empty.Planned="1";SelectedPlan=empty;}else{Plan.Add(new(){Profile=SelectedProductChoice,Planned="1"});SelectedPlan=Plan[^1];}SelectedProductChoice=null;Notify();}
    public void RemoveOutput(){if(!CanRemoveOutput)return;Plan.Remove(SelectedPlan!);SelectedPlan=null;}
    private void TrackPlan()
    {
        if(trackingPlan)return;trackingPlan=true;
        Plan.CollectionChanged+=(_,_)=>
        {
            foreach(var row in trackedPlan)row.PropertyChanged-=PlanChanged;
            trackedPlan.Clear();foreach(var row in Plan){trackedPlan.Add(row);row.PropertyChanged+=PlanChanged;}
            InvalidatePreview();
        };
    }
    private void PlanChanged(object? sender,PropertyChangedEventArgs args)=>InvalidatePreview();
    private void InvalidatePreview(){PreviewReady=false;PreviewConfirmable=false;PreviewState="";FormulaRows=[];RequirementRows=[];Notify();}
    private PrepareProductionCommand Command()
    {
        var lines=Plan.Select(x=>{if(x.Profile is not {Ready:true} profile||profile.VersionId is not {} version)throw new ArgumentException(x.Profile?.Detail??"Selecciona un producto listo para producir.");if(!long.TryParse(x.Planned,out var count)||count<=0)throw new ArgumentException("La cantidad planeada debe ser un entero mayor que cero.");return new RequestedProductionOutput(version,count);}).ToArray();
        if(Plan.Select(x=>x.Profile!.ProductId).Distinct().Count()!=lines.Length)throw new ArgumentException("No repitas el mismo producto en el plan.");
        var choices=Alternatives.Where(x=>x.Selected is {Eligible:true}).Select(x=>new ProductionAlternativeSelection(x.FormulaVersionId,x.GroupId,x.Selected!.Id)).ToArray();return new(lines,choices);
    }
    public Task PreviewAsync()=>Run(async()=>
    {
        var command=Command();var result=await Task.Run(()=>service.PrepareProduction(command));
        FormulaRows=result.Planning.RequiredFormulas.Select(x=>{var f=recipes.GetFormulaVersion(x.FormulaVersionId)!.Value;return new ProductionFormulaRow(f.Name,f.Version,InventoryDisplay.FormatProduction(x.Required.AtomicUnits,x.Required.Dimension));}).ToArray();
        RequirementRows=(result.Planning.Plan?.Ingredients??[]).Select(x=>{var item=items[x.InventoryItemId];var stock=result.Stock.Single(s=>s.ItemId==x.InventoryItemId);var shortage=result.Shortages.FirstOrDefault(s=>s.ItemId==x.InventoryItemId);var missing=shortage is null?"—":InventoryDisplay.FormatProduction(shortage.Required-(shortage.Available??0),stock.Dimension);return new ProductionRequirementRow(item.Name,InventoryDisplay.FormatProduction(x.Quantity.AtomicUnits,x.Quantity.Dimension),stock.Available is {} a?InventoryDisplay.FormatProduction(a,stock.Dimension):"Sin saldo inicial",missing,!stock.Active?"Inactivo":stock.Available is null?"Sin saldo inicial":shortage is null?"Suficiente":"Faltante");}).ToArray();
        Alternatives.Clear();int n=0;foreach(var formula in result.Planning.Plan?.Formulas??[]){foreach(var group in formula.Version.Alternatives){n++;var ineligible=result.Planning.IneligibleOptions.Where(x=>x.GroupId==group.Id).Select(x=>x.OptionId).ToHashSet();var options=group.PreferenceOrder.Concat(group.Options.Select(x=>x.Id)).Distinct().Select(id=>{var o=group.Options.Single(x=>x.Id==id);return new ProductionAlternativeOption(id,items[o.Ingredient.InventoryItemId].Name,!ineligible.Contains(id));}).ToArray();var selected=formula.Ingredients.FirstOrDefault(x=>x.GroupId==group.Id)?.OptionId;Alternatives.Add(new(){FormulaVersionId=formula.Version.Id,GroupId=group.Id,Label=$"Grupo {n} · {formula.Version.Name}",Options=options,Selected=options.FirstOrDefault(x=>x.Id==selected)});}}
        foreach(var choice in Alternatives)choice.PropertyChanged+=PlanChanged;
        PreviewReady=true;PreviewConfirmable=result.CanConfirm;PreviewState=result.CanConfirm?"Plan calculado. El stock mostrado es informativo y se validará de nuevo al registrar.":HumanProblems(result);Notify();
    });
    private static string HumanProblems(ProductionPreview value)
    {
        if(value.Planning.Issues.Any(x=>x.Code==ProductionError.NonRepresentableScale))return "No es posible calcular esta receta exactamente con las unidades configuradas. Revisa la cantidad planeada o la receta.";
        if(value.InactiveItems.Count>0)return "Hay insumos o productos terminados inactivos.";if(value.Shortages.Count>0)return "No hay existencia suficiente. Revisa todos los faltantes mostrados.";if(value.Stock.Any(x=>x.Available is null))return "Hay insumos o producto terminado sin saldo inicial.";return "El plan tiene datos pendientes de corregir.";
    }
    public Task CreateDraftAsync()=>Run(async()=>
    {
        if(!PreviewReady||!PreviewConfirmable)throw new ArgumentException("Calcula un preview válido antes de crear la producción.");var command=Command();
        var current=await Task.Run(()=>service.PrepareProduction(command));
        if(!current.CanConfirm)throw new ArgumentException(HumanProblems(current));
        batch=await Task.Run(()=>service.CreateProduction(new(createBatchId!.Value,createOperationId!.Value,command)));LoadDetail();screen="detail";MessageIsSuccess=true;Message="Producción preparada como borrador. El plan ya quedó fijado.";
    });
    public Task ShowDraftsAsync()=>LoadRows(true);public Task ShowHistoryAsync()=>LoadRows(false);
    private Task LoadRows(bool drafts)=>Run(async()=>
    {
        var ids=await Task.Run(()=>service.ListProductions(200));var batches=(await Task.WhenAll(ids.Select(id=>Task.Run(()=>service.GetProduction(id))))).Where(x=>x is not null).Cast<StoredProductionBatch>().Where(x=>drafts?x.Status=="DRAFT":x.Status=="CONFIRMED").ToArray();
        Rows=batches.Select(x=>new ProductionHistoryRow(x.Id,x.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),x.Status=="DRAFT"?"Borrador":"Confirmada",x.CreatedByName,x.TerminalName,string.Join(", ",x.Outputs.Select(o=>o.ProductName)),string.Join(" · ",x.Outputs.Select(o=>$"{o.PlannedCount} {(o.PlannedCount==1?"planeada":"planeadas")} · {(o.ActualCount is {} actual?$"{actual} {(actual==1?"real":"reales")}":"resultado pendiente")}")),string.Join(" · ",x.Outputs.Select(o=>o.ActualCount switch{null=>$"{o.ProductName}: resultado pendiente",0=>$"{o.ProductName}: sin producto terminado",var actual=>$"{o.ProductName}: {actual} {(actual==1?"pieza obtenida":"piezas obtenidas")}"})))).ToArray();screen=drafts?"drafts":"history";SelectedRow=null;
    });
    public Task OpenDetailAsync()=>Run(async()=>{var row=SelectedRow??throw new ArgumentException("Selecciona una producción.");batch=await Task.Run(()=>service.GetProduction(row.Id))??throw new ArgumentException("La producción ya no existe.");LoadDetail();screen="detail";});
    private void LoadDetail(){foreach(var row in Actuals)row.PropertyChanged-=ActualChanged;Actuals.Clear();foreach(var o in batch!.Outputs){var row=new ProductionActualEdit{ProductId=o.ProductId,ProductName=o.ProductName,Planned=o.PlannedCount,Actual=o.ActualCount?.ToString()??o.PlannedCount.ToString()};row.PropertyChanged+=ActualChanged;Actuals.Add(row);}Notify();}
    private void ActualChanged(object? sender,PropertyChangedEventArgs args){if(args.PropertyName==nameof(ProductionActualEdit.Actual))Notify();}
    public Task ConfirmAsync()=>Run(async()=>
    {
        if(batch is null||!CanConfirmDetail)throw new ArgumentException(ConfirmationRestriction);var actual=Actuals.Select(x=>{if(!long.TryParse(x.Actual,out var value)||value<0)throw new ArgumentException("La cantidad real debe ser un entero igual o mayor que cero.");return new ActualOutputLine(x.ProductId,value);}).ToArray();
        batch=await Task.Run(()=>service.ConfirmProduction(new(batch.Id,batch.OperationId,ProductionContract.Revision(batch),actual)));LoadDetail();MessageIsSuccess=true;Message="Producción registrada correctamente.";
    });
    public void OpenWaste()
    {
        if(!Ready||!CanRecordWaste)return;selectedWasteProduct=WasteProducts.FirstOrDefault();selectedWasteReason=WasteReasons.First();wasteQuantity="1";wasteNote="";screen="waste";Message="";Notify();
    }
    public Task SaveWasteAsync()=>Run(async()=>
    {
        if(!CanRecordWaste||SelectedWasteProduct?.FinishedItemId is not {} item||SelectedWasteReason is null||(SelectedWasteReason.Value==WasteReason.Other&&string.IsNullOrWhiteSpace(WasteNote)))throw new ArgumentException("Completa producto, piezas y motivo de la merma.");
        if(!long.TryParse(WasteQuantity,out var quantity)||quantity<=0)throw new ArgumentException("La merma debe ser un número entero de piezas mayor que cero.");
        await Task.Run(()=>inventory.RecordWaste(new(Guid.NewGuid(),item,InventoryLocationIds.Main,quantity,InventoryUnits.Piece.Code,SelectedWasteReason.Value,string.IsNullOrWhiteSpace(WasteNote)?null:WasteNote.Trim())));
        MessageIsSuccess=true;Message=$"Merma registrada: {quantity} {(quantity==1?"pieza":"piezas")} de {SelectedWasteProduct.ProductName}.";WasteQuantity="1";WasteNote="";
    });
    public void Back(){screen=screen=="detail"?"home":"home";Message="";Notify();}
    public static string ErrorMessage(ProductionException e)=>e.Code switch
    {
        ProductionFailure.InsufficientStock=>"No hay existencia suficiente para registrar esta producción.",ProductionFailure.InactiveItem=>"Un insumo o producto terminado ya no está activo.",ProductionFailure.NotInitialized=>"Un insumo o producto terminado todavía no tiene saldo inicial.",ProductionFailure.ConcurrencyConflict or ProductionFailure.RevisionConflict=>"La producción cambió o ya fue registrada desde otra operación. Actualiza la información.",ProductionFailure.IdempotencyConflict=>"La identidad de esta producción corresponde a datos diferentes.",ProductionFailure.UnresolvedPlan=>"El plan tiene problemas pendientes y no puede registrarse.",_=>"No se pudo completar la producción. Actualiza la información."
    };
    public static IReadOnlyList<string> InputLines(IEnumerable<StoredProductionInput> inputs)=>inputs
        .GroupBy(x=>(x.ItemId,x.Required.Dimension))
        .Select(group=>$"{group.First().Name}: {InventoryDisplay.FormatProduction(group.Sum(x=>x.Required.AtomicUnits),group.Key.Dimension)}"+
            (group.Any(x=>x.GroupId.HasValue)?" · alternativa seleccionada":"")).ToArray();
    private async Task Run(Func<Task> action){if(Busy)return;Busy=true;Message="";MessageIsSuccess=false;Notify();try{await action();}catch(AccessDeniedException){Message="No tienes permiso para esta operación de producción.";log("production.authorization.denied",null);}catch(ProductionException e){Message=ErrorMessage(e);if(e.Shortages.Count>0)Message+=" "+string.Join(" ",e.Shortages.Select(x=>$"Faltan {InventoryDisplay.FormatProduction(x.Required-(x.Available??0),items[x.ItemId].Dimension)} de {items[x.ItemId].Name}."));}catch(InventoryApplicationException e){Message=e.Error switch{InventoryError.InsufficientStock=>"No hay suficientes piezas terminadas para registrar la merma.",InventoryError.NotInitialized=>"El producto terminado todavía no tiene saldo inicial.",InventoryError.InventoryItemInactive=>"El producto terminado está inactivo.",InventoryError.InvalidNote=>e.Message,_=>"No se pudo registrar la merma de producto terminado."};}catch(ProductionDomainException){Message="Revisa las cantidades y configuración del plan.";}catch(ArgumentException e){Message=e.Message;}catch(Exception e){Message=$"No se completó la operación de producción (código: {e.GetType().Name}).";log("production.operation.failed",e.GetType().Name);}finally{Busy=false;Notify();}}
}
