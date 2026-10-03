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
}
