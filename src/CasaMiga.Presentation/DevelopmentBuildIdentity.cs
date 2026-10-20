using System.Reflection;

namespace CasaMiga.Presentation;

/// <summary>Optional build-time identity, shown only for development installations.</summary>
public static class DevelopmentBuildIdentity
{
    public static string? ReviewBuild => typeof(DevelopmentBuildIdentity).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .SingleOrDefault(x => x.Key == "CasaMiga.ReviewBuild")?.Value;

    public static string WindowTitle(string title, bool isDevelopment) => !isDevelopment
        ? title
        : string.IsNullOrWhiteSpace(ReviewBuild)
            ? $"{title} · DESARROLLO"
            : $"{title} · DESARROLLO · {ReviewBuild}";

    /// <summary>Versión del programa (la que pone el instalador, p. ej. 1.0.0), sin el sufijo técnico del commit.</summary>
    public static string Version
    {
        get
        {
            var assembly = typeof(DevelopmentBuildIdentity).Assembly;
            var text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString(3) ?? "";
            var plus = text.IndexOf('+');
            return plus < 0 ? text : text[..plus];
        }
    }

    /// <summary>Pie de la pantalla de entrada: una instalación normal muestra su versión; sólo una copia de desarrollo lo dice.</summary>
    public static string LoginCaption(bool isDevelopment) => isDevelopment
        ? $"Aplicación local · Versión de desarrollo{(string.IsNullOrWhiteSpace(ReviewBuild) ? "" : $" · {ReviewBuild}")}"
        : $"Aplicación local · Versión {Version}";
}
