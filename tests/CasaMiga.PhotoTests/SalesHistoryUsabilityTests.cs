using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class SalesHistoryUsabilityTests
{
    [TestMethod]
    public async Task HumanFiltersUseLocalDatesAndTheRealCurrentCashSession()
    {
        using var fixture=new Fixture();var vm=fixture.ViewModel();await vm.LoadAsync();
        Assert.AreEqual("Todos · 5 ventas",vm.FilterSummary);Assert.IsTrue(vm.Sales.All(x=>x.SaleNumber.StartsWith("Venta #")));Assert.IsTrue(vm.Sales.All(x=>x.Terminal=="CAJA PRUEBA"));
        Assert.IsTrue(vm.Sales.All(x=>x.Method=="Efectivo"));Assert.IsFalse(vm.Sales.Any(x=>x.SaleNumber.Contains(fixture.CurrentSessionId.ToString(),StringComparison.OrdinalIgnoreCase)));

        await vm.TodayAsync();Assert.AreEqual("Hoy · 3 ventas",vm.FilterSummary);Assert.IsTrue(vm.Sales.All(x=>x.Date.StartsWith("07/10/2026 · ")));
        await vm.YesterdayAsync();Assert.AreEqual("Ayer · 1 venta",vm.FilterSummary);Assert.AreEqual("06/10/2026 · 12:00",vm.Sales.Single().Date);
        await vm.SearchDatesAsync("06/10/2026","07/10/2026");Assert.AreEqual("Rango personalizado · 4 ventas",vm.FilterSummary);
        await vm.CurrentSessionAsync();Assert.AreEqual("Sesión actual · 2 ventas",vm.FilterSummary);Assert.IsTrue(vm.Sales.All(x=>fixture.CurrentSaleIds.Contains(x.SaleId)));

        var withoutSession=fixture.ViewModel(()=>null);await withoutSession.CurrentSessionAsync();
        Assert.AreEqual("No hay una sesión de caja abierta.",withoutSession.Message);Assert.AreEqual("Sesión actual · 0 ventas",withoutSession.FilterSummary);Assert.HasCount(0,withoutSession.Sales);
        var emptySession=fixture.ViewModel(()=>Guid.NewGuid());await emptySession.CurrentSessionAsync();
        Assert.IsTrue(emptySession.HasNoSales);Assert.AreEqual("No hay ventas registradas en esta sesión.",emptySession.EmptyMessage);Assert.AreEqual("Sesión actual · 0 ventas",emptySession.FilterSummary);
        fixture.AssertReadOnly();
    }

    [TestMethod]
    public async Task ManualDateRangeIsStrictAndInvalidInputKeepsTheAppliedFilter()
    {
        using var fixture=new Fixture();var vm=fixture.ViewModel();await vm.TodayAsync();
        var priorSummary=vm.FilterSummary;var priorSales=vm.Sales.Select(x=>x.SaleId).ToArray();
        foreach(var invalid in new[]{"32/10/2026","31/02/2026","abc","07/13/2026","7-10-2026"})
        {
            await vm.SearchDatesAsync(invalid,"07/10/2026");
            Assert.AreEqual(invalid,vm.FromDateText);Assert.AreEqual("07/10/2026",vm.ThroughDateText);StringAssert.Contains(vm.FromDateError,"dd/mm/aaaa");Assert.IsTrue(vm.HasFromDateError);
            Assert.AreEqual(priorSummary,vm.FilterSummary);CollectionAssert.AreEqual(priorSales,vm.Sales.Select(x=>x.SaleId).ToArray());
        }
        await vm.SearchDatesAsync("08/10/2026","07/10/2026");
        StringAssert.Contains(vm.DateRangeError,"no puede ser posterior");Assert.AreEqual(priorSummary,vm.FilterSummary);CollectionAssert.AreEqual(priorSales,vm.Sales.Select(x=>x.SaleId).ToArray());

        await vm.SearchDatesAsync("06/10/2026","07/10/2026");
        Assert.AreEqual("",vm.Message);Assert.AreEqual("",vm.FromDateError);Assert.AreEqual("",vm.ThroughDateError);Assert.AreEqual("",vm.DateRangeError);
        Assert.AreEqual("Rango personalizado · 4 ventas",vm.FilterSummary);Assert.AreEqual(new DateTime(2026,10,6),vm.FromDate);Assert.AreEqual(new DateTime(2026,10,7),vm.ThroughDate);
        Assert.AreEqual("06/10/2026",vm.FromDateText);Assert.AreEqual("07/10/2026",vm.ThroughDateText);
        fixture.AssertReadOnly();
    }

    [TestMethod]
    public async Task PartialDateTypingIsToleratedAndValidationDoesNotEraseRawText()
    {
        using var fixture=new Fixture();var vm=fixture.ViewModel();await vm.TodayAsync();var prior=vm.FilterSummary;
        foreach(var partial in new[]{"0","07","07/","07/1","07/10/","07/10/202"})
        {
            vm.FromDateText=partial;Assert.AreEqual(partial,vm.FromDateText);Assert.AreEqual("",vm.FromDateError);Assert.AreEqual(prior,vm.FilterSummary);
        }
        vm.ValidateFromDateText();Assert.AreEqual("07/10/202",vm.FromDateText);StringAssert.Contains(vm.FromDateError,"dd/mm/aaaa");Assert.AreEqual(prior,vm.FilterSummary);
        vm.FromDateText="07/10/2026";vm.ValidateFromDateText();Assert.AreEqual("",vm.FromDateError);Assert.AreEqual("07/10/2026",vm.FromDateText);
        fixture.AssertReadOnly();
    }

    [TestMethod]
    public async Task DateMaskInsertsSeparatorsLimitsDigitsAndKeepsSemanticValidation()
    {
        var edit=new DateMaskEdit("",0);
        var progressive=new[]{"0","07","07/1","07/10","07/10/2","07/10/20","07/10/202","07/10/2026"};
        for(var index=0;index<8;index++)
        {
            edit=DateTextMask.Insert(edit.Text,edit.Caret,0,"07102026"[index].ToString());
            Assert.AreEqual(progressive[index],edit.Text);
        }
        Assert.AreEqual("07/10/2026",DateTextMask.Insert(edit.Text,edit.Caret,0,"99").Text,"The mask must keep at most eight significant digits.");

        using var fixture=new Fixture();var vm=fixture.ViewModel();await vm.TodayAsync();var prior=vm.FilterSummary;
        var invalid=DateTextMask.Insert("",0,0,"32102026");Assert.AreEqual("32/10/2026",invalid.Text);
        await vm.SearchDatesAsync(invalid.Text,"07/10/2026");Assert.AreEqual("32/10/2026",vm.FromDateText);StringAssert.Contains(vm.FromDateError,"dd/mm/aaaa");Assert.AreEqual(prior,vm.FilterSummary);
        fixture.AssertReadOnly();
    }

    [TestMethod]
    public void DateMaskSupportsBackspaceDeleteSelectionReplacementAndPaste()
    {
        var backspace=DateTextMask.Backspace("07/10/2026",10,0);Assert.AreEqual("07/10/202",backspace.Text);
        backspace=DateTextMask.Backspace("07/10",5,0);Assert.AreEqual("07/1",backspace.Text,"Backspace must remove a digit without leaving a broken slash.");
        var delete=DateTextMask.Delete("07/10/2026",3,0);Assert.AreEqual("07/02/026",delete.Text,"Delete at the month must reformat the remaining digits.");
        var replace=DateTextMask.Insert("07/10/2026",0,10,"06102026");Assert.AreEqual("06/10/2026",replace.Text,"Ctrl+A then typing must replace the complete value.");
        var partial=DateTextMask.Insert("07/10/2026",3,2,"12");Assert.AreEqual("07/12/2026",partial.Text);
        Assert.IsTrue(DateTextMask.TryNormalizePaste("07102026",out var compact));Assert.AreEqual("07/10/2026",DateTextMask.Format(compact));
        Assert.IsTrue(DateTextMask.TryNormalizePaste("07/10/2026",out var formatted));Assert.AreEqual("07102026",formatted);
        Assert.IsFalse(DateTextMask.TryNormalizePaste("07-10-2026",out _));Assert.IsFalse(DateTextMask.TryNormalizePaste("071020269",out _));
    }

    [TestMethod]
    public Task DateEntryControlMasksRealTextInputAndKeyboardDeletion()=>WpfTestHost.OnUi(()=>
    {
        var control=new DateEntryControl();var window=new Window{Content=control,Width=320,Height=120,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        try
        {
            window.Show();WpfTestHost.Layout(window,320,120);var editor=Descendants<TextBox>(control).Single();editor.Focus();Keyboard.Focus(editor);
            foreach(var character in "07102026")SendText(editor,character.ToString());
            Assert.AreEqual("07/10/2026",control.Text);Assert.AreEqual("07/10/2026",editor.Text);
            SendText(editor,"x");Assert.AreEqual("07/10/2026",editor.Text,"Letters must not contaminate the date entry.");
            SendKey(editor,window,Key.Back);Assert.AreEqual("07/10/202",editor.Text);
            editor.SelectAll();foreach(var character in "06102026")SendText(editor,character.ToString());
            Assert.AreEqual("06/10/2026",control.Text,"Selection plus numeric typing must replace the complete date.");
        }
        finally{window.Close();}
        return Task.CompletedTask;
    });

    [TestMethod]
    public async Task PagingShowsRealTotalsPageCountAndSingularPlural()
    {
        using var fixture=new Fixture(addPagedSales:true);var vm=fixture.ViewModel();await vm.CurrentSessionAsync();
        Assert.AreEqual("Sesión actual · 57 ventas",vm.FilterSummary);Assert.AreEqual("Página 1 de 2 · 57 ventas · 50 por página",vm.PageLabel);Assert.HasCount(50,vm.Sales);Assert.IsFalse(vm.HasPrevious);Assert.IsTrue(vm.HasNext);
        await vm.PageAsync(1);Assert.AreEqual("Página 2 de 2 · 57 ventas · 50 por página",vm.PageLabel);Assert.HasCount(7,vm.Sales);Assert.IsTrue(vm.HasPrevious);Assert.IsFalse(vm.HasNext);
        await vm.YesterdayAsync();Assert.AreEqual("Ayer · 1 venta",vm.FilterSummary);Assert.AreEqual("Página 1 de 1 · 1 venta · 50 por página",vm.PageLabel);
        var empty=fixture.ViewModel(()=>Guid.NewGuid());await empty.CurrentSessionAsync();Assert.AreEqual("",empty.PageLabel);Assert.IsFalse(empty.HasPrevious);Assert.IsFalse(empty.HasNext);
        fixture.AssertReadOnly();
    }

    [TestMethod]
    public async Task DetailUsesSnapshotsQuantitiesAndFinancialLabelsWithoutTechnicalIds()
    {
        using var fixture=new Fixture();var vm=fixture.ViewModel();await vm.CurrentSessionAsync();vm.SelectedSale=vm.Sales.Single(x=>x.SaleId==fixture.DetailedSaleId);await vm.OpenDetailAsync();
        StringAssert.StartsWith(vm.DetailTitle,"Venta #");Assert.IsFalse(vm.DetailTitle.Contains("CAJA",StringComparison.Ordinal));StringAssert.Contains(vm.DetailContext,"Responsable: Administrador de prueba");StringAssert.Contains(vm.DetailContext,"Caja: CAJA PRUEBA");
        CollectionAssert.AreEqual(new[]{"2 piezas","1 pieza"},vm.DetailItems.Select(x=>x.Quantity).ToArray());
        Assert.AreEqual("$41.00",vm.DetailSubtotal);Assert.AreEqual("$41.00",vm.DetailTotal);Assert.AreEqual("Efectivo",vm.DetailPaymentMethod);Assert.AreEqual("$50.00",vm.DetailReceived);Assert.AreEqual("$9.00",vm.DetailChange);
        var text=string.Join('|',new[]{vm.DetailTitle,vm.DetailContext,vm.DetailPaymentMethod}.Concat(vm.DetailItems.SelectMany(x=>new[]{x.Name,x.Quantity,x.UnitPrice,x.LineTotal})));
        Assert.IsFalse(text.Contains(fixture.CurrentSessionId.ToString(),StringComparison.OrdinalIgnoreCase));Assert.IsFalse(text.Contains("CASH",StringComparison.Ordinal));Assert.IsFalse(text.Contains("Aplicado",StringComparison.Ordinal));
        Assert.IsTrue(vm.ReprintAllowed,"The administrator keeps the dedicated reprint permission, without executing it.");fixture.AssertReadOnly();
    }

    [TestMethod]
    [DataRow(1280,820)]
    [DataRow(1920,1080)]
    public Task HistoryAndDetailFitReviewSizesWithoutHorizontalScrolling(int width,int height)=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();var vm=fixture.ViewModel();await vm.LoadAsync();
        var contentWidth=width-280;var contentHeight=height-190;var view=new SalesHistoryView{DataContext=vm};var window=new Window{Content=view,Width=contentWidth,Height=contentHeight,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
        try
        {
            window.Show();WpfTestHost.Layout(window,contentWidth,contentHeight);
            var grid=Descendants<DataGrid>(view).Single(x=>AutomationProperties.GetName(x)=="Historial de ventas"&&x.Visibility==Visibility.Visible);
            Assert.IsFalse(grid.Columns.Any(x=>Equals(x.Header,"Folio")||Equals(x.Header,"CashSessionId")));Assert.AreEqual(0d,Descendants<ScrollViewer>(grid).First().ScrollableWidth,0.5);
            Assert.AreEqual("SaleNumber",((Binding)((DataGridBoundColumn)grid.Columns.Single(x=>Equals(x.Header,"Venta"))).Binding).Path.Path);Assert.AreEqual("Terminal",((Binding)((DataGridBoundColumn)grid.Columns.Single(x=>Equals(x.Header,"Caja"))).Binding).Path.Path);
            Assert.IsTrue(Descendants<Button>(view).Any(x=>Equals(x.Content,"Sesión actual")&&x.IsVisible));
            var entries=Descendants<DateEntryControl>(view).ToArray();Assert.HasCount(2,entries);Assert.IsTrue(entries.All(x=>x.ActualWidth>=209),"Both date editors keep the intended width.");
            var hints=Descendants<TextBlock>(view).Where(x=>x.Text=="dd/mm/aaaa").ToArray();Assert.HasCount(2,hints);Assert.IsTrue(hints.All(x=>x.TextWrapping==TextWrapping.NoWrap&&x.Visibility==Visibility.Visible&&x.DesiredSize.Width<=x.ActualWidth));
            foreach(var entry in entries)
            {
                var editor=Descendants<TextBox>(entry).Single();var calendarButton=Descendants<Button>(entry).Single();
                Assert.IsTrue(editor.ActualWidth>=165,$"The date text area must remain readable; actual {editor.ActualWidth}.");Assert.AreEqual(42d,calendarButton.ActualWidth,0.5);
                editor.SelectAll();foreach(var character in "07102026")SendText(editor,character.ToString());WpfTestHost.Layout(window,contentWidth,contentHeight);
                Assert.AreEqual("07/10/2026",editor.Text);Assert.AreEqual("07/10/2026",entry.Text);
            }
            Assert.AreEqual("07/10/2026",vm.FromDateText);Assert.AreEqual("07/10/2026",vm.ThroughDateText,"Masked text input must keep both outer bindings alive.");
            var from=entries[0];var calendar=(Calendar)from.FindName("PickerCalendar");calendar.SelectedDate=new DateTime(2026,10,6);Assert.AreEqual("06/10/2026",from.Text,"Calendar selection must keep the strict human format.");
            Assert.AreEqual("06/10/2026",vm.FromDateText,"Calendar selection must update the same ViewModel buffer.");
            vm.SelectedSale=vm.Sales.First();await vm.OpenDetailAsync();WpfTestHost.Layout(window,contentWidth,contentHeight);
            var detail=Descendants<DataGrid>(view).Single(x=>AutomationProperties.GetName(x)=="Partidas de venta"&&x.Visibility==Visibility.Visible);
            Assert.AreEqual(0d,Descendants<ScrollViewer>(detail).First().ScrollableWidth,0.5);Assert.IsTrue(Descendants<Button>(view).Any(x=>Equals(x.Content,"Volver")&&x.IsVisible));
            view.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            Assert.IsTrue(vm.IsList,"Escape must retain the approved return from sale detail.");
        }
        finally{window.Close();}
        fixture.AssertReadOnly();
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}
    }

    private static void SendText(TextBox editor,string text)
    {
        var composition=new TextComposition(InputManager.Current,editor,text);
        editor.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputEvent});
    }
    private static void SendKey(UIElement source,Window window,Key key)=>source.RaiseEvent(
        new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});

    private sealed class Fixture:IDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"CasaMiga1K4C",Guid.NewGuid().ToString("N"));
        private readonly Database database;private readonly SqliteLocalUnitOfWork work;private readonly UserSession user=new();private readonly FixedClock clock=new();private readonly TerminalIdentity terminal;private readonly Product concha;private readonly Product dona;
        private readonly long salesBefore;private readonly long paymentsBefore;private readonly long auditsBefore;private readonly Guid priorSessionId;
        public Guid CurrentSessionId{get;}public IReadOnlySet<Guid> CurrentSaleIds{get;}public Guid DetailedSaleId{get;}
        public Fixture(bool addPagedSales=false)
        {
            Directory.CreateDirectory(root);database=new(Path.Combine(root,"casamiga.db"));database.Initialize();work=new(database);terminal=new(Guid.NewGuid(),"CAJA PRUEBA");work.Run(store=>{store.EnsureTerminal(terminal,clock.UtcNow);return true;});
            var identity=new IdentityService(work,new LocalPasswords(),clock,user);var password=Guid.NewGuid().ToString("N")+"!Aa";identity.Bootstrap("admin","Administrador de prueba",password);Assert.IsTrue(identity.Login("admin",password));
            var catalog=new CatalogService(work,user,clock,terminal.Id);var category=catalog.CreateCategory("Pan dulce");concha=catalog.CreateProduct(category.Id,"CONCHA","Concha de vainilla","",new Money(1300));dona=catalog.CreateProduct(category.Id,"DONA","Dona especial","",new Money(1500));
            priorSessionId=Guid.NewGuid();CurrentSessionId=Guid.NewGuid();var actor=user.Current!.Id;
            work.Run(store=>{store.AddCashSession(CashSession.Open(priorSessionId,terminal.Id,actor,clock.UtcNow.AddDays(-3),new Money(0)));return true;});
            _=Persist(priorSessionId,new(2026,10,5,18,0,0,TimeSpan.Zero),[(concha,1)],1300);
            _=Persist(priorSessionId,new(2026,10,6,18,0,0,TimeSpan.Zero),[(dona,1)],1500);
            _=Persist(priorSessionId,new(2026,10,7,15,0,0,TimeSpan.Zero),[(concha,1)],1300);
            work.Run(store=>{var prior=store.FindSession(priorSessionId)!;store.CloseSession(prior.Close(actor,clock.UtcNow.AddDays(-2),new Money(4100),new Money(4100)));store.AddCashSession(CashSession.Open(CurrentSessionId,terminal.Id,actor,clock.UtcNow.AddHours(-4),new Money(0)));return true;});
            DetailedSaleId=Persist(CurrentSessionId,new(2026,10,7,17,30,0,TimeSpan.Zero),[(concha,2),(dona,1)],5000);
            var second=Persist(CurrentSessionId,new(2026,10,7,17,45,0,TimeSpan.Zero),[(dona,1)],2000);var currentSaleIds=new HashSet<Guid>{DetailedSaleId,second};
            if(addPagedSales)for(var index=0;index<55;index++)currentSaleIds.Add(Persist(CurrentSessionId,new DateTimeOffset(2026,10,7,17,46,index%60,TimeSpan.Zero),[(concha,1)],1300));
            CurrentSaleIds=currentSaleIds;
            salesBefore=Count("sale");paymentsBefore=Count("payment");auditsBefore=Count("audit_entry");
        }
        public SalesHistoryViewModel ViewModel(Func<Guid?>? session=null)
        {
            var settings=new SettingsProvider();var receipts=new ReceiptService(work,user,clock,terminal.Id,TimeZoneInfo.Utc,settings,new EscPosReceiptRenderer(),new NoPrintTransport());
            return new(new SalesHistoryService(work,user,Zone),receipts,Zone,clock,session??(()=>CurrentSessionId),(_,_)=>{});
        }
        private Guid Persist(Guid session,DateTimeOffset at,(Product Product,int Quantity)[] lines,long received)
            =>work.Run(store=>{var sale=new Sale(Guid.NewGuid(),Guid.NewGuid(),user.Current!.Id,terminal.Id,session,store.NextSaleSequence(terminal.Id),at,lines.Select(x=>new SaleItem(x.Product.Id,x.Product.Name,x.Product.SalePrice,x.Quantity)),Guid.NewGuid(),new Money(received));store.AddSale(sale);return sale.Id;});
        public void AssertReadOnly(){Assert.AreEqual(salesBefore,Count("sale"));Assert.AreEqual(paymentsBefore,Count("payment"));Assert.AreEqual(auditsBefore,Count("audit_entry"));}
        private long Count(string table){using var connection=database.Open(true);using var command=connection.CreateCommand();command.CommandText=$"SELECT count(*) FROM {table}";return (long)command.ExecuteScalar()!;}
        public void Dispose()=>Directory.Delete(root,true);
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.CreateCustomTimeZone("CasaMiga1K4C",TimeSpan.FromHours(-6),"CasaMiga1K4C","CasaMiga1K4C");
    }
    private sealed class FixedClock:IClock{public DateTimeOffset UtcNow=>new(2026,10,7,18,0,0,TimeSpan.Zero);}
    private sealed class SettingsProvider:IPrintingSettingsProvider{public PrintingSettingsResult Read()=>new(new(1,false,"TEST",48,"ibm850","Disabled",false,new BusinessReceiptProfile("CASA MIGA PRUEBA"),2),null);}
    private sealed class NoPrintTransport:IReceiptPrintTransport{public PrintSubmissionResult Submit(PrintSubmission submission)=>throw new AssertFailedException("History usability tests must never print.");}
}
