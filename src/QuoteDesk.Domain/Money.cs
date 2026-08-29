namespace QuoteDesk.Domain;

/// <summary>
/// The single rounding helper. Every monetary calculation in this project rounds through this
/// method — two code paths rounding differently is a bug even when both tests pass.
/// </summary>
public static class Money
{
    /// <summary>
    /// Rounds to two decimal places using away-from-zero rounding (round-half-up), matching Indian
    /// invoicing convention rather than the banker's rounding <see cref="decimal.Round(decimal)"/>
    /// uses by default.
    /// </summary>
    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
