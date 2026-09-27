using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;

namespace CasaMiga.Presentation;

public sealed record RecipeVersionRow(Guid Id,int Number,string State,string Name,long Revision,bool Active);
public sealed record RecipeFormulaRow(FormulaSummary Summary,string ActiveVersion,string Draft)
{ public string Code=>Summary.Code;public string Name=>Summary.Name; }
public sealed record RecipeProfileRow(ProfileSummary Summary,string Product,string FinishedItem,string ActiveVersion,string Draft);
public sealed record RecipeItemChoice(Guid Id,string Name,InventoryDimension Dimension)
{
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
public sealed class IngredientEdit
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public RecipeItemChoice? Item {get;set;}
    public string Quantity {get;set;}="1";
    public string Unit {get;set;}="g";
    public IngredientCountPolicy Policy {get;set;}=IngredientCountPolicy.NotApplicable;
    public int Group {get;set;}
    public int Preference {get;set;}
    public string GroupLabel=>Group==0?"Ingrediente normal":$"Grupo {Group}";
}
public sealed class FormulaRequirementEdit
{
    public RecipeFormulaChoice? Formula {get;set;}
    public string Quantity {get;set;}="1";
    public string Unit {get;set;}="g";
}
public sealed class RecipeViewModel(RecipeService service,InventoryService inventory,CatalogService catalog,Action<string,string?> log):INotifyPropertyChanged
{
    private string screen="list"; private Guid? formulaId,profileId; private RecipeVersionRow? selectedVersion;
    public bool Busy{get;private set;} public bool Ready=>!Busy; public bool CanView{get;private set;} public bool CanConfigure{get;private set;}
    public bool IsList=>screen=="list"; public bool IsFormula=>screen=="formula"; public bool IsFormulaEditor=>screen=="formulaEditor";
    public bool IsProfiles=>screen=="profiles"; public bool IsProfile=>screen=="profile"; public bool IsProfileEditor=>screen=="profileEditor";
    public bool ReadOnly=>!CanConfigure; public string ModeLabel=>ReadOnly?"Consulta · sólo lectura":"Configuración habilitada";
    public bool IsNewFormulaOwner=>IsFormulaEditor&&formulaId is null; public bool CanSaveFormulaVersion=>IsFormulaEditor&&formulaId is not null;
    public bool IsNewProfileOwner=>IsProfileEditor&&profileId is null; public bool CanSaveProfileVersion=>IsProfileEditor&&profileId is not null;
    public string Message{get;private set;}=""; public bool MessageIsSuccess{get;private set;}
    public IReadOnlyList<RecipeFormulaRow> Formulas{get;private set;}=[]; public RecipeFormulaRow? SelectedFormula{get;set;}
    public IReadOnlyList<RecipeProfileRow> Profiles{get;private set;}=[]; public RecipeProfileRow? SelectedProfile{get;set;}
    public IReadOnlyList<RecipeVersionRow> Versions{get;private set;}=[]; public RecipeVersionRow? SelectedVersion{get=>selectedVersion;set=>selectedVersion=value;}
    public IReadOnlyList<RecipeItemChoice> Items{get;private set;}=[]; public IReadOnlyList<RecipeFormulaChoice> PublishedFormulas{get;private set;}=[];
    public IReadOnlyList<RecipeProductChoice> Products{get;private set;}=[]; public IReadOnlyList<RecipeItemChoice> FinishedItems=>Items.Where(x=>x.Dimension==InventoryDimension.Count).ToArray();
    public ObservableCollection<IngredientEdit> Ingredients{get;}=[]; public ObservableCollection<IngredientEdit> Alternatives{get;}=[];
    public ObservableCollection<FormulaRequirementEdit> FormulaRequirements{get;}=[]; public ObservableCollection<IngredientEdit> DirectIngredients{get;}=[];
    public IngredientEdit? SelectedIngredient{get;set;} public IngredientEdit? SelectedAlternative{get;set;} public FormulaRequirementEdit? SelectedFormulaRequirement{get;set;} public IngredientEdit? SelectedDirect{get;set;}
    public string Code{get;set;}=""; public string Name{get;set;}=""; public string Notes{get;set;}=""; public string VersionNumber{get;set;}="1";
    public string Yield{get;set;}="1"; public InventoryDimension YieldDimension{get;set;}=InventoryDimension.Mass; public string YieldUnit{get;set;}="g";
    public RecipeProductChoice? ProfileProduct{get;set;} public RecipeItemChoice? ProfileFinishedItem{get;set;}
    public IReadOnlyList<InventoryDimension> Dimensions{get;}=Enum.GetValues<InventoryDimension>();
    public IReadOnlyList<IngredientPolicyChoice> PolicyChoices{get;}=
    [
        new(IngredientCountPolicy.NotApplicable,"Cantidad exacta"),
        new(IngredientCountPolicy.ExactWholeCount,"Sólo piezas completas"),
        new(IngredientCountPolicy.CeilingWholeCount,"Redondear hacia arriba a pieza completa")
    ];
    public string PublishedNotice=>"Las versiones publicadas forman parte del historial y no se editan. Para hacer cambios crea una nueva versión.";
    public event PropertyChangedEventHandler? PropertyChanged; private void Notify()=>PropertyChanged?.Invoke(this,new(null));

