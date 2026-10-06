using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class PopupEscapePresentationTests
{
    [TestMethod]
    public Task InventoryEditorEscapeClosesOnlyItsOpenCombo()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var inventory=fixture.Model.Inventory!;
        var view=new InventoryView{DataContext=inventory};
        var window=OffscreenWindow(new Window{Content=new ScrollViewer{Content=view}});
        try
        {
            window.Show();WpfTestHost.Layout(view);
            var combo=TypeCombo(view);var selected=combo.SelectedItem;
            combo.Focus();Keyboard.Focus(combo);combo.IsDropDownOpen=true;WpfTestHost.Layout(view);
            Assert.IsTrue(combo.IsDropDownOpen);
            SendKey(combo,window,Key.Escape);WpfTestHost.Layout(view);
            Assert.IsFalse(combo.IsDropDownOpen);
            Assert.IsTrue(inventory.IsItemEditor,"The first Escape must not call Inventory.Back.");
            Assert.AreSame(selected,combo.SelectedItem);
            SendKey(combo,window,Key.Escape);WpfTestHost.Layout(view);
            Assert.IsTrue(inventory.IsDetail,"Escape without a popup keeps the existing editor cancellation.");
            fixture.AssertNoOperations();
        }
        finally{window.Close();}
    });

    [TestMethod]
    public Task ShellEscapePrioritizesPopupThenFullscreenThenEditorAndKeepsF11()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var window=OffscreenWindow(new MainWindow{DataContext=fixture.Model});
        try
        {
            window.Show();WpfTestHost.Layout(window,1280,820);
            var view=Descendants<InventoryView>(window).Single();var combo=TypeCombo(view);
            Assert.IsTrue(view.IsVisible,"Use the actual InventoryView hosted by MainWindow.");
            var selected=combo.SelectedItem;
            SendKey(combo,window,Key.F11);WpfTestHost.Layout(window,1280,820);
            Assert.AreEqual(WindowStyle.None,window.WindowStyle);
            combo.Focus();Keyboard.Focus(combo);combo.IsDropDownOpen=true;WpfTestHost.Layout(window,1280,820);
            Assert.IsTrue(combo.IsDropDownOpen);
            SendKey(combo,window,Key.Escape);WpfTestHost.Layout(window,1280,820);
            Assert.IsFalse(combo.IsDropDownOpen);
            Assert.AreEqual(WindowStyle.None,window.WindowStyle,"First Escape closes the popup, not fullscreen.");
            Assert.IsTrue(fixture.Model.Inventory!.IsItemEditor);
            Assert.AreSame(selected,combo.SelectedItem);
            SendKey(combo,window,Key.Escape);WpfTestHost.Layout(window,1280,820);
            Assert.AreEqual(WindowStyle.SingleBorderWindow,window.WindowStyle,"Next Escape retains the approved fullscreen exit.");
            Assert.IsTrue(fixture.Model.Inventory.IsItemEditor);
            SendKey(combo,window,Key.F11);Assert.AreEqual(WindowStyle.None,window.WindowStyle);
            SendKey(combo,window,Key.F11);Assert.AreEqual(WindowStyle.SingleBorderWindow,window.WindowStyle);
            SendKey(combo,window,Key.Escape);WpfTestHost.Layout(window,1280,820);
            Assert.IsTrue(fixture.Model.Inventory.IsDetail);
            fixture.AssertNoOperations();
        }
        finally{window.Close();}
    });

    [TestMethod]
    public Task EscapeFromPopupItemKeepsInventoryEditorAndSelection()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var window=OffscreenWindow(new MainWindow{DataContext=fixture.Model});
        try
        {
            window.Show();WpfTestHost.Layout(window,1280,820);
            var view=Descendants<InventoryView>(window).Single();var combo=TypeCombo(view);
            var selected=combo.SelectedItem;
            combo.Focus();Keyboard.Focus(combo);combo.IsDropDownOpen=true;WpfTestHost.Layout(window,1280,820);
            Assert.IsTrue(combo.IsDropDownOpen);
            var item=(ComboBoxItem)combo.ItemContainerGenerator.ContainerFromIndex(combo.SelectedIndex);
            Assert.IsNotNull(item,"The test must materialize the actual popup item.");
            SendKey(item,window,Key.Escape);WpfTestHost.Layout(window,1280,820);
            Assert.IsFalse(combo.IsDropDownOpen);
            Assert.IsTrue(fixture.Model.Inventory!.IsItemEditor);
            Assert.AreSame(selected,combo.SelectedItem);
            fixture.AssertNoOperations();
        }
        finally{window.Close();}
    });

    [TestMethod]
    public Task ShellEscapeClosesContextHelpBeforeFullscreenOrEditorFromNearbyFocus()=>WpfTestHost.OnUi(async()=>
    {
        using var fixture=new Fixture();await fixture.InitializeAsync();
        var window=OffscreenWindow(new MainWindow{DataContext=fixture.Model});
        try
        {
            window.Show();WpfTestHost.Layout(window,1280,820);
            var root=(Grid)window.Content;var help=new ContextHelp{HelpText="Ayuda flotante de prueba",Label="Ayuda de prueba",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            Panel.SetZIndex(help,100);root.Children.Add(help);WpfTestHost.Layout(window,1280,820);
            var trigger=(Button)help.FindName("HelpButton");var editor=Descendants<InventoryView>(window).Single();var nearby=TypeCombo(editor);
            SendKey(nearby,window,Key.F11);Assert.AreEqual(WindowStyle.None,window.WindowStyle);
            trigger.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Assert.IsTrue(help.IsOpen);
            nearby.Focus();Keyboard.Focus(nearby);SendKey(nearby,window,Key.Escape);WpfTestHost.Layout(window,1280,820);
            Assert.IsFalse(help.IsOpen);Assert.AreEqual(WindowStyle.None,window.WindowStyle,"Escape must close help before leaving fullscreen.");Assert.IsTrue(fixture.Model.Inventory!.IsItemEditor);
            SendKey(nearby,window,Key.Escape);Assert.AreEqual(WindowStyle.SingleBorderWindow,window.WindowStyle);Assert.IsTrue(fixture.Model.Inventory.IsItemEditor);
            trigger.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Assert.IsTrue(help.IsOpen);
            SendKey(nearby,window,Key.F11);WpfTestHost.Layout(window,1280,820);
            Assert.IsFalse(help.IsOpen,"A fullscreen layout change closes the overlay instead of retaining stale coordinates.");
            Assert.AreEqual(WindowStyle.None,window.WindowStyle);
            trigger.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Assert.IsTrue(help.IsOpen);
            SendKey(nearby,window,Key.F11);WpfTestHost.Layout(window,1280,820);
            Assert.IsFalse(help.IsOpen);Assert.AreEqual(WindowStyle.SingleBorderWindow,window.WindowStyle);
            fixture.AssertNoOperations();
        }
        finally{window.Close();}
    });

    private static T OffscreenWindow<T>(T window) where T:Window
    {
        window.Width=1280;window.Height=820;window.Left=-10000;window.Top=-10000;
        window.ShowInTaskbar=false;window.ShowActivated=false;window.WindowStartupLocation=WindowStartupLocation.Manual;
        return window;
    }
    private static ComboBox TypeCombo(DependencyObject view)=>Descendants<ComboBox>(view).Single(x=>
        BindingOperations.GetBinding(x,ComboBox.SelectedItemProperty)?.Path.Path=="SelectedItemKind");
    private static void SendKey(UIElement source,Window window,Key key)
    {
        var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,key)
            {RoutedEvent=Keyboard.PreviewKeyDownEvent};
        source.RaiseEvent(args);
        if(!args.Handled){args.RoutedEvent=Keyboard.KeyDownEvent;source.RaiseEvent(args);}
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;
            foreach(var nested in Descendants<T>(child))yield return nested;
        }
    }
    private sealed class Fixture:IDisposable
    {
        private readonly string directory=Path.Combine(Path.GetTempPath(),"CasaMigaPopupEscape",Guid.NewGuid().ToString("N"));
        public MainViewModel Model{get;}=new();
        public async Task InitializeAsync()
        {
            await Model.InitializeAsync(new FoundationOptions(directory,IsDevelopment:true));
            Assert.IsTrue(Model.IsBootstrap,Model.Message);
            var password=Guid.NewGuid().ToString("N");
            await Model.BootstrapAsync("escape-admin","Prueba de teclado",password,password);
            await Model.LoginAsync("escape-admin",password);Assert.IsTrue(Model.CanNavigate,Model.Message);
            await Model.ShowInventoryAsync();Assert.IsTrue(Model.IsInventory,Model.Message);
            var inventory=Model.Inventory!;inventory.NewItem();inventory.ItemCode="TEST-ESC-HAR";inventory.ItemName="Harina de prueba";
            await inventory.SaveItemAsync();Assert.IsTrue(inventory.IsDetail,inventory.Message);
            inventory.EditItem();Assert.IsTrue(inventory.IsItemEditor);
        }
        public void AssertNoOperations()
        {
            using var connection=new Database(Path.Combine(directory,"casamiga.db")).Open(true);
            foreach(var table in new[]{"sale","inventory_operation","production_batch"})
            {
                using var command=connection.CreateCommand();command.CommandText=$"SELECT count(*) FROM {table}";
                Assert.AreEqual(0L,(long)command.ExecuteScalar()!,table);
            }
        }
        public void Dispose()=>Directory.Delete(directory,true);
    }
}
