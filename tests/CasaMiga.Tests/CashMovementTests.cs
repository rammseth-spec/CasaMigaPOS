using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class CashMovementTests : SalesFixture
{
    private CashMovementService Movements(UserSession? session = null) => new(work, session ?? user, clock, terminal.Id);
    private static CashMovementRequest Expense(long cents, ExpenseCategory category = ExpenseCategory.Supplies, string note = "") =>
        new(Guid.NewGuid(), CashMovementKind.Expense, category, new Money(cents), note);
    private static CashMovementRequest Withdrawal(long cents, string note = "") => new(Guid.NewGuid(), CashMovementKind.Withdrawal, null, new Money(cents), note);
    private static CashMovementRequest Deposit(long cents, string note = "") => new(Guid.NewGuid(), CashMovementKind.Deposit, null, new Money(cents), note);
    private Money Expected() => work.Run(s => s.FindSession(cash.Id)!.OpeningFund + s.CashSalesTotal(cash.Id) - s.CashRefundsTotal(cash.Id) + s.CashMovementTotals(cash.Id).Net);

    /// <summary>Crea un usuario con el rol indicado y devuelve su sesión ya iniciada.</summary>
    private UserSession As(string role)
    {
        var name = role.ToLowerInvariant() + Guid.NewGuid().ToString("N")[..6];
        var secret = "Aa1!" + Guid.NewGuid().ToString("N")[..12];
        var admin = new IdentityService(work, new LocalPasswords(), clock, user);
        var temporary = admin.CreateUser(name, role + " de prueba", role);
        admin.ChangePassword(name, temporary, secret);
        var session = new UserSession();
        Assert.IsTrue(new IdentityService(work, new LocalPasswords(), clock, session).Login(name, secret));
        return session;
    }

    [TestMethod]
    public void CashierRecordsExpensesAndDepositsButNotWithdrawals()
    {
        var cashier = As("Cashier");
        Assert.IsTrue(Movements(cashier).CanRecord());
        Assert.IsFalse(Movements(cashier).CanWithdraw());

        var gas = Movements(cashier).Record(Expense(35000, ExpenseCategory.Gas, "Tanque 30 kg"));
        var change = Movements(cashier).Record(Deposit(20000, "Cambio del banco"));
        Assert.AreEqual(ExpenseCategory.Gas, gas.Category);
        Assert.AreEqual("Tanque 30 kg", gas.Note);
        Assert.ThrowsExactly<CashMovementException>(() => Movements(cashier).Record(Withdrawal(1000)));

        Assert.AreEqual(50000 - 35000 + 20000, Expected().Cents);
        var listed = Movements(cashier).Current();
        Assert.HasCount(2, listed);
        Assert.AreEqual(change.Id, listed[0].Id, "Most recent first.");
        Assert.AreEqual(1L, Sql($"SELECT count(*) FROM audit_entry WHERE action='cash.expense' AND entity_id='{gas.Id}'"));
        Assert.AreEqual(1L, Sql($"SELECT count(*) FROM audit_entry WHERE action='cash.deposit' AND entity_id='{change.Id}'"));
    }

    [TestMethod]
    public void ManagerAndAdministratorRecordWithdrawals()
    {
        var manager = As("Manager");
        Assert.IsTrue(Movements(manager).CanWithdraw());
        Assert.IsTrue(Movements().CanWithdraw());
        var bank = Movements(manager).Record(Withdrawal(30000, "Depósito al banco"));
        Movements().Record(Withdrawal(5000));
        Assert.AreEqual(1L, Sql($"SELECT count(*) FROM audit_entry WHERE action='cash.withdrawal' AND entity_id='{bank.Id}'"));
        Assert.AreEqual(15000L, Expected().Cents);
        var totals = work.Run(s => s.CashMovementTotals(cash.Id));
        Assert.AreEqual(35000L, totals.Withdrawals.Cents);
        Assert.AreEqual(0L, totals.Expenses.Cents);
    }

    [TestMethod]
    public void UserWithoutPermissionIsDenied()
    {
        Sql("DELETE FROM role_permission WHERE role_name='Cashier' AND permission_name='cash.movement'");
        var cashier = As("Cashier");
        Assert.IsFalse(Movements(cashier).CanRecord());
        Assert.ThrowsExactly<AccessDeniedException>(() => Movements(cashier).Record(Deposit(100)));
        Assert.ThrowsExactly<AccessDeniedException>(() => Movements(cashier).Current());
        user.SignOut();
        Assert.ThrowsExactly<AccessDeniedException>(() => Movements().Record(Deposit(100)));
    }

    [TestMethod]
    public void ValidatesAmountCategoryAndNote()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Movements().Record(Expense(0)));
        Assert.ThrowsExactly<ArgumentException>(() => Movements().Record(new(Guid.NewGuid(), CashMovementKind.Expense, null, new Money(100), "")));
        Assert.ThrowsExactly<ArgumentException>(() => Movements().Record(new(Guid.NewGuid(), CashMovementKind.Deposit, ExpenseCategory.Gas, new Money(100), "")));
        Assert.ThrowsExactly<ArgumentException>(() => Movements().Record(Expense(100, ExpenseCategory.Other, "  ")));
        Assert.ThrowsExactly<ArgumentException>(() => Movements().Record(Expense(100, ExpenseCategory.Other, "ok")));
        Assert.ThrowsExactly<ArgumentException>(() => Movements().Record(Expense(100, note: new string('x', 201))));
        Assert.ThrowsExactly<ArgumentException>(() => Movements().Record(Expense(100, note: "línea\nnueva")));
        Assert.ThrowsExactly<ArgumentException>(() => Movements().Record(new(Guid.Empty, CashMovementKind.Deposit, null, new Money(100), "")));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM cash_movement"));

        var other = Movements().Record(Expense(100, ExpenseCategory.Other, "  Garrafón de agua  "));
        Assert.AreEqual("Garrafón de agua", other.Note, "The note is stored trimmed.");
    }

    [TestMethod]
    public void RetryWithSameOperationDoesNotDuplicate()
    {
        var request = Expense(12000, ExpenseCategory.Cleaning, "Cloro");
        var first = Movements().Record(request);
        var again = Movements().Record(request);
        Assert.AreEqual(first.Id, again.Id);
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM cash_movement"));
        Assert.AreEqual(1L, Sql("SELECT count(*) FROM audit_entry WHERE action='cash.expense'"));
    }

    [TestMethod]
    public void CannotTakeOutMoreCashThanExpected()
    {
        Persist();                                                    // fondo $500 + venta en efectivo $38
        Assert.ThrowsExactly<CashMovementException>(() => Movements().Record(Withdrawal(53801)));
        Movements().Record(Withdrawal(50000));
        Movements().Record(Expense(3800, ExpenseCategory.Transport));
        Assert.AreEqual(0L, Expected().Cents);
        Assert.ThrowsExactly<CashMovementException>(() => Movements().Record(Expense(1)));
        Movements().Record(Deposit(1000));
        Movements().Record(Expense(1000, ExpenseCategory.Maintenance));
        Assert.AreEqual(0L, Expected().Cents);
    }

    [TestMethod]
    public void RequiresOpenCashAndStopsAtBlindCount()
    {
        var closing = new CashClosingService(work, user, clock);
        Movements().Record(Expense(2000, ExpenseCategory.Supplies, "Harina"));
        Movements().Record(Deposit(10000));
        Movements().Record(Withdrawal(3000));
        var result = closing.SubmitCount(cash.Id, new Money(55000));
        Assert.AreEqual(50000 - 2000 + 10000 - 3000, result.Expected.Cents);
        Assert.AreEqual(0L, result.Difference.Cents);
        Assert.AreEqual(2000L, result.Movements!.Expenses.Cents);
        Assert.AreEqual(3000L, result.Movements.Withdrawals.Cents);
        Assert.AreEqual(10000L, result.Movements.Deposits.Cents);
        Assert.AreEqual(55000L, closing.Resume(cash.Id)!.Expected.Cents);

        Assert.ThrowsExactly<CashMovementException>(() => Movements().Record(Deposit(100)), "After the blind count the drawer is frozen.");
        var closed = closing.Close(cash.Id);
        Assert.AreEqual(55000L, closed.Expected!.Value.Cents);
        Assert.ThrowsExactly<CashMovementException>(() => Movements().Record(Deposit(100)), "No open cash.");
        Assert.IsEmpty(Movements().Current());
    }

    [TestMethod]
    public void RefundsCannotUseCashAlreadyTakenOut()
    {
        var sale = Persist();
        Movements().Record(Withdrawal(53800));
        Assert.ThrowsExactly<RefundException>(() => new RefundService(work, user, clock, terminal.Id).Cancel(sale.Id, "Cobro equivocado"));
        Movements().Record(Deposit(3800, "Cambio"));
        var refund = new RefundService(work, user, clock, terminal.Id).Cancel(sale.Id, "Cobro equivocado");
        Assert.AreEqual(3800L, refund.Total.Cents);
        Assert.AreEqual(0L, Expected().Cents);
    }

    [TestMethod]
    public void ReportShowsMovementsInShiftExpectedCash()
    {
        Persist();
        Movements().Record(Expense(5000, ExpenseCategory.Gas));
        Movements().Record(Deposit(2000));
        var shift = new ReportService(work, user, TimeZoneInfo.Utc).Sales(DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow)).Data.Sessions.Single();
        Assert.AreEqual(-3000L, shift.Movements.Cents);
        Assert.AreEqual(50000 + 3800 - 3000, shift.Expected.Cents);
    }

    [TestMethod]
    public void DatabaseKeepsMovementsImmutableAndValid()
    {
        var saved = Movements().Record(Expense(1000, ExpenseCategory.Gas));
        Assert.ThrowsExactly<SqliteException>(() => Sql($"UPDATE cash_movement SET amount_cents=1 WHERE id='{saved.Id}'"));
        Assert.ThrowsExactly<SqliteException>(() => Sql($"DELETE FROM cash_movement WHERE id='{saved.Id}'"));

        Assert.ThrowsExactly<SqliteException>(() => Raw("WITHDRAWAL", null, 49001, ""), "More than the expected cash.");
        Assert.ThrowsExactly<SqliteException>(() => Raw("DEPOSIT", "GAS", 100, ""), "Only expenses have a category.");
        Assert.ThrowsExactly<SqliteException>(() => Raw("EXPENSE", null, 100, ""), "Expenses need a category.");
        Assert.ThrowsExactly<SqliteException>(() => Raw("EXPENSE", "OTHER", 100, "no"), "Other needs a note.");
        Assert.ThrowsExactly<SqliteException>(() => Raw("DEPOSIT", null, 0, ""));
        Assert.ThrowsExactly<SqliteException>(() => Raw("DEPOSIT", null, 100, "", sessionId: Guid.NewGuid()), "Unknown session.");
        Raw("WITHDRAWAL", null, 49000, "");
        Assert.AreEqual(0L, Expected().Cents);

        new CashClosingService(work, user, clock).SubmitCount(cash.Id, new Money(0));
        Assert.ThrowsExactly<SqliteException>(() => Raw("DEPOSIT", null, 100, ""), "Frozen after the blind count.");
        Assert.AreEqual(2L, Sql("SELECT count(*) FROM cash_movement"));
    }

    [TestMethod]
    public void MigrationGrantsPermissionsByRole()
    {
        Assert.AreEqual(3L, Sql("SELECT count(*) FROM role_permission WHERE permission_name='cash.movement'"));
        Assert.AreEqual(0L, Sql("SELECT count(*) FROM role_permission WHERE permission_name='cash.withdrawal' AND role_name='Cashier'"));
        Assert.AreEqual(2L, Sql("SELECT count(*) FROM role_permission WHERE permission_name='cash.withdrawal'"));
    }

    private void Raw(string kind, string? category, long cents, string note, Guid? sessionId = null)
    {
        using var c = db.Open(); using var q = c.CreateCommand();
        q.CommandText = "INSERT INTO cash_movement VALUES($id,$op,$session,$terminal,$user,$kind,$category,$cents,$note,$at)";
        q.Parameters.AddWithValue("$id", Guid.NewGuid().ToString()); q.Parameters.AddWithValue("$op", Guid.NewGuid().ToString());
        q.Parameters.AddWithValue("$session", (sessionId ?? cash.Id).ToString()); q.Parameters.AddWithValue("$terminal", terminal.Id.ToString());
        q.Parameters.AddWithValue("$user", user.Current!.Id.ToString()); q.Parameters.AddWithValue("$kind", kind);
        q.Parameters.AddWithValue("$category", (object?)category ?? DBNull.Value); q.Parameters.AddWithValue("$cents", cents);
        q.Parameters.AddWithValue("$note", note); q.Parameters.AddWithValue("$at", clock.UtcNow.ToString("O"));
        q.ExecuteNonQuery();
    }
}
