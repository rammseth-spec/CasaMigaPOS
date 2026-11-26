using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CasaMiga.Presentation;

/// <summary>
/// Modo ligero para computadoras sin aceleración gráfica (WPF «RenderCapability.Tier» menor a 2, que incluye el escritorio remoto):
/// ahí cada sombra se calcula en el procesador y vuelve lenta la pantalla. Quita sombras y el desvanecido de menús y limita las
/// animaciones a 30 cuadros por segundo. Pantallas, botones y textos no cambian.
/// Se decide solo; la variable de entorno <see cref="Variable"/> lo fuerza para soporte: «1» lo enciende y «0» lo apaga.
/// </summary>
public static class LightRendering
{
    public const string Variable = "CASAMIGA_MODO_LIGERO";
    /// <summary>Recursos de Theme.xaml que se usan con DynamicResource y que el modo ligero sustituye.</summary>
    public static readonly IReadOnlyList<string> ShadowKeys = ["ShadowSubtle", "ShadowMenu", "ShadowDialog", "ShadowHelp"];
    public const string PopupAnimationKey = "MenuPopupAnimation";
    public const int AnimationFrameRate = 30;
    private static bool frameRateLimited;
    public static bool Enabled { get; private set; }
    /// <summary>Nivel de aceleración de WPF: 0 = sin aceleración, 1 = parcial, 2 = completa.</summary>
    public static int CurrentTier => RenderCapability.Tier >> 16;
    public static bool Decide(int tier, string? setting) => setting?.Trim() switch
    {
        "1" => true,
        "0" => false,
        _ => tier < 2
    };
    /// <summary>Aplica el modo ligero si corresponde. Debe llamarse al iniciar, antes de abrir la ventana principal.</summary>
    public static bool Apply(System.Windows.Application app) =>
        Apply(app.Resources, Decide(CurrentTier, Environment.GetEnvironmentVariable(Variable)));
    public static bool Apply(ResourceDictionary resources, bool enabled)
    {
        Enabled = enabled;
        if (!enabled) return false;
        ApplyResources(resources);
        if (!frameRateLimited)
        {
            frameRateLimited = true;
            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline),
                new FrameworkPropertyMetadata { DefaultValue = (int?)AnimationFrameRate });
        }
        return true;
    }
    /// <summary>Sustituye sombras (por ninguna) y el desvanecido de menús en el diccionario del tema donde están definidos;
    /// los elementos que los usan con DynamicResource se actualizan solos.</summary>
    public static void ApplyResources(ResourceDictionary resources)
    {
        foreach (var key in ShadowKeys) Replace(resources, key, null);
        Replace(resources, PopupAnimationKey, PopupAnimation.None);
    }
    // Un valor nulo en el diccionario de arriba no tapa al del tema combinado (WPF sigue buscando): se cambia donde vive la clave.
    private static void Replace(ResourceDictionary resources, string key, object? value)
    {
        if (!ReplaceExisting(resources, key, value)) resources[key] = value;
    }
    private static bool ReplaceExisting(ResourceDictionary resources, string key, object? value)
    {
        var replaced = false;
        foreach (var merged in resources.MergedDictionaries) replaced |= ReplaceExisting(merged, key, value);
        if (resources.Keys.Cast<object>().Contains(key)) { resources[key] = value; replaced = true; }
        return replaced;
    }
}