    public Task LoadAsync()=>Run(async()=>
    {
        CanView=await Task.Run(()=>catalog.Can("recipe.view"));CanConfigure=await Task.Run(()=>catalog.Can("recipe.configure"));
        if(!CanView)throw new AccessDeniedException(); await LoadChoices(); await RefreshFormulas();screen="list";
    });
    private async Task LoadChoices()
    {
        var rows=new List<InventoryListRow>();for(int page=0;;page++){var result=await Task.Run(()=>inventory.List(new(Page:page,PageSize:100)));rows.AddRange(result.Items);if(rows.Count>=result.Total)break;}
        Items=rows.Where(x=>x.Active).Select(x=>new RecipeItemChoice(x.InventoryItemId,$"{x.Code} · {x.Name}",x.Dimension)).ToArray();
        var products=new List<ProductRow>();for(int page=0;;page++){var result=await Task.Run(()=>catalog.Products(new(IncludeInactive:false,Page:page)));products.AddRange(result.Items);if(products.Count>=result.Total)break;}
        Products=products.Select(x=>new RecipeProductChoice(x.Product.Id,x.Product.Name)).ToArray();
    }
    private async Task RefreshFormulas(){var summaries=await Task.Run(()=>service.ListFormulas(200));Formulas=summaries.Select(f=>{var versions=service.ListFormulaVersions(f.Id);return new RecipeFormulaRow(f,f.ActiveVersionId is {} active?$"Versión {versions.Single(x=>x.Value.Id==active).Value.Version}":"Sin activar",versions.Any(x=>x.Value.State==ProductionVersionState.Draft)?"Sí":"No");}).ToArray();SelectedFormula=null;LoadPublishedFormulaChoices(summaries);}
    private void LoadPublishedFormulaChoices(IEnumerable<FormulaSummary> summaries)=>PublishedFormulas=summaries.SelectMany(f=>service.ListFormulaVersions(f.Id).Where(v=>v.Value.State==ProductionVersionState.Published).Select(v=>new RecipeFormulaChoice(v.Value.Id,$"{f.Name} · versión {v.Value.Version}",v.Value.BaseYield.Dimension))).ToArray();
    private async Task RefreshPublishedFormulaChoices()=>LoadPublishedFormulaChoices(await Task.Run(()=>service.ListFormulas(200)));
    private async Task RefreshProfiles(){var summaries=await Task.Run(()=>service.ListProfiles(200));Profiles=summaries.Select(p=>{var versions=service.ListProfileVersions(p.Id);return new RecipeProfileRow(p,Products.SingleOrDefault(x=>x.Id==p.ProductId)?.Name??"Producto",Items.SingleOrDefault(x=>x.Id==p.FinishedItemId)?.Name??"Artículo terminado",p.ActiveVersionId is {} active?$"Versión {versions.Single(x=>x.Value.Id==active).Value.Version}":"Sin activar",versions.Any(x=>x.Value.State==ProductionVersionState.Draft)?"Sí":"No");}).ToArray();SelectedProfile=null;}
    public Task ShowFormulasAsync()=>Run(async()=>{await RefreshFormulas();screen="list";});
    public Task ShowProfilesAsync()=>Run(async()=>{await RefreshProfiles();screen="profiles";});
    public void NewFormula(){if(!CanConfigure)return;formulaId=null;Code="";Name="";screen="formulaEditor";Ingredients.Clear();Alternatives.Clear();Message="";Notify();}
    public Task CreateFormulaAsync()=>Run(async()=>{var id=Guid.NewGuid();await Task.Run(()=>service.CreateFormula(new(id,Code),Name));formulaId=id;await LoadFormula(id);MessageIsSuccess=true;Message="Receta creada. Agrega su primera versión borrador.";});
    public Task OpenFormulaAsync()=>Run(async()=>{var row=SelectedFormula??throw new ArgumentException("Selecciona una receta.");await LoadFormula(row.Summary.Id);});
    private async Task LoadFormula(Guid id){await RefreshFormulas();formulaId=id;var f=await Task.Run(()=>service.GetFormula(id))??throw new ArgumentException("La receta ya no existe.");Name=f.Name;Code=f.Code;Versions=(await Task.Run(()=>service.ListFormulaVersions(id))).Select(v=>new RecipeVersionRow(v.Value.Id,v.Value.Version,v.Value.State==ProductionVersionState.Published?"Publicada":"Borrador",v.Value.Name,v.Revision,f.ActiveVersionId==v.Value.Id)).ToArray();selectedVersion=null;screen="formula";}
    public void NewFormulaVersion(){if(!CanConfigure||formulaId is null)return;selectedVersion=null;VersionNumber=(Versions.Select(x=>x.Number).DefaultIfEmpty(0).Max()+1).ToString();Name="";Notes="";Yield="1";YieldDimension=InventoryDimension.Mass;YieldUnit="g";Ingredients.Clear();Alternatives.Clear();screen="formulaEditor";Notify();}
    public Task EditFormulaVersionAsync()=>Run(async()=>
    {
        var row=selectedVersion??throw new ArgumentException("Selecciona una versión.");if(row.State!="Borrador")throw new ArgumentException(PublishedNotice);
        var stored=await Task.Run(()=>service.GetFormulaVersion(row.Id))??throw new ArgumentException("La versión ya no existe.");var v=stored.Value;
        VersionNumber=v.Version.ToString();Name=v.Name;Notes=v.Notes??"";YieldDimension=v.BaseYield.Dimension;Yield=InventoryDisplay.Number(v.BaseYield.AtomicUnits,v.BaseYield.Dimension);YieldUnit=InventoryDisplay.Unit(v.BaseYield.AtomicUnits,v.BaseYield.Dimension);Ingredients.Clear();Alternatives.Clear();
        foreach(var i in v.Ingredients)Ingredients.Add(Edit(i));int group=0;foreach(var g in v.Alternatives){group++;var order=g.PreferenceOrder.ToArray();foreach(var o in g.Options){var e=Edit(o.Ingredient);e.Id=o.Id;e.Group=group;e.Preference=Array.IndexOf(order,o.Id)+1;Alternatives.Add(e);}}
        screen="formulaEditor";
    });
    private IngredientEdit Edit(FormulaIngredient value)=>new(){Item=Items.SingleOrDefault(x=>x.Id==value.InventoryItemId),Quantity=InventoryDisplay.Number(value.Quantity.AtomicUnits,value.Quantity.Dimension),Unit=InventoryDisplay.Unit(value.Quantity.AtomicUnits,value.Quantity.Dimension),Policy=value.CountPolicy};
    public void AddIngredient(bool alternative){var row=new IngredientEdit();if(alternative){row.Group=Alternatives.Select(x=>x.Group).DefaultIfEmpty(0).Max()+1;row.Preference=1;Alternatives.Add(row);}else Ingredients.Add(row);Notify();}
    public void RemoveIngredient(bool alternative){if(alternative&&SelectedAlternative is {} a)Alternatives.Remove(a);else if(!alternative&&SelectedIngredient is {} i)Ingredients.Remove(i);Notify();}
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

