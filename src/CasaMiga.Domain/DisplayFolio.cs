using System.Globalization;
using System.Text.RegularExpressions;

namespace CasaMiga.Domain;

/// <summary>
/// Folio que ven el cajero y el cliente: clave corta de la caja y consecutivo (C01-001061).
/// El folio interno (<see cref="Sale.Folio"/>: identificador de la terminal y consecutivo) sigue siendo la identidad única
/// para auditoría y sincronización; éste sólo cambia lo que se muestra, se imprime y se busca.
/// </summary>
public static partial class DisplayFolio
{
    /// <summary>Clave de la caja tomada del número al final de su nombre: «CASA-MIGA-CAJA-01» → C01. Sin número, C01.</summary>
    public static string TerminalCode(string? terminalName)
    {
        var match = TrailingNumber().Match(terminalName ?? "");
        var number = match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 1;
        return "C" + number.ToString("D2", CultureInfo.InvariantCulture);
    }

    public static string For(string? terminalName, long sequence) =>
        TerminalCode(terminalName) + "-" + sequence.ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>Del folio interno («…-00001061») al visible; si no lo reconoce, lo deja igual.</summary>
    public static string FromInternal(string internalFolio, string? terminalName)
    {
        var separator = internalFolio.LastIndexOf('-');
        return separator >= 0 && long.TryParse(internalFolio[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) && sequence > 0
            ? For(terminalName, sequence) : internalFolio;
    }

    /// <summary>Lee «C01-001061», «c1-1061», «001061» o «1061». La clave de caja es opcional.</summary>
    public static bool TryParse(string text, out string? terminalCode, out long sequence)
    {
        terminalCode = null; sequence = 0;
        var match = Pattern().Match(text.Trim());
        if (!match.Success || !long.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out sequence) || sequence < 1)
        { sequence = 0; return false; }
        if (match.Groups["c"].Success)
        {
            if (!int.TryParse(match.Groups["c"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code < 1) { sequence = 0; return false; }
            terminalCode = "C" + code.ToString("D2", CultureInfo.InvariantCulture);
        }
        return true;
    }

    [GeneratedRegex(@"(\d+)\s*$")]
    private static partial Regex TrailingNumber();
    [GeneratedRegex(@"^(?:[Cc](?<c>\d{1,4})\s*-\s*)?(?<n>\d{1,12})$")]
    private static partial Regex Pattern();
}
