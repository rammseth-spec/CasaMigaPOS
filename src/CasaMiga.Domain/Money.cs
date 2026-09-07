namespace CasaMiga.Domain;

/// <summary>MXN in cents. Signed to support differences and reversals.</summary>
public readonly record struct Money(long Cents)
{
    public static Money FromPesos(decimal pesos)
    {
        decimal cents = checked(pesos * 100m);
        if (cents != decimal.Truncate(cents))
            throw new ArgumentException("El importe debe tener como máximo dos decimales.", nameof(pesos));
        return new Money(checked((long)cents));
    }

    public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));
    public static Money operator -(Money a, Money b) => new(checked(a.Cents - b.Cents));
    public Money Multiply(long pieces) => new(checked(Cents * pieces));

    public Money ChangeFrom(Money received)
    {
        if (Cents < 0 || received.Cents < Cents)
            throw new ArgumentOutOfRangeException(nameof(received));
        return received - this;
    }
}
