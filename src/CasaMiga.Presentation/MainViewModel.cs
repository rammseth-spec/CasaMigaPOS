using System.ComponentModel;
using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;

namespace CasaMiga.Presentation;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public PosViewModel? Pos { get; private set; }
    public bool IsPos => screen == "pos";
    public bool IsNotPos => !IsPos;
    public bool HasCart => user?.Cart is { IsEmpty:false };
    public bool HasPendingCheckout => user?.CheckoutPending==true;
    public void DiscardCart() => Pos?.Discard(true);
    public CatalogViewModel? Catalog { get; private set; }
    public bool IsCatalog => screen == "catalog";
    public bool CanNavigate => user?.Current is not null;
    private IdentityService? login;
    private CashService? cash;
    private CashClosingService? closing;
    private ClosingResult? closingResult;
    private UserSession? user;
    private TerminalIdentity? terminal;
    private FoundationRuntime? runtime;
    private CashSessionView? opened;
    private string screen = "loading";
    public bool Busy { get; private set; }
    public bool IsReady => !Busy;
    public bool IsBootstrap => screen == "bootstrap";
    public bool IsLogin => screen == "login";
    public bool IsDashboard => screen == "dashboard";
    public bool IsCounting => screen == "counting";
    public bool IsClosingResult => screen == "closingResult";
    public bool HasOpenCash => opened is not null;
    public string CountedAmount { get; set; } = "";
    public string ClosingOwner => opened is null ? "" : $"Abierta por: {opened.OpenedByName}" + (opened.Session.OpenedBy != user?.Current?.Id ? " · Cierre por otro usuario" : " · Tu sesión");
    public string ClosingDetails => closingResult is not { } r ? "" : $"Fondo inicial: {Format(r.Session.OpeningFund)}\nEfectivo esperado: {Format(r.Expected)}\nEfectivo contado: {Format(r.Count.Counted)}\n{DifferenceLabel(r.Difference)}";
    private static string Format(Money value) => (value.Cents/100m).ToString("C2",CultureInfo.GetCultureInfo("es-MX"));
    private static string DifferenceLabel(Money value) => value.Cents==0 ? "Diferencia: $0.00 · Caja exacta" : value.Cents>0 ? $"SOBRANTE {Format(value)}" : $"FALTANTE {Format(new Money(checked(-value.Cents)))}";
    public bool IsOpening => screen == "opening";
    public bool CanOpen { get; private set; }
    public bool HasNoOpenCash => opened is null;
    public string Amount { get; set; } = "0.00";
    public bool MessageIsSuccess { get; private set; }
    public string Message { get; private set; } = "";
    public string Heading => screen switch { "catalog" => "Catálogo", "counting" => "Conteo de caja", "closingResult" => "Resultado de caja", "bootstrap" => "Bienvenido a Casa Miga", "login" => "Inicia sesión", "dashboard" => "Tu caja", "opening" => "Abrir caja", _ => "Preparando Casa Miga" };
    public string Subtitle => screen switch { "catalog" => "Categorías y productos · Sin proceso de venta.", "counting" => "Cuenta el efectivo sin consultar el esperado. Continuar guarda tu conteo y no permite cambiarlo después.", "closingResult" => "Revisa el resultado y confirma el cierre definitivo.", "bootstrap" => "Configura el primer administrador. No hay contraseñas predeterminadas.", "login" => "Identifícate para consultar o abrir esta caja.", "opening" => "Registra el efectivo con el que comienza la sesión.", "dashboard" => "Sesión local · Tus datos permanecen en este equipo.", _ => "Validando base y respaldo…" };
    public string UserLabel => $"Usuario: {user?.Current?.DisplayName}";
    public string TerminalLabel => $"Terminal: {terminal?.Name}";
    public string CashLabel => opened is null ? "CAJA CERRADA" : "CAJA ABIERTA";
    public string CashDetails => opened is null ? "Captura el fondo inicial para comenzar." :
        $"Abierta por: {opened.OpenedByName}\nHora de apertura: {opened.Session.OpenedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss zzz}\n\nImportes ocultos hasta confirmar el conteo ciego. Las ventas confirmadas en efectivo se incluyen en el cierre.";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public async Task InitializeAsync(FoundationOptions options)
    {
        runtime = new(options);
        await Execute(async () =>
        {
            await Task.Run(() =>
            {
                runtime.Start();
                var database = new SqliteLocalUnitOfWork(new Database(System.IO.Path.Combine(options.DataDirectory, "casamiga.db")));
                var clock = new SystemClock();
                terminal = TerminalConfiguration.LoadOrCreate(options.DataDirectory);
                database.Run(store => { store.EnsureTerminal(terminal, clock.UtcNow); return true; });
                var journal = new CheckoutJournal(options.DataDirectory);
                user = new UserSession(journal); login = new(database, new LocalPasswords(), clock, user); cash = new(database, user, clock); closing = new(database,user,clock);
                var checkout = new CheckoutService(database,user,clock,terminal.Id,journal);
                Pos = new(new PosService(database,user,terminal.Id),checkout,()=>UserLabel + " · " + TerminalLabel,(eventName,errorType)=>runtime.TryLog(eventName,errorType)){IsDevelopment=options.IsDevelopment};
                Catalog = new(new CatalogService(database,user,clock,terminal.Id,options.IsDevelopment),options.IsDevelopment,(eventName,errorType)=>runtime.TryLog(eventName,errorType));
                screen = login.NeedsBootstrap() ? "bootstrap" : "login";
            });
        });
    }
    public Task BootstrapAsync(string name, string display, string password, string confirmation) => Execute(async () =>
    {
        if (password != confirmation) throw new ArgumentException("Las contraseñas no coinciden.");
        await Task.Run(() => login!.Bootstrap(name, display, password));
        screen = "login";
    });
    public Task LoginAsync(string name, string password) => Execute(async () =>
    {
        if (!await Task.Run(() => login!.Login(name, password))) { Message = "No se pudo iniciar sesión. Revisa tus datos o espera un minuto e inténtalo de nuevo."; return; }
        await RefreshAsync(); screen = "dashboard";
    });
    public void Logout() { if (Busy) return; user?.SignOut(); Pos?.ForgetView(); opened = null; closingResult = null; CountedAmount = ""; screen = "login"; Message = ""; Notify(); }
    public void ShowOpening() { if (Busy || !CanOpen) return; Amount = "0.00"; screen = "opening"; Message = ""; Notify(); }
    public void CancelOpening() { if (Busy) return; screen = "dashboard"; Message = ""; Notify(); }
    public Task OpenAsync() => Execute(async () =>
    {
        if (!decimal.TryParse(Amount, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var pesos))
            throw new ArgumentException("Escribe un importe válido, por ejemplo 500.00, sin comas.");
        var fund = Money.FromPesos(pesos);
        try { opened = await Task.Run(() => cash!.Open(terminal!.Id, fund)); }
        catch (CashAlreadyOpenException) { await RefreshAsync(); screen = "dashboard"; throw; }
        runtime!.TryLog("cash.opened"); CanOpen = false; screen = "dashboard";
    });
    public Task ShowPosAsync() => Execute(async () => {await Pos!.EnterAsync();screen="pos";});
    public Task ShowCatalogAsync() => Execute(async () => {await Catalog!.LoadAsync();screen="catalog";});
    public Task ShowCashAsync() => Execute(async () => {await RefreshAsync();screen="dashboard";});
    public Task BeginClosingAsync() => Execute(async () =>
    {
        if(opened is null) return;
        closingResult=await Task.Run(()=>closing!.Resume(opened.Session.Id));
        CountedAmount=""; screen=closingResult is null?"counting":"closingResult";
    });
    public void BackFromClosing() { if(Busy) return; screen="dashboard"; CountedAmount=""; Message=""; Notify(); }
    public Task SubmitCountAsync() => Execute(async () =>
    {
        if(!decimal.TryParse(CountedAmount,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var pesos))
            throw new ArgumentException("Escribe un importe válido con punto decimal, sin comas.");
        var counted=Money.FromPesos(pesos);
        closingResult=await Task.Run(()=>closing!.SubmitCount(opened!.Session.Id,counted));
        CountedAmount=""; runtime!.TryLog("cash.counted"); screen="closingResult";
    });
    public Task ConfirmCloseAsync() => Execute(async () =>
    {
        await Task.Run(()=>closing!.Close(opened!.Session.Id));
        runtime!.TryLog("cash.closed"); closingResult=null;
        await RefreshAsync(); screen="dashboard"; MessageIsSuccess=true; Message="Cierre registrado y auditado. La sesión histórica permanece cerrada.";
    });
    private async Task RefreshAsync()
    {
        opened = await Task.Run(() => cash!.Current(terminal!.Id));
        CanOpen = opened is null && await Task.Run(() => cash!.CanOpen());
    }
    private async Task Execute(Func<Task> action)
    {
        if (Busy) return;
        Busy = true; MessageIsSuccess=false; Message = ""; Notify();
        try { await action(); }
        catch (ArgumentOutOfRangeException) { Message = "El importe no puede ser negativo o exceder el límite permitido."; }
        catch (ArgumentException error) { Message = error.Message; }
        catch (OverflowException) { Message = "El importe excede el límite permitido."; }
        catch (AccessDeniedException error) { runtime?.TryLog("authorization.denied"); Message = error.Message; }
        catch (CashClosingException error) { runtime?.TryLog("cash.closing.rejected"); Message = error.Message; }
        catch (CashAlreadyOpenException error) { Message = error.Message; }
        catch (PosException error) { Message = error.Message; }
        catch (CartDiscardRequiredException error) { Message = error.Message; }
        catch (CheckoutException error) { Message = error.Message; }
        catch (Exception error) { runtime?.TryLog("operation.failed", error.GetType().Name); Message = "No se completó la operación. Revisa el espacio y permisos del equipo; si persiste, contacta al encargado."; }
        finally { Busy = false; Notify(); }
    }
}
