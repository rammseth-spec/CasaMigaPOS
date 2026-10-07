using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Presentation;
public sealed record PosCategory(Guid? Id, string Label, bool Selected);
public sealed record PosCartLine(CartLine Line)
{
    public Guid Id => Line.ProductId;
    public string Name => Line.ProductName;
    public string UnitPrice => PosViewModel.Format(Line.UnitPrice) + " / pieza";
    public int Quantity => Line.Quantity;
    public string Total => PosViewModel.Format(Line.LineTotal);
    public bool CanDecrease => Quantity > 1;
}
public sealed class PosViewModel(PosService service, CheckoutService checkout, ReceiptService receipts,
    IProductPhotoUiGateway photos, Func<string> operatorLabel, Action<string,string?> log) : INotifyPropertyChanged
{
    private Guid? category;
    private int page;
    private Cart? cart;
    private long searchRevision;
    private CancellationTokenSource? searchCancellation;
    private readonly SemaphoreSlim cartOperations = new(1, 1);
    private int pendingCartOperations;
    private bool entered;
    public bool Busy { get; private set; }
    public bool Ready => !Busy;
    public CheckoutService Checkout => checkout;
    public ReceiptService Receipts => receipts;
    public Money TotalMoney => cart?.Subtotal ?? new Money(0);
    public bool RecoveryRequired { get; private set; }
    public bool CanOperate => Ready && !RecoveryRequired;
    public bool CanCheckout => CanOperate && HasLines;
    public bool IsDevelopment { get; init; }
    public string OperatorLabel => operatorLabel();
    private string search = "";
    public string Search
    {
        get => search;
        set
        {
            if (search == value) return;
            search = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Search)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSearch)));
            if(entered&&Ready)_=StartSearchAsync(debounce:true);
        }
    }
    public bool HasSearch=>!string.IsNullOrWhiteSpace(Search);
    public bool IsSearching {get;private set;}
    public string Message { get; private set; } = "";
    public IReadOnlyList<PosCategory> Categories { get; private set; } = [];
    private CancellationTokenSource photoLoad = new();
    public IReadOnlyList<ProductCardViewModel> Products { get; private set; } = [];
    public IReadOnlyList<PosCartLine> Lines => cart?.Lines.Select(l=>new PosCartLine(l)).ToArray() ?? [];
    public int ProductCount => cart?.Lines.Count ?? 0;
    public long PieceCount => cart?.Lines.Aggregate(0L,(total,line)=>checked(total+line.Quantity)) ?? 0;
    public string CartSummary => $"{ProductCount} {(ProductCount==1?"producto":"productos")} · {PieceCount} {(PieceCount==1?"pieza":"piezas")}";
    public bool HasLines => cart is { IsEmpty:false };
    public bool IsEmpty => !HasLines;
    public bool HasNoProducts=>Products.Count==0&&!RecoveryRequired;
    public string EmptyProductsMessage=>HasSearch
        ?"No encontramos productos con esa búsqueda."
        :category is null?"No hay productos disponibles.":"No hay productos disponibles en esta categoría.";
    public string Subtotal => Format(cart?.Subtotal ?? new Money(0));
    public string Total => Format(cart?.PreliminaryTotal ?? new Money(0));
    public string PageLabel { get; private set; } = "";
    public bool Previous => page>0;
    public bool Next { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
    public static string Format(Money money)=>(money.Cents/100m).ToString("C2",CultureInfo.GetCultureInfo("es-MX"));
    public async Task EnterAsync()
    {
        CancelSearch();entered=false;Search="";category=null;page=0;Products=[];Categories=[];cart=null;Message="";Notify();
        try { RecoveryRequired=checkout.HasPending; }
        catch { RecoveryRequired=true; }
        if(RecoveryRequired){Message="Verifica el cobro pendiente antes de continuar.";Notify();return;}
        // Propagate refusal to the shell: a closed cash session must not enter POS.
        var result=await Task.Run(()=>service.Load()); Apply(result);entered=true;Notify();
    }
    private void Apply(PosPage result)
    {
        cart=result.Cart;
        var cards=result.Products.Items.Select(row=>new ProductCardViewModel(row)).ToArray();Products=cards;StartPhotoLoad(cards);
        Categories=new[]{new PosCategory(null,"Todos",category is null)}.Concat(result.Categories.Select(c=>new PosCategory(c.Id,c.Name,c.Id==category))).ToArray();
        PageLabel=$"{result.Products.Total} productos · página {page+1}";Next=(page+1)*result.Products.PageSize<result.Products.Total;
    }
    private void StartPhotoLoad(IReadOnlyList<ProductCardViewModel> cards)
    {
        photoLoad.Cancel();photoLoad.Dispose();photoLoad=new();
        _=LoadPhotosSafeAsync(cards,photoLoad.Token);
    }
    private async Task LoadPhotosSafeAsync(IReadOnlyList<ProductCardViewModel> cards,CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(cards.Select(async card=>card.SetPhoto(await photos.LoadAsync(card.Id,cancellationToken))));
        }
        catch(OperationCanceledException){ }
        catch(Exception error){log("pos.photo.load.failed",error.GetType().Name);}
    }
    private async Task Refresh()=>Apply(await Task.Run(()=>service.Load(Search,category,page)));
    private void CancelSearch()
    {
        searchRevision++;searchCancellation?.Cancel();searchCancellation?.Dispose();searchCancellation=null;IsSearching=false;
    }
    private async Task StartSearchAsync(bool debounce)
    {
        CancelSearch();var revision=searchRevision;var cancellation=new CancellationTokenSource();searchCancellation=cancellation;var token=cancellation.Token;
        var query=Search;var selectedCategory=category;IsSearching=true;Notify();
        try
        {
            if(debounce)await Task.Delay(250,token);
            var result=await Task.Run(()=>service.Load(query,selectedCategory,0),token);
            if(token.IsCancellationRequested||revision!=searchRevision||!entered)return;
            page=0;Apply(result);Message="";
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){ }
        catch(Exception error)
        {
            if(token.IsCancellationRequested||revision!=searchRevision)return;
            log("pos.search.failed",error.GetType().Name);Message="No se pudo completar la búsqueda. Intenta nuevamente.";Products=[];
        }
        finally
        {
            if(revision==searchRevision){searchCancellation=null;IsSearching=false;Notify();}
            cancellation.Dispose();
        }
    }
    public Task SearchAsync()=>entered&&Ready?StartSearchAsync(debounce:false):Task.CompletedTask;
    public Task ClearSearchAsync(){Search="";return SearchAsync();}
    public Task CategoryAsync(Guid? id)=>Run(async()=>{CancelSearch();category=id;page=0;await Refresh();});
    public Task PageAsync(int delta)=>Run(async()=>{page=Math.Max(0,page+delta);await Refresh();});
    public Task AddAsync(Guid id)=>RunCart(async()=>cart=await Task.Run(()=>service.Add(id)));
    public Task IncrementAsync(Guid id)=>RunCart(async()=>cart=await Task.Run(()=>service.Increment(id)));
    public Task DecrementAsync(Guid id)=>RunCart(async()=>cart=await Task.Run(()=>service.Decrement(id)));
    public Task RemoveAsync(Guid id)=>RunCart(async()=>cart=await Task.Run(()=>service.Remove(id)));
    public void Discard(bool confirmed){service.Discard(confirmed);cart=null;Message="";Notify();}
    public void ForgetView(){CancelSearch();entered=false;photoLoad.Cancel();cart=null;Products=[];Categories=[];Search="";Message="";Notify();}
    public Task AfterCheckoutAsync()=>Run(EnterAsync);
    private async Task Run(Func<Task> operation)
    {
        if(Busy)return;Busy=true;Message="";Notify();
        try{await operation();}
        catch(OverflowException){Message="La cantidad o el total excede el límite permitido. El carrito no cambió.";}
        catch(ArgumentException e){Message=e.Message;}
        catch(PosException e){Message=e.Message;Products=[];}
        catch(CartDiscardRequiredException e){Message=e.Message;}
        catch(CheckoutException e){Message=e.Message;}
        catch(AccessDeniedException){Message="No tienes permiso para usar el POS.";Products=[];}
        catch(Exception e){log("pos.operation.failed",e.GetType().Name);Message="No se completó la operación. Revisa el estado del equipo antes de continuar.";}
        finally{Busy=false;Notify();}
    }
    private async Task RunCart(Func<Task> operation)
    {
        if(Interlocked.Increment(ref pendingCartOperations)==1){Busy=true;Message="";Notify();}
        await cartOperations.WaitAsync();
        try{await operation();}
        catch(OverflowException){Message="La cantidad o el total excede el límite permitido. El carrito no cambió.";}
        catch(ArgumentException e){Message=e.Message;}
        catch(PosException e){Message=e.Message;Products=[];}
        catch(CartDiscardRequiredException e){Message=e.Message;}
        catch(CheckoutException e){Message=e.Message;}
        catch(AccessDeniedException){Message="No tienes permiso para usar el POS.";Products=[];}
        catch(Exception e){log("pos.operation.failed",e.GetType().Name);Message="No se completó la operación. Revisa el estado del equipo antes de continuar.";}
        finally
        {
            cartOperations.Release();
            Busy=Interlocked.Decrement(ref pendingCartOperations)>0;
            Notify();
        }
    }
}
