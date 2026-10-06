using System.Globalization;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class PasswordManagementTests
{
    private static readonly LocalPasswords Passwords = new();
    private const string AdminPassword = "Admin#2026ok";
    private string directory = null!;
    private Database database = null!;
    private SqliteLocalUnitOfWork work = null!;
    private UserSession session = null!;
    private IdentityService identity = null!;
    private TestClock clock = null!;
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = new(2026,10,6,12,0,0,TimeSpan.Zero); }

    [TestInitialize] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CasaMigaTests", Guid.NewGuid().ToString("N"));
        database = new(Path.Combine(directory,"identity.db")); database.Initialize();
        work = new(database); session = new(); clock = new();
        identity = new(work, Passwords, clock, session);
        identity.Bootstrap("admin", "Administrador", AdminPassword);
        Assert.IsTrue(identity.Login("admin", AdminPassword));
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory,true);

    private object? Scalar(string sql)
    {
        using var c = database.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = sql; return cmd.ExecuteScalar();
    }
    private long Audits(string action) => (long)Scalar($"SELECT count(*) FROM audit_entry WHERE action='{action}'")!;

    [TestMethod]
    [DataRow("Ab1!", DisplayName = "corta")]
    [DataRow("Abcdefgh1!234567X", DisplayName = "17 caracteres")]
    [DataRow("abcdef1!", DisplayName = "sin mayúscula")]
    [DataRow("ABCDEF1!", DisplayName = "sin minúscula")]
    [DataRow("Abcdefg!", DisplayName = "sin número")]
    [DataRow("Abcdefg1", DisplayName = "sin especial")]
    public void PolicyRejectsWeakPasswords(string password)
    {
        Assert.IsFalse(PasswordPolicy.IsValid(password));
        Assert.ThrowsExactly<ArgumentException>(() => PasswordPolicy.Validate(password));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("panaderia")]
    [DataRow("PANADERIA#1")]
    [DataRow("Panaderia2026")]
    [DataRow("Panaderia#2026")]
    [DataRow("Pa#1")]
    [DataRow("Panaderia#2026xyz")]
    [DataRow("Ñandú#2026ok")]
    public void LiveChecklistAndValidationNeverDisagree(string password)
    {
        var allMet = PasswordPolicy.Evaluate(password).All(r => r.Met);
        Assert.AreEqual(allMet, PasswordPolicy.IsValid(password));
        if (!allMet)
        {
            var error = Assert.ThrowsExactly<ArgumentException>(() => PasswordPolicy.Validate(password));
            foreach (var missing in PasswordPolicy.Evaluate(password).Where(r => !r.Met))
                StringAssert.Contains(error.Message, missing.Label[1..], "The error lists exactly what the checklist shows in red.");
        }
    }

    [TestMethod]
    public void PolicyAcceptsBoundaryLengthsAndTemporaryPasswordsComply()
    {
        Assert.IsTrue(PasswordPolicy.IsValid("Abcde1!x"));
        Assert.IsTrue(PasswordPolicy.IsValid("Abcdefgh1!234567"));
        for (int i = 0; i < 200; i++) Assert.IsTrue(PasswordPolicy.IsValid(PasswordPolicy.GenerateTemporary()));
    }

    [TestMethod]
    public void BootstrapRejectsPasswordOutsidePolicy()
    {
        var other = new Database(Path.Combine(directory, "other.db")); other.Initialize();
        var fresh = new IdentityService(new SqliteLocalUnitOfWork(other), Passwords, clock, new UserSession());
        Assert.ThrowsExactly<ArgumentException>(() => fresh.Bootstrap("owner", "Dueño", "contrasenalarga"));
        Assert.IsTrue(fresh.NeedsBootstrap());
    }

    [TestMethod]
    public void NewUserMustReplaceTemporaryPasswordBeforeSigningIn()
    {
        var temporary = identity.CreateUser("cajero1", "Cajero sucursal centro", "Cashier");
        Assert.AreEqual(1L, Audits("identity.user.created"));

        Assert.AreEqual(LoginOutcome.PasswordChangeRequired, identity.Authenticate("cajero1", temporary));
        Assert.IsNull(session.Current, "A temporary password must not open a session.");

        identity.ChangePassword("cajero1", temporary, "Centro#2026a");
        Assert.AreEqual(0L, Scalar("SELECT must_change_password FROM app_user WHERE username='CAJERO1'"));
        Assert.AreEqual(1L, Audits("identity.password.changed"));
        Assert.IsFalse(identity.Login("cajero1", temporary));
        Assert.IsTrue(identity.Login("cajero1", "Centro#2026a"));
        Assert.AreEqual("Cajero sucursal centro", session.Current!.DisplayName);
    }

    [TestMethod]
    public void OnlyOneAdministratorCanExist()
    {
        Assert.ThrowsExactly<ArgumentException>(() => identity.CreateUser("admin2", "Otro administrador", "Administrator"));
        Assert.AreEqual(1L, Scalar("SELECT count(*) FROM app_user WHERE role_name='Administrator'"));
    }

    [TestMethod]
    public void AdministratorResetUnlocksUserAndForcesChange()
    {
        var temporary = identity.CreateUser("encargado", "Encargado norte", "Manager");
        identity.ChangePassword("encargado", temporary, "Norte#2026ab");
        for (int i = 0; i < 5; i++) Assert.IsFalse(identity.Login("encargado", "Incorrecta#1"));
        Assert.IsNotNull(Scalar("SELECT locked_until_utc FROM app_user WHERE username='ENCARGADO'"));

        Assert.IsTrue(identity.Login("admin", AdminPassword));
        var userId = identity.ListUsers().Single(u => u.Username == "ENCARGADO").Id;
        var reset = identity.ResetPassword(userId);

        Assert.AreEqual(DBNull.Value, Scalar("SELECT locked_until_utc FROM app_user WHERE username='ENCARGADO'"));
        Assert.IsFalse(identity.Login("encargado", "Norte#2026ab"), "The forgotten password stops working.");
        Assert.AreEqual(LoginOutcome.PasswordChangeRequired, identity.Authenticate("encargado", reset));
        var adminId = (string)Scalar("SELECT id FROM app_user WHERE username='ADMIN'")!;
        Assert.AreEqual(adminId, Scalar($"SELECT actor_id FROM audit_entry WHERE action='identity.password.reset' AND entity_id='{userId}'"));
    }

    [TestMethod]
    public void AdministratorCannotResetOwnPasswordFromUserManagement()
    {
        var adminId = identity.ListUsers().Single(u => u.Username == "ADMIN").Id;
        Assert.ThrowsExactly<ArgumentException>(() => identity.ResetPassword(adminId));
        Assert.AreEqual(0L, Audits("identity.password.reset"));
    }

    [TestMethod]
    public void NonAdministratorsCannotManageUsers()
    {
        var temporary = identity.CreateUser("cajero2", "Cajero sur", "Cashier");
        identity.ChangePassword("cajero2", temporary, "Sur#2026abcd");
        Assert.IsTrue(identity.Login("cajero2", "Sur#2026abcd"));
        Assert.IsFalse(identity.CanManageUsers());
        Assert.ThrowsExactly<AccessDeniedException>(() => identity.ListUsers());
        Assert.ThrowsExactly<AccessDeniedException>(() => identity.CreateUser("x", "X", "Cashier"));
        Assert.ThrowsExactly<AccessDeniedException>(() => identity.ResetPassword(Guid.NewGuid()));
    }

    [TestMethod]
    public void WrongCurrentPasswordIsRejectedAndAudited()
    {
        Assert.ThrowsExactly<ArgumentException>(() => identity.ChangePassword("admin", "Equivocada#1", "Nueva#2026ab"));
        Assert.AreEqual(1L, Audits("identity.password.change_failed"));
        Assert.ThrowsExactly<ArgumentException>(() => identity.ChangePassword("admin", AdminPassword, AdminPassword));
        Assert.IsTrue(identity.Login("admin", AdminPassword));
    }

    [TestMethod]
    public void ForgottenPasswordRequestIsAuditedOnlyForExistingUsers()
    {
        identity.RequestPasswordHelp("admin");
        identity.RequestPasswordHelp("no-existe");
        Assert.AreEqual(1L, Audits("identity.password.help_requested"));
    }

    [TestMethod]
    public void LegacyPasswordsOutsidePolicyStillSignIn()
    {
        const string legacy = "contraseña larga anterior sin reglas";
        using (var c = database.Open())
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT INTO app_user(id,username,display_name,password_hash,role_name,created_utc) VALUES($id,'LEGACY','Anterior',$hash,'Manager',$at)";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D")); cmd.Parameters.AddWithValue("$hash", Passwords.Hash(legacy));
            cmd.Parameters.AddWithValue("$at", clock.UtcNow.ToString("O", CultureInfo.InvariantCulture)); cmd.ExecuteNonQuery();
        }
        Assert.IsTrue(identity.Login("legacy", legacy));
    }

    [TestMethod]
    [DataRow("BEFORE INSERT ON permission WHEN NEW.name='users.manage'", DisplayName = "falla al crear permiso")]
    [DataRow("BEFORE INSERT ON schema_migration WHEN NEW.version=17", DisplayName = "falla al registrar migración")]
    public void FailedMigration17RollsBackCompletelyAndCanRetry(string trigger)
    {
        var path = Path.Combine(directory, "upgrade.db");
        var assembly = typeof(Database).Assembly;
        var scripts = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql", StringComparison.Ordinal)).Order().Take(16).ToArray();
        using (var c = new Database(path).Open())
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "CREATE TABLE schema_migration(version INTEGER PRIMARY KEY,sha256 TEXT NOT NULL,applied_utc TEXT NOT NULL) STRICT"; cmd.ExecuteNonQuery();
            for (int i = 0; i < scripts.Length; i++)
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(scripts[i])!);
                var sql = reader.ReadToEnd(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
                cmd.CommandText = "INSERT INTO schema_migration VALUES($v,$h,'utc')"; cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$v", i + 1);
                cmd.Parameters.AddWithValue("$h", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql))));
                cmd.ExecuteNonQuery(); cmd.Parameters.Clear();
            }
            cmd.CommandText = "CREATE TRIGGER inject_017 " + trigger + " BEGIN SELECT RAISE(ABORT,'injected'); END"; cmd.ExecuteNonQuery();
        }
        var upgrade = new Database(path);
        Assert.Throws<Exception>(() => upgrade.Initialize()); // SQLite abort or the post-migration structure guard.
        long Probe(string sql) { using var c = upgrade.Open(); using var q = c.CreateCommand(); q.CommandText = sql; return (long)q.ExecuteScalar()!; }
        Assert.AreEqual(16L, Probe("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(0L, Probe("SELECT count(*) FROM pragma_table_info('app_user') WHERE name='must_change_password'"), "The column must roll back with the failed migration.");
        Assert.AreEqual(0L, Probe("SELECT count(*) FROM permission WHERE name='users.manage'"));
        using (var c = upgrade.Open()) { using var q = c.CreateCommand(); q.CommandText = "DROP TRIGGER inject_017"; q.ExecuteNonQuery(); }
        upgrade.Initialize(); upgrade.Validate();
        Assert.AreEqual((long)Database.LatestVersion, Probe("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual(1L, Probe("SELECT count(*) FROM role_permission WHERE permission_name='users.manage'"));
    }

    [TestMethod]
    public void AdministratorEditsAndDeactivatesUsersWithAudit()
    {
        var temporary = identity.CreateUser("cajero3", "Cajero centro", "Cashier");
        identity.ChangePassword("cajero3", temporary, "Centro#2026xy");
        Assert.IsTrue(identity.Login("admin", AdminPassword));
        var id = identity.ListUsers().Single(u => u.Username == "CAJERO3").Id;

        identity.UpdateUser(id, "Encargada norte", "Manager");
        var updated = identity.ListUsers().Single(u => u.Id == id);
        Assert.AreEqual("Encargada norte", updated.DisplayName); Assert.AreEqual("Manager", updated.Role);
        Assert.AreEqual(1L, Audits("identity.user.updated"));

        identity.SetActive(id, false);
        Assert.IsFalse(identity.Login("cajero3", "Centro#2026xy"), "Inactive users cannot sign in.");
        Assert.IsTrue(identity.Login("admin", AdminPassword));
        identity.SetActive(id, false);
        Assert.AreEqual(1L, Audits("identity.user.deactivated"), "Repeating the same state does not audit twice.");
        identity.SetActive(id, true);
        Assert.AreEqual(1L, Audits("identity.user.reactivated"));
        Assert.IsTrue(identity.Login("cajero3", "Centro#2026xy"));
    }

    [TestMethod]
    public void AdministratorAndOwnUserCannotBeEditedOrPromoted()
    {
        var adminId = identity.ListUsers().Single(u => u.Username == "ADMIN").Id;
        Assert.ThrowsExactly<ArgumentException>(() => identity.SetActive(adminId, false));
        Assert.ThrowsExactly<ArgumentException>(() => identity.UpdateUser(adminId, "Otro", "Manager"));
        identity.CreateUser("cajero4", "Cajero sur", "Cashier");
        var id = identity.ListUsers().Single(u => u.Username == "CAJERO4").Id;
        Assert.ThrowsExactly<ArgumentException>(() => identity.UpdateUser(id, "Cajero sur", "Administrator"));
        Assert.ThrowsExactly<ArgumentException>(() => identity.UpdateUser(id, "  ", "Cashier"));
        Assert.AreEqual(1L, Scalar("SELECT count(*) FROM app_user WHERE role_name='Administrator'"));
    }

    [TestMethod]
    public void MaintenanceRecoversForgottenAdministratorPassword()
    {
        for (int i = 0; i < 5; i++) Assert.IsFalse(identity.Login("admin", "Olvidada#123"));
        var recovery = new IdentityService(work, Passwords, clock, new UserSession());
        var (username, temporary) = recovery.RecoverAdministrator();
        Assert.AreEqual("ADMIN", username);
        Assert.IsTrue(PasswordPolicy.IsValid(temporary));
        Assert.IsFalse(identity.Login("admin", AdminPassword), "The forgotten password stops working.");
        Assert.AreEqual(LoginOutcome.PasswordChangeRequired, identity.Authenticate("admin", temporary), "Recovery unlocks and forces a new password.");
        identity.ChangePassword("admin", temporary, "Nueva#2026adm");
        Assert.IsTrue(identity.Login("admin", "Nueva#2026adm"));
        Assert.AreEqual(1L, Audits("identity.admin.recovered"));
    }

    [TestMethod]
    public void MigrationAddsManagePermissionOnlyToAdministrator()
    {
        Assert.AreEqual((long)Database.LatestVersion, Scalar("SELECT max(version) FROM schema_migration"));
        Assert.AreEqual("Administrator", Scalar("SELECT group_concat(role_name) FROM role_permission WHERE permission_name='users.manage'"));
    }
}
