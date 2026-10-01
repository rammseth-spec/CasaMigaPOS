using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;

// Read/render probe restricted to a separately prepared copy. Never starts FoundationRuntime
// or composes printing/checkout. Login auditing is confined to that copy.
internal static class InventoryDetailProbe
{
    public static int Run(string directory, string password)
    {
        var target=Path.GetFullPath(directory);
        if(!Path.GetFileName(target).StartsWith("inventory-crash-",StringComparison.Ordinal))
            throw new ArgumentException("Sólo se admite una copia inventory-crash-*.");
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var app=new System.Windows.Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/CasaMiga.Presentation;component/Resources/Theme.xaml",UriKind.Relative)});
            app.DispatcherUnhandledException+=(_,e)=>{failure=e.Exception;e.Handled=true;app.Dispatcher.InvokeShutdown();};
            app.Dispatcher.BeginInvoke(async ()=>
            {
                try
                {
                    using var lease=new DirectoryLease(target);
                    var database=new Database(Path.Combine(target,"casamiga.db"));
                    database.Validate();Console.WriteLine("Integrity and foreign keys: OK (isolated copy).");
                    var work=new SqliteLocalUnitOfWork(database);var clock=new SystemClock();var session=new UserSession();
                    var identity=new IdentityService(work,new LocalPasswords(),clock,session);
                    if(!identity.Login("catalog-admin",password))throw new InvalidOperationException("Credencial de la copia inválida.");
                    var terminal=TerminalConfiguration.LoadOrCreate(target);
                    for(var restart=0;restart<2;restart++)
                    {
                        var vm=new InventoryViewModel(new(new SqliteInventoryStore(database,clock),work,session,clock,terminal.Id),new(work,session,clock,terminal.Id),(_,_)=>{});
                        await vm.LoadAsync();vm.Search="MP-HAR-001";await vm.SearchAsync();
                        var view=new InventoryView{DataContext=vm};
                        for(var attempt=0;attempt<3;attempt++)
                        {
                            vm.SelectedItem=vm.Items.Single(x=>x.Code=="MP-HAR-001");await vm.OpenDetailAsync();
                            if(vm.Detail is null||vm.Detail.Balance is not null||vm.History.Count!=0||vm.ProductLinks.Count!=0)
                                throw new InvalidOperationException("El detalle no coincide con Harina sin inicializar.");
                            var unit=vm.Units.Single(x=>x.Code=="HAR-BULTO-25KG");
                            if(!unit.IsActive||unit.Quantity!="25 kg")throw new InvalidOperationException("Presentación inesperada.");
                            view.Measure(new Size(1200,1600));view.Arrange(new Rect(0,0,1200,1600));view.UpdateLayout();
                            Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);view.UpdateLayout();
                            var grids=Descendants<DataGrid>(view);
                            var unitsGrid=grids.Single(g=>System.Windows.Automation.AutomationProperties.GetName(g)=="Presentaciones del artículo");
                            var check=Descendants<CheckBox>(unitsGrid).Single();
                            if(check.IsChecked!=true)throw new InvalidOperationException("Activa no está marcada en WPF.");
                            Console.WriteLine(JsonSerializer.Serialize(new{Restart=restart,Open=attempt,unit.Code,unit.Quantity,Checked=check.IsChecked,Balance="SIN INICIALIZAR",Links=0,Movements=0}));
                            vm.Back();
                        }
                    }
                }
                catch(Exception e){failure=e;}
                finally{app.Dispatcher.InvokeShutdown();}
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(failure is not null){Console.Error.WriteLine(failure);return 1;}return 0;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);
            if(child is T typed)yield return typed;
            foreach(var descendant in Descendants<T>(child))yield return descendant;
        }
    }
}
