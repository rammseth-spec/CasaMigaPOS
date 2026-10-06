using System.Security.Cryptography;

namespace CasaMiga.Application;

public sealed record PasswordRequirement(string Label, bool Met);

/// <summary>Regla única de contraseñas para alta, cambio y restablecimiento. El login no la aplica para no bloquear contraseñas previas.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 16;
    public const string Description = "De 8 a 16 caracteres, con al menos una mayúscula, una minúscula, un número y un carácter especial.";

    /// <summary>Cada requisito con su estado; la interfaz lo muestra en vivo y <see cref="Validate"/> usa exactamente la misma lista.</summary>
    public static IReadOnlyList<PasswordRequirement> Evaluate(string password) =>
    [
        new("De 8 a 16 caracteres", password.Length is >= MinLength and <= MaxLength),
        new("Una mayúscula", password.Any(char.IsUpper)),
        new("Una minúscula", password.Any(char.IsLower)),
        new("Un número", password.Any(char.IsDigit)),
        new("Un carácter especial (# $ % & * ! …)", password.Any(IsSpecial)),
    ];

    public static void Validate(string password)
    {
        var missing = Evaluate(password).Where(r => !r.Met).Select(r => char.ToLowerInvariant(r.Label[0]) + r.Label[1..]).ToList();
        if (password.Any(char.IsControl)) missing.Add("sin caracteres de control");
        if (missing.Count > 0) throw new ArgumentException("La contraseña debe tener " + string.Join(", ", missing) + ".");
    }

    public static bool IsValid(string password)
    {
        try { Validate(password); return true; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Contraseña temporal aleatoria que cumple la regla; se muestra una sola vez y nunca se guarda en claro.</summary>
    public static string GenerateTemporary()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789", special = "!#$%&*+-=?@";
        const string all = upper + lower + digits + special;
        var chars = new char[12];
        chars[0] = Pick(upper); chars[1] = Pick(lower); chars[2] = Pick(digits); chars[3] = Pick(special);
        for (int i = 4; i < chars.Length; i++) chars[i] = Pick(all);
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
        static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
    }

    private static bool IsSpecial(char c) => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsControl(c);
}
