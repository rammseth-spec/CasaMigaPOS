namespace CasaMiga.Application;

public sealed class IdentityService(ILocalUnitOfWork database, IPasswords passwords, IClock clock, UserSession session)
{
    public bool NeedsBootstrap() => database.Run(store => store.UserCount() == 0);

    public void Bootstrap(string username, string displayName, string password)
    {
        var normalized = Normalize(username);
        if (normalized.Length == 0 || normalized.Length > 80 || displayName.Trim().Length is < 1 or > 100)
            throw new ArgumentException("Revisa el usuario y nombre (máximo 80 y 100 caracteres).");
        if (password.Length is < 12 or > 128) throw new ArgumentException("Usa una contraseña de 12 a 128 caracteres.");
        var hash = passwords.Hash(password);
        database.Run(store =>
        {
            if (store.UserCount() != 0) throw new InvalidOperationException("La configuración inicial ya está completa.");
            var id = Guid.NewGuid(); var at = clock.UtcNow;
            store.AddAdministrator(id, normalized, displayName.Trim(), hash, at);
            store.AddAudit(new(Guid.NewGuid(), id, null, "identity.bootstrap", id, at, "{}"));
            return true;
        });
    }

    public bool Login(string username, string password)
    {
        session.SignOut();
        if (username.Length > 80 || password.Length is < 1 or > 128) return false;
        var identity = database.Run(store =>
        {
            var user = store.FindUser(Normalize(username));
            var check = passwords.Verify(user?.PasswordHash, password);
            var now = clock.UtcNow;
            if (user is null || !user.Active || user.LockedUntil > now) return null;
            if (check == PasswordCheck.Failed)
            {
                int failures = user.LockedUntil is not null ? 1 : user.FailedAttempts + 1;
                store.UpdateLogin(user.Id, failures, failures >= 5 ? now.AddMinutes(1) : null, null);
                return null;
            }
            store.UpdateLogin(user.Id, 0, null, check == PasswordCheck.Rehash ? passwords.Hash(password) : null);
            return new UserIdentity(user.Id, user.DisplayName);
        });
        if (identity is null) return false;
        session.SignIn(identity);
        return true;
    }

    private static string Normalize(string username) => username.Trim().ToUpperInvariant();
}
