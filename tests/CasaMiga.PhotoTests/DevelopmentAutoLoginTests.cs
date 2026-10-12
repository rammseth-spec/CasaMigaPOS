using System.IO;
using System.Text.Json;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass, DoNotParallelize]
public sealed class DevelopmentAutoLoginTests
{
    private string directory = null!;
    private string password = null!;

    [TestInitialize]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "CasaMigaAutoLogin", Guid.NewGuid().ToString("N"));
        new FoundationRuntime(new FoundationOptions(directory, true)).Start();
        password = "Aa1!" + Guid.NewGuid().ToString("N")[..12];
        new IdentityService(new SqliteLocalUnitOfWork(new Database(Path.Combine(directory, "casamiga.db"))), new LocalPasswords(), new SystemClock(), new UserSession())
            .Bootstrap("admin", "Administrador", password);
    }

    [TestCleanup]
    public void Cleanup() { try { Directory.Delete(directory, true); } catch (IOException) { } }

    private void WriteFile(string user, string secret) =>
        File.WriteAllText(Path.Combine(directory, DevelopmentAutoLogin.FileName), JsonSerializer.Serialize(new { Username = user, Password = secret }));

    private async Task<MainViewModel> Start(bool development)
    {
        var model = new MainViewModel();
        await model.InitializeAsync(new FoundationOptions(directory, IsDevelopment: development));
        return model;
    }

    [TestMethod]
    public Task DevelopmentCopySignsInAutomaticallyAndLogoutReturnsToLogin() => WpfTestHost.OnUi(async () =>
    {
        WriteFile("admin", password);
        var model = await Start(development: true);
        try
        {
            Assert.IsTrue(model.CanNavigate, model.Message);
            Assert.AreEqual("Administrador", model.CurrentUserName);
            StringAssert.Contains(model.Message, "automáticamente");
            model.Logout();
            Assert.IsTrue(model.IsLogin, "After logging out the normal login is shown so other users can be tested.");
        }
        finally { model.Sync?.Dispose(); }
    });

    [TestMethod]
    public Task AfterLogoutEachAccountOfTheFileIsOneTapAway() => WpfTestHost.OnUi(async () =>
    {
        File.WriteAllText(Path.Combine(directory, DevelopmentAutoLogin.FileName), JsonSerializer.Serialize(new
        {
            Username = "admin", Password = password,
            Users = new[] { new { Username = "ADMIN", Password = password }, new { Username = "ana", Password = "Equivocada#2026" } },
        }));
        var model = await Start(development: true);
        try
        {
            Assert.IsTrue(model.CanNavigate, model.Message);
            Assert.IsFalse(model.HasDevelopmentAccounts, "Signed in: no quick accounts on screen.");
            model.Logout();
            CollectionAssert.AreEqual(new[] { "admin", "ana" }, model.DevelopmentAccounts.ToArray(), "The automatic one first, no repeated users.");
            await model.LoginAsDevelopmentAsync("ana");
            Assert.IsFalse(model.CanNavigate, "Quick accounts use the normal login: a wrong password does not enter.");
            await model.LoginAsDevelopmentAsync("ADMIN");
            Assert.AreEqual("Administrador", model.CurrentUserName);
            model.Logout();
            await model.LoginAsDevelopmentAsync("nadie");
            Assert.IsFalse(model.CanNavigate);
        }
        finally { model.Sync?.Dispose(); }
    });

    [TestMethod]
    public Task NormalInstallationIgnoresTheFileEvenIfPresent() => WpfTestHost.OnUi(async () =>
    {
        WriteFile("admin", password);
        var model = await Start(development: false);
        try
        {
            Assert.IsFalse(model.CanNavigate);
            Assert.IsTrue(model.IsLogin);
            Assert.IsFalse(model.HasDevelopmentAccounts);
            Assert.IsNull(DevelopmentAutoLogin.TryLoad(new FoundationOptions(directory, IsDevelopment: false)));
        }
        finally { model.Sync?.Dispose(); }
    });

    [TestMethod]
    public Task WrongOrMalformedCredentialsFallBackToTheNormalLogin() => WpfTestHost.OnUi(async () =>
    {
        WriteFile("admin", "Equivocada#2026");
        var model = await Start(development: true);
        try
        {
            Assert.IsFalse(model.CanNavigate);
            Assert.IsTrue(model.IsLogin);
            StringAssert.Contains(model.Message, "no funcionó");
        }
        finally { model.Sync?.Dispose(); }

        File.WriteAllText(Path.Combine(directory, DevelopmentAutoLogin.FileName), "{ no es json");
        model = await Start(development: true);
        try
        {
            Assert.IsTrue(model.IsLogin);
            StringAssert.Contains(model.Message, DevelopmentAutoLogin.FileName);
        }
        finally { model.Sync?.Dispose(); }
    });

    [TestMethod]
    public Task ANewLocalTestInstallationCreatesTheAdministratorAndSignsIn() => WpfTestHost.OnUi(async () =>
    {
        // Prueba local en otra computadora (scripts/run-local.ps1): la carpeta está vacía.
        var fresh = Path.Combine(Path.GetTempPath(), "CasaMigaAutoLogin", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fresh);
        File.WriteAllText(Path.Combine(fresh, DevelopmentAutoLogin.FileName), JsonSerializer.Serialize(new
        {
            Username = "admin", Password = "Casa#2026prueba",
            Users = new[] { new { Username = "encargado", Password = "Encargado#2026", Role = "Manager", DisplayName = "Encargado de prueba" },
                            new { Username = "cajero", Password = "Cajero#2026", Role = "Cashier", DisplayName = "Cajero de prueba" } },
            // Nube local apagada: el registro falla con su mensaje y la caja sigue lista para vender.
            Cloud = new { Url = "http://localhost:9", Secret = "secreto-de-alta-de-prueba-local" },
        }));
        var model = new MainViewModel();
        try
        {
            await model.InitializeAsync(new FoundationOptions(fresh, IsDevelopment: true));
            Assert.IsTrue(model.CanNavigate, model.Message);
            Assert.AreEqual("Administrador", model.CurrentUserName);
            Assert.IsFalse(model.Sync!.IsRegistered);
            StringAssert.Contains(model.Sync.Message, "No se pudo conectar");
            // Un usuario por rol, listo para entrar con su contraseña fija (sin cambio obligatorio).
            var identity = new IdentityService(new SqliteLocalUnitOfWork(new Database(Path.Combine(fresh, "casamiga.db"))), new LocalPasswords(), new SystemClock(), new UserSession());
            Assert.AreEqual(LoginOutcome.SignedIn, identity.Authenticate("encargado", "Encargado#2026"));
            Assert.AreEqual(LoginOutcome.SignedIn, identity.Authenticate("cajero", "Cajero#2026"));
            model.Logout();
            CollectionAssert.AreEqual(new[] { "admin", "encargado", "cajero" }, model.DevelopmentAccounts.ToArray(), "After logout, one button per role.");
        }
        finally { model.Sync?.Dispose(); try { Directory.Delete(fresh, true); } catch (IOException) { } }

        // Una instalación normal (sin modo desarrollo) nunca crea usuarios sola: pide el administrador.
        var normal = Path.Combine(Path.GetTempPath(), "CasaMigaAutoLogin", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(normal);
        File.WriteAllText(Path.Combine(normal, DevelopmentAutoLogin.FileName), JsonSerializer.Serialize(new { Username = "admin", Password = "Casa#2026prueba" }));
        model = new MainViewModel();
        try
        {
            await model.InitializeAsync(new FoundationOptions(normal, IsDevelopment: false));
            Assert.IsTrue(model.IsBootstrap);
            Assert.IsFalse(model.CanNavigate);
        }
        finally { model.Sync?.Dispose(); try { Directory.Delete(normal, true); } catch (IOException) { } }
    });
}
