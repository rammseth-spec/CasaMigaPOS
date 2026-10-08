using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Domain;
using Microsoft.Data.Sqlite;

namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork(Database database) : ILocalUnitOfWork, ILocalReadUnitOfWork, ILocalCheckoutUnitOfWork
{
    public T RunCheckout<T>(Func<ILocalStore,T> operation)
    {
        try { return Run(operation); }
        catch(SqliteException e) when(e.SqliteErrorCode is 5 or 6)
        {throw new CheckoutInventoryException(CheckoutInventoryError.InventoryConcurrencyConflict);}
    }
    internal T RunTechnical<T>(Func<ILocalStore, SqliteConnection, SqliteTransaction, T> operation)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        var result = operation(new Store(connection, transaction), connection, transaction);
        transaction.Commit();
        return result;
    }

    public T Run<T>(Func<ILocalStore, T> operation)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        var result = operation(new Store(connection, transaction));
        transaction.Commit();
        return result;
    }

    public T Read<T>(Func<ILocalStore, T> operation)
    {
        using var connection = database.Open(readOnly: true);
        using var transaction = connection.BeginTransaction(deferred: true);
        var result = operation(new Store(connection, transaction));
        transaction.Commit();
        return result;
    }

    private sealed partial class Store(SqliteConnection connection, SqliteTransaction transaction) : ILocalStore
    {
        private SqliteCommand Command(string sql, params (string Name, object? Value)[] parameters)
        {
            var cmd = connection.CreateCommand(); cmd.Transaction = transaction; cmd.CommandText = sql;
            foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return cmd;
        }
        private static string Id(Guid id) => id.ToString("D");
        private static string At(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        private static DateTimeOffset? Date(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : DateTimeOffset.Parse(r.GetString(ordinal), CultureInfo.InvariantCulture);
        public int UserCount() { using var cmd = Command("SELECT count(*) FROM app_user"); return Convert.ToInt32(cmd.ExecuteScalar()); }
        public LocalUser? FindUser(string name) => ReadUser("username=$key", name);
        public LocalUser? FindUser(Guid id) => ReadUser("id=$key", Id(id));
        private LocalUser? ReadUser(string predicate, string value)
        {
            using var cmd = Command("SELECT id,username,display_name,password_hash,active,failed_attempts,locked_until_utc FROM app_user WHERE " + predicate, ("$key", value));
            using var r = cmd.ExecuteReader();
            return r.Read() ? new(Guid.Parse(r.GetString(0)), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4) == 1, r.GetInt32(5), Date(r,6)) : null;
        }
        public void AddAdministrator(Guid id, string username, string displayName, string hash, DateTimeOffset at)
        {
            using var cmd = Command("INSERT INTO app_user(id,username,display_name,password_hash,role_name,created_utc) VALUES($id,$name,$display,$hash,'Administrator',$at)",
                ("$id", Id(id)), ("$name", username), ("$display", displayName), ("$hash", hash), ("$at", At(at)));
            cmd.ExecuteNonQuery();
        }
        public void UpdateLogin(Guid id, int failures, DateTimeOffset? lockedUntil, string? replacementHash)
        {
            using var cmd = Command("UPDATE app_user SET failed_attempts=$failures,locked_until_utc=$lock,password_hash=COALESCE($hash,password_hash) WHERE id=$id",
                ("$id", Id(id)), ("$failures", failures), ("$lock", lockedUntil is { } at ? At(at) : null), ("$hash", replacementHash));
            cmd.ExecuteNonQuery();
        }
        public bool HasPermission(Guid user, string permission)
        {
            using var cmd = Command("SELECT EXISTS(SELECT 1 FROM app_user u JOIN role_permission p ON p.role_name=u.role_name WHERE u.id=$id AND u.active=1 AND p.permission_name=$permission)",
                ("$id", Id(user)), ("$permission", permission));
            return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
        }
        public bool HasRole(Guid user, string role)
        {
            using var cmd = Command("SELECT EXISTS(SELECT 1 FROM app_user WHERE id=$id AND active=1 AND role_name=$role)",
                ("$id", Id(user)), ("$role", role));
            return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
        }
        public TerminalIdentity? FindTerminal(Guid id)
        {
            using var cmd = Command("SELECT name FROM terminal WHERE id=$id", ("$id", Id(id)));
            return cmd.ExecuteScalar() is string name ? new(id, name) : null;
        }
        public void EnsureTerminal(TerminalIdentity terminal, DateTimeOffset at)
        {
            using var cmd = Command("INSERT INTO terminal(id,name,created_utc) VALUES($id,$name,$at) ON CONFLICT(id) DO UPDATE SET name=excluded.name",
                ("$id", Id(terminal.Id)), ("$name", terminal.Name), ("$at", At(at)));
            cmd.ExecuteNonQuery();
        }
        public CashSessionView? FindOpenSession(Guid terminal)
        {
            using var cmd = Command("SELECT c.id,c.opened_by,c.opened_utc,c.opening_cents,u.display_name FROM cash_session c JOIN app_user u ON u.id=c.opened_by WHERE c.terminal_id=$terminal AND c.state='OPEN'", ("$terminal", Id(terminal)));
            using var r = cmd.ExecuteReader();
            return r.Read() ? new(CashSession.Restore(Guid.Parse(r.GetString(0)), terminal, Guid.Parse(r.GetString(1)), Date(r,2)!.Value,
                new Money(r.GetInt64(3)), CashState.Open, null, null, null, null, null), r.GetString(4)) : null;
        }
        public void AddCashSession(CashSession session)
        {
            using var cmd = Command("INSERT INTO cash_session(id,terminal_id,opened_by,opened_utc,opening_cents,state) VALUES($id,$terminal,$user,$at,$fund,'OPEN')",
                ("$id", Id(session.Id)), ("$terminal", Id(session.TerminalId)), ("$user", Id(session.OpenedBy)), ("$at", At(session.OpenedAt)), ("$fund", session.OpeningFund.Cents));
            try { cmd.ExecuteNonQuery(); }
            catch (SqliteException error) when (error.SqliteExtendedErrorCode == 2067) { throw new CashAlreadyOpenException(); }
        }
        public CashSession? FindSession(Guid id)
        {
            // Named fields also allow reading legacy fixtures before their explicit upgrade.
            using var cmd=Command("SELECT * FROM cash_session WHERE id=$id",("$id",Id(id)));
            using var r=cmd.ExecuteReader();
            if(!r.Read()) return null;
            Money? Amount(string name) => r[name] is long cents ? new Money(cents) : null;
            var noteIndex = Enumerable.Range(0,r.FieldCount).FirstOrDefault(i=>r.GetName(i)=="discrepancy_note",-1);
            return CashSession.Restore(id,Guid.Parse((string)r["terminal_id"]),Guid.Parse((string)r["opened_by"]),Date(r,r.GetOrdinal("opened_utc"))!.Value,
                Amount("opening_cents")!.Value,(string)r["state"]=="OPEN"?CashState.Open:CashState.Closed,Date(r,r.GetOrdinal("closed_utc")),
                r["closed_by"] is string actor?Guid.Parse(actor):null,Amount("expected_cents"),Amount("counted_cents"),Amount("difference_cents"),
                noteIndex<0 || r.IsDBNull(noteIndex)?null:r.GetString(noteIndex));
        }
        public CashCount? FindCount(Guid id)
        {
            using var cmd=Command("SELECT counted_by,counted_utc,counted_cents FROM cash_count WHERE session_id=$id",("$id",Id(id)));
            using var r=cmd.ExecuteReader();
            return r.Read()?new(id,Guid.Parse(r.GetString(0)),Date(r,1)!.Value,new Money(r.GetInt64(2))):null;
        }
        public void AddCount(CashCount count)
        {
            using var cmd=Command("INSERT INTO cash_count VALUES($id,$actor,$at,$amount)",("$id",Id(count.SessionId)),
                ("$actor",Id(count.CountedBy)),("$at",At(count.CountedAt)),("$amount",count.Counted.Cents));
            cmd.ExecuteNonQuery();
        }
        public void CloseSession(CashSession session)
        {
            using var cmd=Command("UPDATE cash_session SET state='CLOSED',closed_by=$actor,closed_utc=$at,expected_cents=$expected,counted_cents=$counted,difference_cents=$difference,discrepancy_note=$note WHERE id=$id AND state='OPEN'",
                ("$id",Id(session.Id)),("$actor",Id(session.ClosedBy!.Value)),("$at",At(session.ClosedAt!.Value)),
                ("$expected",session.Expected!.Value.Cents),("$counted",session.Counted!.Value.Cents),("$difference",session.Difference!.Value.Cents),("$note",session.DiscrepancyNote));
            if(cmd.ExecuteNonQuery()!=1) throw new CashClosingException("La sesión ya no está abierta.");
        }
        public void AddAudit(AuditEntry entry)
        {
            using var cmd = Command("INSERT INTO audit_entry VALUES($id,$actor,$terminal,$action,$entity,$at,$details)",
                ("$id", Id(entry.Id)), ("$actor", Id(entry.Actor)), ("$terminal", entry.Terminal is { } t ? Id(t) : null),
                ("$action", entry.Action), ("$entity", Id(entry.Entity)), ("$at", At(entry.At)), ("$details", entry.DetailsJson));
            cmd.ExecuteNonQuery();
        }
    }
}
