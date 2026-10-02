using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass]
[DoNotParallelize]
public sealed class InventoryPresentationTests
{


    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public Task DetailMaterializesReadOnlyPresentationActiveFlag(bool active) => OnUi(async () =>
    {
        using var fixture = new InventoryFixture();
        var item = fixture.Service.CreateInventoryItem("MP-HAR-001", "Harina", InventoryItemKind.RawMaterial, InventoryDimension.Mass);
        var unit = new InventoryItemUnit(Guid.NewGuid(), item, "HAR-BULTO-25KG", "Bulto 25 kg", 25_000_000, active);
        fixture.Store.AddItemUnit(unit, new(Guid.NewGuid(), fixture.Session.Current!.Id, fixture.Terminal.Id,
            "inventory.unit.added", unit.Id, fixture.Clock.UtcNow, "{}"));
        var model = fixture.Model();
        await model.LoadAsync();
        model.SelectedItem = model.Items.Single();
        await model.OpenDetailAsync();
        var view = new InventoryView { DataContext = model };
        Layout(view);
        Assert.IsTrue(model.IsDetail);
        Assert.AreEqual(active, model.Units.Single().IsActive);
        Assert.AreEqual("25 kg", model.Units.Single().Quantity);
        var grid = Descendants<DataGrid>(view).Single(x => AutomationProperties.GetName(x) == "Presentaciones del artículo");
        var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.IsNotNull(row, "The real presentation row must be materialized.");
        var checkBox = Descendants<CheckBox>(row).Single();
        Assert.AreEqual(active, checkBox.IsChecked);
        Assert.IsTrue(grid.IsReadOnly);
        Assert.IsFalse(checkBox.IsHitTestVisible, "Presentation status is display-only.");
        Assert.IsEmpty(model.ProductLinks);
        Assert.IsEmpty(model.History);
        Assert.IsNull(model.Detail!.Balance);
        fixture.AssertInventoryWithoutEffects();
        view.DataContext = null;
    });