    public void NewProfile(){if(!CanConfigure)return;profileId=null;ProfileProduct=null;ProfileFinishedItem=null;Name="";FormulaRequirements.Clear();DirectIngredients.Clear();screen="profileEditor";Notify();}
    public Task CreateProfileAsync()=>Run(async()=>{var product=ProfileProduct??throw new ArgumentException("Selecciona un producto.");var item=ProfileFinishedItem??throw new ArgumentException("Selecciona el artículo terminado.");var id=Guid.NewGuid();await Task.Run(()=>service.CreateProfile(new(id,product.Id,item.Id)));profileId=id;await LoadProfile(id);MessageIsSuccess=true;Message="Perfil creado. Agrega su primera versión borrador.";});
    public Task OpenProfileAsync()=>Run(async()=>{var row=SelectedProfile??throw new ArgumentException("Selecciona un perfil.");await LoadProfile(row.Summary.Id);});
    private async Task LoadProfile(Guid id){await RefreshProfiles();profileId=id;var p=await Task.Run(()=>service.GetProfile(id))??throw new ArgumentException("El perfil ya no existe.");ProfileProduct=Products.SingleOrDefault(x=>x.Id==p.ProductId);ProfileFinishedItem=Items.SingleOrDefault(x=>x.Id==p.FinishedItemId);Versions=(await Task.Run(()=>service.ListProfileVersions(id))).Select(v=>new RecipeVersionRow(v.Value.Id,v.Value.Version,v.Value.State==ProductionVersionState.Published?"Publicada":"Borrador",v.Value.Name,v.Revision,p.ActiveVersionId==v.Value.Id)).ToArray();selectedVersion=null;screen="profile";}
    public void NewProfileVersion(){if(!CanConfigure||profileId is null)return;selectedVersion=null;VersionNumber=(Versions.Select(x=>x.Number).DefaultIfEmpty(0).Max()+1).ToString();Name="";FormulaRequirements.Clear();DirectIngredients.Clear();screen="profileEditor";Notify();}
    public Task EditProfileVersionAsync()=>Run(async()=>
    {
        var row=selectedVersion??throw new ArgumentException("Selecciona una versión.");if(row.State!="Borrador")throw new ArgumentException(PublishedNotice);
        var stored=await Task.Run(()=>service.GetProfileVersion(row.Id))??throw new ArgumentException("La versión ya no existe.");var v=stored.Value;
        VersionNumber=v.Version.ToString();Name=v.Name;FormulaRequirements.Clear();DirectIngredients.Clear();
        foreach(var f in v.Formulas){var choice=PublishedFormulas.SingleOrDefault(x=>x.Id==f.FormulaVersionId);FormulaRequirements.Add(new(){Formula=choice,Quantity=InventoryDisplay.Number(f.QuantityPerPiece.AtomicUnits,f.QuantityPerPiece.Dimension),Unit=InventoryDisplay.Unit(f.QuantityPerPiece.AtomicUnits,f.QuantityPerPiece.Dimension)});}
        foreach(var d in v.DirectIngredients)DirectIngredients.Add(Edit(d.Ingredient));screen="profileEditor";
    });
    public void AddFormulaRequirement()=>FormulaRequirements.Add(new()); public void AddDirectIngredient()=>DirectIngredients.Add(new());
    public void RemoveFormulaRequirement(){if(SelectedFormulaRequirement is {} r)FormulaRequirements.Remove(r);} public void RemoveDirectIngredient(){if(SelectedDirect is {} r)DirectIngredients.Remove(r);}
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
    public static string ErrorMessage(ProductionDomainException error)=>error.Code switch
    {
        ProductionError.InvalidVersion=>"Revisa el nombre y número de la versión.",
        ProductionError.InvalidFormulaYield=>"El rendimiento debe ser mayor que cero y usar una unidad compatible.",
        ProductionError.InvalidRecipeQuantity=>"Revisa que todas las cantidades sean mayores que cero.",
        ProductionError.DimensionMismatch=>"Una unidad o política no corresponde al tipo de artículo seleccionado.",
        ProductionError.DuplicateIngredient=>"Un mismo artículo no puede repetirse en la receta.",
        ProductionError.InvalidAlternative=>"Cada grupo de alternativas requiere al menos dos opciones válidas y un orden de preferencia.",
        ProductionError.Overflow=>"Una cantidad excede el límite permitido.",
        _=>"Revisa cantidades, unidades, duplicados y alternativas de la receta."
    };
    private async Task Run(Func<Task> action){if(Busy)return;Busy=true;Message="";MessageIsSuccess=false;Notify();try{await action();}catch(AccessDeniedException){Message="No tienes permiso para esta operación de recetas.";log("recipe.authorization.denied",null);}catch(ProductionDomainException e){Message=ErrorMessage(e);}catch(RecipeConcurrencyException){Message="La receta cambió. Actualiza la información antes de continuar.";}catch(ArgumentException e){Message=e.Message;}catch(Exception e){Message="No se completó la operación de recetas.";log("recipe.operation.failed",e.GetType().Name);}finally{Busy=false;Notify();}}
}
