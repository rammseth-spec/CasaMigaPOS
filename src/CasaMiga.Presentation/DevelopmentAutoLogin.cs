using System.IO;
using System.Text.Json;
using CasaMiga.Infrastructure;

namespace CasaMiga.Presentation;

/// <summary>
/// Inicio de sesión automático SÓLO para copias de revisión en modo desarrollo (development.settings.json junto al exe,
/// que el instalador nunca incluye). Lee <c>dev-autologin.json</c> de la carpeta de datos y usa el login normal con la
/// contraseña real: no existe ningún camino de acceso sin contraseña. Para desactivarlo basta con borrar el archivo.
/// </summary>
public sealed record DevelopmentAutoLogin(string Username, string Password)
{
    public const string FileName = "dev-autologin.json";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static DevelopmentAutoLogin? TryLoad(FoundationOptions options)
    {
        if (!options.IsDevelopment) return null; // Una instalación normal ignora el archivo aunque exista.
        var path = Path.Combine(options.DataDirectory, FileName);
        if (!File.Exists(path)) return null;
        var value = JsonSerializer.Deserialize<DevelopmentAutoLogin>(File.ReadAllText(path), Json);
        if (value is null || string.IsNullOrWhiteSpace(value.Username) || string.IsNullOrEmpty(value.Password))
            throw new InvalidDataException($"{FileName} inválido.");
        return value;
    }
}
