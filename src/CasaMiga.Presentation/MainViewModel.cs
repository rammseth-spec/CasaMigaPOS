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
    private bool posSidebarExpanded;
    public bool IsPosSidebarCompact => IsPos && !posSidebarExpanded;
    public bool IsPosSidebarExpanded => IsPos && posSidebarExpanded;
    public double SidebarWidth => IsPosSidebarCompact ? 84 : 248;
    public System.Windows.Controls.ScrollBarVisibility SidebarScrollBarVisibility => IsPosSidebarCompact
        ? System.Windows.Controls.ScrollBarVisibility.Hidden
        : System.Windows.Controls.ScrollBarVisibility.Auto;
    public System.Windows.Thickness ShellContentMargin => IsPos ? new(14,12,14,12) : new(24,20,24,20);
    public void TogglePosSidebar(){if(!IsPos)return;posSidebarExpanded=!posSidebarExpanded;Notify();}
    public bool HasCart => user?.Cart is { IsEmpty:false };
    public bool HasPendingCheckout => user?.CheckoutPending==true;
    public void DiscardCart() => Pos?.Discard(true);
    public CatalogViewModel? Catalog { get; private set; }
    public bool IsCatalog => screen == "catalog";
    public SalesHistoryViewModel? SalesHistory { get; private set; }
    public bool IsSalesHistory => screen == "salesHistory";
    public bool CanViewSales { get; private set; }
    public InventoryViewModel? Inventory { get; private set; }
    public bool IsInventory => screen == "inventory";
    public bool CanViewInventory { get; private set; }
    public RecipeViewModel? Recipes { get; private set; }
    public bool IsRecipes => screen == "recipes";
    public bool CanViewRecipes { get; private set; }
    public ProductionViewModel? Production { get; private set; }
    public bool IsProduction => screen == "production";
    public bool CanViewProduction { get; private set; }
    private CatalogService? catalogService;
    public bool CanNavigate => user?.Current is not null;
    public bool IsSignedOut => !CanNavigate;
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
    public bool IsChangePassword => screen == "changePassword";
    public bool IsUsers => screen == "users";
    public bool CanManageUsers { get; private set; }
    public string PasswordRules => PasswordPolicy.Description;
    public string ChangePasswordUser { get; set; } = "";
    public IReadOnlyList<UserRow> Users { get; private set; } = [];
    public UserRow? SelectedUser { get; set; }
    public string NewUserName { get; set; } = "";
    public string NewUserDisplay { get; set; } = "";
    public IReadOnlyList<RoleOption> RoleOptions { get; } = [new("Cashier", "Cajero"), new("Manager", "Encargado")];
    private static string RoleName(string role) => role switch { "Administrator" => "Administrador", "Manager" => "Encargado", "Cashier" => "Cajero", _ => role };
    public string NewUserRole { get; set; } = "Cashier";
    public string TemporaryPasswordNotice { get; private set; } = "";
    public bool HasTemporaryPasswordNotice => TemporaryPasswordNotice.Length > 0;
    public bool IsDashboard => screen == "dashboard";
    public bool IsCounting => screen == "counting";
    public bool IsClosingResult => screen == "closingResult";
    public bool HasOpenCash => opened is not null;
    public string CountedAmount { get; set; } = "";
    public string ClosingOwner => opened is null ? "" : $"Abierta por: {opened.OpenedByName}" + (opened.Session.OpenedBy != user?.Current?.Id ? " · Cierre por otro usuario" : " · Tu sesión");
    public string ClosingDetails => closingResult is not { } r ? "" : $"Fondo inicial: {Format(r.Session.OpeningFund)}\n" + (r.Refunds.Cents > 0 ? $"Devoluciones en efectivo: −{Format(r.Refunds)}\n" : "") + $"Efectivo esperado: {Format(r.Expected)}\nEfectivo contado: {Format(r.Count.Counted)}\n{DifferenceLabel(r.Difference)}";
    private static string Format(Money value) => (value.Cents/100m).ToString("C2",CultureInfo.GetCultureInfo("es-MX"));
    private static string DifferenceLabel(Money value) => value.Cents==0 ? "Diferencia: $0.00 · Caja exacta" : value.Cents>0 ? $"SOBRANTE {Format(value)}" : $"FALTANTE {Format(new Money(checked(-value.Cents)))}";
    public bool IsOpening => screen == "opening";
    public bool CanOpen { get; private set; }
    public bool HasNoOpenCash => opened is null;
    public string Amount { get; set; } = "0.00";
    public bool MessageIsSuccess { get; private set; }
    public string Message { get; private set; } = "";
    public string Heading => screen switch { "pos"=>"Punto de venta", "catalog" => "Catálogo", "salesHistory"=>"Historial de ventas", "inventory" => "Inventario", "recipes"=>"Recetas", "production"=>"Producción", "counting" => "Conteo de caja", "closingResult" => "Resultado de caja", "bootstrap" => "Bienvenido a Casa Miga", "login" => "Inicia sesión", "changePassword" => "Cambiar contraseña", "users" => "Usuarios", "dashboard" => "Tu caja", "opening" => "Abrir caja", _ => "Preparando Casa Miga" };
    public string Subtitle => screen switch { "pos"=>"Selecciona productos, revisa el carrito y cobra.", "catalog" => "Categorías y productos · Sin proceso de venta.", "salesHistory"=>"Ventas confirmadas · Consulta de solo lectura.", "inventory" => "Existencias y movimientos del almacén principal · consulta abajo el estado del consumo automático por ventas.", "recipes"=>"Fórmulas y perfiles productivos versionados.", "production"=>"Planeación, confirmación e historial de producción.", "counting" => "Cuenta el efectivo sin consultar el esperado. Continuar guarda tu conteo y no permite cambiarlo después.", "closingResult" => "Revisa el resultado y confirma el cierre definitivo.", "bootstrap" => "Configura el primer administrador. No hay contraseñas predeterminadas.", "login" => "Identifícate para consultar o abrir esta caja.", "changePassword" => "Confirma tu contraseña actual o temporal y elige una nueva.", "users" => "Crea usuarios y restablece contraseñas olvidadas · Cada acción queda auditada.", "opening" => "Registra el efectivo con el que comienza la sesión.", "dashboard" => "Sesión local · Tus datos permanecen en este equipo.", _ => "Validando base y respaldo…" };
    public string UserLabel => $"Usuario: {user?.Current?.DisplayName}";
    public string TerminalLabel => $"Terminal: {terminal?.Name}";
    public string CurrentUserName => user?.Current?.DisplayName ?? "";
    public string CurrentTerminalName => terminal?.Name ?? "";
    public string CurrentRoleLabel { get; private set; } = "";
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
                var sqlite = new Database(System.IO.Path.Combine(options.DataDirectory, "casamiga.db"));
                var database = new SqliteLocalUnitOfWork(sqlite);
                var clock = new SystemClock();
                terminal = TerminalConfiguration.LoadOrCreate(options.DataDirectory);
                database.Run(store => { store.EnsureTerminal(terminal, clock.UtcNow); return true; });
                var journal = new CheckoutJournal(options.DataDirectory);
                user = new UserSession(journal); login = new(database, new LocalPasswords(), clock, user); cash = new(database, user, clock); closing = new(database,user,clock);
                var checkout = new CheckoutService(database,user,clock,terminal.Id,journal, new CheckoutReceiptSnapshot(new PrintingConfigurationProvider(options.DataDirectory),TimeZoneInfo.Local));
                var receipts = new ReceiptService(database,user,clock,terminal.Id,TimeZoneInfo.Local,
                    new PrintingConfigurationProvider(options.DataDirectory),new EscPosReceiptRenderer(),
                    new SerializedReceiptPrintTransport(new WindowsRawPrintTransport()));
                var photoService=new ProductPhotoService(new SqliteProductPhotoStore(sqlite),new ProductPhotoFileStore(options.DataDirectory),new WpfProductPhotoProcessor(),user,clock,terminal.Id);
                var photoUi=new ProductPhotoUiGateway(photoService,new BoundedProductPhotoCache());
                Pos = new(new PosService(database,user,terminal.Id),checkout,receipts,photoUi,()=>UserLabel + " · " + TerminalLabel,(eventName,errorType)=>runtime.TryLog(eventName,errorType)){IsDevelopment=options.IsDevelopment};
                catalogService = new CatalogService(database,user,clock,terminal.Id,options.IsDevelopment);
                Catalog = new(catalogService,photoUi,options.IsDevelopment,(eventName,errorType)=>runtime.TryLog(eventName,errorType));
                SalesHistory = new(new SalesHistoryService(database,user,TimeZoneInfo.Local),receipts,(eventName,errorType)=>runtime.TryLog(eventName,errorType),new RefundService(database,user,clock,terminal.Id));
                Inventory = new(new InventoryService(new SqliteInventoryStore(sqlite, clock), database, user, clock, terminal.Id),
                    catalogService, (eventName,errorType)=>runtime.TryLog(eventName,errorType),new ConsumptionViewModel(new SaleConsumptionService(database,database,user,clock,terminal.Id)));
                var inventoryService=new InventoryService(new SqliteInventoryStore(sqlite,clock),database,user,clock,terminal.Id);
                var productionWork=new SqliteProductionUnitOfWork(sqlite);
                var recipeService=new RecipeService(productionWork,user,clock,terminal.Id);
                Recipes=new(recipeService,inventoryService,catalogService,(eventName,errorType)=>runtime.TryLog(eventName,errorType));
                Production=new(new ProductionService(productionWork,user,clock,terminal.Id),recipeService,inventoryService,catalogService,user,terminal.Id,(eventName,errorType)=>runtime.TryLog(eventName,errorType));
                screen = login.NeedsBootstrap() ? "bootstrap" : "login";
            });
        });
    }
    public Task BootstrapAsync(string name, string display, string password, string confirmation) => Execute(async () =>
    {
        if (password != confirmation) throw new ArgumentException("Las contraseñas no coinciden.");
        await Task.Run(() => login!.Bootstrap(name, display, password));
        screen = "login";
        Message = "Administrador creado. Inicia sesión con el usuario y la contraseña que acabas de elegir.";
        MessageIsSuccess = true;
    });
    public Task LoginAsync(string name, string password) => Execute(async () =>
    {
        var outcome = await Task.Run(() => login!.Authenticate(name, password));
        if (outcome == LoginOutcome.PasswordChangeRequired)
        {
            ChangePasswordUser = name.Trim(); screen = "changePassword";
            Message = "Tu contraseña es temporal. Crea una contraseña nueva para continuar."; MessageIsSuccess = true; return;
        }
        if (outcome != LoginOutcome.SignedIn) { Message = "No se pudo iniciar sesión. Revisa tus datos o espera un minuto e inténtalo de nuevo."; return; }
        await RefreshAsync(); screen = "dashboard";
    });
    public void ShowChangePassword(string name) { if (Busy) return; ChangePasswordUser = name.Trim(); screen = "changePassword"; MessageIsSuccess = false; Message = ""; Notify(); }
    public void CancelChangePassword() { if (Busy) return; screen = "login"; MessageIsSuccess = false; Message = ""; Notify(); }
    public Task ChangePasswordAsync(string current, string next, string confirmation) => Execute(async () =>
    {
        if (next != confirmation) throw new ArgumentException("Las contraseñas nuevas no coinciden.");
        var name = ChangePasswordUser;
        await Task.Run(() => login!.ChangePassword(name, current, next));
        runtime?.TryLog("identity.password.changed");
        if (await Task.Run(() => login!.Authenticate(name, next)) == LoginOutcome.SignedIn) { await RefreshAsync(); screen = "dashboard"; }
        else screen = "login";
        MessageIsSuccess = true; Message = "Contraseña actualizada.";
    });
    public Task ForgotPasswordAsync(string name) => Execute(async () =>
    {
        if (name.Trim().Length > 0) await Task.Run(() => login!.RequestPasswordHelp(name));
        MessageIsSuccess = true;
        Message = "Pide a un administrador que restablezca tu contraseña en Usuarios. Te dará una contraseña temporal; al entrar con ella crearás una nueva.";
    });
    public Task ShowUsersAsync() => Execute(async () => { await LoadUsersAsync(); TemporaryPasswordNotice = ""; screen = "users"; });
    public Task CreateUserAsync() => Execute(async () =>
    {
        string name = NewUserName, display = NewUserDisplay, role = NewUserRole;
        var temporary = await Task.Run(() => login!.CreateUser(name, display, role));
        runtime?.TryLog("identity.user.created");
        TemporaryPasswordNotice = Notice(name.Trim().ToUpperInvariant(), temporary);
        NewUserName = ""; NewUserDisplay = ""; NewUserRole = "Cashier";
        await LoadUsersAsync(); MessageIsSuccess = true; Message = "Usuario creado.";
    });
    public Task ResetPasswordAsync() => Execute(async () =>
    {
        if (SelectedUser is not { } target) throw new ArgumentException("Selecciona un usuario.");
        var temporary = await Task.Run(() => login!.ResetPassword(target.Id));
        runtime?.TryLog("identity.password.reset");
        TemporaryPasswordNotice = Notice(target.Username, temporary);
        await LoadUsersAsync(); MessageIsSuccess = true; Message = "Contraseña restablecida.";
    });
    private static string Notice(string username, string temporary) =>
        $"Contraseña temporal de {username}: {temporary}\nEntrégala en persona. Sólo se muestra esta vez y deberá cambiarla al iniciar sesión.";
    private async Task LoadUsersAsync()
    {
        var users = await Task.Run(() => login!.ListUsers());
        Users = users.Select(u => new UserRow(u.Id, u.Username, u.DisplayName, RoleName(u.Role),
            !u.Active ? "Inactivo" : u.MustChangePassword ? "Debe cambiar contraseña" : "Activo")).ToList();
        SelectedUser = null;
    }
    public void Logout() { if (Busy) return; user?.SignOut(); Users = []; SelectedUser = null; TemporaryPasswordNotice = ""; CanManageUsers = false; Pos?.ForgetView(); SalesHistory?.Forget(); Inventory?.Forget(); opened = null; closingResult = null; CountedAmount = ""; CanViewSales=false; CanViewInventory=false;CanViewRecipes=false;CanViewProduction=false;CurrentRoleLabel=""; screen = "login"; Message = ""; Notify(); }
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
    public Task ShowSalesHistoryAsync() => Execute(async () => {await SalesHistory!.LoadAsync();screen="salesHistory";});
    public Task ShowInventoryAsync() => Execute(async () =>
    {
        await Inventory!.LoadAsync(); CanViewInventory = Inventory.CanView;
        if (Inventory.CanView) screen = "inventory"; else Message = Inventory.Message;
    });
    public Task ShowRecipesAsync()=>Execute(async()=>{await Recipes!.LoadAsync();CanViewRecipes=Recipes.CanView;if(CanViewRecipes)screen="recipes";else Message=Recipes.Message;});
    public Task ShowProductionAsync()=>Execute(async()=>{await Production!.LoadAsync();CanViewProduction=Production.CanView;if(CanViewProduction)screen="production";else Message=Production.Message;});
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
        CanViewSales = await Task.Run(() => SalesHistory!.CanView());
        CanViewInventory = await Task.Run(() => Inventory!.CanEnter());
        CanViewRecipes=await Task.Run(()=>catalogService!.Can("recipe.view"));
        CanViewProduction=await Task.Run(()=>catalogService!.Can("production.view"));
        var canConfigureRecipes=await Task.Run(()=>catalogService!.Can("recipe.configure"));
        CurrentRoleLabel=canConfigureRecipes?"Administrador":CanViewProduction?"Manager":"Cajero";
        CanManageUsers=await Task.Run(()=>login!.CanManageUsers());
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
public sealed record UserRow(Guid Id, string Username, string DisplayName, string Role, string Status);
public sealed record RoleOption(string Id, string Name);
