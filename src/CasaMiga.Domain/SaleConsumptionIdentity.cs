using System.Security.Cryptography;
using System.Text;

namespace CasaMiga.Domain;

public enum SaleInventoryEvaluationStatus { Disabled, NoConsumption, Consumed }
public enum InventorySaleConsumptionState { Disabled, Enabled, Paused }

public static class SaleConsumptionIdentity
{
    public const int Version = 1;
    public const int PolicyVersion = 1;
    public static readonly Guid NamespaceV1 = new("82b7820c-b1d4-4cb2-a5a6-19419f756cb9");

    public static Guid OperationIdV1(Guid checkoutOperationId)
    {
        if (checkoutOperationId == Guid.Empty) throw new ArgumentException("Checkout OperationId vacío.");
        return UuidV5(NamespaceV1, checkoutOperationId.ToString("D"));
    }

    // RFC UUIDv5 uses network byte order, not the legacy Guid byte-array order.
    public static Guid UuidV5(Guid namespaceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var bytes = namespaceId.ToByteArray(bigEndian: true).Concat(Encoding.UTF8.GetBytes(name)).ToArray();
        var hash = SHA1.HashData(bytes);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}
