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

    /// <summary>
    /// Alta hecha en la versión web: la nube ya generó la contraseña temporal y su hash (mismo formato PBKDF2) y el identificador,
    /// para que todas las cajas de la sucursal tengan el mismo usuario. Repetir el mismo alta no hace nada.
    /// </summary>
    public void CreateUserWithHash(Guid id, string username, string displayName, string role, string passwordHash)
    {
        var normalized = Normalize(username);
        ValidateNames(normalized, displayName);
        if (role is not ("Manager" or "Cashier")) throw new ArgumentException("Sólo existe un administrador. El rol debe ser Encargado o Cajero.");
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Alta inválida.");
        database.Run(store =>
        {
            var actor = Manager(store) ?? throw new AccessDeniedException();
            if (store.FindUser(id) is not null) return true;
            if (store.FindUser(normalized) is not null) throw new ArgumentException("Ya existe un usuario con ese nombre en esta caja.");
            var at = clock.UtcNow;
            store.AddUser(id, normalized, displayName.Trim(), role, passwordHash, mustChangePassword: true, at);
            store.AddAudit(new(Guid.NewGuid(), actor.Id, null, "identity.user.created", id, at, JsonSerializer.Serialize(new { role, source = "web" })));
            return true;
        });
    }

    /// <summary>
    /// «Cambiar contraseña» hecho en la versión web por el propio usuario (con su contraseña actual o temporal): ya viene como hash y queda
    /// como definitiva. Sólo el mismo usuario, activo; el administrador también (su contraseña es suya).
    /// </summary>
    public void ChangeOwnPasswordWithHash(Guid userId, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Cambio de contraseña inválido.");
        database.Run(store =>
        {
            if (session.Current?.Id != userId) throw new AccessDeniedException();
            var user = store.FindUser(userId) ?? throw new ArgumentException("El usuario ya no existe.");
            if (!user.Active) throw new ArgumentException("El usuario está inactivo.");
            store.SetPassword(user.Id, passwordHash, mustChangePassword: false);
            store.AddAudit(new(Guid.NewGuid(), user.Id, null, "identity.password.changed", user.Id, clock.UtcNow,
                JsonSerializer.Serialize(new { fromTemporary = user.MustChangePassword, source = "web" })));
            return true;
        });
    }

    /// <summary>Restablecimiento hecho en la versión web: la contraseña temporal ya viene como hash; debe cambiarse al entrar.</summary>
    public void ResetPasswordWithHash(Guid userId, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Restablecimiento inválido.");
        database.Run(store =>
        {
            var actor = Manager(store) ?? throw new AccessDeniedException();
            var target = store.FindUser(userId) ?? throw new ArgumentException("El usuario ya no existe.");
            if (target.Role == "Administrator") throw new ArgumentException("El administrador se recupera en el equipo con la herramienta de mantenimiento.");
            if (!target.Active) throw new ArgumentException("El usuario está inactivo.");
            store.SetPassword(target.Id, passwordHash, mustChangePassword: true);
            store.AddAudit(new(Guid.NewGuid(), actor.Id, null, "identity.password.reset", target.Id, clock.UtcNow, JsonSerializer.Serialize(new { source = "web" })));
            return true;
        });
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

    public void UpdateUser(Guid userId, string displayName, string role)
    {
        displayName = displayName.Trim();
        if (displayName.Length is < 1 or > 100) throw new ArgumentException("El nombre debe tener de 1 a 100 caracteres.");
        if (role is not ("Manager" or "Cashier")) throw new ArgumentException("Sólo existe un administrador. El rol debe ser Encargado o Cajero.");
        database.Run(store =>
        {
            var (actor, target) = ManagedTarget(store, userId);
            store.UpdateUser(target.Id, displayName, role);
            store.AddAudit(new(Guid.NewGuid(), actor.Id, null, "identity.user.updated", target.Id, clock.UtcNow,
                JsonSerializer.Serialize(new { before = new { target.DisplayName, target.Role }, after = new { DisplayName = displayName, Role = role } })));
            return true;
        });
    }

    public void SetActive(Guid userId, bool active) => database.Run(store =>
    {
        var (actor, target) = ManagedTarget(store, userId);
        if (target.Active == active) return true;
        store.SetUserActive(target.Id, active);
        store.AddAudit(new(Guid.NewGuid(), actor.Id, null, active ? "identity.user.reactivated" : "identity.user.deactivated", target.Id, clock.UtcNow, "{}"));
        return true;
    });

    /// <summary>
    /// Recuperación del administrador único desde la herramienta de mantenimiento local (requiere acceso al equipo y la app cerrada).
    /// Asigna una contraseña temporal, reactiva y desbloquea; debe cambiarse al entrar. Devuelve (usuario, temporal).
    /// </summary>
    public (string Username, string Temporary) RecoverAdministrator()
    {
        var temporary = PasswordPolicy.GenerateTemporary();
        var hash = passwords.Hash(temporary);
        var username = database.Run(store =>
        {
            var admin = store.ListUsers().SingleOrDefault(u => u.Role == "Administrator")
                ?? throw new InvalidOperationException("No hay administrador; usa la configuración inicial de la aplicación.");
            store.SetUserActive(admin.Id, true);
            store.SetPassword(admin.Id, hash, mustChangePassword: true);
            store.AddAudit(new(Guid.NewGuid(), admin.Id, null, "identity.admin.recovered", admin.Id, clock.UtcNow, JsonSerializer.Serialize(new { channel = "maintenance" })));
            return admin.Username;
        });
        return (username, temporary);
    }

    private (LocalUser Actor, LocalUser Target) ManagedTarget(ILocalStore store, Guid userId)
    {
        var actor = Manager(store) ?? throw new AccessDeniedException();
        if (actor.Id == userId) throw new ArgumentException("No puedes modificar tu propio usuario desde aquí.");
        var target = store.FindUser(userId) ?? throw new ArgumentException("El usuario ya no existe.");
        if (target.Role == "Administrator") throw new ArgumentException("El administrador no se puede editar ni desactivar.");
        return (actor, target);
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
