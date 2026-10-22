using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaMiga.Infrastructure;

namespace CasaMiga.Presentation;

/// <summary>
/// Inicio de sesión automático de la caja normal, pedido por el dueño el 22/10/2026 «por el momento». Usa siempre el login
/// normal con la contraseña real: no hay acceso sin contraseña, y una contraseña equivocada deja la pantalla de acceso.
/// <list type="bullet">
/// <item>«Recuérdame» en la pantalla de acceso guarda usuario y contraseña en <c>recordarme.dat</c>, cifrados con Windows
/// (DPAPI, sólo el mismo usuario de Windows en esta computadora puede leerlos).</item>
/// <item>También se acepta <c>autologin-caja.json</c> (<c>{"Username":"…","Password":"…"}</c>) escrito a mano por quien administra la caja.</item>
/// </list>
/// Entrar sin «Recuérdame», o borrar los archivos, vuelve a pedir contraseña. El instalador nunca los crea.
/// Una copia de desarrollo usa <see cref="DevelopmentAutoLogin"/> y no lee estos archivos.
/// </summary>
public sealed record StoreAutoLogin(string Username, string Password)
{
    public const string FileName = "autologin-caja.json";
    public const string RememberedFileName = "recordarme.dat";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CasaMiga.Recordarme.v1");

    public static bool HasRemembered(FoundationOptions options) =>
        !options.IsDevelopment && File.Exists(Path.Combine(options.DataDirectory, RememberedFileName));

    public static StoreAutoLogin? TryLoad(FoundationOptions options)
    {
        if (options.IsDevelopment) return null;
        var remembered = Path.Combine(options.DataDirectory, RememberedFileName);
        if (File.Exists(remembered))
            return Valid(JsonSerializer.Deserialize<StoreAutoLogin>(
                ProtectedData.Unprotect(File.ReadAllBytes(remembered), Entropy, DataProtectionScope.CurrentUser), Json), RememberedFileName);
        var path = Path.Combine(options.DataDirectory, FileName);
        return File.Exists(path) ? Valid(JsonSerializer.Deserialize<StoreAutoLogin>(File.ReadAllText(path), Json), FileName) : null;
    }

    private static StoreAutoLogin Valid(StoreAutoLogin? value, string file) =>
        value is null || string.IsNullOrWhiteSpace(value.Username) || string.IsNullOrEmpty(value.Password)
            ? throw new InvalidDataException($"{file} inválido.") : value;

    /// <summary>«Recuérdame»: guarda cifrado el usuario y la contraseña con los que se acaba de entrar y quita el archivo a mano.</summary>
    public static void Remember(FoundationOptions options, string username, string password)
    {
        if (options.IsDevelopment) return;
        var path = Path.Combine(options.DataDirectory, RememberedFileName);
        var secret = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(new StoreAutoLogin(username.Trim(), password)), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path + ".tmp", secret);
        File.Move(path + ".tmp", path, overwrite: true);
        File.Delete(Path.Combine(options.DataDirectory, FileName));
    }

    /// <summary>Entrar sin «Recuérdame» olvida lo guardado: la próxima vez vuelve a pedir contraseña.</summary>
    public static void Forget(FoundationOptions options)
    {
        if (options.IsDevelopment) return;
        File.Delete(Path.Combine(options.DataDirectory, RememberedFileName));
        File.Delete(Path.Combine(options.DataDirectory, FileName));
    }
}
