using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record RecipeVersionRow(Guid Id,int Number,string State,string Name,long Revision,bool Active)
{
    public string VersionLabel=>$"Versión {Number}";
    public string DisplayState=>Active?"Activa · Publicada":State;
}
public sealed record RecipeFormulaRow(FormulaSummary Summary,string ActiveVersion,string Draft)
{ public string Code=>Summary.Code;public string Name=>Summary.Name; }
public sealed record RecipeProfileRow(ProfileSummary Summary,string Product,string FinishedItem,string ActiveVersion,string Draft);
public sealed record RecipeItemChoice(Guid Id,string Code,string DisplayName,InventoryDimension Dimension)
{
    public RecipeItemChoice(Guid id,string name,InventoryDimension dimension):this(id,"",name,dimension){}
    public string Name=>string.IsNullOrWhiteSpace(Code)?DisplayName:$"{Code} · {DisplayName}";
    public override string ToString()=>Name;
}
public sealed record RecipeFormulaChoice(Guid Id,string Name,InventoryDimension Dimension)
{
    public override string ToString()=>Name;
}
public sealed record RecipeProductChoice(Guid Id,string Name)
{
    public override string ToString()=>Name;
}
public sealed record IngredientPolicyChoice(IngredientCountPolicy Value,string Label);
public sealed class IngredientEdit:INotifyPropertyChanged
{
    private RecipeItemChoice? item; private string quantity="1"; private string unit="g";
    private IngredientCountPolicy policy=IngredientCountPolicy.NotApplicable; private int group; private int preference;
    private readonly ObservableCollection<RecipeItemChoice> availableItems=[];
    public Action? Changed {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public RecipeItemChoice? Item {get=>item;set{if(Equals(item,value))return;item=value;if(value is not null){var units=InventoryDisplay.Units(value.Dimension);if(!units.Contains(unit))unit=units[0];policy=value.Dimension==InventoryDimension.Count?IngredientCountPolicy.ExactWholeCount:IngredientCountPolicy.NotApplicable;}Raise();}}
    public string Quantity {get=>quantity;set{if(quantity==value)return;quantity=value;Raise();}}
    public string Unit {get=>unit;set{var next=Item is {} selected&&!InventoryDisplay.Units(selected.Dimension).Contains(value)?InventoryDisplay.Units(selected.Dimension)[0]:value;if(unit==next)return;unit=next;Raise();}}
    public IngredientCountPolicy Policy {get=>policy;set{if(policy==value)return;policy=value;Raise();}}
    public int Group {get=>group;set{if(group==value)return;group=value;Raise();}}
    public int Preference {get=>preference;set{if(preference==value)return;preference=value;Raise();}}
    public string GroupLabel=>Group==0?"Ingrediente normal":$"Grupo {Group}";
    public string OptionLabel=>Preference>0?$"Opción {Preference}":"Opción";
    public IReadOnlyList<string> UnitChoices=>Item is null?[]:InventoryDisplay.Units(Item.Dimension);
    public IReadOnlyList<IngredientPolicyChoice> AvailablePolicies=>Item?.Dimension==InventoryDimension.Count
        ? [new(IngredientCountPolicy.ExactWholeCount,"Sólo piezas completas"),new(IngredientCountPolicy.CeilingWholeCount,"Redondear hacia arriba")]
        : [new(IngredientCountPolicy.NotApplicable,"Cantidad exacta")];
    public ObservableCollection<RecipeItemChoice> AvailableItems=>availableItems;
    public void UpdateAvailableItems(IEnumerable<RecipeItemChoice> candidates)
    {
        var desired=candidates.DistinctBy(x=>x.Id).ToArray();
        if(Item is {} selected&&!desired.Any(x=>x.Id==selected.Id))desired=[selected,..desired];
        for(var index=0;index<desired.Length;index++)
        {
            var currentIndex=-1;
            for(var position=0;position<availableItems.Count;position++)
                if(availableItems[position].Id==desired[index].Id){currentIndex=position;break;}
            if(currentIndex<0)availableItems.Insert(index,desired[index]);
            else if(currentIndex!=index)availableItems.Move(currentIndex,index);
        }
        while(availableItems.Count>desired.Length)availableItems.RemoveAt(availableItems.Count-1);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(){PropertyChanged?.Invoke(this,new(null));Changed?.Invoke();}
}
public sealed class FormulaRequirementEdit:INotifyPropertyChanged
{
    private RecipeFormulaChoice? formula; private string quantity="1"; private string unit="g";
    public Action? Changed {get;set;}
    public RecipeFormulaChoice? Formula {get=>formula;set{if(Equals(formula,value))return;formula=value;if(value is not null&&!UnitChoices.Contains(unit))unit=UnitChoices[0];Raise();}}
    public string Quantity {get=>quantity;set{if(quantity==value)return;quantity=value;Raise();}}
    public string Unit {get=>unit;set{var next=Formula is {} selected&&!InventoryDisplay.Units(selected.Dimension).Contains(value)?InventoryDisplay.Units(selected.Dimension)[0]:value;if(unit==next)return;unit=next;Raise();}}
    public IReadOnlyList<string> UnitChoices=>Formula is null?[]:InventoryDisplay.Units(Formula.Dimension);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(){PropertyChanged?.Invoke(this,new(null));Changed?.Invoke();}
}
public sealed class RecipeViewModel(RecipeService service,InventoryService inventory,CatalogService catalog,Action<string,string?> log):INotifyPropertyChanged
{
    private string screen="list"; private Guid? formulaId,profileId; private RecipeVersionRow? selectedVersion;
    public bool Busy{get;private set;} public bool Ready=>!Busy; public bool CanView{get;private set;} public bool CanConfigure{get;private set;}
    public bool IsList=>screen=="list"; public bool IsFormula=>screen=="formula"; public bool IsFormulaEditor=>screen=="formulaEditor";
    public bool IsProfiles=>screen=="profiles"; public bool IsProfile=>screen=="profile"; public bool IsProfileEditor=>screen=="profileEditor";
    public bool ReadOnly=>!CanConfigure; public string ModeLabel=>ReadOnly?"Consulta · sólo lectura":"Puedes crear y editar recetas y perfiles";
    public bool IsNewFormulaOwner=>IsFormulaEditor&&formulaId is null; public bool CanSaveFormulaVersion=>IsFormulaEditor&&formulaId is not null;
    public bool CanCreateFormula=>Ready&&CanConfigure&&IsNewFormulaOwner&&!string.IsNullOrWhiteSpace(Code)&&!string.IsNullOrWhiteSpace(Name);
    public bool CanSaveFormulaDraft=>Ready&&CanConfigure&&CanSaveFormulaVersion&&FormulaDraftIsComplete();
    public bool IsNewProfileOwner=>IsProfileEditor&&profileId is null; public bool CanSaveProfileVersion=>IsProfileEditor&&profileId is not null;
    public bool CanSaveProfileDraft=>Ready&&CanConfigure&&CanSaveProfileVersion&&ProfileDraftIsComplete();
    public string Message{get;private set;}=""; public bool MessageIsSuccess{get;private set;}
    private RecipeFormulaRow? selectedFormula; private RecipeProfileRow? selectedProfile;
    public IReadOnlyList<RecipeFormulaRow> Formulas{get;private set;}=[]; public RecipeFormulaRow? SelectedFormula{get=>selectedFormula;set=>Set(ref selectedFormula,value);}
    public IReadOnlyList<RecipeProfileRow> Profiles{get;private set;}=[]; public RecipeProfileRow? SelectedProfile{get=>selectedProfile;set=>Set(ref selectedProfile,value);}
    public IReadOnlyList<RecipeVersionRow> Versions{get;private set;}=[]; public RecipeVersionRow? SelectedVersion{get=>selectedVersion;set=>Set(ref selectedVersion,value);}
    public bool CanEditSelectedVersion=>Ready&&CanConfigure&&SelectedVersion?.State=="Borrador";
    public bool CanActivateSelectedVersion=>Ready&&CanConfigure&&SelectedVersion is {State:"Publicada",Active:false};
    public bool CanCreateProfile=>Ready&&CanConfigure&&ProfileProduct is not null&&ProfileFinishedItem is not null;
    public IReadOnlyList<RecipeItemChoice> Items{get;private set;}=[]; public IReadOnlyList<RecipeFormulaChoice> PublishedFormulas{get;private set;}=[];
    public bool CanAddIngredient=>Ready&&CanConfigure&&Items.Count>0;
    public bool CanAddPreparation=>Ready&&CanConfigure&&PublishedFormulas.Count>0;
    public bool CanRemoveIngredient=>Ready&&CanConfigure&&SelectedIngredient is not null;
    public bool CanRemoveAlternative=>Ready&&CanConfigure&&SelectedAlternative is not null;
    public bool CanAddAlternativeOption=>CanRemoveAlternative;
    public bool CanRemovePreparation=>Ready&&CanConfigure&&SelectedFormulaRequirement is not null;
    public bool CanRemoveDirect=>Ready&&CanConfigure&&SelectedDirect is not null;
    public IReadOnlyList<RecipeProductChoice> Products{get;private set;}=[]; public IReadOnlyList<RecipeItemChoice> FinishedItems{get;private set;}=[];
    public ObservableCollection<IngredientEdit> Ingredients{get;}=[]; public ObservableCollection<IngredientEdit> Alternatives{get;}=[];
    public ObservableCollection<FormulaRequirementEdit> FormulaRequirements{get;}=[]; public ObservableCollection<IngredientEdit> DirectIngredients{get;}=[];
    private IngredientEdit? selectedIngredient,selectedAlternative,selectedDirect; private FormulaRequirementEdit? selectedFormulaRequirement;
    public IngredientEdit? SelectedIngredient{get=>selectedIngredient;set=>Set(ref selectedIngredient,value);} public IngredientEdit? SelectedAlternative{get=>selectedAlternative;set=>Set(ref selectedAlternative,value);} public FormulaRequirementEdit? SelectedFormulaRequirement{get=>selectedFormulaRequirement;set=>Set(ref selectedFormulaRequirement,value);} public IngredientEdit? SelectedDirect{get=>selectedDirect;set=>Set(ref selectedDirect,value);}
    private string code="",name="",notes="",versionNumber="1",yield="1",yieldUnit="g",ingredientSearch="";
    private InventoryDimension yieldDimension=InventoryDimension.Mass; private CancellationTokenSource? ingredientSearchCancellation;
    private IReadOnlyList<RecipeItemChoice> ingredientMatches=[];
    public string Code{get=>code;set=>Set(ref code,value);} public string Name{get=>name;set=>Set(ref name,value);} public string Notes{get=>notes;set=>Set(ref notes,value);} public string VersionNumber{get=>versionNumber;set=>Set(ref versionNumber,value);}
    public string Yield{get=>yield;set=>Set(ref yield,value);}
    public InventoryDimension YieldDimension{get=>yieldDimension;set{if(yieldDimension==value)return;yieldDimension=value;YieldUnit=InventoryDisplay.Units(value)[0];Notify();}}
    public string YieldUnit{get=>yieldUnit;set{var next=InventoryDisplay.Units(YieldDimension).Contains(value)?value:InventoryDisplay.Units(YieldDimension)[0];Set(ref yieldUnit,next);}}
    public IReadOnlyList<string> YieldUnitChoices=>InventoryDisplay.Units(YieldDimension);
    public string IngredientSearch{get=>ingredientSearch;set{if(ingredientSearch==value)return;ingredientSearch=value;Notify();ScheduleIngredientFilter();}}
    public IReadOnlyList<RecipeItemChoice> IngredientMatches{get=>ingredientMatches;private set{ingredientMatches=value;Notify();}}
    public bool HasIngredientMatches=>IngredientMatches.Count>0;
    public bool HasIngredientSearch=>!string.IsNullOrWhiteSpace(IngredientSearch);
    public string IngredientEmptyMessage=>"No encontramos ingredientes con esa búsqueda.";
    private RecipeProductChoice? profileProduct; private RecipeItemChoice? profileFinishedItem;
    public RecipeProductChoice? ProfileProduct{get=>profileProduct;set=>Set(ref profileProduct,value);} public RecipeItemChoice? ProfileFinishedItem{get=>profileFinishedItem;set=>Set(ref profileFinishedItem,value);}
    public IReadOnlyList<InventoryDimensionChoice> Dimensions{get;}=Enum.GetValues<InventoryDimension>().Select(x=>new InventoryDimensionChoice(x,UiLabels.Dimension(x))).ToArray();
    public IReadOnlyList<IngredientPolicyChoice> PolicyChoices{get;}=
    [
        new(IngredientCountPolicy.NotApplicable,"Cantidad exacta"),
        new(IngredientCountPolicy.ExactWholeCount,"Sólo piezas completas"),
        new(IngredientCountPolicy.CeilingWholeCount,"Redondear hacia arriba a pieza completa")
    ];
    public string YieldHelpText=>"Qué es: la cantidad total que produce esta versión.\n\nCómo se usa: escribe la cantidad, elige su dimensión y después una unidad compatible. El sistema usa este rendimiento como base para calcular proporcionalmente los ingredientes cuando otra preparación necesita una cantidad distinta.\n\nEjemplos sintéticos: si produce 1 kg, captura Rendimiento = 1, Dimensión = Masa y Unidad = kg. Si produce 20 piezas, captura 20, Piezas y pieza.\n\nImportante: usa una cantidad mayor que cero y no mezcles una dimensión con una unidad diferente.";
    public string PolicyHelpText=>"Qué es: indica cómo tratar la cantidad calculada de cada ingrediente.\n\nCómo se usa: para Masa y Volumen se conserva la cantidad exacta. Para Piezas, «Sólo piezas completas» exige que el cálculo ya produzca un número entero; si da 2.4, esa opción no es válida. «Redondear hacia arriba» convierte una necesidad de 2.4 piezas en 3 piezas.\n\nImportante: elige una política de piezas sólo para artículos cuya unidad es pieza.";
    public string DirectIngredientsHelpText=>"Qué es: un ingrediente que la fórmula o el perfil utiliza directamente.\n\nCómo se usa: busca por código o nombre, agrega una fila, selecciona el ingrediente, captura una cantidad y elige una unidad compatible. Usa Política de cantidad cuando el ingrediente se mide en piezas.\n\nEjemplo sintético: Ingrediente masa sintético, Cantidad = 500, Unidad = g significa que la fórmula utiliza 500 g de ese ingrediente.\n\nImportante: el buscador limita los candidatos disponibles; no cambia los ingredientes ya asignados.";
    public string AlternativesHelpText=>"Qué es: un grupo reúne ingredientes que pueden sustituirse entre sí. Opción 1 tiene la primera preferencia, después Opción 2 y las siguientes. Cada opción puede usar una cantidad y unidad distintas.\n\nCómo se usa: «Nuevo grupo de alternativas» crea un conjunto independiente. Selecciona una fila y usa «Agregar opción al grupo» para añadir otra opción al grupo actual. Cada grupo necesita al menos dos opciones válidas antes de guardar.\n\nEjemplo sintético: Grupo 1 puede contener Opción A = 10 g y Opción B = 12 g. Agregar opción crea Opción C dentro de Grupo 1; Nuevo grupo crea Grupo 2.\n\nImportante: el orden expresa preferencia. No agrupes ingredientes que deban utilizarse al mismo tiempo.";
    public string PreparationsHelpText=>"Qué es: una preparación es otra receta publicada y reutilizable que forma parte del producto; un ingrediente directo se agrega sin pasar por otra receta.\n\nCómo se usa: selecciona la preparación publicada, captura la cantidad necesaria por pieza y elige una unidad compatible con su rendimiento. Usa «Agregar preparación» para incluir otro componente.\n\nEjemplo sintético: un perfil puede requerir 25 g de Preparación sintética de revisión por cada pieza terminada.\n\nImportante: una preparación debe estar publicada para poder seleccionarse. Si cambia, crea y publica una nueva versión.";
    public string PublishedNotice=>"BORRADOR: se puede editar y guardar. PUBLICADA: queda cerrada e inmutable para conservar el historial. ACTIVA: es la versión vigente para los usos correspondientes.\n\nPara cambiar versiones publicadas, usa «Crear nueva versión» y publica el nuevo borrador cuando esté revisado; no se modifica silenciosamente el historial.";
    public event PropertyChangedEventHandler? PropertyChanged; private void Notify()=>PropertyChanged?.Invoke(this,new(null));
    private void Set<T>(ref T field,T value){if(EqualityComparer<T>.Default.Equals(field,value))return;field=value;Notify();}

    public Task LoadAsync()=>Run(async()=>
    {
        CanView=await Task.Run(()=>catalog.Can("recipe.view"));CanConfigure=await Task.Run(()=>catalog.Can("recipe.configure"));
        if(!CanView)throw new AccessDeniedException(); await LoadChoices(); await RefreshFormulas();screen="list";
    });
    private async Task LoadChoices()
    {
        var rows=new List<InventoryListRow>();for(int page=0;;page++){var result=await Task.Run(()=>inventory.List(new(Page:page,PageSize:100)));rows.AddRange(result.Items);if(rows.Count>=result.Total)break;}
        Items=rows.Where(x=>x.Active).Select(x=>new RecipeItemChoice(x.InventoryItemId,x.Code,x.Name,x.Dimension)).ToArray();
        ApplyIngredientFilter();
        var finishedIds=rows.Where(x=>x.Active&&x.Kind==InventoryItemKind.FinishedGood&&x.Dimension==InventoryDimension.Count).Select(x=>x.InventoryItemId).ToHashSet();
        FinishedItems=Items.Where(x=>finishedIds.Contains(x.Id)).ToArray();
        var products=new List<ProductRow>();for(int page=0;;page++){var result=await Task.Run(()=>catalog.Products(new(IncludeInactive:false,Page:page)));products.AddRange(result.Items);if(products.Count>=result.Total)break;}
        Products=products.Select(x=>new RecipeProductChoice(x.Product.Id,x.Product.Name)).ToArray();
    }
    private async Task RefreshFormulas(){var summaries=await Task.Run(()=>service.ListFormulas(200));Formulas=summaries.Select(f=>{var versions=service.ListFormulaVersions(f.Id);return new RecipeFormulaRow(f,f.ActiveVersionId is {} active?$"Versión {versions.Single(x=>x.Value.Id==active).Value.Version}":"Sin activar",versions.Any(x=>x.Value.State==ProductionVersionState.Draft)?"Sí":"No");}).ToArray();SelectedFormula=null;LoadPublishedFormulaChoices(summaries);}
    private void LoadPublishedFormulaChoices(IEnumerable<FormulaSummary> summaries)=>PublishedFormulas=summaries.SelectMany(f=>service.ListFormulaVersions(f.Id).Where(v=>v.Value.State==ProductionVersionState.Published).Select(v=>new RecipeFormulaChoice(v.Value.Id,$"{f.Name} · versión {v.Value.Version}",v.Value.BaseYield.Dimension))).ToArray();
    private async Task RefreshPublishedFormulaChoices()=>LoadPublishedFormulaChoices(await Task.Run(()=>service.ListFormulas(200)));
    private async Task RefreshProfiles(){var summaries=await Task.Run(()=>service.ListProfiles(200));Profiles=summaries.Select(p=>{var versions=service.ListProfileVersions(p.Id);return new RecipeProfileRow(p,Products.SingleOrDefault(x=>x.Id==p.ProductId)?.Name??"Producto",Items.SingleOrDefault(x=>x.Id==p.FinishedItemId)?.Name??"Artículo terminado",p.ActiveVersionId is {} active?$"Versión {versions.Single(x=>x.Value.Id==active).Value.Version}":"Sin activar",versions.Any(x=>x.Value.State==ProductionVersionState.Draft)?"Sí":"No");}).ToArray();SelectedProfile=null;}
    public Task ShowFormulasAsync()=>Run(async()=>{await RefreshFormulas();screen="list";});
    public Task ShowProfilesAsync()=>Run(async()=>{await RefreshProfiles();screen="profiles";});
    public void NewFormula(){if(!CanConfigure)return;formulaId=null;Code="";Name="";IngredientSearch="";screen="formulaEditor";Ingredients.Clear();Alternatives.Clear();Message="";Notify();}
    public Task CreateFormulaAsync()=>Run(async()=>{var id=Guid.NewGuid();await Task.Run(()=>service.CreateFormula(new(id,Code),Name));formulaId=id;await LoadFormula(id);MessageIsSuccess=true;Message="Receta creada. Agrega su primera versión borrador.";});
    public Task OpenFormulaAsync()=>Run(async()=>{var row=SelectedFormula??throw new ArgumentException("Selecciona una receta.");await LoadFormula(row.Summary.Id);});
    private async Task LoadFormula(Guid id){await RefreshFormulas();formulaId=id;var f=await Task.Run(()=>service.GetFormula(id))??throw new ArgumentException("La receta ya no existe.");Name=f.Name;Code=f.Code;Versions=(await Task.Run(()=>service.ListFormulaVersions(id))).Select(v=>new RecipeVersionRow(v.Value.Id,v.Value.Version,v.Value.State==ProductionVersionState.Published?"Publicada":"Borrador",v.Value.Name,v.Revision,f.ActiveVersionId==v.Value.Id)).ToArray();selectedVersion=null;screen="formula";}
    public void NewFormulaVersion(){if(!CanConfigure||formulaId is null)return;selectedVersion=null;VersionNumber=(Versions.Select(x=>x.Number).DefaultIfEmpty(0).Max()+1).ToString();Name="";Notes="";Yield="1";YieldDimension=InventoryDimension.Mass;YieldUnit="g";IngredientSearch="";Ingredients.Clear();Alternatives.Clear();screen="formulaEditor";Notify();}
    public Task EditFormulaVersionAsync()=>Run(async()=>
    {
        var row=selectedVersion??throw new ArgumentException("Selecciona una versión.");if(row.State!="Borrador")throw new ArgumentException(PublishedNotice);
        var stored=await Task.Run(()=>service.GetFormulaVersion(row.Id))??throw new ArgumentException("La versión ya no existe.");var v=stored.Value;
        VersionNumber=v.Version.ToString();Name=v.Name;Notes=v.Notes??"";YieldDimension=v.BaseYield.Dimension;Yield=InventoryDisplay.Number(v.BaseYield.AtomicUnits,v.BaseYield.Dimension);YieldUnit=InventoryDisplay.Unit(v.BaseYield.AtomicUnits,v.BaseYield.Dimension);Ingredients.Clear();Alternatives.Clear();
        IngredientSearch="";foreach(var i in v.Ingredients)Ingredients.Add(Edit(i));int group=0;foreach(var g in v.Alternatives){group++;var order=g.PreferenceOrder.ToArray();foreach(var o in g.Options){var e=Edit(o.Ingredient);e.Id=o.Id;e.Group=group;e.Preference=Array.IndexOf(order,o.Id)+1;Alternatives.Add(e);}}
        screen="formulaEditor";
    });
    private IngredientEdit Edit(FormulaIngredient value)=>Prepare(new IngredientEdit{Item=Items.SingleOrDefault(x=>x.Id==value.InventoryItemId),Quantity=InventoryDisplay.Number(value.Quantity.AtomicUnits,value.Quantity.Dimension),Unit=InventoryDisplay.Unit(value.Quantity.AtomicUnits,value.Quantity.Dimension),Policy=value.CountPolicy});
    private IngredientEdit Prepare(IngredientEdit row){row.Changed=Notify;row.UpdateAvailableItems(IngredientMatches);return row;}
    private FormulaRequirementEdit Prepare(FormulaRequirementEdit row){row.Changed=Notify;return row;}
    public void AddIngredient(bool alternative){var row=Prepare(new IngredientEdit());if(alternative){row.Group=Alternatives.Select(x=>x.Group).DefaultIfEmpty(0).Max()+1;row.Preference=1;Alternatives.Add(row);SelectedAlternative=row;}else{Ingredients.Add(row);SelectedIngredient=row;}Notify();}
    public void AddAlternativeOption(){if(SelectedAlternative is not {} selected)return;var group=selected.Group;var row=Prepare(new IngredientEdit{Group=group,Preference=Alternatives.Where(x=>x.Group==group).Select(x=>x.Preference).DefaultIfEmpty(0).Max()+1});Alternatives.Add(row);SelectedAlternative=row;Notify();}
    public void RemoveIngredient(bool alternative){if(alternative&&SelectedAlternative is {} a){var group=a.Group;Alternatives.Remove(a);var preference=0;foreach(var row in Alternatives.Where(x=>x.Group==group).OrderBy(x=>x.Preference))row.Preference=++preference;SelectedAlternative=null;}else if(!alternative&&SelectedIngredient is {} i){Ingredients.Remove(i);SelectedIngredient=null;}Notify();}
    public Task SaveFormulaVersionAsync()=>Run(async()=>
    {
        if(formulaId is null)throw new ArgumentException("Primero crea la receta.");if(!int.TryParse(VersionNumber,out var number)||number<1)throw new ArgumentException("La versión debe ser un entero positivo.");
        if(string.IsNullOrWhiteSpace(Name))throw new ArgumentException("Escribe el nombre de la versión.");
        if(Ingredients.Count+Alternatives.Count==0)throw new ArgumentException("Agrega al menos un ingrediente o grupo de alternativas.");
        if(Alternatives.GroupBy(x=>x.Group).Any(x=>x.Key<=0||x.Count()<2))throw new ArgumentException("Cada grupo de alternativas debe tener al menos dos opciones.");
        var id=selectedVersion?.Id??Guid.NewGuid();var revision=selectedVersion?.Revision;
        var normal=Ingredients.Select(ToIngredient).ToArray();var groups=Alternatives.GroupBy(x=>x.Group).Select(g=>new IngredientAlternativeGroup(Guid.NewGuid(),g.Select(x=>new IngredientAlternativeOption(x.Id,ToIngredient(x))),g.OrderBy(x=>x.Preference<=0?int.MaxValue:x.Preference).Select(x=>x.Id))).ToArray();
        var version=new FormulaVersion(id,formulaId.Value,number,Name,new(YieldDimension,InventoryDisplay.ParseOperational(Yield,YieldDimension,YieldUnit)),normal,groups,DateTimeOffset.UtcNow,Notes);
        await Task.Run(()=>service.SaveFormulaDraft(version,revision));await LoadFormula(formulaId.Value);MessageIsSuccess=true;Message="Versión borrador guardada.";
    });
    private static FormulaIngredient ToIngredient(IngredientEdit row){var item=row.Item??throw new ArgumentException("Selecciona un artículo en cada ingrediente.");return new(item.Id,new(item.Dimension,InventoryDisplay.ParseOperational(row.Quantity,item.Dimension,row.Unit)),row.Policy);}
    public Task PublishAsync()=>Run(async()=>{var row=selectedVersion??throw new ArgumentException("Selecciona una versión.");await Task.Run(()=>service.PublishFormula(row.Id,row.Revision));await LoadFormula(formulaId!.Value);await RefreshPublishedFormulaChoices();MessageIsSuccess=true;Message="Versión publicada. Ya es histórica e inmutable.";});
    public Task ActivateAsync()=>Run(async()=>{var row=selectedVersion??throw new ArgumentException("Selecciona una versión publicada.");if(row.State!="Publicada")throw new ArgumentException("Publica la versión antes de activarla.");var summary=service.GetFormula(formulaId!.Value)!;await Task.Run(()=>service.ActivateFormula(formulaId.Value,row.Id,summary.Revision));await LoadFormula(formulaId.Value);MessageIsSuccess=true;Message="Versión activa actualizada.";});

    public void NewProfile(){if(!CanConfigure)return;profileId=null;ProfileProduct=null;ProfileFinishedItem=null;Name="";IngredientSearch="";FormulaRequirements.Clear();DirectIngredients.Clear();screen="profileEditor";Notify();}
    public Task CreateProfileAsync()=>Run(async()=>{var product=ProfileProduct??throw new ArgumentException("Selecciona un producto.");var item=ProfileFinishedItem??throw new ArgumentException("Selecciona el artículo terminado.");var id=Guid.NewGuid();await Task.Run(()=>service.CreateProfile(new(id,product.Id,item.Id)));profileId=id;await LoadProfile(id);MessageIsSuccess=true;Message="Perfil creado. Agrega su primera versión borrador.";});
    public Task OpenProfileAsync()=>Run(async()=>{var row=SelectedProfile??throw new ArgumentException("Selecciona un perfil.");await LoadProfile(row.Summary.Id);});
    private async Task LoadProfile(Guid id){await RefreshProfiles();profileId=id;var p=await Task.Run(()=>service.GetProfile(id))??throw new ArgumentException("El perfil ya no existe.");ProfileProduct=Products.SingleOrDefault(x=>x.Id==p.ProductId);ProfileFinishedItem=Items.SingleOrDefault(x=>x.Id==p.FinishedItemId);Versions=(await Task.Run(()=>service.ListProfileVersions(id))).Select(v=>new RecipeVersionRow(v.Value.Id,v.Value.Version,v.Value.State==ProductionVersionState.Published?"Publicada":"Borrador",v.Value.Name,v.Revision,p.ActiveVersionId==v.Value.Id)).ToArray();selectedVersion=null;screen="profile";}
    public void NewProfileVersion(){if(!CanConfigure||profileId is null)return;selectedVersion=null;VersionNumber=(Versions.Select(x=>x.Number).DefaultIfEmpty(0).Max()+1).ToString();Name="";IngredientSearch="";FormulaRequirements.Clear();DirectIngredients.Clear();screen="profileEditor";Notify();}
    public Task EditProfileVersionAsync()=>Run(async()=>
    {
        var row=selectedVersion??throw new ArgumentException("Selecciona una versión.");if(row.State!="Borrador")throw new ArgumentException(PublishedNotice);
        var stored=await Task.Run(()=>service.GetProfileVersion(row.Id))??throw new ArgumentException("La versión ya no existe.");var v=stored.Value;
        VersionNumber=v.Version.ToString();Name=v.Name;IngredientSearch="";FormulaRequirements.Clear();DirectIngredients.Clear();
        foreach(var f in v.Formulas){var choice=PublishedFormulas.SingleOrDefault(x=>x.Id==f.FormulaVersionId);FormulaRequirements.Add(Prepare(new FormulaRequirementEdit{Formula=choice,Quantity=InventoryDisplay.Number(f.QuantityPerPiece.AtomicUnits,f.QuantityPerPiece.Dimension),Unit=InventoryDisplay.Unit(f.QuantityPerPiece.AtomicUnits,f.QuantityPerPiece.Dimension)}));}
        foreach(var d in v.DirectIngredients)DirectIngredients.Add(Edit(d.Ingredient));screen="profileEditor";
    });
    public void AddFormulaRequirement(){var row=Prepare(new FormulaRequirementEdit());FormulaRequirements.Add(row);SelectedFormulaRequirement=row;Notify();} public void AddDirectIngredient(){var row=Prepare(new IngredientEdit());DirectIngredients.Add(row);SelectedDirect=row;Notify();}
    public void RemoveFormulaRequirement(){if(SelectedFormulaRequirement is {} r)FormulaRequirements.Remove(r);SelectedFormulaRequirement=null;} public void RemoveDirectIngredient(){if(SelectedDirect is {} r)DirectIngredients.Remove(r);SelectedDirect=null;}
    public Task SaveProfileVersionAsync()=>Run(async()=>
    {
        if(profileId is null||ProfileProduct is null||ProfileFinishedItem is null)throw new ArgumentException("El perfil no está completo.");if(!int.TryParse(VersionNumber,out var number)||number<1)throw new ArgumentException("La versión debe ser un entero positivo.");
        if(string.IsNullOrWhiteSpace(Name))throw new ArgumentException("Escribe el nombre de la versión del perfil.");
        if(FormulaRequirements.Count+DirectIngredients.Count==0)throw new ArgumentException("Agrega al menos una preparación o ingrediente directo.");
        var id=selectedVersion?.Id??Guid.NewGuid();var revision=selectedVersion?.Revision;
        var formulas=FormulaRequirements.Select(x=>{var f=x.Formula??throw new ArgumentException("Selecciona una fórmula publicada.");return new FormulaRequirement(f.Id,new(f.Dimension,InventoryDisplay.ParseOperational(x.Quantity,f.Dimension,x.Unit)));}).ToArray();
        var direct=DirectIngredients.Select(x=>new DirectIngredientRequirement(ToIngredient(x))).ToArray();
        await Task.Run(()=>service.SaveProfileDraft(new(id,profileId.Value,number,ProfileProduct.Id,ProfileFinishedItem.Id,Name,formulas,direct,DateTimeOffset.UtcNow),revision));await LoadProfile(profileId.Value);MessageIsSuccess=true;Message="Versión de perfil guardada.";
    });
    public Task PublishProfileAsync()=>Run(async()=>{var row=selectedVersion??throw new ArgumentException("Selecciona una versión.");await Task.Run(()=>service.PublishProfile(row.Id,row.Revision));await LoadProfile(profileId!.Value);MessageIsSuccess=true;Message="Versión de perfil publicada.";});
    public Task ActivateProfileAsync()=>Run(async()=>{var row=selectedVersion??throw new ArgumentException("Selecciona una versión publicada.");if(row.State!="Publicada")throw new ArgumentException("Publica la versión antes de activarla.");var p=service.GetProfile(profileId!.Value)!;await Task.Run(()=>service.ActivateProfile(profileId.Value,row.Id,p.Revision));await LoadProfile(profileId.Value);MessageIsSuccess=true;Message="Versión de perfil activada.";});
    public void Back(){if(screen=="formulaEditor"&&formulaId.HasValue)screen="formula";else if(screen=="profileEditor"&&profileId.HasValue)screen="profile";else screen=screen.StartsWith("profile",StringComparison.Ordinal)?"profiles":"list";Message="";Notify();}
    public static string PolicyLabel(IngredientCountPolicy value)=>value switch{IngredientCountPolicy.CeilingWholeCount=>"Redondear hacia arriba a pieza completa",IngredientCountPolicy.ExactWholeCount=>"Sólo piezas completas",_=>"Cantidad exacta"};
    public static string ErrorMessage(ProductionDomainException error)=>RecipeProblems.Describe(error);
    public void ClearIngredientSearch(){IngredientSearch="";}
    private async void ScheduleIngredientFilter()
    {
        ingredientSearchCancellation?.Cancel();var cancellation=new CancellationTokenSource();ingredientSearchCancellation=cancellation;
        try{await Task.Delay(250,cancellation.Token);if(cancellation.IsCancellationRequested)return;ApplyIngredientFilter();}
        catch(OperationCanceledException){}
    }
    private void ApplyIngredientFilter()
    {
        var search=IngredientSearch.Trim();IngredientMatches=string.IsNullOrEmpty(search)?Items:Items.Where(x=>x.Code.Contains(search,StringComparison.OrdinalIgnoreCase)||x.DisplayName.Contains(search,StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach(var row in Ingredients.Concat(Alternatives).Concat(DirectIngredients))row.UpdateAvailableItems(IngredientMatches);
        Notify();
    }
    private static bool ValidQuantity(string text,InventoryDimension dimension,string unit)
    {
        try{InventoryDisplay.ParseOperational(text,dimension,unit);return true;}catch{return false;}
    }
    private static bool ValidIngredient(IngredientEdit row)=>row.Item is {} item&&row.UnitChoices.Contains(row.Unit)&&row.AvailablePolicies.Any(x=>x.Value==row.Policy)&&ValidQuantity(row.Quantity,item.Dimension,row.Unit);
    private bool FormulaDraftIsComplete()=>!string.IsNullOrWhiteSpace(Name)&&int.TryParse(VersionNumber,out var version)&&version>0&&ValidQuantity(Yield,YieldDimension,YieldUnit)&&
        Ingredients.Concat(Alternatives).Any()&&Ingredients.All(ValidIngredient)&&Alternatives.All(ValidIngredient)&&Alternatives.GroupBy(x=>x.Group).All(x=>x.Key>0&&x.Count()>=2&&x.Select(r=>r.Preference).Distinct().Count()==x.Count());
    private bool ProfileDraftIsComplete()=>!string.IsNullOrWhiteSpace(Name)&&int.TryParse(VersionNumber,out var version)&&version>0&&FormulaRequirements.Count+DirectIngredients.Count>0&&
        FormulaRequirements.All(x=>x.Formula is {} formula&&x.UnitChoices.Contains(x.Unit)&&ValidQuantity(x.Quantity,formula.Dimension,x.Unit))&&DirectIngredients.All(ValidIngredient);
    private async Task Run(Func<Task> action){if(Busy)return;Busy=true;Message="";MessageIsSuccess=false;Notify();try{await action();}catch(AccessDeniedException){Message="No tienes permiso para esta operación de recetas.";log("recipe.authorization.denied",null);}catch(ProductionDomainException e){Message=ErrorMessage(e);}catch(RecipeConcurrencyException){Message="La receta cambió. Actualiza la información antes de continuar.";}catch(ArgumentException e){Message=e.Message;}catch(Exception e){Message=$"No se completó la operación de recetas (código: {e.GetType().Name}).";log("recipe.operation.failed",e.GetType().Name);}finally{Busy=false;Notify();}}
}
