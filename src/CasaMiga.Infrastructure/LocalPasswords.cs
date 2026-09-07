using CasaMiga.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CasaMiga.Infrastructure;

public sealed class LocalPasswords : IPasswords
{
    private readonly PasswordHasher<object> hasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = 210000
    }));
    private readonly object user = new();
    private readonly string dummyHash;
    public LocalPasswords() => dummyHash = hasher.HashPassword(user, Guid.NewGuid().ToString("N"));
    public string Hash(string password) => hasher.HashPassword(user, password);
    public PasswordCheck Verify(string? hash, string password)
    {
        try
        {
            var result = hasher.VerifyHashedPassword(user, hash ?? dummyHash, password);
            if (hash is null) return PasswordCheck.Failed;
            return result switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Valid,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.Rehash,
                _ => PasswordCheck.Failed
            };
        }
        catch (FormatException) { return PasswordCheck.Failed; }
    }
}
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
