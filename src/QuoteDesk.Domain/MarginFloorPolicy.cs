namespace QuoteDesk.Domain;

/// <summary>
/// The minimum net margin a line may carry. A line below the floor is not refused outright — it is
/// marked <c>requires_override</c> and routed to approval with the shortfall shown, per docs/DOMAIN.md.
/// </summary>
public static class MarginFloorPolicy
{
    public const decimal FloorPct = 0.10m;

    /// <summary>A margin exactly at the floor passes — the rule is "at or above", not "above".</summary>
    public static bool IsBelowFloor(decimal marginPct) => marginPct < FloorPct;

    /// <summary>How far below the floor the margin sits, or zero when it already clears the floor.</summary>
    public static decimal Shortfall(decimal marginPct) => IsBelowFloor(marginPct) ? FloorPct - marginPct : 0m;
}