    [TestMethod]
    public Task SavePresentationRefreshesRealViewAndSurvivesReopenAndRestart() => OnUi(async () =>
    {
        using var fixture = new InventoryFixture();
        var model = fixture.Model();
        await model.LoadAsync();
        var view = new InventoryView { DataContext = model };
        Layout(view);
        model.NewItem();
        model.ItemCode = "MP-HAR-001";
        model.ItemName = "Harina";
        model.SelectedItemKind = model.ItemKinds.Single(x => x.Value == InventoryItemKind.RawMaterial);
        model.SelectedDimension = model.Dimensions.Single(x => x.Value == InventoryDimension.Mass);
        await model.SaveItemAsync();
        Assert.IsTrue(model.MessageIsSuccess, model.Message);
        Layout(view);
        var itemId = model.Detail!.Item.Id;
        model.NewUnit();
        model.UnitCode = "HAR-BULTO-25KG";
        model.UnitName = "Bulto 25 kg";
        model.UnitFactor = "25000";
        Layout(view);
        await model.SaveUnitAsync();
        Assert.IsTrue(model.MessageIsSuccess, model.Message);
        Layout(view);
        var unitId = model.Units.Single().Unit.Id;
        Assert.AreEqual(25_000_000L, model.Units.Single().Unit.AtomicUnitsPerWholeUnit);
        Assert.IsTrue(model.Units.Single().IsActive);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            model.Back();
            Layout(view);
            model.SelectedItem = model.Items.Single(x => x.Id == itemId);
            await model.OpenDetailAsync();
            Layout(view);
            Assert.AreEqual(unitId, model.Units.Single().Unit.Id);
            Assert.IsEmpty(model.ProductLinks);
            Assert.IsEmpty(model.History);
            Assert.IsNull(model.Detail!.Balance);
        }

        view.DataContext = null;
        fixture.Restart();
        var restarted = fixture.Model();
        await restarted.LoadAsync();
        restarted.SelectedItem = restarted.Items.Single(x => x.Id == itemId);
        await restarted.OpenDetailAsync();
        var reopenedView = new InventoryView { DataContext = restarted };
        Layout(reopenedView);
        Assert.AreEqual(unitId, restarted.Units.Single().Unit.Id);
        Assert.AreEqual("25 kg", restarted.Units.Single().Quantity);
        Assert.IsNull(restarted.Detail!.Balance);

        restarted.NewUnit();
        restarted.UnitCode = "HAR-BULTO-25KG";
        restarted.UnitName = "Bulto 25 kg";
        restarted.UnitFactor = "25000";
        await restarted.SaveUnitAsync();
        Layout(reopenedView);
        Assert.IsFalse(restarted.MessageIsSuccess, "A repeated code must be rejected, not duplicated.");
        fixture.AssertInventoryWithoutEffects();
        reopenedView.DataContext = null;
    });

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1100, 1800));
        view.Arrange(new Rect(0, 0, 1100, 1800));
        view.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        view.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static Task OnUi(Func<Task> action) => WpfTestHost.OnUi(action);

    private sealed class InventoryFixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "CasaMigaInventoryWpf", Guid.NewGuid().ToString("N"));
        private readonly string password = Guid.NewGuid().ToString("N");
        private readonly string initialCash;
        private SqliteLocalUnitOfWork work = null!;
        private CatalogService catalog = null!;
        public Database Database { get; private set; } = null!;
        public SqliteInventoryStore Store { get; private set; } = null!;
        public InventoryService Service { get; private set; } = null!;
        public UserSession Session { get; private set; } = null!;
        public TerminalIdentity Terminal { get; }
        public IClock Clock { get; } = new SystemClock();

        public InventoryFixture()
        {
            Directory.CreateDirectory(directory);
            Terminal = TerminalConfiguration.LoadOrCreate(directory);
            Connect();
            work.Run(store => { store.EnsureTerminal(Terminal, Clock.UtcNow); return true; });
            var identity = new IdentityService(work, new LocalPasswords(), Clock, Session);
            identity.Bootstrap("admin", "Inventory WPF test", password);
            Assert.IsTrue(identity.Login("admin", password));
            new CashService(work, Session, Clock).Open(Terminal.Id, new Money(0));
            initialCash = CashSnapshot();
        }

        private void Connect()
        {
            Database = new(Path.Combine(directory, "casamiga.db"));
            Database.Initialize();
            work = new(Database);
            Session = new();
            Store = new(Database, Clock);
            Service = new(Store, work, Session, Clock, Terminal.Id);
            catalog = new(work, Session, Clock, Terminal.Id);
        }

        public void Restart()
        {
            Connect();
            Assert.IsTrue(new IdentityService(work, new LocalPasswords(), Clock, Session).Login("admin", password));
        }

        public InventoryViewModel Model() => new(Service, catalog, (_, _) => { });

        public void AssertInventoryWithoutEffects()
        {
            Assert.AreEqual(1L, Count("inventory_item"));
            Assert.AreEqual(1L, Count("inventory_item_unit"));
            foreach (var table in new[] { "inventory_balance", "inventory_operation", "inventory_movement", "product_inventory_link", "sale", "payment" })
                Assert.AreEqual(0L, Count(table), table);
            Assert.AreEqual(initialCash, CashSnapshot(), "The complete open cash row must remain unchanged.");
            using var connection = Database.Open(true);
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_key_check";
            using var reader = command.ExecuteReader();
            Assert.IsFalse(reader.Read(), "No orphan records are allowed.");
        }

        private long Count(string table)
        {
            using var connection = Database.Open(true);
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT count(*) FROM {table}";
            return (long)command.ExecuteScalar()!;
        }

        private string CashSnapshot()
        {
            using var connection = Database.Open(true);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM cash_session ORDER BY id";
            using var reader = command.ExecuteReader();
            Assert.IsTrue(reader.Read());
            var fields = Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? "NULL" : reader.GetValue(index).ToString()).ToArray();
            Assert.IsFalse(reader.Read());
            return string.Join("|", fields);
        }

        public void Dispose() => Directory.Delete(directory, true);
    }
}
