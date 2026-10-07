using CasaMiga.Application;
using CasaMiga.Domain;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        private bool? hasCashMovements;
        // Bases previas a la migración 022 (upgrades/restauraciones en curso) no tienen movimientos de caja.
        private bool HasCashMovements()
        {
            if (hasCashMovements is null)
            {
                using var probe = Command("SELECT count(*) FROM sqlite_master WHERE type='table' AND name='cash_movement'");
                hasCashMovements = Convert.ToInt64(probe.ExecuteScalar()) == 1;
            }
            return hasCashMovements.Value;
        }

        public void AddCashMovement(CashMovement movement)
        {
            using var insert = Command("INSERT INTO cash_movement VALUES($id,$operation,$cash,$terminal,$user,$kind,$category,$amount,$note,$at)",
                ("$id", Id(movement.Id)), ("$operation", Id(movement.OperationId)), ("$cash", Id(movement.CashSessionId)),
                ("$terminal", Id(movement.TerminalId)), ("$user", Id(movement.UserId)), ("$kind", CashMovementService.Code(movement.Kind)),
                ("$category", movement.Category is { } c ? CashMovementService.Code(c) : null), ("$amount", movement.Amount.Cents),
                ("$note", movement.Note), ("$at", At(movement.OccurredAt)));
            insert.ExecuteNonQuery();
        }

        public CashMovement? FindCashMovementByOperation(Guid operation) =>
            !HasCashMovements() ? null : ReadCashMovements("m.operation_id=$key", Id(operation)).SingleOrDefault();

        public IReadOnlyList<CashMovement> ListCashMovements(Guid session) =>
            !HasCashMovements() ? [] : ReadCashMovements("m.cash_session_id=$key", Id(session));

        public CashMovementTotals CashMovementTotals(Guid session)
        {
            if (!HasCashMovements()) return Application.CashMovementTotals.None;
            using var query = Command("SELECT COALESCE(SUM(CASE kind WHEN 'EXPENSE' THEN amount_cents END),0),COALESCE(SUM(CASE kind WHEN 'WITHDRAWAL' THEN amount_cents END),0),"
                + "COALESCE(SUM(CASE kind WHEN 'DEPOSIT' THEN amount_cents END),0) FROM cash_movement WHERE cash_session_id=$cash", ("$cash", Id(session)));
            using var r = query.ExecuteReader();
            r.Read();
            return new(new(r.GetInt64(0)), new(r.GetInt64(1)), new(r.GetInt64(2)));
        }

        private List<CashMovement> ReadCashMovements(string predicate, string key)
        {
            using var query = Command("SELECT m.id,m.operation_id,m.cash_session_id,m.terminal_id,m.user_id,u.display_name,m.kind,m.category,m.amount_cents,m.note,m.occurred_utc"
                + " FROM cash_movement m JOIN app_user u ON u.id=m.user_id WHERE " + predicate + " ORDER BY m.occurred_utc DESC,m.id DESC", ("$key", key));
            using var r = query.ExecuteReader();
            var result = new List<CashMovement>();
            while (r.Read())
                result.Add(new(Guid.Parse(r.GetString(0)), Guid.Parse(r.GetString(1)), Guid.Parse(r.GetString(2)), Guid.Parse(r.GetString(3)),
                    Guid.Parse(r.GetString(4)), r.GetString(5), CashMovementService.ParseKind(r.GetString(6)),
                    r.IsDBNull(7) ? null : CashMovementService.ParseCategory(r.GetString(7)), new(r.GetInt64(8)), r.GetString(9), Date(r, 10)!.Value));
            return result;
        }
    }
}
