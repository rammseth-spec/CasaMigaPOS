using System.IO;
using System.Text.Json;
using CasaMiga.Infrastructure;

namespace CasaMiga.Presentation;

/// <summary>
/// Inicio de sesión automático SÓLO para copias de revisión en modo desarrollo (development.settings.json junto al exe,
/// que el instalador nunca incluye). Lee <c>dev-autologin.json</c> de la carpeta de datos y usa el login normal con la
/// contraseña real: no existe ningún camino de acceso sin contraseña. Para desactivarlo basta con borrar el archivo.
/// <c>Users</c> (opcional) agrega más cuentas que la pantalla de acceso ofrece como botones para entrar sin teclear
/// (por ejemplo, un cajero), también con su contraseña real.
/// Prueba local (scripts/run-local.ps1): en una instalación nueva crea el administrador con estas credenciales, y con <c>Cloud</c>
/// registra la caja en la nube local; las cuentas de <c>Users</c> con <c>Role</c> se crean con esa contraseña si no existen
/// (un usuario por rol para ver cada vista). Todo sólo en modo desarrollo.
/// </summary>
public sealed record DevelopmentAutoLogin(string Username, string Password, IReadOnlyList<DevelopmentAutoLogin.Account>? Users = null, DevelopmentAutoLogin.CloudRegistration? Cloud = null)
{
    public const string FileName = "dev-autologin.json";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public sealed record Account(string Username, string Password, string? Role = null, string? DisplayName = null);
    public sealed record CloudRegistration(string Url, string Secret, string? Branch = null);

    /// <summary>La cuenta automática primero y luego las adicionales, sin repetir usuario.</summary>
    public IReadOnlyList<Account> Accounts =>
        new[] { new Account(Username, Password) }.Concat(Users ?? [])
            .DistinctBy(a => a.Username.Trim().ToUpperInvariant()).ToList();

    public static DevelopmentAutoLogin? TryLoad(FoundationOptions options)
    {
        if (!options.IsDevelopment) return null; // Una instalación normal ignora el archivo aunque exista.
        var path = Path.Combine(options.DataDirectory, FileName);
        if (!File.Exists(path)) return null;
        var value = JsonSerializer.Deserialize<DevelopmentAutoLogin>(File.ReadAllText(path), Json);
        if (value is null || string.IsNullOrWhiteSpace(value.Username) || string.IsNullOrEmpty(value.Password)
            || (value.Users ?? []).Any(u => u is null || string.IsNullOrWhiteSpace(u.Username) || string.IsNullOrEmpty(u.Password) || u.Role is not (null or "Manager" or "Cashier"))
            || value.Cloud is { } cloud && (string.IsNullOrWhiteSpace(cloud.Url) || string.IsNullOrWhiteSpace(cloud.Secret)))
            throw new InvalidDataException($"{FileName} inválido.");
        return value;
    }
}
