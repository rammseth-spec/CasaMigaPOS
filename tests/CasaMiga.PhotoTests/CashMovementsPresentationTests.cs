using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class CashMovementsPresentationTests
{
    [TestMethod]
    public Task MovementsAppearOnlyWithOpenCashAndListWhatWasRecorded() => WpfTestHost.OnUi(async () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "CasaMigaCashMovementsUi", Guid.NewGuid().ToString("N"));
        try
        {
            var model = new MainViewModel();
            await model.InitializeAsync(new FoundationOptions(directory, IsDevelopment: true));
            var password = "Aa1!" + Guid.NewGuid().ToString("N")[..12];
            await model.BootstrapAsync("gastos", "Prueba de gastos", password, password);
            await model.LoginAsync("gastos", password); Assert.IsTrue(model.CanNavigate, model.Message);
            Assert.IsFalse(model.ShowCashMovements, "Sin caja abierta no hay movimientos.");

            model.ShowOpening(); model.Amount = "500.00"; await model.OpenAsync();
            Assert.IsTrue(model.ShowCashMovements, model.Message);
            var movements = model.CashMovements!;
            CollectionAssert.AreEqual(new[] { "Gasto", "Entrada", "Retiro" }, movements.Kinds.Select(k => k.Label).ToArray(), "El administrador también registra retiros.");

            var view = new CashMovementsView { DataContext = movements };
            var window = new Window { Content = new ScrollViewer { Content = view }, Width = 1280, Height = 820, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
            try
            {
                window.Show(); WpfTestHost.Layout(view);
                movements.SelectCategory(ExpenseCategory.Gas); movements.AmountText = "150.00"; movements.Note = "Tanque";
                await movements.RecordAsync();
                Assert.IsTrue(movements.MessageIsSuccess, movements.Message);
                StringAssert.Contains(movements.Message, "Gasto de $150.00");
                Assert.AreEqual("", movements.AmountText, "El formulario queda listo para el siguiente.");

                movements.SelectCategory(ExpenseCategory.Other); movements.AmountText = "20.00";
                await movements.RecordAsync();
                Assert.IsFalse(movements.MessageIsSuccess); StringAssert.Contains(movements.Message, "«Otro»");

                movements.SelectKind(CashMovementKind.Deposit); movements.AmountText = "50";
                await movements.RecordAsync();
                Assert.IsTrue(movements.MessageIsSuccess, movements.Message);
                WpfTestHost.Layout(view);

                var list = Descendants<ItemsControl>(view).Single(i => AutomationProperties.GetName(i) == "Movimientos del turno");
                Assert.AreEqual(2, list.Items.Count); Assert.IsTrue(list.ActualHeight > 0);
                Assert.AreEqual("+$50.00", ((CashMovementRow)list.Items[0]!).Amount);
                StringAssert.Contains(((CashMovementRow)list.Items[1]!).Detail, "Gas · Tanque");
                StringAssert.Contains(movements.TotalsText, "Gastos $150.00");
                Assert.IsTrue(view.ActualWidth <= 1280);
            }
            finally { window.Close(); }

            // El cajero no ve «Retiro».
            var clock = new SystemClock(); var work = new SqliteLocalUnitOfWork(new Database(Path.Combine(directory, "casamiga.db")));
            var admin = new UserSession(); var identity = new IdentityService(work, new LocalPasswords(), clock, admin);
            Assert.IsTrue(identity.Login("gastos", password));
            var temporary = identity.CreateUser("cajera", "Cajera", "Cashier"); identity.ChangePassword("cajera", temporary, password);
            var cashier = new UserSession(); Assert.IsTrue(new IdentityService(work, new LocalPasswords(), clock, cashier).Login("cajera", password));
            var cashierModel = new CashMovementsViewModel(new CashMovementService(work, cashier, clock, TerminalConfiguration.LoadOrCreate(directory).Id), (_, _) => { });
            await cashierModel.LoadAsync();
            Assert.IsTrue(cashierModel.CanRecord); Assert.IsFalse(cashierModel.CanWithdraw);
            CollectionAssert.AreEqual(new[] { "Gasto", "Entrada" }, cashierModel.Kinds.Select(k => k.Label).ToArray());
            Assert.HasCount(2, cashierModel.Movements, "Ve lo registrado en la caja abierta de la terminal.");

            await model.BeginClosingAsync(); model.CountedAmount = "400.00"; await model.SubmitCountAsync();
            Assert.IsTrue(model.IsClosingResult, model.Message);
            StringAssert.Contains(model.ClosingDetails, "Gastos: −$150.00");
            StringAssert.Contains(model.ClosingDetails, "Entradas: +$50.00");
            StringAssert.Contains(model.ClosingDetails, "Efectivo esperado: $400.00");
            model.BackFromClosing();
            Assert.IsFalse(model.ShowCashMovements, "Después del conteo ciego ya no se registran movimientos.");
            await cashierModel.LoadAsync();
            Assert.IsTrue(cashierModel.Counted, "Al volver a cargar (p. ej. tras reiniciar la app) el turno contado sigue congelado.");

            model.Logout();
            Assert.IsEmpty(movements.Movements, "Salir del usuario olvida la lista.");
        }
        finally { try { Directory.Delete(directory, true); } catch (IOException) { } }
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
