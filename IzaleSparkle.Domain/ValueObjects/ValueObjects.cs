namespace IzaleSparkle.Domain.ValueObjects;

/// <summary>Immutable money value object with currency.</summary>
public sealed record Money(decimal Amount, string Currency = "GBP")
{
    public static Money Zero => new(0);
    public Money Add(Money other)
    {
        if (Currency != other.Currency) throw new InvalidOperationException("Currency mismatch");
        return new Money(Amount + other.Amount, Currency);
    }
    public Money Multiply(int qty) => new(Amount * qty, Currency);
    public override string ToString() => $"£{Amount:N2}";
}

/// <summary>Immutable shipping address value object.</summary>
public sealed record Address(
    string FirstName,
    string LastName,
    string Line1,
    string? Line2,
    string City,
    string Postcode,
    string Country = "United Kingdom"
)
{
    public string FullName => $"{FirstName} {LastName}";
    public override string ToString() => $"{Line1}, {City}, {Postcode}, {Country}";
}
