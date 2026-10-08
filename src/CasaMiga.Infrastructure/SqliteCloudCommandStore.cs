using CasaMiga.Application;
namespace CasaMiga.Infrastructure;

public sealed partial class SqliteLocalUnitOfWork
{
    private sealed partial class Store
    {
        public long LastCloudCommandSequence()
        {
            using var q = Command("SELECT COALESCE(max(seq),0) FROM cloud_command");
            return Convert.ToInt64(q.ExecuteScalar());
        }

        public CloudCommandResult? FindCloudCommand(Guid id)
        {
            using var q = Command("SELECT id,seq,status,message FROM cloud_command WHERE id=$id", ("$id", Id(id)));
            using var r = q.ExecuteReader();
            return r.Read() ? new(Guid.Parse(r.GetString(0)), r.GetInt64(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3)) : null;
        }

        public void AddCloudCommand(CloudCommandResult result, string kind, string requestedBy, DateTimeOffset at)
        {
            using var q = Command("INSERT INTO cloud_command(id,seq,kind,requested_by,status,message,applied_utc) VALUES($id,$seq,$kind,$by,$status,$message,$at)",
                ("$id", Id(result.Id)), ("$seq", result.Sequence), ("$kind", kind[..Math.Min(kind.Length, 60)]),
                ("$by", string.IsNullOrWhiteSpace(requestedBy) ? "web" : requestedBy[..Math.Min(requestedBy.Length, 120)]), ("$status", result.Status),
                ("$message", result.Message is null ? null : result.Message[..Math.Min(result.Message.Length, 300)]), ("$at", At(at)));
            q.ExecuteNonQuery();
        }

        public IReadOnlyList<CloudCommandResult> UnreportedCloudCommands()
        {
            using var q = Command("SELECT id,seq,status,message FROM cloud_command WHERE reported_utc IS NULL ORDER BY seq LIMIT 50");
            using var r = q.ExecuteReader();
            var list = new List<CloudCommandResult>();
            while (r.Read()) list.Add(new(Guid.Parse(r.GetString(0)), r.GetInt64(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3)));
            return list;
        }

        public void MarkCloudCommandsReported(IReadOnlyCollection<Guid> ids, DateTimeOffset at)
        {
            foreach (var id in ids)
            {
                using var q = Command("UPDATE cloud_command SET reported_utc=$at WHERE id=$id AND reported_utc IS NULL", ("$id", Id(id)), ("$at", At(at)));
                q.ExecuteNonQuery();
            }
        }
    }
}
