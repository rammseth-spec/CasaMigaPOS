using System.Text.Json;

namespace CasaMiga.Application;

public enum LoginOutcome { Failed, SignedIn, PasswordChangeRequired }

public sealed class IdentityService(ILocalUnitOfWork database, IPasswords passwords, IClock clock, UserSession session)
{
    public const string ManageUsersPermission = "users.manage";
    public static readonly IReadOnlyList<string> Roles = ["Administrator", "Manager", "Cashier"];

    public bool NeedsBootstrap() => database.Run(store => store.UserCount() == 0);

    public void Bootstrap(string username, string displayName, string password)
    {
        var normalized = Normalize(username);
        ValidateNames(normalized, displayName);
        PasswordPolicy.Validate(password);
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

    public bool Login(string username, string password) => Authenticate(username, password) == LoginOutcome.SignedIn;

    /// <summary>Un usuario con contraseña temporal no inicia sesión hasta crear su contraseña definitiva.</summary>
    public LoginOutcome Authenticate(string username, string password)
    {
        session.SignOut();
        var outcome = database.Run(store =>
        {
            var user = Verify(store, username, password);
            if (user is null) return (LoginOutcome.Failed, (UserIdentity?)null);
            if (user.MustChangePassword) return (LoginOutcome.PasswordChangeRequired, null);
            return (LoginOutcome.SignedIn, new UserIdentity(user.Id, user.DisplayName));
        });
        if (outcome.Item2 is { } identity) session.SignIn(identity);
        return outcome.Item1;
    }

    public void ChangePassword(string username, string currentPassword, string newPassword)
    {
        PasswordPolicy.Validate(newPassword);
        if (newPassword == currentPassword) throw new ArgumentException("La contraseña nueva debe ser distinta de la actual.");
        var hash = passwords.Hash(newPassword);
        var changed = database.Run(store =>
        {
            var user = Verify(store, username, currentPassword, rehash: false);
            if (user is null)
            {
                if (store.FindUser(Normalize(username)) is { } known)
                    store.AddAudit(new(Guid.NewGuid(), known.Id, null, "identity.password.change_failed", known.Id, clock.UtcNow, "{}"));
                return false;
            }
            store.SetPassword(user.Id, hash, mustChangePassword: false);
            store.AddAudit(new(Guid.NewGuid(), user.Id, null, "identity.password.changed", user.Id, clock.UtcNow,
                JsonSerializer.Serialize(new { fromTemporary = user.MustChangePassword })));
            return true;
        });
        if (!changed) throw new ArgumentException("El usuario o la contraseña actual no son correctos, o el usuario está bloqueado un minuto.");
    }

    /// <summary>Registra la solicitud sin revelar si el usuario existe; el restablecimiento lo hace un administrador.</summary>
    public void RequestPasswordHelp(string username)
    {
        var normalized = Normalize(username);
        if (normalized.Length is 0 or > 80) return;
        database.Run(store =>
        {
            if (store.FindUser(normalized) is { Active: true } user)
                store.AddAudit(new(Guid.NewGuid(), user.Id, null, "identity.password.help_requested", user.Id, clock.UtcNow, "{}"));
            return true;
        });
    }

    public bool CanManageUsers() => database.Run(store => Manager(store) is not null);

    public IReadOnlyList<UserSummary> ListUsers() => database.Run(store =>
    {
        _ = Manager(store) ?? throw new AccessDeniedException();
        return store.ListUsers();
    });

    /// <summary>Crea el usuario con una contraseña temporal que debe cambiar en su primer acceso. Devuelve la temporal para mostrarla una sola vez.</summary>
    public string CreateUser(string username, string displayName, string role)
    {
        var normalized = Normalize(username);
        ValidateNames(normalized, displayName);
        if (role == "Administrator") throw new ArgumentException("Sólo existe un administrador. Crea el usuario como Encargado o Cajero.");
        if (!Roles.Contains(role)) throw new ArgumentException("Selecciona un rol válido.");
        var temporary = PasswordPolicy.GenerateTemporary();
        var hash = passwords.Hash(temporary);
        database.Run(store =>
        {
            var actor = Manager(store) ?? throw new AccessDeniedException();
            if (store.FindUser(normalized) is not null) throw new ArgumentException("Ya existe un usuario con ese nombre.");
            var id = Guid.NewGuid(); var at = clock.UtcNow;
            store.AddUser(id, normalized, displayName.Trim(), role, hash, mustChangePassword: true, at);
            store.AddAudit(new(Guid.NewGuid(), actor.Id, null, "identity.user.created", id, at, JsonSerializer.Serialize(new { role })));
            return true;
        });
        return temporary;
    }

    /// <summary>Asigna una contraseña temporal, desbloquea al usuario y obliga a cambiarla. Devuelve la temporal para mostrarla una sola vez.</summary>
    public string ResetPassword(Guid userId)
    {
        var temporary = PasswordPolicy.GenerateTemporary();
        var hash = passwords.Hash(temporary);
        database.Run(store =>
        {
            var actor = Manager(store) ?? throw new AccessDeniedException();
            if (actor.Id == userId) throw new ArgumentException("Para tu propio usuario sal de la sesión y usa «Cambiar contraseña» en la pantalla de inicio.");
            var target = store.FindUser(userId) ?? throw new ArgumentException("El usuario ya no existe.");
            if (!target.Active) throw new ArgumentException("El usuario está inactivo.");
            store.SetPassword(target.Id, hash, mustChangePassword: true);
            store.AddAudit(new(Guid.NewGuid(), actor.Id, null, "identity.password.reset", target.Id, clock.UtcNow, "{}"));
            return true;
        });
        return temporary;
    }

    private LocalUser? Verify(ILocalStore store, string username, string password, bool rehash = true)
    {
        if (username.Length > 80 || password.Length is < 1 or > 128) return null;
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
        store.UpdateLogin(user.Id, 0, null, rehash && check == PasswordCheck.Rehash ? passwords.Hash(password) : null);
        return user;
    }

    private LocalUser? Manager(ILocalStore store) =>
        session.Current is { } current && store.FindUser(current.Id) is { Active: true } user && store.HasPermission(user.Id, ManageUsersPermission) ? user : null;

    private static void ValidateNames(string normalized, string displayName)
    {
        if (normalized.Length == 0 || normalized.Length > 80 || displayName.Trim().Length is < 1 or > 100)
            throw new ArgumentException("Revisa el usuario y nombre (máximo 80 y 100 caracteres).");
    }

    private static string Normalize(string username) => username.Trim().ToUpperInvariant();
}
