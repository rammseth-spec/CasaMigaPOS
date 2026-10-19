using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        private static string CopyKind(ReceiptCopyKind kind) => kind switch
        {
            ReceiptCopyKind.Original => "ORIGINAL",
            ReceiptCopyKind.Retry => "RETRY",
            ReceiptCopyKind.Reprint => "REPRINT",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        private static ReceiptCopyKind CopyKind(string value) => value switch
        {
            "ORIGINAL" => ReceiptCopyKind.Original,
            "RETRY" => ReceiptCopyKind.Retry,
            "REPRINT" => ReceiptCopyKind.Reprint,
            _ => throw new InvalidDataException("Tipo de impresión inválido.")
        };
        private static string PrintState(ReceiptPrintState state) => state switch
        {
            ReceiptPrintState.Requested => "REQUESTED",
            ReceiptPrintState.Submitted => "SUBMITTED",
            ReceiptPrintState.Completed => "COMPLETED",
            ReceiptPrintState.Failed => "FAILED",
            ReceiptPrintState.Unknown => "UNKNOWN",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        private static ReceiptPrintState PrintState(string value) => value switch
        {
            "REQUESTED" => ReceiptPrintState.Requested,
            "SUBMITTED" => ReceiptPrintState.Submitted,
            "COMPLETED" => ReceiptPrintState.Completed,
            "FAILED" => ReceiptPrintState.Failed,
            "UNKNOWN" => ReceiptPrintState.Unknown,
            _ => throw new InvalidDataException("Estado de impresión inválido.")
        };

        public ReceiptSnapshot? FindReceiptSnapshot(Guid saleId)
        {
            using var cmd=Command("SELECT schema_version,content_json,content_sha256,created_utc FROM receipt_snapshot WHERE sale_id=$sale",("$sale",Id(saleId)));
            using var r=cmd.ExecuteReader();
            return r.Read()?new(saleId,r.GetInt32(0),r.GetString(1),r.GetString(2),Date(r,3)!.Value):null;
        }
        public void AddReceiptSnapshot(ReceiptSnapshot snapshot)
        {
            using var cmd=Command("INSERT INTO receipt_snapshot VALUES($sale,$version,$json,$hash,$at)",
                ("$sale",Id(snapshot.SaleId)),("$version",snapshot.SchemaVersion),("$json",snapshot.ContentJson),("$hash",snapshot.Sha256),("$at",At(snapshot.CreatedAt)));
            cmd.ExecuteNonQuery();
        }
        public ReceiptPrintRequest? FindOriginalReceiptPrintRequest(Guid saleId)
        {
            using var cmd=Command("SELECT id,parent_request_id,requested_by,requested_utc,queue_name,snapshot_sha256 FROM receipt_print_request WHERE sale_id=$sale AND kind='ORIGINAL'",("$sale",Id(saleId)));
            using var r=cmd.ExecuteReader();
            return r.Read()?Request(r,saleId,ReceiptCopyKind.Original):null;
        }
        public ReceiptPrintRequest? FindReceiptPrintRequest(Guid requestId)
        {
            using var cmd=Command("SELECT sale_id,kind,parent_request_id,requested_by,requested_utc,queue_name,snapshot_sha256 FROM receipt_print_request WHERE id=$id",("$id",Id(requestId)));
            using var r=cmd.ExecuteReader();
            return r.Read()?new(requestId,Guid.Parse(r.GetString(0)),CopyKind(r.GetString(1)),r.IsDBNull(2)?null:Guid.Parse(r.GetString(2)),
                Guid.Parse(r.GetString(3)),Date(r,4)!.Value,r.IsDBNull(5)?null:r.GetString(5),r.IsDBNull(6)?null:r.GetString(6)):null;
        }
        private static ReceiptPrintRequest Request(Microsoft.Data.Sqlite.SqliteDataReader r,Guid saleId,ReceiptCopyKind kind)
            =>new(Guid.Parse(r.GetString(0)),saleId,kind,r.IsDBNull(1)?null:Guid.Parse(r.GetString(1)),Guid.Parse(r.GetString(2)),
                Date(r,3)!.Value,r.IsDBNull(4)?null:r.GetString(4),r.IsDBNull(5)?null:r.GetString(5));
        public ReceiptPrintEvent? FindLatestReceiptPrintEvent(Guid requestId)
        {
            using var cmd=Command("SELECT id,state,occurred_utc,spooler_job_id,error_code FROM receipt_print_event WHERE request_id=$request ORDER BY sequence DESC LIMIT 1",("$request",Id(requestId)));
            using var r=cmd.ExecuteReader();
            return r.Read()?new(Guid.Parse(r.GetString(0)),requestId,PrintState(r.GetString(1)),Date(r,2)!.Value,
                r.IsDBNull(3)?null:r.GetInt32(3),r.IsDBNull(4)?null:r.GetString(4)):null;
        }
        public void AddReceiptPrintRequest(ReceiptPrintRequest request)
        {
            using var cmd=Command("INSERT INTO receipt_print_request VALUES($id,$sale,$kind,$parent,$actor,$at,$queue,$hash)",
                ("$id",Id(request.Id)),("$sale",Id(request.SaleId)),("$kind",CopyKind(request.Kind)),
                ("$parent",request.ParentRequestId is { } parent?Id(parent):null),("$actor",Id(request.RequestedBy)),
                ("$at",At(request.RequestedAt)),("$queue",request.QueueName),("$hash",request.SnapshotSha256));
            cmd.ExecuteNonQuery();
        }
        public void AddReceiptPrintEvent(ReceiptPrintEvent entry)
        {
            using var cmd=Command("INSERT INTO receipt_print_event VALUES($id,$request,(SELECT COALESCE(MAX(sequence),0)+1 FROM receipt_print_event WHERE request_id=$request),$state,$at,$job,$error)",
                ("$id",Id(entry.Id)),("$request",Id(entry.RequestId)),("$state",PrintState(entry.State)),
                ("$at",At(entry.OccurredAt)),("$job",entry.SpoolerJobId),("$error",entry.ErrorCode));
            cmd.ExecuteNonQuery();
        }
        public IReadOnlyList<ReceiptActivity> ListReceiptActivity(int limit)
        {
            // Cada ticket con su último evento (REQUESTED, SUBMITTED, COMPLETED, FAILED o UNKNOWN), más reciente primero.
            using var cmd=Command("SELECT r.id,r.sale_id,t.name,s.sequence,r.kind,r.requested_utc,u.display_name,e.state,e.spooler_job_id,e.error_code,s.total_cents "+
                "FROM receipt_print_request r JOIN sale s ON s.id=r.sale_id JOIN terminal t ON t.id=s.terminal_id JOIN app_user u ON u.id=r.requested_by "+
                "JOIN receipt_print_event e ON e.request_id=r.id AND e.sequence=(SELECT MAX(sequence) FROM receipt_print_event WHERE request_id=r.id) "+
                "ORDER BY r.requested_utc DESC,r.id LIMIT $limit",("$limit",limit));
            using var r=cmd.ExecuteReader();
            var rows=new List<ReceiptActivity>();
            while(r.Read())
                rows.Add(new(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),r.GetString(2),r.GetInt64(3),CopyKind(r.GetString(4)),
                    Date(r,5)!.Value,r.GetString(6),PrintState(r.GetString(7)),r.IsDBNull(8)?null:r.GetInt32(8),r.IsDBNull(9)?null:r.GetString(9),r.GetInt64(10)));
            return rows;
        }
    }
}
