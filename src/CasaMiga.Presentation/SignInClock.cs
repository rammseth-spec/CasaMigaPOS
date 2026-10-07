using System.Globalization;

namespace CasaMiga.Presentation;

/// <summary>Textos de la pantalla de acceso según la hora local: reloj, fecha y saludo.</summary>
public static class SignInClock
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

    public static string Time(DateTime now) => now.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Date(DateTime now)
    {
        var text = now.ToString("dddd d 'de' MMMM", Spanish);
        return char.ToUpper(text[0], Spanish) + text[1..];
    }

    public static string Greeting(DateTime now) => now.Hour switch
    {
        >= 5 and < 12 => "Buenos días",
        >= 12 and < 19 => "Buenas tardes",
        _ => "Buenas noches",
    };
}
