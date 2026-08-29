using QuoteDesk.Domain;

namespace QuoteDesk.Agents.Tools;

/// <summary>Maps a customer's ship-to city to a freight zone for <see cref="FreightPolicy"/>.</summary>
public static class ShipToZoneResolver
{
    /// <summary>Every city QuoteDesk's seed data uses is within the Surat industrial belt
    /// (`DeterministicSeeder.ShipToCities`) — all of them are Local. National is reserved for an
    /// explicit far destination that no seeded customer has; nothing in the demo needs it.</summary>
    private static readonly HashSet<string> LocalCities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Surat", "Sachin", "Palsana", "Kadodara", "Pandesara",
    };

    public static FreightZone Resolve(string? shipTo) =>
        shipTo is not null && LocalCities.Contains(shipTo) ? FreightZone.Local : FreightZone.Regional;
}
